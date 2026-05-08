using System; // 引入 C# 基础命名空间。当前代码中没有直接使用，可以删除
using System.Collections; // 引入 IEnumerator、协程相关功能。当前代码中没有直接使用，可以删除
using System.Collections.Generic; // 引入 List、Dictionary 等集合类型。当前代码中没有直接使用，可以删除
using UnityEngine; // 引入 Unity 核心命名空间，例如 MonoBehaviour、Vector3、Animator、CharacterController、Physics 等

// 敌人移动控制器
// 这个类继承自 MonoBehaviour，说明它可以挂载到 Unity 场景中的敌人 GameObject 上
// 主要作用：
// 1. 控制敌人移动
// 2. 处理敌人重力
// 3. 检测敌人是否站在地面上
// 4. 修正敌人在坡面上的移动方向
// 5. 检测动画移动时前方是否有障碍物
// 6. 保持敌人身体不会因为物理或移动产生 X/Z 轴倾斜
public class EnemyMovementController : MonoBehaviour
{
    // =========================
    // 引用部分
    // =========================

    // protected 表示：
    // 当前类可以访问，继承它的子类也可以访问
    // 适合用来写“敌人移动基类”，之后可以派生出普通敌人、Boss、精英怪等移动控制器

    // 敌人的 Animator 动画组件
    // 用于控制敌人动画参数，例如移动速度、横向移动、纵向移动等
    protected Animator animator;

    // Unity 自带的 CharacterController 角色控制器
    // 这个组件不使用 Rigidbody 物理刚体推动角色
    // 而是通过 CharacterController.Move() 主动移动角色
    protected CharacterController characterController;

    // =========================
    // 移动方向相关
    // =========================

    // 敌人的水平移动方向
    // 例如敌人向玩家靠近时，这个变量就是“敌人指向玩家”的方向
    protected Vector3 movementDirection;

    // 敌人的垂直方向移动
    // 主要用于处理重力，也就是 Y 轴方向的移动
    protected Vector3 verticalDirection;

    // =========================
    // 移动速度与重力相关
    // =========================

    // SerializeField 表示这个 private/protected 变量可以在 Unity Inspector 面板中显示和修改
    // Header("移动速度") 会在 Inspector 中显示一个标题，方便整理参数

    [SerializeField, Header("移动速度")]
    // 敌人的重力加速度
    // 通常这个值应该是负数，例如 -15、-20、-30
    // 原理：每帧 verticalSpeed += characterGravity * Time.deltaTime
    // 如果 characterGravity 是负数，verticalSpeed 会越来越小，角色就会向下掉落
    protected float characterGravity;

    [SerializeField]
    // 当前角色移动速度
    // 这个变量在当前代码中没有被实际使用到
    // 可能是预留给子类、AI 状态机、动画系统使用
    protected float characterCurrentMoveSpeed;

    // 下落缓冲时间
    // 作用：角色离开地面后，不是立刻进入下落状态，而是有一个短暂缓冲
    // 常用于避免角色经过小台阶、小坡度、地面检测抖动时频繁切换落地/下落状态
    protected float characterFallTime = 0.15f;

    // 当前剩余的下落缓冲时间
    // Start 中会初始化为 characterFallTime
    protected float characterFallOutDeltaTime;

    // 当前垂直速度
    // 用于模拟敌人受到重力影响时的 Y 轴速度
    protected float verticalSpeed;

    // 最大垂直速度
    // 按变量名理解应该是限制最大下落速度
    // 但当前代码的使用方式存在问题，后面会单独说明
    protected float maxVerticalSpeed = 53f;

    // =========================
    // 地面检测相关
    // =========================

    [SerializeField, Header("地面检测")]
    // 地面检测球体半径
    // 原理：使用 Physics.CheckSphere 在角色脚底生成一个小球
    // 如果这个球碰到了地面层，就认为角色在地面上
    protected float groundDetectionRang;

    [SerializeField]
    // 地面检测点的 Y 轴偏移
    // 因为 transform.position 通常在角色身体中心或脚底附近
    // 通过这个偏移可以把检测球放到更接近脚底的位置
    protected float groundDetectionOffset;

    [SerializeField]
    // 坡度检测射线长度
    // 用于从角色位置向下发射射线，检测地面的法线方向
    // 然后根据法线修正角色移动方向
    protected float slopRayExtent;

    [SerializeField]
    // 哪些 Layer 被认为是地面
    // 例如 Ground、Terrain、Map 等层
    // CheckSphere 只会检测这些层
    protected LayerMask whatIsGround;

    [SerializeField, Tooltip("角色动画移动时检测障碍物的层级")]
    // 哪些 Layer 被认为是障碍物
    // 例如 Wall、Obstacle、Building 等
    // 用于判断敌人动画移动时前方是否被挡住
    protected LayerMask whatIsObs;

    [SerializeField]
    // 当前敌人是否在地面上
    // true 表示站在地面上
    // false 表示在空中或正在下落
    protected bool isOnGround;

    // =========================
    // 动画参数 ID
    // =========================

    // Animator.StringToHash 的作用：
    // 把字符串参数名转换成 int 类型 Hash
    // 原理：Animator.SetFloat("MoveSpeed", value) 每次都用字符串查找，性能略低
    // 使用 Hash 后，可以减少字符串查找开销
    // 在频繁调用 Animator 参数时，这种写法更推荐

    // 动画移动参数 ID
    // 对应 Animator Controller 中名为 "AnimationMove" 的 Float 参数
    // 这个参数可能用于表示当前动画的位移强度，或者动画根运动的移动量
    protected int animationMoveID = Animator.StringToHash("AnimationMove");

    // 横向动画参数 ID
    // 对应 Animator Controller 中名为 "Horizontal" 的 Float 参数
    // 当前代码里没有使用，可能是预留给八方向移动、锁定移动、战斗横移使用
    protected int horizontalHash = Animator.StringToHash("Horizontal");

    // 纵向动画参数 ID
    // 对应 Animator Controller 中名为 "Vertical" 的 Float 参数
    // 当前代码里没有使用，可能是预留给前后移动动画混合使用
    protected int verticalHash = Animator.StringToHash("Vertical");

    // 移动速度动画参数 ID
    // 对应 Animator Controller 中名为 "MoveSpeed" 的 Float 参数
    // CharacterMoveInterface 中会设置这个参数，用来控制移动动画状态
    protected int moveSpeedHash = Animator.StringToHash("MoveSpeed");

    // Awake 会在脚本实例被加载时执行
    // 通常用于获取组件引用
    protected virtual void Awake()
    {
        // 从当前物体或子物体中获取 Animator
        // GetComponentInChildren 表示 Animator 可以在敌人的子物体上
        // 很多角色模型结构是：
        // Enemy 根物体
        // └── Model 子物体
        //     └── Animator
        animator = GetComponentInChildren<Animator>();

        // 从当前敌人根物体上获取 CharacterController
        // CharacterController 一般挂在敌人根物体上
        characterController = GetComponent<CharacterController>();
    }

    // Start 会在第一帧 Update 之前执行
    // 通常用于初始化运行时数据
    protected virtual void Start()
    {
        // 初始化下落缓冲时间
        // 一开始让 characterFallOutDeltaTime 等于 characterFallTime
        characterFallOutDeltaTime = characterFallTime;
    }

    // Update 每帧执行一次
    // 这里每帧都会处理重力、地面检测、旋转修正
    protected virtual void Update()
    {
        // 处理敌人重力
        CharacterGravity();

        // 检测敌人是否在地面上
        CheckOnGround();

        // 修正敌人的 X/Z 轴旋转，防止敌人倾斜
        FreezeRotation();
    }

    // LateUpdate 在所有 Update 执行之后执行
    // 当前没有实际逻辑
    private void LateUpdate()
    {
        // 这里原本可能想把 FreezeRotation 放到 LateUpdate
        // 因为 LateUpdate 在动画、移动逻辑之后执行
        // 如果敌人在 Update 中被其他逻辑旋转，LateUpdate 再修正会更稳定
        //FreezeRotation();
    }

    #region 内部函数

    /// <summary>
    /// 角色重力
    /// 作用：
    /// 让敌人在不接触地面时向下掉落
    /// 在接触地面时保持一个轻微向下的速度，保证 CharacterController 贴地
    /// </summary>
    private void CharacterGravity()
    {
        // 如果敌人在地面上
        if (isOnGround)
        {
            // 重置下落缓冲时间
            // 表示角色重新站稳了，可以重新开始计算离地后的缓冲
            characterFallOutDeltaTime = characterFallTime;

            // 如果当前垂直速度小于 0，说明角色正在向下掉
            if (verticalSpeed < 0.0f)
            {
                // 把垂直速度设置为 -2
                // 原理：
                // CharacterController 不会自动吸附地面
                // 给一个轻微向下的速度，可以让角色稳定贴在地面上
                // 避免角色在斜坡、小台阶上出现轻微悬空
                verticalSpeed = -2f;
            }
        }
        else
        {
            // 如果敌人不在地面上，说明正在空中或开始下落

            // 如果下落缓冲时间还没结束
            if (characterFallOutDeltaTime >= 0.0f)
            {
                // 每帧减少缓冲时间
                characterFallOutDeltaTime -= Time.deltaTime;

                // 限制 characterFallOutDeltaTime 的范围
                // 防止它小于 0 或大于 characterFallTime
                characterFallOutDeltaTime = Mathf.Clamp(
                    characterFallOutDeltaTime,
                    0,
                    characterFallTime
                );
            }
        }

        // 应用重力
        // 原理：
        // 速度 = 速度 + 加速度 * 时间
        // verticalSpeed += characterGravity * Time.deltaTime
        //
        // 注意：
        // 如果 characterGravity 是负数，例如 -20
        // verticalSpeed 会越来越小，角色向下移动
        //
        // 当前代码这里的 maxVerticalSpeed 判断逻辑可能有问题
        // 后面会详细说明
        if (verticalSpeed < maxVerticalSpeed)
        {
            verticalSpeed += characterGravity * Time.deltaTime;
        }
    }

    /// <summary>
    /// 地面检测
    /// 作用：
    /// 检测敌人当前是否站在地面上
    /// 原理：
    /// 在角色脚底附近创建一个虚拟球体
    /// 如果球体和地面 Layer 发生重叠，就认为角色在地面上
    /// </summary>
    private void CheckOnGround()
    {
        // 计算地面检测球的位置
        // x 和 z 使用角色当前位置
        // y 轴向下偏移 groundDetectionOffset
        // 这样检测点会更接近角色脚底
        Vector3 spherePosition = new Vector3(
            transform.position.x,
            transform.position.y - groundDetectionOffset,
            transform.position.z
        );

        // 使用物理检测球判断是否接触地面
        // 参数解释：
        // spherePosition：球心位置
        // groundDetectionRang：球体半径
        // whatIsGround：只检测指定的地面 Layer
        // QueryTriggerInteraction.Ignore：忽略 Trigger 触发器
        isOnGround = Physics.CheckSphere(
            spherePosition,
            groundDetectionRang,
            whatIsGround,
            QueryTriggerInteraction.Ignore
        );
    }

    // 这个函数只会在 Scene 视图中选中该物体时绘制 Gizmos
    // 作用：
    // 可视化地面检测球的位置和范围，方便调试
    private void OnDrawGizmosSelected()
    {
        // 如果敌人在地面上，检测球显示绿色
        if (isOnGround)
            Gizmos.color = Color.green;
        else
            // 如果敌人不在地面上，检测球显示红色
            Gizmos.color = Color.red;

        // 创建一个临时位置变量
        Vector3 position = Vector3.zero;

        // 设置检测球的位置
        // 和 CheckOnGround 中的检测位置保持一致
        position.Set(
            transform.position.x,
            transform.position.y - groundDetectionOffset,
            transform.position.z
        );

        // 在 Scene 视图中画出地面检测球的线框
        Gizmos.DrawWireSphere(position, groundDetectionRang);
    }

    /// <summary>
    /// 坡度检测
    /// 作用：
    /// 当敌人在斜坡上移动时，把移动方向投影到坡面上
    /// 防止敌人沿着水平向量移动导致卡坡、浮空、穿坡
    /// </summary>
    /// <param name="dir">当前水平移动方向</param>
    /// <returns>修正后的移动方向</returns>
    protected Vector3 ResetMoveDirectionOnSlop(Vector3 dir)
    {
        // 从敌人当前位置向下发射一条射线
        // 检测脚下地面
        // slopRayExtent 是射线长度
        if (Physics.Raycast(
            transform.position,
            -Vector3.up,
            out var hit,
            slopRayExtent
        ))
        {
            // hit.normal 是地面的法线方向
            // Vector3.up 是世界向上的方向
            // Dot 点乘可以判断地面法线和世界上方向的接近程度
            //
            // 如果地面是完全水平的：
            // hit.normal 大约等于 Vector3.up
            // Dot 结果接近 1
            //
            // 如果是斜坡：
            // hit.normal 会倾斜
            // Dot 结果小于 1
            //
            // 如果是垂直墙面：
            // Dot 结果接近 0
            float newAnle = Vector3.Dot(Vector3.up, hit.normal);

            // 如果检测到了有效地面，并且角色正在下落或贴地
            if (newAnle != 0 && verticalSpeed <= 0)
            {
                // 把移动方向投影到坡面上
                //
                // 原理：
                // dir 是原始移动方向
                // hit.normal 是坡面的法线
                // Vector3.ProjectOnPlane(dir, hit.normal)
                // 会得到一个“贴着坡面走”的方向
                //
                // 举例：
                // 如果敌人往前走，但前方是斜坡
                // 修正后敌人的移动方向会沿着斜坡表面移动
                // 而不是硬往水平面方向撞
                return Vector3.ProjectOnPlane(dir, hit.normal);
            }
        }

        // 如果没有检测到坡面，就返回原始方向
        return dir;
    }

    /// <summary>
    /// 检测动画移动时前方是否有障碍物
    /// 作用：
    /// 判断敌人按照当前动画位移方向移动时，前方会不会撞到障碍物
    /// </summary>
    /// <param name="dir">移动方向</param>
    /// <returns>
    /// true：前方检测到了障碍物
    /// false：前方没有障碍物
    /// </returns>
    protected bool CanAnimationMotion(Vector3 dir)
    {
        // 注意：
        // 这个函数名叫 CanAnimationMotion，字面意思像是“能否动画移动”
        // 但它实际返回的是 Physics.Raycast 的结果
        // Physics.Raycast 返回 true 表示射线打到了障碍物
        //
        // 所以当前函数实际含义更接近：
        // “动画移动方向上是否有障碍物”
        //
        // 如果想让命名更准确，可以改名为：
        // HasObstacleInAnimationMotionDirection()

        // 从角色身体稍微往上的位置发射射线
        // transform.position + transform.up * .5f
        // 表示射线起点在角色中心往上微往 0.5 米的位置
        //
        // dir.normalized * animator.GetFloat(animationMoveID)
        // 表示射线方向和长度受动画参数 AnimationMove 影响
        //
        // out var hit 用来接收射线击中的信息
        //
        // 1f 是最大检测距离
        //
        // whatIsObs 表示只检测障碍物层
        return Physics.Raycast(
            transform.position + transform.up * .5f,
            dir.normalized * animator.GetFloat(animationMoveID),
            out var hit,
            1f,
            whatIsObs
        );
    }

    // 修正角色旋转
    // 作用：
    // 保证敌人只绕 Y 轴旋转，不会向前后左右倾斜
    private void FreezeRotation()
    {
        // 把 X 轴和 Z 轴旋转强制设置为 0
        // 保留 Y 轴旋转
        //
        // 原理：
        // 第三人称敌人通常只需要左右转向
        // 不希望因为坡面、碰撞、动画或其他逻辑导致身体歪斜
        transform.eulerAngles = new Vector3(
            0,
            transform.eulerAngles.y,
            0
        );
    }

    #endregion

    #region 公共函数

    /// <summary>
    /// 移动接口
    /// 作用：
    /// 给外部 AI、状态机、攻击逻辑、巡逻逻辑调用
    /// 外部只需要传入移动方向、移动速度、是否使用重力
    /// 这个函数内部负责真正移动敌人
    /// </summary>
    /// <param name="moveDirection">移动方向，例如朝向玩家的方向</param>
    /// <param name="moveSpeed">移动速度</param>
    /// <param name="useGravity">是否使用重力</param>
    public virtual void CharacterMoveInterface(
        Vector3 moveDirection,
        float moveSpeed,
        bool useGravity
    )
    {
        // 检测当前移动方向上是否存在障碍物
        //
        // CanAnimationMotion(moveDirection) 当前返回 true 表示前方有障碍物
        // 所以这里取反：
        // 如果前方没有障碍物，才允许移动
        if (!CanAnimationMotion(moveDirection))
        {
            // 把外部传入的移动方向归一化
            // normalized 的作用：
            // 保证方向长度为 1
            // 这样移动速度只由 moveSpeed 控制，不会因为方向向量长度不同导致速度异常
            movementDirection = moveDirection.normalized;

            // 根据坡面修正移动方向
            // 如果敌人在坡上，就让移动方向贴着坡面
            movementDirection = ResetMoveDirectionOnSlop(movementDirection);

            // 如果允许使用重力
            if (useGravity)
            {
                // 设置垂直移动方向
                // X = 0
                // Y = 当前垂直速度
                // Z = 0
                verticalDirection.Set(0.0f, verticalSpeed, 0.0f);
            }
            else
            {
                // 如果不使用重力，则垂直方向为 0
                // 这种情况可能用于：
                // 1. 攻击位移
                // 2. 动画根运动
                // 3. 击退
                // 4. 特殊技能移动
                verticalDirection = Vector3.zero;
            }

            // 真正移动角色
            //
            // CharacterController.Move 的参数是“本帧要移动的位移量”
            // 不是速度
            //
            // 水平位移：
            // moveSpeed * Time.deltaTime * movementDirection.normalized
            //
            // 垂直位移：
            // Time.deltaTime * verticalDirection
            //
            // 最终位移：
            // 水平移动 + 垂直重力移动
            characterController.Move(
                (moveSpeed * Time.deltaTime) * movementDirection.normalized
                + Time.deltaTime * verticalDirection
            );

            // 设置 Animator 中的 MoveSpeed 参数
            // 作用：
            // 让动画状态机知道当前敌人的移动速度
            // 例如：
            // moveSpeed = 0，播放 Idle
            // moveSpeed > 0，播放 Walk/Run
            animator.SetFloat(moveSpeedHash, moveSpeed);
        }
    }

    #endregion
}