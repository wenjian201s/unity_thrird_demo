using UnityEngine; // 引入 Unity 核心命名空间，例如 ScriptableObject、Animator、Transform、SerializeField 等

/// <summary>
/// 有限状态机的状态配置文件基类
/// </summary>
///
/// StateActionSO 是敌人有限状态机中“状态行为”的抽象基类。
///
/// 它继承自 ScriptableObject，说明它不是挂载到 GameObject 上的普通组件，
/// 而是可以作为 Unity 资源文件存在。
///
/// 主要作用：
/// 1. 作为所有敌人状态的父类
/// 2. 保存状态运行时需要用到的组件引用
/// 3. 提供状态生命周期函数：OnEnter、OnUpdate、OnExit
/// 4. 提供状态优先级 statePriority
/// 5. 让具体状态子类重写 OnUpdate，实现不同状态行为
///
/// 例如可以派生出：
/// IdleStateSO      待机状态
/// ChaseStateSO     追击状态
/// AttackStateSO    攻击状态
/// HitStateSO       受击状态
/// DeathStateSO     死亡状态
public abstract class StateActionSO : ScriptableObject
{
    #region 组件
    
    // =========================
    // 状态运行时需要用到的组件
    // =========================

    // 敌人的 Animator 动画机
    //
    // 状态中可以通过它控制动画参数或者播放动画。
    //
    // 例如：
    // 待机状态设置 MoveSpeed = 0
    // 追击状态设置 MoveSpeed = runSpeed
    // 攻击状态播放攻击动画
    [SerializeField] protected Animator animator;

    // 敌人战斗控制器
    //
    // 状态中可以通过它获取战斗相关信息。
    //
    // 例如：
    // 1. 当前目标是谁
    // 2. 当前目标距离
    // 3. 当前是否有可用技能
    // 4. 获取一个随机可用技能
    [SerializeField] protected EnemyCombatController enemyCombatController;

    // 敌人移动控制器
    //
    // 状态中可以通过它控制敌人移动。
    //
    // 例如：
    // 追击状态中让敌人朝玩家移动
    // 巡逻状态中让敌人沿路线移动
    // 后撤状态中让敌人远离玩家
    [SerializeField] protected EnemyMovementController enemyMovementController;

    // 敌人基础属性
    //
    // 状态中可以通过它读取敌人的基础数值。
    //
    // 例如：
    // 1. 行走速度
    // 2. 奔跑速度
    // 3. 转身速度
    // 4. 血量
    // 5. 耐力
    [SerializeField] protected EnemyBase enemyParameter;

    // 敌人自身 Transform
    //
    // 状态中可以通过它获取敌人的位置、朝向和旋转。
    //
    // 例如：
    // 1. 让敌人转向玩家
    // 2. 获取敌人当前位置
    // 3. 计算敌人与目标之间的方向
    //
    // 注意：
    // 这里变量名叫 transform。
    // 虽然 ScriptableObject 没有 MonoBehaviour 的 transform 属性，
    // 但这个命名容易和 MonoBehaviour.transform 混淆。
    // 更推荐命名为 enemyTransform 或 ownerTransform。
    [SerializeField] protected Transform transform;
    
    #endregion
    
    // =========================
    // 状态优先级
    // =========================

    // 该状态的状态优先级
    //
    // 作用：
    // 当多个状态都可能被切换时，可以通过优先级决定哪个状态更重要。
    //
    // 例如：
    // Death 死亡状态优先级最高
    // Hit 受击状态优先级次高
    // Attack 攻击状态优先级中等
    // Chase 追击状态优先级较低
    //
    // 这样可以避免敌人在死亡时还进入攻击或追击状态。
    [SerializeField] protected int statePriority;

    // 初始化状态所需的运行时引用
    //
    // protected 表示只有当前类和子类可以调用
    // virtual 表示子类可以重写这个方法，添加自己的初始化逻辑
    //
    // 参数 stateMachineSystem：
    // 当前敌人身上的状态机系统。
    // 它里面保存了敌人的 Animator、战斗控制器、移动控制器、基础属性等组件。
    protected virtual void Init(StateMachineSystem stateMachineSystem)
    {
        // 从状态机系统中获取 Animator
        animator = stateMachineSystem.animator;

        // 从状态机系统中获取敌人战斗控制器
        enemyCombatController = stateMachineSystem.enemyCombatController;

        // 从状态机系统中获取敌人移动控制器
        enemyMovementController = stateMachineSystem.enemyMovementController;

        // 从状态机系统中获取敌人基础属性
        enemyParameter = stateMachineSystem.enemyParameter;

        // 获取状态机所属敌人的 Transform
        transform = stateMachineSystem.transform;
    }
    
    // 进入该状态
    //
    // 当状态机切换到这个状态时，会调用 OnEnter。
    //
    // 例如：
    // 从 Idle 切换到 Chase 时：
    // ChaseStateSO.OnEnter(stateMachineSystem)
    //
    // 这里默认只做初始化，把状态机中的组件引用保存到当前状态里。
    // 子类可以重写 OnEnter，在进入状态时执行额外逻辑。
    public virtual void OnEnter(StateMachineSystem stateMachineSystem)
    {
        // 初始化状态，获取该状态所需要的参数
        Init(stateMachineSystem);
    }

    // 处于该状态
    //
    // 这是状态每一帧执行的核心逻辑。
    //
    // abstract 表示这个方法没有默认实现，必须由子类重写。
    //
    // 例如：
    // IdleStateSO.OnUpdate()
    //     播放待机动画
    //
    // ChaseStateSO.OnUpdate()
    //     朝玩家移动
    //
    // AttackStateSO.OnUpdate()
    //     选择技能并释放
    public abstract void OnUpdate();

    // 退出该状态
    //
    // 当状态机从当前状态切换到其他状态时，会调用 OnExit。
    //
    // 例如：
    // 从 Chase 切换到 Attack 时：
    // ChaseStateSO.OnExit()
    //
    // 当前基类中默认不做任何事情。
    // 子类可以重写它，用于清理状态。
    //
    // 例如：
    // 1. 停止移动动画参数
    // 2. 清空临时变量
    // 3. 关闭某些状态标记
    // 4. 停止协程或特效
    public virtual void OnExit() { }
    
    // 提供给外部，获取状态优先级
    //
    // 状态转换系统可以通过这个函数比较不同状态的重要程度。
    public int GetStatePriority() => statePriority;
}