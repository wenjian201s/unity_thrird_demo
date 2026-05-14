using System.Collections; // 引入协程相关命名空间。当前代码中没有使用，可以删除
using System.Collections.Generic; // 引入泛型集合命名空间。当前代码中没有使用，可以删除
//using LitJson; // 引入 LitJson JSON 解析库。当前代码中没有使用，可以删除
using UnityEngine; // 引入 Unity 核心命名空间，例如 ScriptableObject、CreateAssetMenu 等
//判断敌人是否已经发现 / 锁定了玩家目标。如果敌人当前有目标，就允许状态机切换到战斗状态。
// CreateAssetMenu 表示这个 ScriptableObject 可以在 Unity Project 面板中创建成资源文件
//
// fileName = "ToCombatCondition"
// 默认创建出来的资源文件名叫 ToCombatCondition
//
// menuName = "StateMachine/Condition/ToCombatCondition"
// 创建路径为：
// Create -> StateMachine -> Condition -> ToCombatCondition
//
// ToCombatCondition 继承 ConditionSO，说明它是状态机中的一个“状态转换条件”
//
// 这个条件的作用：
// 判断敌人是否已经拥有当前目标。
// 如果 enemyCombatController.GetCurrentTarget() 不为空，说明敌人发现了玩家，
// 状态机就可以从待机、巡逻等状态切换到战斗状态。
[CreateAssetMenu(fileName = "ToCombatCondition", menuName = "StateMachine/Condition/ToCombatCondition")]
public class ToCombatCondition : ConditionSO
{
    // 判断状态转换条件是否成立
    //
    // 这个方法会被 NB_Transition.TryGetApplyCondition() 每帧调用。
    //
    // 返回 true：
    // 当前条件成立，可以触发状态转换。
    //
    // 返回 false：
    // 当前条件不成立，状态机保持原状态。
    public override bool ConditionSetUp()
    {
        // enemyCombatController 来自父类 ConditionSO
        //
        // 它是在 ConditionSO.Init(StateMachineSystem stateSystem) 中初始化的：
        //
        // enemyCombatController = stateSystem.enemyCombatController;
        //
        // 所以这里可以直接通过 enemyCombatController 获取敌人的战斗信息。
        //
        // GetCurrentTarget() 的作用：
        // 获取敌人当前锁定 / 发现的目标。
        //
        // 如果返回值不为空：
        // 说明敌人当前已经发现玩家或拥有攻击目标。
        //
        // 如果返回值为空：
        // 说明敌人还没有发现玩家，不能进入战斗状态。
        //
        // 条件表达式：
        // enemyCombatController.GetCurrentTarget() ? true : false
        //
        // 在 Unity 中，Transform 可以直接作为 bool 判断。
        // 有对象时为 true，没有对象时为 false。
        return enemyCombatController.GetCurrentTarget() ? true : false;
    }
}