using System.Collections; // 引入协程相关命名空间。当前代码中没有使用，可以删除
using System.Collections.Generic; // 引入泛型集合命名空间。当前代码中没有使用，可以删除
using UnityEngine; // 引入 Unity 核心命名空间，例如 MonoBehaviour、Animator、GetComponent 等
using Sirenix.OdinInspector; // 引入 Odin Inspector 插件命名空间。当前代码中没有使用，可以删除

// StateMachineSystem 状态机系统
// 这个脚本通常挂载在敌人根物体上
//
// 主要作用：
// 1. 保存敌人的当前状态 currentState
// 2. 保存状态转换配置 transition
// 3. 初始化敌人状态机需要用到的组件
// 4. 初始化状态转换条件
// 5. 进入初始状态
// 6. 每帧检测是否满足状态切换条件
// 7. 每帧执行当前状态逻辑
public class StateMachineSystem : MonoBehaviour
{
    // =========================
    // 状态转换条件
    // =========================

    // transition 表示当前状态机的状态转换配置
    //
    // NB_Transition 应该是你项目中自定义的状态转换类
    // 它可能负责：
    // 1. 保存多个 ConditionSO 条件
    // 2. 判断哪些条件成立
    // 3. 根据条件优先级选择要切换的状态
    // 4. 执行状态切换
    //
    // 例如：
    // 没有目标 -> 巡逻状态
    // 发现玩家 -> 追击状态
    // 进入攻击距离 -> 攻击状态
    // 血量为 0 -> 死亡状态
    public NB_Transition transition;
    
    // =========================
    // 当前状态
    // =========================

    // 当前正在运行的状态
    //
    // StateActionSO 应该是你项目中自定义的状态行为基类
    // 它很可能是 ScriptableObject 类型
    //
    // currentState 可能代表：
    // 1. Idle 待机状态
    // 2. Patrol 巡逻状态
    // 3. Chase 追击状态
    // 4. Attack 攻击状态
    // 5. Hit 受击状态
    // 6. Death 死亡状态
    //
    // 状态本身一般会有：
    // OnEnter() 进入状态时执行
    // OnUpdate() 状态持续期间每帧执行
    // OnExit() 离开状态时执行
    public StateActionSO currentState;

    #region 组件

    // =========================
    // 敌人系统常用组件引用
    // =========================

    // 敌人战斗控制器
    //
    // 状态逻辑和转换条件可以通过它获取：
    // 1. 当前目标
    // 2. 当前目标距离
    // 3. 可用技能
    // 4. 攻击相关接口
    public EnemyCombatController enemyCombatController;

    // 敌人移动控制器
    //
    // 状态逻辑可以通过它控制敌人移动
    //
    // 例如：
    // 巡逻状态调用移动接口
    // 追击状态朝玩家移动
    // 后退状态远离玩家
    public EnemyMovementController enemyMovementController;

    // 敌人动画机
    //
    // 状态逻辑可以通过它设置动画参数或播放动画
    public Animator animator;

    // 敌人基础属性
    //
    // 状态逻辑和条件可以读取敌人属性，例如：
    // 1. 血量
    // 2. 耐力
    // 3. 移动速度
    // 4. 跑步速度
    // 5. 旋转速度
    public EnemyBase enemyParameter;

    #endregion


    // Awake 会在脚本实例加载时执行
    // 执行时机早于 Start
    //
    // 这里用来初始化状态机系统
    private void Awake()
    {
        // =========================
        // 初始化组件引用
        // =========================

        // 获取当前敌人身上的 EnemyCombatController 组件
        enemyCombatController = GetComponent<EnemyCombatController>();

        // 获取当前敌人身上的 EnemyMovementController 组件
        enemyMovementController = GetComponent<EnemyMovementController>();

        // 获取当前敌人身上的 Animator 组件
        //
        // 注意：
        // 如果 Animator 挂在子物体模型上，
        // 这里应该改成 GetComponentInChildren<Animator>()
        animator = GetComponent<Animator>();

        // 获取当前敌人身上的 EnemyBase 组件
        enemyParameter = GetComponent<EnemyBase>();
        
        // 初始化状态转换系统
        //
        // ?. 是空条件运算符
        // 如果 transition 不为空，就调用 Init(this)
        // 如果 transition 为空，就不会调用，也不会报错
        //
        // Init(this) 的作用：
        // 把当前 StateMachineSystem 传给 transition
        // 让 transition 能访问敌人的组件、当前状态、Transform 等信息
        transition?.Init(this);

        // 进入初始状态
        //
        // 如果 currentState 不为空，就调用 currentState.OnEnter(this)
        //
        // OnEnter(this) 的作用：
        // 把当前状态机系统传给状态对象
        // 状态对象可以通过它获取移动、战斗、动画、属性等组件
        //
        // 例如 IdleState.OnEnter()
        // 可以设置动画参数为待机
        currentState?.OnEnter(this);
    }


    // Update 每帧执行一次
    private void Update()
    {
        // 每帧驱动状态机运行
        StateMachineTick();
    }

    // 状态机每帧运行逻辑
    private void StateMachineTick() 
    {
        // 第一步：检查是否有条件成立的状态切换
        //
        // transition?.TryGetApplyCondition()
        //
        // 可能做的事情：
        // 1. 遍历当前状态可用的所有转换条件
        // 2. 调用 ConditionSO.ConditionSetUp()
        // 3. 判断哪个条件满足
        // 4. 根据优先级选择最合适的转换
        // 5. 切换 currentState
        //
        // 也就是说，状态切换发生在当前状态 Update 之前
        transition?.TryGetApplyCondition(); 

        // 第二步：执行当前状态的运行逻辑
        //
        // currentState?.OnUpdate()
        //
        // 例如：
        // IdleState.OnUpdate()：播放待机或观察玩家
        // ChaseState.OnUpdate()：朝玩家移动
        // AttackState.OnUpdate()：选择技能并释放
        // PatrolState.OnUpdate()：沿路线巡逻
        currentState?.OnUpdate();
    }
}