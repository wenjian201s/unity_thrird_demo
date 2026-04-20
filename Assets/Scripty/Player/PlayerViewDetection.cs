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
    [SerializeField] private CinemachineTargetGroup cinemachineTargetGroup; // Cinemachine 目标组
    
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
    
    
    void Start()
    {
        animator = GetComponent<Animator>();                         // 获取 Animator
        thirdPersonController = GetComponent<ThirdPersonController>(); // 获取角色控制器
    }

    void LateUpdate()
    {
        FindEnemyInFront(); // 查找玩家面前可以锁定的敌人
        SwitchAnimator();   // 更新锁敌状态下的动画输入
        LockOnEnemy();      // 锁定状态下让角色面朝敌人

        // TEST: 测试代码
        // 如果有最近锁定目标，则让主相机朝向目标
        if (nearestLockOnTarget)
        {
            Vector3 dir = nearestLockOnTarget.position - mainCamera.transform.position; // 相机指向目标的方向
            dir.Normalize();                                                            // 单位化
            Quaternion targetRotation = Quaternion.LookRotation(dir);                   // 计算朝向目标的旋转
            Vector3 eulerAngles = targetRotation.eulerAngles;                           // 转欧拉角
            eulerAngles.y = 0;                                                          // 保留某些轴控制（这里实际是把 Y 清零）
            mainCamera.transform.localEulerAngles = eulerAngles;                        // 设置相机局部旋转
        }
    }
    
    [SerializeField] private Transform viewTransform;                // 视野检测参考点（通常是玩家头部/相机参考点）
    [SerializeField][Range(0, 180)] private float viewAngle = 50f;  // 可锁定视野角范围
    // TODO: 此处将来要改成 EnemyCombatController
    [SerializeField] private List<EnemyLockOn> availableTargets = new List<EnemyLockOn>(); // 当前可用锁敌目标列表
    [SerializeField] private Transform nearestLockOnTarget;          // 当前最近的锁定目标

    private void FindEnemyInFront()
    {
        // 如果当前不处于锁定状态，则不查找敌人
        if (!isLockTarget)
            return;
        
        availableTargets.Clear(); // 每次检测前先清空上一帧的可用目标列表

        // 获取相机当前位置和前方向
        Vector3 cameraPos = mainCamera.transform.position;
        Vector3 cameraForward = mainCamera.transform.forward;

        // 根据 offset 和摄像机前方计算检测盒中心点
        cubeCenter = new Vector3(
            offset.x * cameraForward.x,
            offset.y * cameraForward.y,
            offset.z * cameraForward.z
        ) + cameraPos;

        // 在盒形范围内检测敌人碰撞体
        enemyColliders = Physics.OverlapBox(cubeCenter, size / 2, Quaternion.Euler(rotateEuler), enemyLayer);
        
        if (enemyColliders.Length > 0)
        {
            // 遍历所有检测到的敌人
            for (int i = 0; i < enemyColliders.Length; i++)
            {
                // TODO: 此处将来要改成 EnemyCombatController
                EnemyLockOn enemy = enemyColliders[i].GetComponent<EnemyLockOn>();
                if (enemy)
                {
                    Vector3 lockTargetDirection = new Vector3();

                    // 计算从视角参考点到敌人的方向
                    lockTargetDirection = enemy.transform.position - viewTransform.position;

                    // 计算玩家与目标的距离
                    float distanceFromTarget = Vector3.Distance(viewTransform.position, enemy.transform.position);

                    // 计算该目标与相机前方向的夹角
                    float viewableAngle = Vector3.Angle(lockTargetDirection, mainCamera.transform.forward);

                    // 如果目标处于可视角范围内，且距离不超过最大锁定距离，则加入可用目标列表
                    if (viewableAngle > -viewAngle && viewableAngle < viewAngle && distanceFromTarget <= maxLockOnDistance)
                    {
                        availableTargets.Add(enemy);
                    }
                }
            }

            // 在可锁定目标中寻找最近的目标
            float shortestDistance = float.MaxValue;
            for (int i = 0; i < availableTargets.Count; i++)
            {
                float distanceFromTarget = Vector3.Distance(viewTransform.position, availableTargets[i].transform.position);

                if (distanceFromTarget <= maxLockOnDistance)
                {
                    shortestDistance = distanceFromTarget;

                    // TODO: 改为下面这句
                    //nearestLockOnTarget = availableTargets[i].GetLockOnTransform();

                    // 当前逻辑：直接使用 enemy 的 lockOnTransform
                    nearestLockOnTarget = availableTargets[i].lockOnTransform;
                }
            }

            // 如果存在可以锁定的最近目标
            if (nearestLockOnTarget)
            {
                // 将该目标设置到 Cinemachine 的目标组中
                SetCameraTarget(nearestLockOnTarget);
            }
        }
    }

    [SerializeField] private float targetWeight; // 目标在 CinemachineTargetGroup 中的权重

    private void SetCameraTarget(Transform targetTransform)
    {
        animator.SetFloat(lockOnHash, 1f); // 设置动画参数，进入锁敌状态

        // CinemachineTargetGroup 中理论上最多只有两个成员：
        // [0] 玩家
        // [1] 敌人目标

        // 如果当前只有玩家一个成员
        if (cinemachineTargetGroup.m_Targets.Length == 1)
        {
            // 直接把目标加入 TargetGroup
            cinemachineTargetGroup.AddMember(targetTransform, targetWeight, 1);
        }
        // 如果当前已经有两个成员，则替换第二个目标
        else if(cinemachineTargetGroup.m_Targets.Length == 2)
        {
            CinemachineTargetGroup.Target newTarget = new CinemachineTargetGroup.Target
            {
                target = targetTransform, // 目标 Transform
                weight = targetWeight,    // 权重
                radius = 1f               // 半径
            };

            // 替换 TargetGroup 中的敌人目标
            cinemachineTargetGroup.m_Targets[1] = newTarget;
        }
        else
        {
            // 如果 TargetGroup 中对象数量异常，输出错误
            Debug.LogError(string.Format("CinemachineTargetGroup的对象数量不正确, 此时其中有{0}个对象", cinemachineTargetGroup.m_Targets.Length));
        }
        
        // 让相机朝向目标
        Vector3 dir = targetTransform.position - mainCamera.transform.position;
        dir.Normalize();
        Quaternion targetRotation = Quaternion.LookRotation(dir);
        Vector3 eulerAngles = targetRotation.eulerAngles;
        eulerAngles.y = 0;
        mainCamera.transform.localEulerAngles = eulerAngles;
        
        // 手动更新 TargetGroup
        cinemachineTargetGroup.DoUpdate(); 
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
        // 如果没有处于锁定状态，或者没有可锁定目标
        if (!isLockTarget || !nearestLockOnTarget)
        {
            // 退出锁定动画状态
            animator.SetFloat(lockOnHash, 0f);

            // 清空之前查找到的目标
            ClearViewTarget();

            // 清空相机目标组中除了玩家之外的对象
            ClearCameraTarget();

            // 退出锁定状态
            isLockTarget = false;
            return;
        }
            
        // 计算从玩家到目标的方向，只保留水平面
        Vector3 toTarget = nearestLockOnTarget.position - transform.position;
        toTarget.y = 0;

        // 只有在某些动画状态下，才持续朝向目标
        if (animator.GetCurrentAnimatorStateInfo(0).IsTag("EquipMotion") ||
            animator.GetCurrentAnimatorStateInfo(0).IsTag("Equip") ||
            animator.GetCurrentAnimatorStateInfo(0).IsTag("KatanaAttack") ||
            animator.GetCurrentAnimatorStateInfo(0).IsTag("GSAttack") ||
            ((animator.GetCurrentAnimatorStateInfo(0).IsTag("Roll")) && Vector3.Distance(transform.position, nearestLockOnTarget.position) > stopFaceDis) ||
            animator.IsInTransition(0))
        {
            // 先生成一个面向目标的基础旋转
            Quaternion baseRotation = Quaternion.LookRotation(toTarget);

            // 再人为添加一个绕 Y 轴的偏移角
            Quaternion leftOffset = Quaternion.AngleAxis(offsetAngle, Vector3.up);

            // 组合旋转：先朝向目标，再加偏移
            Quaternion targetRotation = baseRotation * leftOffset;

            // 平滑旋转玩家 root
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, lockRotationSpeed * Time.deltaTime);
        }
    }

    // 清空当前锁定目标和可用目标列表
    private void ClearViewTarget()
    {
        nearestLockOnTarget = null;
        availableTargets.Clear();
    }

    // 清空 CinemachineTargetGroup 中除玩家外的目标
    private void ClearCameraTarget()
    {
        CinemachineTargetGroup.Target[] newTargets = new CinemachineTargetGroup.Target[]{cinemachineTargetGroup.m_Targets[0]};
        cinemachineTargetGroup.m_Targets = newTargets;
    }

    #region Gizmos
    
    private void OnDrawGizmos()
    {
        // 在 Scene 视图里画出锁敌检测盒
        Vector3 cameraPos = mainCamera.transform.position;
        Vector3 cameraForward = mainCamera.transform.forward;
        cubeCenter = new Vector3(offset.x * cameraForward.x, offset.y * cameraForward.y, offset.z * cameraForward.z)+ cameraPos;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(cubeCenter, size);
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
            // 每按一次切换锁定状态
            isLockTarget = !isLockTarget;
        }
    }
    
    #endregion
}