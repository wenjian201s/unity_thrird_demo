using System; // 引入 System 命名空间，用于 Serializable 特性
using System.Collections; // 引入协程相关命名空间。当前代码中没有使用，可以删除
using System.Collections.Generic; // 引入泛型集合命名空间，用于 Dictionary 和 List
using UnityEngine; // 引入 Unity 核心命名空间，例如 ScriptableObject、SerializeField、CreateAssetMenu 等

// CreateAssetMenu 表示这个 ScriptableObject 可以在 Unity Project 面板中创建资源
//
// fileName = "NB_Transition"
// 表示默认创建出来的资源文件名叫 NB_Transition
//
// menuName = "StateMachine/Transition/New NB_Transition"
// 表示可以通过：
// Create -> StateMachine -> Transition -> New NB_Transition
// 创建这个状态转换配置资源
[CreateAssetMenu(fileName = "NB_Transition", menuName = "StateMachine/Transition/New NB_Transition")]
public class NB_Transition : ScriptableObject
{
    // StateActionConfig 是单条状态转换配置
    //
    // Serializable 表示这个内部类可以显示在 Unity Inspector 面板中
    //
    // 一条 StateActionConfig 表示：
    // 从 fromState 状态出发，
    // 当 conditions 中的某些条件成立时，
    // 切换到 toState 状态。
    [Serializable]
    private class StateActionConfig 
    {
        // 起始状态
        //
        // 例如：
        // IdleState
        // ChaseState
        // AttackState
        //
        // 表示当前敌人处于这个状态时，才会检查这条转换配置
        public StateActionSO fromState;

        // 目标状态
        //
        // 当 conditions 中的条件满足时，
        // 状态机会把 currentState 切换成这个 toState
        public StateActionSO toState;

        // 状态转换条件列表
        //
        // 例如：
        // HasTargetCondition
        // InAttackRangeCondition
        // LostTargetCondition
        // DeadCondition
        //
        // 当前代码逻辑是：
        // 只要列表中任意一个 condition.ConditionSetUp() 返回 true，
        // 这条转换就有机会被触发。
        public List<ConditionSO> conditions;
    }
    
    // 存储所有状态转换信息和条件
    //
    // Dictionary 的 Key：
    // 起始状态 fromState
    //
    // Dictionary 的 Value：
    // 从这个 fromState 出发的所有转换配置
    //
    // 举例：
    // states[IdleState] = 
    //      Idle -> Chase
    //
    // states[ChaseState] =
    //      Chase -> Attack
    //      Chase -> Idle
    //
    // states[AttackState] =
    //      Attack -> Chase
    //
    // 这样做的好处：
    // 每帧只需要根据 currentState 快速查找当前状态能走的转换，
    // 不需要遍历所有状态转换配置。
    private Dictionary<StateActionSO, List<StateActionConfig>> states =
        new Dictionary<StateActionSO, List<StateActionConfig>>();

    // 在 Inspector 窗口中显示状态转换配置
    //
    // 因为 Unity 默认 Inspector 不能直接序列化并显示 Dictionary，
    // 所以这里用 List<StateActionConfig> 在 Inspector 中配置数据。
    //
    // 然后运行时再通过 SaveAllStateTransitionInfo()
    // 把这个 List 转换成 Dictionary。
    [SerializeField] private List<StateActionConfig> configStateData =
        new List<StateActionConfig>();

    // 当前使用这份状态转换配置的状态机系统
    //
    // 通过它可以访问：
    // 1. 当前状态 currentState
    // 2. 当前敌人的 Animator
    // 3. 当前敌人的战斗控制器
    // 4. 当前敌人的移动控制器
    // 5. 当前敌人的基础属性
    private StateMachineSystem stateMachineSystem;


    // 初始化状态转换系统
    //
    // 这个方法通常由 StateMachineSystem.Awake() 调用：
    //
    // transition?.Init(this);
    //
    // 作用：
    // 1. 保存当前状态机引用
    // 2. 把 Inspector 中配置的状态转换信息保存到 Dictionary 中
    // 3. 初始化所有 ConditionSO 条件
    public void Init(StateMachineSystem stateMachineSystem) 
    {
        // 保存当前状态机系统引用
        this.stateMachineSystem = stateMachineSystem;

        // 将 Inspector 中配置的 List 数据转换成运行时 Dictionary 数据
        SaveAllStateTransitionInfo();
    }
    
    // 保存所有状态配置信息
    //
    // 作用：
    // 把 configStateData 中配置的所有转换关系，
    // 按照 fromState 分类保存到 states 字典中。
    //
    // 同时初始化每个转换条件。
    private void SaveAllStateTransitionInfo() 
    {
        // 遍历 Inspector 中配置的所有状态转换数据
        foreach (var item in configStateData)
        {
            // 如果 states 字典中还没有这个 fromState
            //
            // 例如第一次遇到 ChaseState，
            // 就先创建一个 ChaseState 对应的转换列表。
            if (!states.ContainsKey(item.fromState)) 
            {
                states.Add(item.fromState, new List<StateActionConfig>());
            }

            // 把当前这条转换配置加入到对应 fromState 的列表中
            //
            // 例如：
            // item.fromState = ChaseState
            // item.toState = AttackState
            //
            // 那么这条配置会被加入：
            // states[ChaseState]
            states[item.fromState].Add(item);

            // 遍历当前转换配置中的所有条件
            foreach (ConditionSO condition in item.conditions)
            {
                // 初始化条件
                //
                // 因为 ConditionSO 是 ScriptableObject，
                // 它自己不能直接 GetComponent 获取敌人身上的组件。
                //
                // 所以这里把 StateMachineSystem 传进去，
                // 让 condition 可以访问：
                // enemyCombatController
                // enemyParameter
                // transform
                //
                // 这样子类条件就能判断：
                // 是否发现玩家
                // 是否进入攻击距离
                // 敌人是否死亡
                condition.Init(stateMachineSystem);
            }
        }
    }

    // 尝试获取条件成立的新状态
    //
    // 这是状态机每帧会调用的核心函数。
    //
    // 它的作用是：
    // 1. 根据当前状态 currentState 找到所有可用转换
    // 2. 检查这些转换里的条件是否成立
    // 3. 挑选条件优先级最高的转换
    // 4. 如果条件优先级相同，再比较目标状态优先级
    // 5. 最后切换到最合适的目标状态
    public void TryGetApplyCondition() 
    {
        // 条件优先级
        //
        // 初始为 0，表示最低优先级
        //
        // ConditionSO 中有：
        // GetConditionPriority()
        //
        // 用来表示这个条件的重要程度。
        //
        // 例如：
        // 死亡条件 priority = 100
        // 攻击条件 priority = 50
        // 追击条件 priority = 20
        int conditionPriority = 0;

        // 状态优先级
        //
        // 初始为 0，表示最低优先级
        //
        // StateActionSO 中有：
        // GetStatePriority()
        //
        // 用来表示目标状态的重要程度。
        //
        // 例如：
        // DeathState priority = 100
        // HitState priority = 80
        // AttackState priority = 50
        // ChaseState priority = 20
        int statePriority = 0;

        // 用于保存所有满足条件的目标状态
        //
        // 例如当前 Chase 状态下同时满足：
        // 1. 进入攻击距离 -> AttackState
        // 2. 血量归零 -> DeathState
        //
        // 那么这两个状态都会被暂时加入 toStates，
        // 后面再根据状态优先级筛选。
        List<StateActionSO> toStates = new List<StateActionSO>();

        // 最终要切换到的目标状态
        StateActionSO toState = null;

        // 判断当前状态是否存在可用的转换配置
        //
        // 如果 states 中存在 currentState 这个 Key，
        // 说明当前状态有可以检查的状态转换规则。
        if (states.ContainsKey(stateMachineSystem.currentState)) 
        {
            // 遍历当前状态能转换到的所有状态配置
            //
            // 例如当前状态是 ChaseState，
            // 那么这里遍历的就是：
            // Chase -> Attack
            // Chase -> Idle
            // Chase -> Death
            foreach (var stateItem in states[stateMachineSystem.currentState])
            {
                // 遍历当前转换配置里的每一个条件
                //
                // 当前逻辑是：
                // 只要某一个 condition 成立，
                // 这个转换配置就有机会生效。
                foreach (var conditionItem in stateItem.conditions)
                {
                    // 判断条件是否成立
                    //
                    // ConditionSetUp() 返回 true：
                    // 表示当前条件满足，可以尝试转换状态。
                    if (conditionItem.ConditionSetUp())
                    {
                        // 如果当前条件的优先级大于等于目前记录的最高条件优先级
                        //
                        // 例如：
                        // 当前最高 conditionPriority = 50
                        // 新条件 priority = 80
                        // 那么更新为 80。
                        //
                        // 使用 >= 表示：
                        // 如果优先级相同，也允许加入候选目标状态。
                        if (conditionItem.GetConditionPriority() >= conditionPriority)
                        {
                            // 更新最高条件优先级
                            conditionPriority = conditionItem.GetConditionPriority();

                            // 将转换关系中的下一个状态保存起来
                            //
                            // 注意：
                            // 这里当前代码会把满足条件的 toState 都加入 toStates。
                            // 后面会再根据状态优先级选择最终状态。
                            toStates.Add(stateItem.toState);
                        }
                    }
                }
            }
        }
        // 如果当前状态没有任何转换配置，则直接返回
        else 
        {
            return;
        }

        // 如果存在候选目标状态，则从中选择状态优先级最高的状态
        //
        // 注意：
        // 当前写法是：
        //
        // if(toStates.Count != 0 || toStates != null)
        //
        // 由于 toStates 是刚刚 new 出来的，所以它永远不为 null。
        // 这里用 || 也不严谨。
        //
        // 更推荐写法：
        // if (toStates != null && toStates.Count != 0)
        if (toStates.Count != 0 || toStates != null) 
        {
            // 遍历所有候选目标状态
            foreach (var item in toStates)
            {
                // 如果这个候选状态的优先级大于等于当前记录的最高状态优先级
                if (item.GetStatePriority() >= statePriority)
                {
                    // 更新最高状态优先级
                    statePriority = item.GetStatePriority();

                    // 把最终目标状态设置为这个状态
                    toState = item;
                }
            }
        }

        // 如果最终找到了要切换到的状态
        if (toState != null) 
        {
            // 进行状态切换

            // 1. 当前状态执行退出逻辑
            //
            // 例如：
            // ChaseState.OnExit()
            // 可以停止移动、清空动画参数等。
            stateMachineSystem.currentState.OnExit();

            // 2. 更新状态机当前状态为目标状态
            stateMachineSystem.currentState = toState;

            // 3. 新状态执行进入逻辑
            //
            // 例如：
            // AttackState.OnEnter()
            // 可以初始化技能选择、设置动画参数等。
            stateMachineSystem.currentState.OnEnter(this.stateMachineSystem);

            // 4. 重置临时变量
            //
            // 注意：
            // 这些变量都是局部变量，函数结束后本来就会被释放。
            // 所以这里重置不是必须的。
            toStates.Clear();
            conditionPriority = 0;
            statePriority = 0;
            toState = null;
        }
    }
}