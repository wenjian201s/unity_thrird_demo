using System;                                      // 提供基础系统类型支持
using System.Collections;                         // 提供协程支持
using System.Collections.Generic;                 // 提供泛型集合支持
using Unity.Collections.LowLevel.Unsafe;         // 当前脚本中未实际使用
using Unity.VisualScripting;                      // 当前脚本中未实际使用
using UnityEngine;                                // Unity 核心命名空间
using UnityEngine.InputSystem;                    // Unity 新输入系统
using Random = UnityEngine.Random;                // 给 UnityEngine.Random 取别名
using UnityEngine.Animations.Rigging;             // 当前脚本中未实际使用（原本可能打算做 IK）
using UnityEngine.InputSystem.Interactions;       // 输入交互类型，如 Hold / Tap
using UnityEngine.Serialization;                  // 支持 FormerlySerializedAs 特性

public class ThirdPersonController : MonoBehaviour
{
    #region 组件
    
    private Transform playerTransform;                 // 玩家自身 Transform
    private Animator animator;                         // 动画状态机组件
    private Transform cameraTransform;                 // 主摄像机 Transform
    private CharacterController characterController;   // Unity 角色控制器
    private PlayerSoundController playerSoundController; // 玩家音效控制器
    private CombatControllerBase combatController;     // 战斗控制器基类
    private PlayerInput playerInput;                   // 新输入系统入口组件
    private InputAction moveAction;                    // 移动输入动作
    private InputAction runSlideAction;                // 奔跑 / 翻滚输入动作
    private InputAction crouchAction;                  // 下蹲输入动作
    private InputAction jumpAction;                    // 跳跃输入动作
    private bool inputActionsBound;                    // 防止重复订阅输入事件
     
    #endregion
     
    // =========================
    // 角色姿态状态机
    // 主要处理垂直维度上的状态切换
    // 会影响 Animator 里的 Posture 参数
    // =========================
    public enum PlayerPosture
    {
        Crouch,   // 下蹲
        Stand,    // 站立
        Falling,  // 下落
        Jumping,  // 跳跃
        Landing,  // 落地缓冲
    }

    [HideInInspector]
    public PlayerPosture playerPosture = PlayerPosture.Stand; // 初始姿态为站立
     
    #region 动画状态机阈值
     
    private float standThreshold = 0f;     // Animator 中站立姿态对应的 Posture 值
    private float crouchThreshold = 1f;    // Animator 中下蹲姿态对应的 Posture 值
    private float midairThreshold = 2.2f;  // Animator 中空中姿态对应的 Posture 值
    private float landingThreshold = 1f;   // Animator 中落地姿态过渡值
     
    #endregion
     
    // =========================
    // 角色运动状态机
    // 处理水平移动速度状态
    // =========================
    public enum LocomotionState
    {
        Idle, // 待机
        Walk, // 行走
        Run,  // 奔跑
    }

    [HideInInspector]
    public LocomotionState locomotionState = LocomotionState.Idle; // 默认待机
    
    #region 角色手持装备
     
    // =========================
    // 装备状态
    // 区分空手和持武器
    // =========================
    public enum ArmState
    {
        Normal = 0, // 无武器
        Equip = 1   // 装备武器
    }

    [HideInInspector]
    public ArmState armState = ArmState.Normal; // 默认空手

    public GameObject weaponOnBack; // 背上的武器模型
    public GameObject weaponInHand; // 手上的武器模型
     
    // TODO: 原本可能打算加右手 IK 约束
    // public TwoBoneIKConstraint rightHandIKConstraint;
     
    #endregion
     
    #region 角色速度
     
    float crouchSpeed = 0.8f; // 下蹲移动速度
    float walkSpeed = 1.27f;  // 行走速度
    float runSpeed = 4.2f;    // 奔跑速度
     
    #endregion
     
    // 角色移动输入（来自 Input System）
    private Vector2 moveInput; // WASD / 左摇杆输入的二维方向

    // 玩家实际移动方向（本地坐标下）
    private Vector3 playerMovement = Vector3.zero; 

    // 角色是否可以急停
    [SerializeField]
    private bool canStop = false;
     
    #region 角色状态
     
    private bool isRunPressed = false;    // 是否按住奔跑键
    private bool isCrouchPressed = false; // 是否处于下蹲状态
    [FormerlySerializedAs("isEquipPressed")]
    public bool isEquip = false;          // 是否装备武器

    private bool isKatana = false;        // 是否为太刀（当前脚本未使用）
    private bool isGrateSword = false;    // 是否为大剑（当前脚本未使用，且命名应为 GreatSword）
    private bool isBow = false;           // 是否为弓（当前脚本未使用）
    private bool isJumpPressed = false;   // 是否按下跳跃键
     
    #endregion
     
    #region Animator中动画状态机的哈希值
     
    private int postureHash;       // Animator 参数：Posture
    private int moveSpeedHash;     // Animator 参数：MoveSpeed
    private int turnSpeedHash;     // Animator 参数：TurnSpeed
    private int verticalSpeedHash; // Animator 参数：VerticalSpeed
    private int jumpTypeHash;      // Animator 参数：JumpType
    // private int equipHash;      // 原计划用于装备类型参数
     
    #endregion
     
    #region 角色跳跃相关
     
    public float gravity = -9.81f;     // 重力加速度
    private float verticalVelocity;    // 角色当前垂直速度
    public float maxHeight = 1.5f;     // 最大跳跃高度

    // 缓存空中水平速度使用
    private static readonly int CACHE_SIZE = 3;     // 速度缓存大小
    private Vector3[] velCache = new Vector3[CACHE_SIZE]; // 速度缓存数组
    private int currentCacheIndex = 0;              // 当前缓存写入索引
    private Vector3 averageVelocity = Vector3.zero; // 平均速度

    public float fallMultiplier = 1.5f; // 下落时的重力倍率，加快下坠

    [SerializeField]
    private bool isGrounded; // 是否在地面上

    private float groundCheckOffset = 0.5f; // 地面检测球体偏移量

    private float footTween; // 用于控制左脚/右脚起跳动画

    private float jumpCD = 0.15f; // 落地后短暂跳跃 CD

    [SerializeField]
    private bool isLanding = false; // 是否处于落地缓冲中

    public float longJumpMultiplier = 2.5f; // 松开跳跃键时额外下拉倍率，用于短跳控制

    [SerializeField]
    private bool couldFall = false; // 是否满足跌落判定条件

    private float fallHeight = 0.5f; // 最小跌落高度，小于此高度不算 Falling

    private float landdingMinVelocity; // 进入 Landing 状态所需的最小垂直速度阈值
     
    #endregion

    #region 角色音效相关
     
    private float lastFootCycle = 0f; // 上一帧动画步态循环进度
     
    #endregion

    #region 角色急停相关

    private float currentFootCycle = 0f; // 当前动画步态循环进度

    #endregion
     
    void Start()
    {
        playerTransform = this.transform;                         // 获取角色 Transform
        animator = this.GetComponent<Animator>();                 // 获取 Animator
        cameraTransform = Camera.main.transform;                  // 获取主摄像机 Transform
        characterController = this.GetComponent<CharacterController>(); // 获取 CharacterController
        playerSoundController = this.GetComponent<PlayerSoundController>(); // 获取音效控制器
        combatController = this.GetComponent<CombatControllerBase>();      // 获取战斗控制器
        playerInput = this.GetComponent<PlayerInput>();                    // 获取 PlayerInput
        BindInputActions();                                                // 运行时主动绑定输入事件
         
        // 缓存 Animator 参数哈希，提高访问效率
        postureHash = Animator.StringToHash("Posture");
        moveSpeedHash = Animator.StringToHash("MoveSpeed");
        turnSpeedHash = Animator.StringToHash("TurnSpeed");
        verticalSpeedHash = Animator.StringToHash("VerticalSpeed");
        jumpTypeHash = Animator.StringToHash("JumpType");
        // equipHash = Animator.StringToHash("WeaponType");
         
        // 锁定鼠标到游戏窗口
        Cursor.lockState = CursorLockMode.Locked;
         
        // 根据最小跌落高度，反推一个最小落地速度阈值
        // 用于判断是否应该进入 Landing 状态
        landdingMinVelocity = -Mathf.Sqrt(-2 * gravity * fallHeight); 
        landdingMinVelocity -= 1f;
    }

    private void OnEnable()
    {
        if (playerInput != null)
        {
            BindInputActions();
        }
    }

    private void OnDisable()
    {
        UnbindInputActions();
    }

    void Update()
    {
        CheckGrounded();         // 地面检测
        SwitchPlayerStates();    // 切换玩家状态机
         
        CaculateGravity();       // 先计算重力
        if (ShouldBlockJumpDuringAttack())
        {
            isJumpPressed = false; // 攻击动画期间消费跳跃输入，避免一边攻击一边起跳
        }
        Jump();                  // 再处理跳跃
         
        CaculateInputDirection(); // 计算输入方向
        SetupAnimator();          // 设置 Animator 参数
        PlayFootStepSound();      // 播放脚步声
         
        NoEquipEmergencyStop();   // 处理空手奔跑急停
    }

    /// <summary>
    /// 根据当前条件切换玩家的姿态状态、装备状态、移动状态
    /// </summary>
    private void SwitchPlayerStates()
    {
        // =========================
        // 姿态状态机切换
        // =========================
        switch (playerPosture)
        {
            case PlayerPosture.Stand:
                // 如果竖直速度大于 0，说明进入跳跃上升阶段
                if (verticalVelocity > 0)
                {
                    playerPosture = PlayerPosture.Jumping;
                }
                // 如果离地且满足跌落高度要求，进入 Falling
                else if (!isGrounded && couldFall)
                {
                    playerPosture = PlayerPosture.Falling;
                }
                // 如果按下下蹲，切换到 Crouch
                else if (isCrouchPressed)
                {
                    playerPosture = PlayerPosture.Crouch;
                }
                break;
             
            case PlayerPosture.Crouch:
                // 下蹲状态离地则进入 Falling
                if (!isGrounded && couldFall)
                {
                    playerPosture = PlayerPosture.Falling;
                }
                // 松开下蹲则恢复站立
                else if (!isCrouchPressed)
                {
                    playerPosture = PlayerPosture.Stand;
                }
                break;

            case PlayerPosture.Falling:
                // 如果下落速度足够大，并且已经接触地面，则触发落地缓冲
                if (verticalVelocity <= landdingMinVelocity && isGrounded)
                {
                    StartCoroutine(CoolDownJump());
                }

                // 如果已经进入落地 CD，则转为 Landing
                if (isLanding)
                {
                    playerPosture = PlayerPosture.Landing;
                }
                break;

            case PlayerPosture.Jumping:
                // 从跳跃状态进入落地
                if (verticalVelocity < 0 && isGrounded)
                {
                    StartCoroutine(CoolDownJump());
                }

                if (isLanding)
                {
                    playerPosture = PlayerPosture.Landing;
                }
                break;

            case PlayerPosture.Landing:
                // 落地缓冲结束后恢复站立
                if (!isLanding) 
                {
                    playerPosture = PlayerPosture.Stand;
                }
                break;
        }
         
        // =========================
        // 装备状态切换
        // =========================
        if (!isEquip)
        {
            armState = ArmState.Normal;
        }
        else
        {
            if (isEquip)
                armState = ArmState.Equip;
        }
         
        // =========================
        // 水平移动状态切换
        // =========================
        if (moveInput.magnitude == 0)
        {
            locomotionState = LocomotionState.Idle;
        }
        else if (isRunPressed)
        {
            locomotionState = LocomotionState.Run;
        }
        else
        {
            locomotionState = LocomotionState.Walk;
        }
    }

    /// <summary>
    /// 落地后的短暂冷却协程
    /// 用于控制 Landing 姿态持续一小段时间
    /// </summary>
    private IEnumerator CoolDownJump()
    {
        // 根据当前下落速度，映射 Landing 过渡阈值
        landingThreshold = Mathf.Clamp(verticalVelocity, -10, 0);
        landingThreshold /= 20f;
        landingThreshold += 0.5f;

        isLanding = true;
        playerPosture = PlayerPosture.Landing;

        // 等待落地缓冲时间
        yield return new WaitForSeconds(jumpCD);

        isLanding = false;
    }

    /// <summary>
    /// 根据摄像机方向和输入，计算角色本地坐标系下的移动向量
    /// </summary>
    private void CaculateInputDirection()
    {
        // TODO: 原计划在攻击时禁止转向
        // if(combatController.CanExecuteCombo == false)
        //     return;

        // 获取摄像机在 XZ 平面上的前方向投影
        Vector3 cameraForwardProjection = new Vector3(
            cameraTransform.forward.x, 
            0, 
            cameraTransform.forward.z
        ).normalized;

        // 将输入映射到世界空间：
        // moveInput.y 控制前后
        // moveInput.x 控制左右
        playerMovement = cameraForwardProjection * moveInput.y + cameraTransform.right * moveInput.x;

        // 将世界空间移动向量转换到玩家本地坐标系
        // 方便后面用来计算角色转向和 Blend Tree 参数
        playerMovement = playerTransform.InverseTransformVector(playerMovement);
    }
     
    /// <summary>
    /// 设置 Animator 参数，驱动动画状态机
    /// </summary>
    private void SetupAnimator()
    {
        // =========================
        // 根据姿态设置 Posture 和 MoveSpeed
        // =========================
        if (playerPosture == PlayerPosture.Stand)
        {
            animator.SetFloat(postureHash, standThreshold, 0.1f, Time.deltaTime);

            switch (locomotionState)
            {
                case LocomotionState.Idle:
                    animator.SetFloat(moveSpeedHash, 0, 0.1f, Time.deltaTime);
                    break;

                case LocomotionState.Walk:
                    animator.SetFloat(moveSpeedHash, playerMovement.magnitude * walkSpeed, 0.1f, Time.deltaTime);
                    break;

                case LocomotionState.Run:
                    
                    animator.SetFloat(moveSpeedHash, playerMovement.magnitude * runSpeed, 0.1f, Time.deltaTime);
                    break;
            }
        }
        else if (playerPosture == PlayerPosture.Crouch)
        {
            animator.SetFloat(postureHash, crouchThreshold, 0.15f, Time.deltaTime);

            switch (locomotionState)
            {
                case LocomotionState.Idle:
                    animator.SetFloat(moveSpeedHash, 0, 0.1f, Time.deltaTime);
                    break;

                default:
                    animator.SetFloat(moveSpeedHash, playerMovement.magnitude * crouchSpeed, 0.1f, Time.deltaTime);
                    break;
            }
        }
        else if (playerPosture == PlayerPosture.Jumping)
        {
            animator.SetFloat(postureHash, midairThreshold, 0.1f, Time.deltaTime);
            animator.SetFloat(verticalSpeedHash, verticalVelocity, 0.1f, Time.deltaTime);
            animator.SetFloat(jumpTypeHash, footTween); // 控制左右脚跳跃动画
        }
        else if (playerPosture == PlayerPosture.Landing)
        {
            animator.SetFloat(postureHash, landingThreshold, 0.1f, Time.deltaTime);

            switch (locomotionState)
            {
                case LocomotionState.Idle:
                    animator.SetFloat(moveSpeedHash, 0, 0.1f, Time.deltaTime);
                    break;

                case LocomotionState.Walk:
                    animator.SetFloat(moveSpeedHash, playerMovement.magnitude * walkSpeed, 0.1f, Time.deltaTime);
                    break;

                case LocomotionState.Run:
                    animator.SetFloat(moveSpeedHash, playerMovement.magnitude * runSpeed, 0.1f, Time.deltaTime);
                    break;
            }
        }
        else if (playerPosture == PlayerPosture.Falling)
        {
            animator.SetFloat(postureHash, midairThreshold, 0.5f, Time.deltaTime);
            animator.SetFloat(verticalSpeedHash, verticalVelocity, 0.1f, Time.deltaTime);
            animator.SetFloat(jumpTypeHash, footTween);
        }
         
        // =========================
        // 根据移动方向设置转向
        // =========================
        // 当前这里无论 Normal 还是 Equip 都允许转向
        if (armState == ArmState.Normal || armState == ArmState.Equip)
        {
            // 计算角色本地坐标下移动方向与前方的夹角
            float rad = Mathf.Atan2(playerMovement.x, playerMovement.z);

            // 设置动画中的转向速度参数
            animator.SetFloat(turnSpeedHash, rad * 1.3f, 0.1f, Time.deltaTime);
             
            // 手动增加角色 Y 轴旋转，让角色真正面向移动方向
            playerTransform.Rotate(0, rad * 240 * Time.deltaTime, 0);
        }
    }

    /// <summary>
    /// 处理起跳
    /// </summary>
    private void Jump()
    {
        // 只有站立 / 下蹲状态下才能起跳
        // 并且要求当前按下跳跃键
        // 同时垂直速度不能太大，避免重复触发起跳
        if ((playerPosture == PlayerPosture.Stand || playerPosture == PlayerPosture.Crouch) && isJumpPressed && verticalVelocity < 2f && !ShouldBlockJumpDuringAttack())
        {
           
            // 播放跳跃音效
            playerSoundController.PlayJumpEffortSound();
             
            // 由最大高度反推起跳初速度
            verticalVelocity = Mathf.Sqrt(-2 * gravity * maxHeight);
            
             
            // 立即设置 Animator 中的垂直速度，提升动画切换流畅度
            animator.SetFloat(verticalSpeedHash, verticalVelocity);
             
            // 随机选择左右脚起跳动画
            footTween = Random.value > 0.5 ? 1f : -1f;
        }
    }

    private bool ShouldBlockJumpDuringAttack()
    {
        return combatController != null && combatController.IsAttackAnimationActive;
    }

    /// <summary>
    /// 计算最近几帧水平移动速度的平均值
    /// 用于在跳跃时保持更平滑的空中移动
    /// </summary>
    private Vector3 AverageVelocity(Vector3 newVel)
    {
        // 使用循环队列思路更新缓存
        velCache[currentCacheIndex] = newVel;
        currentCacheIndex++;
        currentCacheIndex %= CACHE_SIZE;
         
        // 计算平均值
        Vector3 average = Vector3.zero;
        foreach (Vector3 vel in velCache)
        {
            average += vel;
        }
        return average / CACHE_SIZE;
    }
     
    /// <summary>
    /// Animator 驱动位移
    /// 使用根运动控制角色移动
    /// </summary>
    private void OnAnimatorMove()
    {
        // 非 Jumping 状态：使用动画根运动 + 垂直速度
        if (playerPosture != PlayerPosture.Jumping)
        {
            Vector3 playerDeltaMovement = animator.deltaPosition; // 动画在这一帧产生的位移
            playerDeltaMovement.y = verticalVelocity * Time.deltaTime; // 叠加垂直方向位移
            characterController.Move(playerDeltaMovement);
             
            // 记录最近几帧平均水平速度，供空中状态使用
            averageVelocity = AverageVelocity(animator.velocity);
        }
        else
        {
            // Jumping 状态：沿用起跳前几帧的平均水平速度
            averageVelocity.y = verticalVelocity;
            Vector3 playerDeltaMovement = averageVelocity * Time.deltaTime;
            characterController.Move(playerDeltaMovement);
        }
    }

    /// <summary>
    /// 计算重力与上下运动
    /// </summary>
    private void CaculateGravity()
    {
        // 非跳跃、非下落状态
        if (playerPosture != PlayerPosture.Jumping && playerPosture != PlayerPosture.Falling)
        {
            // 不在地面时，说明可能在斜坡或边缘，仍然施加向下速度
            if (!isGrounded)
            {
                verticalVelocity += gravity * fallMultiplier * Time.deltaTime;
            }
            else
            {
                // CharacterController 需要保持一个微小向下速度，才能稳定贴地
                verticalVelocity = gravity * Time.deltaTime;
            }
        }
        else
        {
            // Jumping / Falling 状态下正常模拟重力
            if (verticalVelocity <= 0)
            {
                // 下落时加速更快
                verticalVelocity += gravity * fallMultiplier * Time.deltaTime;
            }
            else
            {
                // 上升时
                if (isJumpPressed)
                {
                    // 按住跳跃键：普通上升
                    verticalVelocity += gravity * Time.deltaTime;
                }
                else
                {
                    // 提前松开跳跃键：更快减速，形成短跳效果
                    verticalVelocity += gravity * longJumpMultiplier * Time.deltaTime;
                }
            }
        }
    }

    /// <summary>
    /// 检测是否接触地面
    /// </summary>
    private void CheckGrounded()
    {
        // 使用球形射线检测地面
        if (Physics.SphereCast(
                playerTransform.position + (Vector3.up * groundCheckOffset),
                characterController.radius,
                Vector3.down,
                out RaycastHit hit,
                groundCheckOffset - characterController.radius + 1.5f * characterController.skinWidth))
        {
            // 命中的对象是地面
            if (hit.collider.gameObject.CompareTag("Ground"))
            {
                isGrounded = true;
                couldFall = false;
            }
        }
        else
        {
            isGrounded = false;

            // 如果没有检测到地面，再用 Raycast 判断脚下是否已经离地超过最小跌落高度
            couldFall = !Physics.Raycast(playerTransform.position, Vector3.down, fallHeight);
        }
    }

    /// <summary>
    /// 在正确的步态时机播放脚步音效
    /// </summary>
    private void PlayFootStepSound()
    {
        // 空中状态不播放脚步声
        if (playerPosture != PlayerPosture.Jumping && playerPosture != PlayerPosture.Falling)
        {
            // 只有 Walk / Run 状态才播放
            if (locomotionState == LocomotionState.Walk || locomotionState == LocomotionState.Run)
            {
                // normalizedTime 可能大于 1，所以用 Repeat 限制到 0~1
                float currentFootCycle = Mathf.Repeat(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, 1f);

                // 在步态循环的 0.1 和 0.6 位置播放脚步音效
                // 这里通常对应左右脚落地时刻
                if ((lastFootCycle < 0.1f && currentFootCycle >= 0.1f) ||
                    (lastFootCycle < 0.6f && currentFootCycle >= 0.6f))
                {
                    playerSoundController.PlayFootStepSound();
                }

                lastFootCycle = currentFootCycle;
            }
        }
    }

    /// <summary>
    /// 空手奔跑时的急停逻辑
    /// </summary>
    public void NoEquipEmergencyStop()
    {
        // 满足奔跑且空手时，允许急停
        if (locomotionState == LocomotionState.Run && armState == ArmState.Normal)
        {
            canStop = true; 
        }
        // 当当前动画速度已经很小，则不再允许急停
        else if (animator.velocity.magnitude < 3.5f)
        {
            canStop = false;
        }

        // 当允许急停，且输入完全归零时，触发急停动画
        if (canStop && moveInput is { x: 0, y: 0 })
        { 
            currentFootCycle = Mathf.Repeat(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, 1f);

            // 根据当前是哪只脚在前，决定触发左右不同的急停动画
            if (currentFootCycle >= 0f && currentFootCycle < 0.5f)
            {
                animator.SetTrigger("StopRight");
            }
            else if (currentFootCycle >= 0.5f && currentFootCycle < 1f)
            {
                animator.SetTrigger("StopLeft");
            }

            canStop = false;
        }
    }

    #region 公共接口 

    public float GetWalkSpeed() => walkSpeed;        // 获取行走速度
    public float GetRunSpeed() => runSpeed;          // 获取奔跑速度
    public Vector3 GetPlayerMovement() => playerMovement; // 获取当前移动向量
    public Vector2 GetMoveInput() => moveInput;      // 获取当前输入方向

    #endregion
     
    #region 玩家输入相关  

    /// <summary>
    /// 主动订阅 PlayerInput 动作，避免依赖 Inspector 里的 UnityEvent 手动绑定。
    /// </summary>
    private void BindInputActions()
    {
        if (inputActionsBound)
            return;

        if (playerInput == null)
            playerInput = GetComponent<PlayerInput>();

        if (playerInput == null || playerInput.actions == null)
        {
            Debug.LogWarning($"{nameof(ThirdPersonController)} 找不到 PlayerInput 或 Input Actions，移动输入不会生效。", this);
            return;
        }

        InputActionMap playerMap = playerInput.actions.FindActionMap(playerInput.defaultActionMap, false);
        if (playerMap == null)
            playerMap = playerInput.actions.FindActionMap("Player", false);

        if (playerMap == null)
        {
            Debug.LogWarning($"{nameof(ThirdPersonController)} 找不到 Player 动作表，移动输入不会生效。", this);
            return;
        }

        moveAction = playerMap.FindAction("PlayerMovement", false);
        runSlideAction = playerMap.FindAction("Run/Slide", false);
        crouchAction = playerMap.FindAction("Crouch", false);
        jumpAction = playerMap.FindAction("Jump", false);

        if (moveAction != null)
        {
            moveAction.performed += GetMoveInput;
            moveAction.canceled += GetMoveInput;
        }
        else
        {
            Debug.LogWarning($"{nameof(ThirdPersonController)} 找不到 Move 动作，WASD 不会生效。", this);
        }

        if (runSlideAction != null)
        {
            runSlideAction.performed += GetRunInput;
            runSlideAction.canceled += GetRunInput;
        }
        else
        {
            Debug.LogWarning($"{nameof(ThirdPersonController)} 找不到 Run/Slide 动作，奔跑和翻滚不会生效。", this);
        }

        if (crouchAction != null)
        {
            crouchAction.started += GetCrouchInput;
        }
        else
        {
            Debug.LogWarning($"{nameof(ThirdPersonController)} 找不到 Crouch 动作，下蹲不会生效。", this);
        }

        if (jumpAction != null)
        {
            jumpAction.performed += GetJumpInput;
            jumpAction.canceled += GetJumpInput;
        }
        else
        {
            Debug.LogWarning($"{nameof(ThirdPersonController)} 找不到 Jump 动作，跳跃不会生效。", this);
        }

        playerMap.Enable();
        inputActionsBound = true;
    }

    /// <summary>
    /// 物体禁用或销毁时解除订阅，避免重复回调。
    /// </summary>
    private void UnbindInputActions()
    {
        if (!inputActionsBound)
            return;

        if (moveAction != null)
        {
            moveAction.performed -= GetMoveInput;
            moveAction.canceled -= GetMoveInput;
        }

        if (runSlideAction != null)
        {
            runSlideAction.performed -= GetRunInput;
            runSlideAction.canceled -= GetRunInput;
        }

        if (crouchAction != null)
        {
            crouchAction.started -= GetCrouchInput;
        }

        if (jumpAction != null)
        {
            jumpAction.performed -= GetJumpInput;
            jumpAction.canceled -= GetJumpInput;
        }

        inputActionsBound = false;
    }

    /// <summary>
    /// 接收移动输入
    /// </summary>
    public void GetMoveInput(InputAction.CallbackContext ctx)
    {
        moveInput = ctx.ReadValue<Vector2>();
    }

    /// <summary>
    /// 接收奔跑输入和空手翻滚输入
    /// Hold：按住奔跑
    /// Tap：轻点触发翻滚
    /// </summary>
    public void GetRunInput(InputAction.CallbackContext ctx)
    {
        if (ctx.interaction is HoldInteraction)
            isRunPressed = ctx.ReadValueAsButton();
        else if (ctx.interaction is TapInteraction)
        {
            // 只有在地面上且为空手时才允许翻滚
            if (isGrounded && armState == ArmState.Normal)
                animator.SetTrigger("Roll"); 
        }
    }

    /// <summary>
    /// 接收下蹲输入
    /// 按一次切换一次下蹲状态
    /// </summary>
    public void GetCrouchInput(InputAction.CallbackContext ctx)
    {
        if (ctx.started)
        {
            isCrouchPressed = !isCrouchPressed;
        }
    }

    /// <summary>
    /// 接收装备输入
    /// 当前逻辑被注释掉，暂未使用
    /// </summary>
    public void GetEquipInput(InputAction.CallbackContext ctx)
    {
        if (ctx.started)
        {
            // isEquipPressed = !isEquipPressed;
        }
    }

    /// <summary>
    /// 接收跳跃输入
    /// </summary>
    public void GetJumpInput(InputAction.CallbackContext ctx)
    {
        bool pressed = ctx.ReadValueAsButton();
        if (pressed && ShouldBlockJumpDuringAttack())
        {
            isJumpPressed = false; // 攻击动画中按下 Space 时只吞掉输入，不缓存到攻击结束后补跳
            return;
        }

        isJumpPressed = pressed;
       
    }
     
    #endregion
}
