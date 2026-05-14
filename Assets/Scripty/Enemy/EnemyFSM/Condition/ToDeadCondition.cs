using System.Collections; // 引入协程相关命名空间。当前代码中没有使用，可以删除
using System.Collections.Generic; // 引入泛型集合命名空间。当前代码中没有使用，可以删除
using UnityEngine; // 引入 Unity 核心命名空间，例如 ScriptableObject、CreateAssetMenu 等


//判断敌人的生命值是否小于等于 0，如果是，就通知状态机可以切换到死亡状态
// CreateAssetMenu 表示这个 ScriptableObject 可以在 Unity Project 面板中创建成资源文件
//
// fileName = "ToDeadCondition"
// 默认创建出来的资源文件名叫 ToDeadCondition
//
// menuName = "StateMachine/Condition/ToDeadCondition"
// 创建路径为：
// Create -> StateMachine -> Condition -> ToDeadCondition
//
// ToDeadCondition 继承 ConditionSO
// 说明它是敌人有限状态机中的一个“状态转换条件”
//
// 这个条件的作用：
// 判断敌人的血量是否已经小于等于 0
// 如果血量 <= 0，说明敌人死亡，可以切换到死亡状态
[CreateAssetMenu(fileName = "ToDeadCondition", menuName = "StateMachine/Condition/ToDeadCondition")]
public class ToDeadCondition : ConditionSO
{
    // 判断状态转换条件是否成立
    //
    // 这个函数会被 NB_Transition.TryGetApplyCondition() 每帧调用
    //
    // 返回 true：
    // 条件成立，敌人可以切换到死亡状态
    //
    // 返回 false：
    // 条件不成立，敌人继续保持当前状态
    public override bool ConditionSetUp()
    {
        // enemyParameter 来自父类 ConditionSO
        //
        // 在 ConditionSO.Init(StateMachineSystem stateSystem) 中初始化：
        //
        // enemyParameter = stateSystem.enemyParameter;
        //
        // enemyParameter 通常是 EnemyBase 组件
        // 它保存敌人的基础属性，例如血量、速度、耐力等
        //
        // enemyParameter.health 表示敌人当前血量
        //
        // 当 health <= 0 时：
        // 说明敌人已经没有生命值，应该进入死亡状态
        //
        // 当 health > 0 时：
        // 说明敌人还活着，不应该进入死亡状态
        return enemyParameter.health <= 0;
    }
}