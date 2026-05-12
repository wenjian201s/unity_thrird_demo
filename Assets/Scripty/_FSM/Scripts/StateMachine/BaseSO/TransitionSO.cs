using System.Collections; // 引入协程命名空间。当前代码中没有使用，可以删除
using System.Collections.Generic; // 引入泛型集合命名空间，用于 Dictionary 和 List
using UnityEngine; // 引入 Unity 核心命名空间，例如 ScriptableObject、SerializeField 等
using Sirenix.OdinInspector; // 引入 Odin Inspector，用于 InlineEditor、InfoBox、FoldoutGroup 等 Inspector 增强显示

//这个是老的
// CreateAssetMenu 表示这个 ScriptableObject 可以在 Unity Project 面板中创建资源
//
// fileName = "Transition"
// 默认创建出来的资源文件名叫 Transition
//用 ScriptableObject 配置敌人状态机中的“状态转换规则”，例如从追击状态切换到攻击状态、从攻击状态切换回追击状态、从任意状态切换到死亡状态等。
// menuName = "StateMachine/Transition/New Transition"
// 右键创建路径为：
// Create -> StateMachine -> Transition -> New Transition
[CreateAssetMenu(fileName = "Transition", menuName = "StateMachine/Transition/New Transition")]
public class TransitionSO : ScriptableObject
{
    // 状态转换字典
    //
    // Key：当前状态 fromState
    // Value：该状态下可以触发转换的所有条件 ConditionSO
    //
    // 举例：
    // _transition[ChaseState] = [InAttackRangeCondition, LostTargetCondition]
    //
    // 表示：
    // 当敌人当前处于 ChaseState 追击状态时，
    // 状态机会检查 InAttackRangeCondition 和 LostTargetCondition。
    private Dictionary<StateActionSO, List<ConditionSO>> _transition =
        new Dictionary<StateActionSO, List<ConditionSO>>();

    // Inspector 中配置的状态转换列表
    //
    // 每一个 TransitionState 表示一条状态转换规则：
    //
    // fromState：从哪个状态出发
    // toState：满足条件后切换到哪个状态
    // condition：触发这条转换需要满足的条件列表
    //
    // 举例：
    // fromState = ChaseState
    // toState = AttackState
    // condition = [InAttackRangeCondition]
    //
    // 表示：
    // 当敌人处于追击状态，并且进入攻击距离时，切换到攻击状态。
    [SerializeField] private List<TransitionState> currentTransition =
        new List<TransitionState>();

    // 当前运行这个状态机的敌人状态机系统
    //
    // 通过它可以访问：
    // 1. 当前状态 currentState
    // 2. 敌人战斗控制器
    // 3. 敌人移动控制器
    // 4. 敌人动画机
    // 5. 敌人基础属性
    private StateMachineSystem stateMachineSystem;

    // 初始化状态转换系统
    //
    // 这个方法通常由 StateMachineSystem.Awake() 调用。
    //
    // 作用：
    // 1. 保存当前状态机引用
    // 2. 根据 currentTransition 配置生成运行时字典 _transition
    // 3. 初始化每个 ConditionSO 条件
    public void Init(StateMachineSystem stateMachine) 
    {      
        // 保存状态机系统引用
        stateMachineSystem = stateMachine;

        // 把 Inspector 中配置的转换列表转换成字典结构
        AddTransition(stateMachine);
    }

    // 检查当前状态下是否有满足的转换条件
    //
    // 这个方法应该在状态机每帧调用。
    //
    // 执行流程：
    // 1. 判断转换字典是否有数据
    // 2. 判断当前状态是否存在转换条件
    // 3. 遍历当前状态下的所有条件
    // 4. 如果某个条件满足，就执行状态转换
    public void TryGetEnableCondition() 
    {
        // 如果转换字典不为空
        if (_transition.Count != 0) 
        {
            // 判断字典中是否存在当前状态对应的条件列表
            //
            // 例如当前状态是 ChaseState，
            // 就检查 _transition 是否有 ChaseState 这个 Key。
            if (_transition.ContainsKey(stateMachineSystem.currentState))
            {
                // 遍历当前状态下的所有转换条件
                foreach (var item in _transition[stateMachineSystem.currentState])
                {
                    // 调用条件的判断函数
                    //
                    // ConditionSetUp() 返回 true：
                    // 表示这个条件满足，可以进行状态转换。
                    //
                    // 例如：
                    // 进入攻击距离
                    // 发现玩家
                    // 血量归零
                    // 目标丢失
                    if (item.ConditionSetUp())
                    {
                        // 条件满足，执行状态转换
                        Transition(item);
                    }
                }
            }
        }
    }

    // 执行状态转换
    //
    // 参数 condition：
    // 当前满足的那个条件。
    //
    // 作用：
    // 1. 退出当前状态
    // 2. 根据条件找到下一个状态
    // 3. 切换 currentState
    // 4. 进入新状态
    public void Transition(ConditionSO condition) 
    {
        // 当前状态退出
        //
        // 例如：
        // ChaseState.OnExit()
        // 可以停止移动、重置动画参数等。
        stateMachineSystem.currentState?.OnExit();

        // 根据当前状态和触发条件，找到下一个状态
        stateMachineSystem.currentState = GetNextState(condition);

        // 进入新状态
        //
        // OnEnter 会初始化新状态需要的组件引用，
        // 并执行进入状态时的逻辑。
        stateMachineSystem.currentState?.OnEnter(this.stateMachineSystem);
    }

    // 根据触发条件获取下一个状态
    //
    // 参数 condition：
    // 当前满足的条件。
    //
    // 返回值：
    // 找到符合规则的目标状态，则返回 toState
    // 找不到则返回 null
    public StateActionSO GetNextState(ConditionSO condition) 
    {
        // 如果配置列表不为空
        if (currentTransition.Count != 0) 
        {
            // 遍历所有状态转换配置
            foreach (var item in currentTransition)
            {
                // 原来的单条件判断写法被注释掉了：
                //
                // if (item.condition == condition && stateMachineSystem.currentState == item.fromState)
                // {
                //     return item.toState;
                // }
                //
                // 因为现在 item.condition 是 List<ConditionSO>，
                // 所以需要判断这个 List 中是否包含当前触发的 condition。

                // 判断：
                // 1. 当前状态是否等于这条转换规则的 fromState
                // 2. 这条转换规则的 condition 列表中是否包含当前触发条件
                //
                // 如果都满足，说明这条规则就是当前应该执行的转换。
                if (stateMachineSystem.currentState == item.fromState &&
                    item.condition.Contains(condition))
                {
                    // 返回目标状态
                    return item.toState;
                }
            }           
        }

        // 如果没有找到对应的转换规则，返回 null
        return null;
    }

    // 添加状态转换配置
    //
    // 作用：
    // 把 Inspector 中配置的 currentTransition 列表，
    // 转换成运行时更方便查询的 Dictionary。
    //
    // 同时会初始化每一个 ConditionSO。
    public void AddTransition(StateMachineSystem stateMachine) 
    {
        // 如果配置列表不为空
        if (currentTransition.Count != 0) 
        {
            // 遍历所有状态转换配置
            foreach (var item in currentTransition)
            {
                // 如果字典中还没有这个 fromState
                //
                // 例如第一次遇到 ChaseState，
                // 那么就先给 ChaseState 创建一个条件列表。
                if (!_transition.ContainsKey(item.fromState))
                {
                    // 给这个 fromState 添加一个新的条件列表
                    _transition.Add(item.fromState, new List<ConditionSO>());

                    // 遍历这条转换规则中的所有条件
                    foreach (var conditions in item.condition)
                    {
                        // 初始化条件
                        //
                        // 因为 ConditionSO 是 ScriptableObject，
                        // 不能自己 GetComponent，
                        // 所以需要把 StateMachineSystem 传进去。
                        //
                        // 这样条件内部就可以访问：
                        // enemyCombatController
                        // enemyParameter
                        // transform 等。
                        conditions.Init(stateMachine);

                        // 把条件加入当前 fromState 对应的条件列表
                        _transition[item.fromState].Add(conditions);
                    }
                }
                // 如果字典中已经存在这个 fromState
                else
                {
                    // 遍历这条转换规则中的所有条件
                    foreach (var newCondition in item.condition)
                    {
                        // 如果当前 fromState 的条件列表中还没有这个条件
                        if (!_transition[item.fromState].Contains(newCondition))
                        {
                            // 初始化条件
                            newCondition.Init(stateMachine);

                            // 添加到当前 fromState 对应的条件列表中
                            _transition[item.fromState].Add(newCondition);
                        }
                        else 
                        {
                            // 如果条件已经存在，就跳过
                            continue;
                        }
                    }
                }
            }
        }
    }

    // TransitionState 是单条状态转换规则的数据结构
    //
    // 它被标记为 Serializable，
    // 所以可以显示在 Unity Inspector 面板中。
    [System.Serializable]
    private class TransitionState
    {
        // 起始状态
        //
        // 也就是“从哪个状态开始转换”
        //
        // Odin 属性说明：
        // InlineEditor：在 Inspector 中内联显示这个 ScriptableObject 的内容
        // InfoBox：显示提示框
        // FoldoutGroup：折叠分组显示
        //
        // 你的中文显示出现乱码，原本应该类似：
        // “上一个状态” 或 “当前状态”
        [InlineEditor, InfoBox("上一个状态"), FoldoutGroup("状态转换")] 
        public StateActionSO fromState;

        // 目标状态
        //
        // 也就是“满足条件后切换到哪个状态”
        //
        // 你的中文显示出现乱码，原本应该类似：
        // “下一个状态”
        [InlineEditor, InfoBox("下一个状态"), FoldoutGroup("状态转换")] 
        public StateActionSO toState;

        // 状态之间的转换条件
        //
        // 可以配置多个条件。
        //
        // 当前代码逻辑是：
        // 只要这个列表里的任意一个条件满足，
        // 就可以从 fromState 切换到 toState。
        //
        // 注意：
        // 当前不是“所有条件都满足才切换”，
        // 而是“任意一个条件满足就切换”。
        [InlineEditor, InfoBox("状态之间的转换条件"), FoldoutGroup("状态转换")] 
        public List<ConditionSO> condition;

        // 构造函数
        //
        // 用于代码中手动创建 TransitionState。
        // 但在 Unity Inspector 中配置时，一般不会手动调用这个构造函数。
        public TransitionState(
            StateActionSO fromState,
            StateActionSO toState,
            List<ConditionSO> condition
        )
        {
            // 保存起始状态
            this.fromState = fromState;

            // 保存目标状态
            this.toState = toState;

            // 保存转换条件列表
            this.condition = condition;
        }      
    }
}