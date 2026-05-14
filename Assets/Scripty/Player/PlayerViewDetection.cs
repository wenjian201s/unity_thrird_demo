using System.Collections;                         // 协程支持（当前脚本中未实际使用）
using System.Collections.Generic;                 // 泛型集合支持
using Cinemachine; // Cinemachine 相机系统
using UnityEngine;                               // Unity 核心命名空间
using UnityEngine.Animations.Rigging;            // 动画绑定/约束（当前脚本中未实际使用）
using UnityEngine.InputSystem;                   // Unity 新输入系统
using UnityEngine.Serialization;                 // 序列化重命名支持

public class PlayerViewDetection : MonoBehaviour
{
    private Animator animator;                           // 玩家 Animator
    private ThirdPersonController thirdPersonController; // 第三人称角色控制器

    [SerializeField] private Camera mainCamera;          // 主相机
    [SerializeField] private CinemachineFreeLook lockOnCamera; // 锁定状态使用的 FreeLook，相机位置继续跟随玩家，只把视角看向敌人。
    
    [FormerlySerializedAs("enemies")]
    [Header("玩家锁敌")]
    [SerializeField] private Collider[] enemyColliders; // 检测范围内的敌人碰撞体数组
    //[SerializeField] private Transform targetTransform = null;
    [SerializeField] private bool isLockTarget = false; // 当前是否处于锁敌状态
    
    [FormerlySerializedAs("viewDistance")]
    [FormerlySerializedAs("distance")]
    [Header("玩家视野检测")]
    [SerializeField] private float maxLockOnDistance = 30f; // 最大锁敌距离
    [SerializeField] private Vector3 offset;                // 检测盒中心相对于相机前方的偏移
    [SerializeField] private Vector3 size;                  // 检测盒尺寸
    [SerializeField] private Vector3 cubeCenter;            // 检测盒中心（运行时计算）
    [SerializeField] private Vector3 rotateEuler;           // 检测盒旋转角度
    [SerializeField] private LayerMask enemyLayer;          // 敌人层
    [SerializeField] private LayerMask ignoreLayer;         // 射线检测时需要忽略的层

    // Animator 参数哈希，提升访问效率
    private int lockOnHash = Animator.StringToHash("LockOn"); // 锁敌动画参数
    private int xInputHash = Animator.StringToHash("XInput"); // 输入 X
    private int yInputHash = Animator.StringToHash("YInput"); // 输入 Y
    private int xSpeedHash = Animator.StringToHash("XSpeed"); // 锁敌移动 X 速度
    private int ySpeedHash = Animator.StringToHash("YSpeed"); // 锁敌移动 Y 速度
    private int stateDrivenCameraLayerIndex = -1;
    
    
    private void Awake()
    {
        CacheComponents();
    }

    void Start()
    {
        CacheComponents();
    }

    private void CacheComponents()
    {
        animator = GetComponent<Animator>();
        thirdPersonController = GetComponent<ThirdPersonController>();

        if (!mainCamera)
            mainCamera = Camera.main;

        if (animator)
        {
            if (stateDrivenCameraLayerIndex < 0)
                stateDrivenCameraLayerIndex = animator.GetLayerIndex("StateDrivenCamera");

            if (stateDrivenCameraLayerIndex >= 0 && animator.GetLayerWeight(stateDrivenCameraLayerIndex) <= 0f)
                animator.SetLayerWeight(stateDrivenCameraLayerIndex, 1f);
        }
    }

    void LateUpdate()
    {
        FindEnemyInFront(); // 查找玩家面前可以锁定的敌人
        SwitchAnimator();   // 更新锁敌状态下的动画输入
        LockOnEnemy();      // 锁定状态下让角色面朝敌人
    }
    
    [SerializeField] private Transform viewTransform;                // 视野检测参考点（通常是玩家头部/相机参考点）
    [SerializeField][Range(0, 180)] private float viewAngle = 90f;  // 可锁定视野角范围
    // TODO: 此处将来要改成 EnemyCombatController
    [SerializeField] private List<EnemyLockOn> availableTargets = new List<EnemyLockOn>(); // 当前可用锁敌目标列表
    [SerializeField] private Transform nearestLockOnTarget;          // 当前最近的锁定目标

    private void FindEnemyInFront()
    {
        // 如果当前不处于锁定状态，则不查找敌人
        if (!isLockTarget)
            return;

        CacheComponents();

        if (!mainCamera)
        {
            CancelLockOn();
            return;
        }
        
        availableTargets.Clear(); // 每次检测前先清空上一帧的可用目标列表

        Transform cameraTransform = mainCamera.transform;
        Vector3 detectionOrigin = viewTransform ? viewTransform.position : transform.position;
        cubeCenter = detectionOrigin + cameraTransform.forward * Mathf.Max(1f, offset.z);

        // 已经锁定后不再用镜头夹角反复筛掉目标，只要目标仍在范围内就维持锁定。
        if (nearestLockOnTarget)
        {
            float lockedDistance = Vector3.Distance(detectionOrigin, nearestLockOnTarget.position);
            if (nearestLockOnTarget.gameObject.activeInHierarchy && lockedDistance <= maxLockOnDistance)
            {
                SetCameraTarget(nearestLockOnTarget);
                return;
            }

            ClearViewTarget();
        }

        // 用球形范围兜住锁定目标，再用相机夹角筛选，避免敌人稍微偏出旧检测盒就锁不到。
        enemyColliders = Physics.OverlapSphere(detectionOrigin, maxLockOnDistance, enemyLayer, QueryTriggerInteraction.Collide);
        
        if (enemyColliders.Length > 0)
        {
            float bestScore = float.MaxValue;

            for (int i = 0; i < enemyColliders.Length; i++)
            {
                EnemyLockOn enemy = enemyColliders[i].GetComponentInParent<EnemyLockOn>();
                if (!enemy || availableTargets.Contains(enemy))
                    continue;

                Transform lockTarget = enemy.lockOnTransform ? enemy.lockOnTransform : enemy.transform;
                Vector3 lockTargetDirection = lockTarget.position - detectionOrigin;
                float distanceFromTarget = lockTargetDirection.magnitude;

                if (distanceFromTarget <= 0.01f || distanceFromTarget > maxLockOnDistance)
                    continue;

                float viewableAngle = Vector3.Angle(lockTargetDirection, cameraTransform.forward);
                if (viewableAngle > viewAngle)
                    continue;

                availableTargets.Add(enemy);

                // 角度优先、距离次之：按 Q 时会优先锁屏幕中更明显的敌人。
                float score = viewableAngle * 0.7f + distanceFromTarget * 0.3f;
                if (score < bestScore)
                {
                    bestScore = score;
                    nearestLockOnTarget = lockTarget;
                }
            }
        }

        if (nearestLockOnTarget)
            SetCameraTarget(nearestLockOnTarget);
        else
            CancelLockOn();
    }

    private void SetCameraTarget(Transform targetTransform)
    {
        CacheComponents();

        if (!targetTransform)
            return;

        if (animator)
            animator.SetFloat(lockOnHash, 1f);

        target = targetTransform;

        if (!lockOnCamera)
            return;

        if (!lockOnCamera.Follow)
            lockOnCamera.Follow = transform;

        if (lockOnCamera.LookAt != targetTransform)
        {
            lockOnCamera.LookAt = targetTransform;
            lockOnCamera.PreviousStateIsValid = false;
        }
    }
    
    /// <summary>
    /// 判断目标物体是否在相机中可见
    /// </summary>
    private bool IsVisableInCamera(Camera camera, Transform target)
    {
        if (!camera || !target)
            return false;

        // 将目标世界坐标转为屏幕坐标
        Vector3 screenPoint = camera.WorldToScreenPoint(target.position);

        // 如果目标在屏幕范围外，则认为不可见
        if(screenPoint.x < 0 || screenPoint.y < 0 || screenPoint.x > Screen.width || screenPoint.y > Screen.height)
            return false;

        // 从相机屏幕点向目标发射射线
        Ray ray = camera.ScreenPointToRay(screenPoint);

        // 射线检测，忽略 ignoreLayer 指定的层
        if (Physics.Raycast(ray, out RaycastHit hit, maxLockOnDistance, ~(ignoreLayer)))
        {
            // 如果中途被别的物体挡住
            if (hit.collider.gameObject != target.gameObject)
            {
                Debug.Log("视线被" + hit.collider.gameObject.name + "阻挡");
            }

            // 只有真正打到目标物体才算可见
            return hit.collider.gameObject == target.gameObject;
        }

        return false;
    }

    private Vector3 dir; // 动画切换时使用的本地移动方向

    private void SwitchAnimator()
    {
        CacheComponents();

        if (!thirdPersonController)
            return;

        // 获取玩家本地坐标下的移动方向（只保留水平面）
        dir = new Vector3(thirdPersonController.GetPlayerMovement().x, 0, thirdPersonController.GetPlayerMovement().z);

        // 只有站立状态下才处理锁敌动画输入
        if (thirdPersonController.playerPosture == ThirdPersonController.PlayerPosture.Stand)
        {
            // 把输入值同步给 Animator
            animator.SetFloat(xInputHash, thirdPersonController.GetMoveInput().x);
            animator.SetFloat(yInputHash, thirdPersonController.GetMoveInput().y);

            switch (thirdPersonController.locomotionState)
            {
                case ThirdPersonController.LocomotionState.Idle:
                    // 待机时速度归零
                    animator.SetFloat(xSpeedHash, 0, 0.1f, Time.deltaTime);
                    animator.SetFloat(ySpeedHash, 0, 0.1f, Time.deltaTime);
                    break;

                case ThirdPersonController.LocomotionState.Walk:
                    // 行走状态：把玩家移动方向分解到 X / Y 速度参数
                    animator.SetFloat(xSpeedHash, dir.x * thirdPersonController.GetWalkSpeed(), 0.1f, Time.deltaTime);
                    animator.SetFloat(ySpeedHash, dir.z * thirdPersonController.GetWalkSpeed(), 0.1f, Time.deltaTime);
                    break;

                case ThirdPersonController.LocomotionState.Run:
                    // 奔跑状态：同理，用奔跑速度驱动
                    animator.SetFloat(xSpeedHash, dir.x * thirdPersonController.GetRunSpeed(), 0.1f, Time.deltaTime);
                    animator.SetFloat(ySpeedHash, dir.z * thirdPersonController.GetRunSpeed(), 0.1f, Time.deltaTime);
                    break;
            }
        }
    }
    
    
    // TEST: 测试代码
    [SerializeField] private Transform target;       // 测试目标（当前未实际使用）
    [SerializeField] private float lockRotationSpeed; // 锁敌旋转速度
    [SerializeField] private float offsetAngle;       // 朝向目标时额外偏移角度
    [SerializeField] private float stopFaceDis;       // 翻滚时停止朝向目标的最小距离
    private Vector3 targetDirection;

    /// <summary>
    /// 锁定状态下，让玩家面朝敌人
    /// </summary>
    private void LockOnEnemy()
    {
        CacheComponents();

        if (!isLockTarget || !nearestLockOnTarget)
        {
            CancelLockOn();
            return;
        }

        Vector3 toTarget = nearestLockOnTarget.position - transform.position;
        toTarget.y = 0;

        if (toTarget.sqrMagnitude <= 0.001f || !animator)
            return;

        AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);
        float targetDistance = toTarget.magnitude;
        bool isRolling = currentState.IsTag("Roll");
        bool rollingTooClose = isRolling && targetDistance <= stopFaceDis;

        Vector2 moveInput = thirdPersonController ? thirdPersonController.GetMoveInput() : Vector2.zero;
        bool hasMoveInput = moveInput.sqrMagnitude > 0.01f;

        bool isCombatAction = currentState.IsTag("Attack") ||
                              currentState.IsTag("GSAttack") ||
                              currentState.IsTag("KatanaAttack") ||
                              currentState.IsTag("Equip") ||
                              currentState.IsTag("EquipMotion");

        bool transitionIntoCombatAction = false;
        if (animator.IsInTransition(0))
        {
            AnimatorStateInfo nextState = animator.GetNextAnimatorStateInfo(0);
            transitionIntoCombatAction = nextState.IsTag("Attack") ||
                                         nextState.IsTag("GSAttack") ||
                                         nextState.IsTag("KatanaAttack") ||
                                         nextState.IsTag("Equip") ||
                                         nextState.IsTag("EquipMotion");
        }

        bool shouldFaceTarget = !hasMoveInput ||
                                isCombatAction ||
                                transitionIntoCombatAction ||
                                (isRolling && !rollingTooClose);

        if (rollingTooClose || !shouldFaceTarget)
            return;

        Quaternion baseRotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
        Quaternion leftOffset = Quaternion.AngleAxis(offsetAngle, Vector3.up);
        Quaternion targetRotation = baseRotation * leftOffset;

        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, lockRotationSpeed * Time.deltaTime);
    }

    // 清空当前锁定目标和可用目标列表
    private void ClearViewTarget()
    {
        target = null;
        nearestLockOnTarget = null;
        availableTargets.Clear();
    }

    // 清空 CinemachineTargetGroup 中除玩家外的目标
    private void ClearCameraTarget()
    {
        CacheComponents();

        if (!lockOnCamera)
            return;

        Transform fallbackLookAt = viewTransform ? viewTransform : transform;
        if (lockOnCamera.LookAt != fallbackLookAt)
        {
            lockOnCamera.LookAt = fallbackLookAt;
            lockOnCamera.PreviousStateIsValid = false;
        }
    }

    private void CancelLockOn()
    {
        CacheComponents();

        if (animator)
            animator.SetFloat(lockOnHash, 0f);

        ClearViewTarget();
        ClearCameraTarget();
        isLockTarget = false;
    }

    #region Gizmos
    
    private void OnDrawGizmos()
    {
        if (!mainCamera)
            return;

        Vector3 detectionOrigin = viewTransform ? viewTransform.position : transform.position;
        cubeCenter = detectionOrigin + mainCamera.transform.forward * Mathf.Max(1f, offset.z);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(detectionOrigin, maxLockOnDistance);

        Matrix4x4 oldMatrix = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(cubeCenter, Quaternion.Euler(rotateEuler), Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, size);
        Gizmos.matrix = oldMatrix;
    }

    private void DrawRay()
    {
        // 在 Scene 视图里绘制相机到敌人的检测射线（当前未调用）
        for (int i = 0; i < enemyColliders.Length; i++)
        {
            Transform target = enemyColliders[i].transform;
            
            Vector3 screenPoint = mainCamera.WorldToScreenPoint(target.position);
            Ray ray = mainCamera.ScreenPointToRay(screenPoint);
            Gizmos.DrawRay(ray.origin, ray.direction * maxLockOnDistance);   
        }
    }

    #endregion
    
    #region 玩家输入相关
    
    // 获取锁定敌人输入
    public void GetLockTargetInput(InputAction.CallbackContext ctx)
    {
        if (ctx.started)
        {
            if (isLockTarget)
            {
                CancelLockOn();
                return;
            }

            isLockTarget = true;
            FindEnemyInFront();
        }
    }
    
    #endregion
}
