using System.Collections; // 引入协程相关命名空间。当前代码中没有使用，可以删除
using System.Collections.Generic; // 引入泛型集合命名空间。当前代码中没有使用，可以删除
using UnityEngine; // 引入 Unity 核心命名空间，例如 ScriptableObject、SerializeField、Transform 等

/// <summary>
/// 状态转换条件配置文件基类
/// </summary>
///
/// ConditionSO 是状态转换条件的抽象基类。
///
/// 它继承 ScriptableObject，说明它不是挂载在 GameObject 上的普通 MonoBehaviour，
/// 而是可以作为 Unity 资源文件存在。
///
/// 作用：
/// 1. 作为所有状态转换条件的父类
/// 2. 保存条件优先级
/// 3. 保存状态判断需要用到的敌人组件引用
/// 4. 提供统一的初始化接口 Init()
/// 5. 强制子类实现 ConditionSetUp() 判断条件是否满足
///
/// 举例：
/// EnemyFindPlayerCondition
///     判断敌人是否发现玩家
///
/// EnemyCanAttackCondition
///     判断敌人是否进入攻击范围
///
/// EnemyDeadCondition
///     判断敌人血量是否小于等于 0
public abstract class ConditionSO : ScriptableObject
{
    // 条件优先级
    //
    // 数值越高或越低代表优先级越高，具体取决于你的状态机排序规则。
    //
    // 作用：
    // 当一个状态同时满足多个转换条件时，
    // 状态机可以根据 priority 决定优先执行哪个转换。
    //
    // 例如：
    // 死亡条件 priority = 100
    // 攻击条件 priority = 50
    // 追击条件 priority = 10
    //
    // 如果敌人同时满足“可以攻击”和“死亡”，
    // 应该优先切换到死亡状态。
    [SerializeField] protected int priority;

    // 敌人战斗控制器引用
    //
    // 用于在条件判断中获取敌人的战斗信息。
    //
    // 例如：
    // 1. 当前目标是谁
    // 2. 当前目标距离
    // 3. 当前是否有可用技能
    // 4. 当前是否处于战斗状态
    [SerializeField] protected EnemyCombatController enemyCombatController;

    // 敌人基础属性引用
    //
    // 用于在条件判断中获取敌人的基础数据。
    //
    // 例如：
    // 1. 血量 health
    // 2. 耐力 endurance
    // 3. 移动速度
    // 4. 攻击范围
    // 5. 旋转速度
    [SerializeField] protected EnemyBase enemyParameter;

    // 敌人自身 Transform 引用
    //
    // 用于条件判断中获取敌人的位置、朝向、旋转等信息。
    //
    // 例如：
    // 1. 判断敌人与玩家距离
    // 2. 判断敌人朝向
    // 3. 获取敌人当前位置
    //
    // 注意：
    // 这里变量名叫 transform。
    // 虽然 ScriptableObject 本身没有 MonoBehaviour 的 transform 属性，
    // 但为了避免阅读混淆，更推荐改名为 ownerTransform 或 enemyTransform。
    [SerializeField] protected Transform transform;

    // 初始化函数
    //
    // 作用：
    // 把状态机系统中的运行时组件引用传给当前条件。
    //
    // 因为 ConditionSO 是 ScriptableObject，
    // 它不能像 MonoBehaviour 一样直接通过 GetComponent 获取敌人身上的组件。
    //
    // 所以需要由 StateMachineSystem 把相关组件传进来。
    public virtual void Init(StateMachineSystem stateSystem)
    {
        // 获取敌人战斗控制器
        //
        // 后续子类条件可以通过 enemyCombatController 判断：
        // 是否有目标、目标距离、是否可攻击等。
        enemyCombatController = stateSystem.enemyCombatController;

        // 获取敌人基础参数
        //
        // 后续子类条件可以通过 enemyParameter 判断：
        // 血量、耐力、速度、攻击范围等。
        enemyParameter = stateSystem.enemyParameter;

        // 获取状态机所属敌人的 Transform
        //
        // 后续子类条件可以获取敌人位置、旋转和朝向。
        transform = stateSystem.transform;
    }
    
    /// <summary>
    /// 判断转换条件是否满足
    /// </summary>
    /// <returns>
    /// true：条件满足，可以进行状态转换
    /// false：条件不满足，不能进行状态转换
    /// </returns>
    ///
    /// abstract 表示这个方法没有具体实现，必须由子类重写。
    ///
    /// 例如：
    /// 发现玩家条件：
    /// return enemyCombatController.GetCurrentTarget() != null;
    ///
    /// 死亡条件：
    /// return enemyParameter.health <= 0;
    ///
    /// 攻击距离条件：
    /// return enemyCombatController.GetCurrentTargetDistance() <= attackDistance;
    public abstract bool ConditionSetUp();

    // 获取条件优先级
    //
    // 状态机可以通过这个方法读取 priority，
    // 然后对多个条件进行排序，优先执行高优先级条件。
    public int GetConditionPriority() => priority;
}