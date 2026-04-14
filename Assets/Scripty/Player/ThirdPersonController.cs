 using System;
 using System.Collections;
 using System.Collections.Generic;
 using Unity.Collections.LowLevel.Unsafe;
 using Unity.VisualScripting;
 using UnityEngine;
 using UnityEngine.InputSystem;
 using Random = UnityEngine.Random;
 using UnityEngine.Animations.Rigging;
 using UnityEngine.InputSystem.Interactions;
 using UnityEngine.Serialization;

 public class ThirdPersonController : MonoBehaviour
 {
     #region 组件
     
     private Transform playerTransform;
     private Animator animator;
     private Transform cameraTransform;
     private CharacterController characterController;
     private PlayerSoundController playerSoundController;
     private CombatControllerBase combatController;
     
     #endregion
     
     //枚举状态机
     //角色姿态枚举 //切换混合树
     public enum PlayerPosture //  姿态层  处理垂直维度的状态。它决定了动画状态机中的 Posture 参数，从而控制 Animator 切换 Blend Tree。
     {
         Crouch, //下蹲
         Stand, //站立
         Falling, //下落
         Jumping,  //跳跃
         Landing,
     }
     [HideInInspector]
     public PlayerPosture playerPosture = PlayerPosture.Stand; //玩家初始状态站立
     
     #region 动画状态机阈值
     
     private float standThreshold = 0f;
     private float crouchThreshold = 1f;
     private float midairThreshold = 2.2f;
     private float landingThreshold = 1f;
     
     #endregion
     
     //角色运动状态枚举
     public enum LocomotionState //处理水平维度的速度。区分 Idle、Walk 和 Run，配合 moveSpeedHash 实现平滑的速度过渡。
     {
         Idle, //等待
         Walk, //行走
         Run, //奔跑
     }
     [HideInInspector]
     public LocomotionState locomotionState = LocomotionState.Idle; //默认等待
    
     #region 角色手持装备
     
     //角色手持装备状态枚举
     public enum ArmState  //预留了战斗系统的接口，区分空手和持枪状态，这符合 TPS 游戏的常规逻辑。
     {
         Normal = 0, //无武器
         Equip = 1 //装备武器
     }
     [HideInInspector]
     public ArmState armState = ArmState.Normal; //无武器

     public GameObject weaponOnBack;
     public GameObject weaponInHand;
     
     //TODO:检查此处修改
     // public TwoBoneIKConstraint rightHandIKConstraint;
     
     #endregion
     
     
     #region 角色速度
     
     float crouchSpeed = 0.8f; //下蹲移动速度
     float walkSpeed = 1.27f; //行走移动速度
     float runSpeed = 4.2f; //奔跑移动速度
     
     #endregion
     
     //角色移动输入
     private Vector2 moveInput;  //接收Inputy System awsd 输入的x和y变化
     //玩家实际移动方向
     private Vector3 playerMovement = Vector3.zero; 
     //角色是否可以急停
     [SerializeField]
     private bool canStop = false;
     //角色是否可以旋转
     
     #region 角色状态
     
     //角色状态输入
     private bool isRunPressed = false;  //状态 是否正在奔跑
     private bool isCrouchPressed = false; //是否正在下蹲
     [FormerlySerializedAs("isEquipPressed")] public bool isEquip = false; //是否装备武器
     private bool isKatana = false; 
     private bool isGrateSword = false;
     private bool isBow = false;
     private bool isJumpPressed = false;
     
     #endregion
     
     #region Animator中动画状态机的哈希值
     
     private int postureHash;  //用于获取引用动画状态机参数调整哈希值
     private int moveSpeedHash;
     private int turnSpeedHash;
     private int verticalSpeedHash;
     private int jumpTypeHash;
     //private int equipHash;
     
     #endregion
     
     #region 角色跳跃相关
     
     //重力
     public float gravity = -9.81f;
     //角色垂直方向速度
     private float verticalVelocity;
     //角色跳跃高度
     public float maxHeight = 1.5f;
     //玩家空中水平移动速度的缓存值
     private static readonly int CACHE_SIZE = 3;
     //缓存池
     private Vector3[] velCache = new Vector3[CACHE_SIZE];
     //缓存池中最老的向量的索引值
     private int currentCacheIndex = 0;
     //平均速度变量
     private Vector3 averageVelocity = Vector3.zero;
     //下坠时的加速度是上升时加速度的倍数
     public float fallMultiplier = 1.5f;
     //角色是否落地
     [SerializeField]
     private bool isGrounded;
     //射线检测偏移量
     private float groundCheckOffset = 0.5f;
     //角色跳跃时的左右脚
     private float footTween;
     //角色跳跃的CD时间
     private float jumpCD = 0.15f;
     //角色是否处于跳跃CD中
     [SerializeField]
     private bool isLanding = false;
     //角色长按跳跃键时的加速度是普通加速度的倍数
     public float longJumpMultiplier = 2.5f;
     [SerializeField]
     //角色是否可以跌落
     private bool couldFall = false;
     //角色跌落的最小高度，小于此高度则不会切换到跌落姿态
     private float fallHeight = 0.5f;
     //角色跌落时，能够被判定为Landing状态的最小速度
     private float landdingMinVelocity;
     
     #endregion

     #region 角色音效相关
     
     //上一帧的动画nornalized时间
     private float lastFootCycle = 0f;
     
     #endregion

     #region 角色急停相关

     private float currentFootCycle = 0f;

     #endregion
     
     void Start()
     {
         playerTransform = this.transform;  //获取玩家位置数据
         animator = this.GetComponent<Animator>(); //获取玩家身上的状态机
         cameraTransform = Camera.main.transform; //获取摄像机位置
         characterController = this.GetComponent<CharacterController>(); //获取角色控制器
         playerSoundController = this.GetComponent<PlayerSoundController>(); //玩家音效控制器
         combatController = this.GetComponent<CombatControllerBase>(); //
         
         //获取哈希值
         postureHash = Animator.StringToHash("Posture"); //获取 姿态
         moveSpeedHash = Animator.StringToHash("MoveSpeed"); //移动
         turnSpeedHash = Animator.StringToHash("TurnSpeed"); //转向
         verticalSpeedHash = Animator.StringToHash("VerticalSpeed"); //下落垂直高度
         jumpTypeHash = Animator.StringToHash("JumpType"); //跳跃类型
         //TODO: 检查此处是否存在问题
         // equipHash = Animator.StringToHash("WeaponType");
         
         //锁定鼠标
         Cursor.lockState = CursorLockMode.Locked; // 将鼠标锁定到游戏里
         
         //根据最小的跌落高度来计算落地CD速度的阈值（绝对值低于该速度则不计算落地CD）
         landdingMinVelocity = -Mathf.Sqrt(-2 * gravity * fallHeight); 
         landdingMinVelocity -= 1f;
     }

     void Update()
     {
         //地面检测
         CheckGrounded();
         //切换角色姿态
         SwitchPlayerStates();
         
         //先计算角色重力
         CaculateGravity();
         //再执行跳跃
         Jump();
         
         //计算输入方向
         CaculateInputDirection();
         //设置动画状态
         SetupAnimator();
         //播放脚步声
         PlayFootStepSound();
         
         //急停
         NoEquipEmergencyStop();
     }

     /// <summary>
     /// 更改玩家状态
     /// </summary>
     private void SwitchPlayerStates()
     {
         //玩家姿态  switch 根据当前姿态在不同姿态根据条件进行切换
         switch (playerPosture)  //swicth 根据角色角色姿态枚举的枚举状态机 来调整玩家当前姿态和要执行的动作
         {
             case PlayerPosture.Stand:
                 //垂直速度大于0，说明此时为跳跃状态
                 if (verticalVelocity > 0)  //切换跳跃状态
                 {
                     playerPosture = PlayerPosture.Jumping;
                 }
                 //站立状态下跌落
                 else if (!isGrounded && couldFall) //切换下落状态
                 {
                     playerPosture = PlayerPosture.Falling;
                 }
                 else if (isCrouchPressed) //切换下蹲
                 {
                     playerPosture = PlayerPosture.Crouch;
                 }
                 break;
             
             case PlayerPosture.Crouch: //当前为下蹲姿态
                 //从下蹲状态跌落
                 if (!isGrounded && couldFall)
                 {
                     playerPosture = PlayerPosture.Falling; //切换下落姿态
                 }
                 else if (!isCrouchPressed)
                 {
                     playerPosture = PlayerPosture.Stand; //切换站立姿态
                 }
                 break;
             //坠落状态下
             case PlayerPosture.Falling: //当前下落姿态
                 //落地
                 //TODO: 有没有比判断当前垂直速度更好的方法，来预防角色在墙体边缘可能出现的卡Landing状态的情况
                 if (verticalVelocity <= landdingMinVelocity && isGrounded) //如果是在地面 且垂直下落速度小于角色跌落时，能够被判定为Landing状态的最小速度
                 {
                     //计算跳跃冷却时间=
                     StartCoroutine(CoolDownJump()); //通过携程异步计算刚下落要跳跃的冷却时间
                 }
                 //冷却状态
                 if (isLanding)
                 {
                     playerPosture = PlayerPosture.Landing;
                 }
                 break;
             //跳跃状态下
             case PlayerPosture.Jumping: //当前在跳跃状态
                 //落地
                 if (verticalVelocity < 0 && isGrounded) //垂直下落速度小于0 且在陆地
                 {
                     //计算跳跃冷却时间
                     StartCoroutine(CoolDownJump()); //通过携程异步计算刚下落要跳跃的冷却时间
                 }
                 //落地冷却状态
                 if (isLanding)
                 {
                     playerPosture = PlayerPosture.Landing;//切换冷却状态
                 }
                 break;
             //落地冷却状态
             case PlayerPosture.Landing:
                 //落地冷却状态下，设为站立姿态，保证此状态下玩家能够行走
                 if (!isLanding) 
                 {
                     playerPosture = PlayerPosture.Stand;
                 }
                 break;
         }
         
         
         //更改玩家装备状态
         //装备状态
         if (!isEquip)
         {
             armState = ArmState.Normal;
         }
         else
         {
             if (isEquip)
                 armState = ArmState.Equip;
         }
         
         //玩家输入
         if (moveInput.magnitude == 0) //如果忘记输入变化量为0则为等待
         {
             locomotionState = LocomotionState.Idle;
         }
         else if(isRunPressed) //切换奔跑状态
         {
             locomotionState = LocomotionState.Run;
         }
         else //切换行走状态
         {
             locomotionState = LocomotionState.Walk;
         }
     }

     
     /// <summary>
     /// 用于计算跳跃cd时间的协程函数
     /// </summary>
     /// <returns></returns>
     private IEnumerator CoolDownJump()
     {
         //根据下落速度来计算落地后跳跃CD状态的阈值，以此设置下蹲动画状态的程度
         //去掉小于-10和大于0的速度
         landingThreshold = Mathf.Clamp(verticalVelocity, -10, 0);
         //限制landingThreshold在[-0.5, 0]
         landingThreshold /= 20f;
         //将landingThreshold变为[0.5, 1]
         //TEST: 将landingThreshold变为[0, 0.5]
         landingThreshold += 0.5f;
         isLanding = true;
         
         playerPosture = PlayerPosture.Landing;
         //等待CD时间后
         yield return new WaitForSeconds(jumpCD);
         //将CD状态设为false
         isLanding = false;
     }

     /// <summary>
     /// 计算移动方向
     /// </summary>
     private void CaculateInputDirection() //根据摄像机方向计算键盘输入移动方向
     {
         //TODO: 控制玩家在攻击时不能旋转
         // if(combatController.CanExecuteCombo == false)
         //     return;
         //获取相机在水平平面（XZ平面）上的投影，并做归一化
         Vector3 cameraForwardProjection = new Vector3(cameraTransform.forward.x, 0, cameraTransform.forward.z).normalized; //根据摄像机的位置在x和z轴平面计算位置
         //根据玩家输入moveInput和相机XZ投影，计算玩家移动的三维向量值
         //输入的Y分量乘以投影的方向，输入的X分量乘以相机右方向
         //TODO: 没看懂
         playerMovement = cameraForwardProjection * moveInput.y + cameraTransform.right * moveInput.x; //根据摄像机位置在xz位置和玩家输入的前后的变化量 以及摄像机的右向量和玩家输入的左右变化量 计算玩家的移动和方向
         //将该向量转换到玩家本地坐标系下，得到玩家Y方向和输入方向的夹角
         playerMovement = playerTransform.InverseTransformVector(playerMovement);//将摄像机计算出来的移动方向 根据玩家位置 计算初玩家的移动方向
     }
     
     /// <summary>
     /// 设置动画状态机
     /// </summary>
     private void SetupAnimator()
     {
         //站立状态
         if (playerPosture == PlayerPosture.Stand) //当前为站立状态
         {
             //线性插值地改变动画状态机中的Posture变量为Stand值
             animator.SetFloat(postureHash, standThreshold, 0.1f, Time.deltaTime);  //设置动画机 姿态为0为站立 间隔施加0.1
             switch (locomotionState) //本地玩家移动状态
             {
                 case LocomotionState.Idle: //当为等待状态 则动画机里移动参数为0
                     animator.SetFloat(moveSpeedHash, 0, 0.1f, Time.deltaTime);
                     break;
                 case LocomotionState.Walk://当为行走状态 则动画机里移动参数为为走了速度乘inputy system输入变化量
                     animator.SetFloat(moveSpeedHash, playerMovement.magnitude * walkSpeed, 0.1f, Time.deltaTime);
                     break;
                 case LocomotionState.Run://当为奔跑状态 则动画机里奔跑参数为为走了速度乘inputy system输入变化量
                     animator.SetFloat(moveSpeedHash, playerMovement.magnitude * runSpeed, 0.1f, Time.deltaTime);
                     break;
             }
         }
         //下蹲状态
         else if (playerPosture == PlayerPosture.Crouch)
         {
             //线性插值地改变动画状态机中的Posture为Crouch值
             animator.SetFloat(postureHash, crouchThreshold, 0.15f, Time.deltaTime);
             switch (locomotionState) //设置动画机 姿态为1为下蹲 间隔施加0.1
             {
                 case LocomotionState.Idle: //动画机等待参数调整
                     animator.SetFloat(moveSpeedHash, 0, 0.1f, Time.deltaTime);
                     break;
                 default:        //动画机下蹲行走参数调整
                     animator.SetFloat(moveSpeedHash, playerMovement.magnitude * crouchSpeed, 0.1f, Time.deltaTime);
                     break;
             }
         }
         //滞空状态
         else if (playerPosture == PlayerPosture.Jumping) //跳跃状态
         {
             //线性插值地改变动画状态机中的Posture为Midair值
             animator.SetFloat(postureHash, midairThreshold, 0.1f, Time.deltaTime);
             //设置状态机中VerticalSpeed的值
             animator.SetFloat(verticalSpeedHash, verticalVelocity, 0.1f, Time.deltaTime);
             animator.SetFloat(jumpTypeHash, footTween);
         }
         //跳跃CD状态
         else if (playerPosture == PlayerPosture.Landing)  //跳跃后的冷静状态
         {
             //线性插值地改变动画状态机中的Posture变量为Stand值
             animator.SetFloat(postureHash, landingThreshold, 0.1f, Time.deltaTime);
             switch (locomotionState)
             {
                 case LocomotionState.Idle:
                     animator.SetFloat(moveSpeedHash, 0, 0.1f, Time.deltaTime);
                     break;
                 case LocomotionState.Walk: //冷静状态行走
                     animator.SetFloat(moveSpeedHash, playerMovement.magnitude * walkSpeed, 0.1f, Time.deltaTime);
                     break;
                 case LocomotionState.Run: //冷静状态奔跑
                     animator.SetFloat(moveSpeedHash, playerMovement.magnitude * runSpeed, 0.1f, Time.deltaTime);
                     break;
             }
         }
         //跌落状态
         else if (playerPosture == PlayerPosture.Falling)
         {
             //线性插值地改变动画状态机中的Posture为Midair值
             animator.SetFloat(postureHash, midairThreshold, 0.5f, Time.deltaTime);
             //设置状态机中VerticalSpeed的值
             animator.SetFloat(verticalSpeedHash, verticalVelocity, 0.1f, Time.deltaTime);
             animator.SetFloat(jumpTypeHash, footTween);
         }
         
         //若不处于瞄准状态
         if (armState == ArmState.Normal || armState == ArmState.Equip)  //角色手持装备状态枚举 要么无装备或者有装备
         {
             //得到玩家当前运动方向playerMovement.x和玩家当前正前方playerMovement.z的夹角（弧度制）
             float rad = Mathf.Atan2(playerMovement.x, playerMovement.z);
             animator.SetFloat(turnSpeedHash, rad * 1.3f, 0.1f, Time.deltaTime);//装修动画参数
             
             //人为添加Y轴上的旋转，令转向速度加快
             playerTransform.Rotate(0, rad * 240 * Time.deltaTime, 0);//玩家方向转向
         }
     }

     /// <summary>
     /// 玩家跳跃
     /// </summary>
     private void Jump()
     {
         //若当前角色姿态为站立或下蹲，且按下了跳跃键，且角色竖直速度小于一个阈值（为防止按下跳跃后角色还没进入Jump状态时，该部分代码被重复执行）
         if ((playerPosture == PlayerPosture.Stand || playerPosture == PlayerPosture.Crouch) && isJumpPressed && verticalVelocity < 2f)
         {
             //播放跳跃音效
             playerSoundController.PlayJumpEffortSound();
             
             //根据跳跃最大高度，计算起跳初速度
             verticalVelocity = Mathf.Sqrt(-2 * gravity * maxHeight);
             
             //为保证站立到跳跃的动画切换流畅，此处立即将速度设为起跳初速度
             animator.SetFloat(verticalSpeedHash, verticalVelocity);
             
             //随机左右脚跳跃动画
             footTween = Random.value > 0.5 ? 1f : -1f;
         }
     }

     /// <summary>
     /// 计算玩家离地前3帧的平均水平移动速度
     /// </summary>
     /// <param name="newVel">当前帧的速度</param>
     /// <returns>计算出的平均速度</returns>
     //TODO: 该方法常用于游戏开发中的各种平滑和去噪场景
     private Vector3 AverageVelocity(Vector3 newVel)
     {
         //缓存池设计为循环队列
         //新速度替换缓存池中最老的速度
         velCache[currentCacheIndex] = newVel;
         currentCacheIndex++;
         //取模，防止索引越界
         currentCacheIndex %= CACHE_SIZE;
         
         //计算缓存池中速度的平均值
         Vector3 average = Vector3.zero;
         foreach (Vector3 vel in velCache)
         {
             average += vel;
         }
         return average / CACHE_SIZE;
     }
     
     /// <summary>
     /// 代码控制角色移动
     /// </summary>
     private void OnAnimatorMove()
     {
         if (playerPosture != PlayerPosture.Jumping)
         {
             //每个deltaTime时间内，角色的移动位置(注意：deltaPosition会受到帧率大小的影响）
             Vector3 playerDeltaMovement = animator.deltaPosition;
             //垂直方向上位移 = 垂直方向上速度 * 间隔时间deltaTime
             playerDeltaMovement.y = verticalVelocity * Time.deltaTime;
             characterController.Move(playerDeltaMovement);
             
             //计算前三帧的水平平均速度
             averageVelocity = AverageVelocity(animator.velocity);
         }
         else
         {
             //沿用角色在地面时的水平移动速度
             //使用角色离地前几帧的平均速度，避免不确定因素对玩家空中移动速度的影响
             //此处使用速度，而不使用deltaPosition，是为了避免deltaPosition受到帧率大小的影响而得到不准确的值
             averageVelocity.y = verticalVelocity;
             Vector3 playerDeltaMovement = averageVelocity * Time.deltaTime;
             characterController.Move(playerDeltaMovement);
         }
     }

     /// <summary>
     /// 计算角色重力
     /// </summary>
     private void CaculateGravity()
     {
         //若角色状态不是跳跃也不是跌落
         if (playerPosture != PlayerPosture.Jumping && playerPosture != PlayerPosture.Falling)
         {
             //不在跳跃状态和跌落状态，但也没有在地面上时（如在斜坡上时）
             if (!isGrounded) //处理斜坡
             {
                 //添加向下的速度
                 verticalVelocity += gravity * fallMultiplier * Time.deltaTime;
             }
             //在地面上时
             else 
             {
                 //CharacterController的isGrounded要求角色必须持续有向下的速度
                 //站在地面上时，重力不会累加
                 verticalVelocity = gravity * Time.deltaTime;
             }
         }
         else //如果玩家在跳跃或下落
         {
             //重力加速度公式，模拟重力
             //下降时
             if (verticalVelocity <= 0)
             {
                 verticalVelocity += gravity * fallMultiplier * Time.deltaTime;
             }
             //上升时
             else
             {
                 if (isJumpPressed) //在跳跃过程中 施加向下作用力
                 {
                     verticalVelocity += gravity * Time.deltaTime;
                 }
                 else
                 {
                     //若玩家没有长按跳跃键，则加快玩家的下落速度
                     verticalVelocity += gravity * longJumpMultiplier * Time.deltaTime;
                 }
             }
         }
     }

     /// <summary>
     /// 检测玩家是否落地
     /// </summary>
     private void CheckGrounded()
     {
         //射线检测到了碰撞体
         //TODO: 1.5f个characterController.skinWidth的距离不会引起误判
         if (Physics.SphereCast(playerTransform.position + (Vector3.up * groundCheckOffset), characterController.radius,
                 Vector3.down, out RaycastHit hit,
                 groundCheckOffset - characterController.radius + 1.5f * characterController.skinWidth)) //通过物理根据玩家位置形成球形的碰撞检测区域来检测是否接触地面
         {
             //碰撞体是地面
             if (hit.collider.gameObject.CompareTag("Ground")) //如果接触地面 则设置在地面状态  如果不在地面计算玩家离地面的高度
             {
                 isGrounded = true;
                 couldFall = false;
             }
         }
         //未检测到碰撞体
         else
         {
             isGrounded = false;
             //再发射一条射线，检测地面是否离角色脚底fallHeight的高度
             couldFall = !Physics.Raycast(playerTransform.position, Vector3.down, fallHeight);
         }
     }

     /// <summary>
     /// 播放脚步音效
     /// </summary>
     private void PlayFootStepSound()
     {
         //角色状态不是跳跃也不是跌落
         if (playerPosture != PlayerPosture.Jumping && playerPosture != PlayerPosture.Falling)
         {
             //当角色正在行走或奔跑
             if (locomotionState == LocomotionState.Walk || locomotionState == LocomotionState.Run)
             {
                 //currentFootCycle为动画在0-1之间的循环值
                 float currentFootCycle = Mathf.Repeat(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, 1f);
                 //在动画播放到0.1或0.6时（即角色动画脚着地时）播放音效
                 if ((lastFootCycle < 0.1f && currentFootCycle >= 0.1f) ||
                     (lastFootCycle < 0.6f && currentFootCycle >= 0.6f))
                 {
                     //播放脚步声音效
                     playerSoundController.PlayFootStepSound();
                 }
                 lastFootCycle = currentFootCycle;
             }
         }
     }

     /// <summary>
     /// 没有装备武器时，奔跑下的急停
     /// </summary>
     public void NoEquipEmergencyStop()
     {
         //控制是否可以急停的条件
         if (locomotionState == LocomotionState.Run && armState == ArmState.Normal) //本地输入状态 正在奔跑且无装备 可以急停
         {
             canStop = true; 
         }
         else if (animator.velocity.magnitude < 3.5f)  //
         {
             canStop = false;
         }
         //TODO: 优化奔跑时急停动画的切换
         //控制奔跑时的急停
         if (canStop && moveInput is { x: 0, y: 0 }) //当能急停时
         { 
             currentFootCycle = Mathf.Repeat(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, 1f); //获取当前动画机第0层信息 获取当前走路循环
             //根据动画此时迈的是哪只脚来播放不同脚的急停动画
             if (currentFootCycle >= 0f && currentFootCycle < 0.5f)
             {
                 //TODO: 急停动画的过渡需要优化
                 //animator.CrossFade("NormalStopRight", 0.25f);
                 animator.SetTrigger("StopRight");
             }
             else if (currentFootCycle >= 0.5f && currentFootCycle < 1f)
             {
                 //TODO: 急停动画的过渡需要优化
                 //animator.CrossFade("NormalStopLeft", 0.25f);
                 animator.SetTrigger("StopLeft");
             }
             canStop = false;
         }
     }


     #region 公共接口 
     //单列接口
     public float GetWalkSpeed() => walkSpeed;  
     
     public float GetRunSpeed() => runSpeed;
     
     public Vector3 GetPlayerMovement() => playerMovement;
     
     public Vector2 GetMoveInput() => moveInput; //使用该方法先调用函数事件

     #endregion
     
     
     #region 玩家输入相关  
     //获取INputy 输入系统变化的回调
     //获取玩家移动输入
     public void GetMoveInput(InputAction.CallbackContext ctx)
     {
         moveInput = ctx.ReadValue<Vector2>();
     }
     //获取玩家奔跑状态和空手状态下闪避的输入
     public void GetRunInput(InputAction.CallbackContext ctx)
     {
         if(ctx.interaction is HoldInteraction)
             isRunPressed = ctx.ReadValueAsButton();
         else if (ctx.interaction is TapInteraction)
         {
             //在地面上 且 处于空手状态下
             if(isGrounded && armState == ArmState.Normal)
                 animator.SetTrigger("Roll"); 
         }
     }
     //获取玩家下蹲状态输入
     public void GetCrouchInput(InputAction.CallbackContext ctx)
     {
         if (ctx.started)
         {
             isCrouchPressed = !isCrouchPressed;
         }
     }
     //获取玩家装备武器状态输入
     //TODO: 该方法有待移除
     public void GetEquipInput(InputAction.CallbackContext ctx)
     {
         if (ctx.started)
         {
             //isEquipPressed = !isEquipPressed;
         }
     }
     //获取玩家跳跃输入
     public void GetJumpInput(InputAction.CallbackContext ctx)
     {
         isJumpPressed = ctx.ReadValueAsButton();
     }
     
     #endregion
 }
