using System; // 引入 System 命名空间，提供基础系统功能，例如 Action、Math 等
using System.Collections; // 引入协程相关接口，例如 IEnumerator
using System.Collections.Generic; // 引入泛型集合，例如 List、Dictionary 等
using Unity.VisualScripting; // 引入 Unity 可视化脚本相关功能，这里当前代码中没有实际使用
using UnityEngine; // 引入 Unity 核心功能，例如 MonoBehaviour、SerializeField、WaitForSecondsRealtime 等

// PerfectDodge 类，继承自 MonoBehaviour
// 这个脚本用于控制“完美闪避”的触发逻辑和冷却时间
public class PerfectDodge : MonoBehaviour
{
    // 在 Inspector 面板中显示该私有变量
    // 用于引用玩家战斗控制器，调用里面的 PerfectDodge() 方法
    [SerializeField] private PlayerCombatController playerCombatController;

    // 是否允许触发完美闪避
    // true 表示当前可以触发
    // false 表示当前处于冷却期间，不能重复触发
    private bool canTriggerPerfectDodge;

    // Start 会在脚本第一次启用时执行一次
    private void Start()
    {
        // 初始化时允许触发完美闪避
        canTriggerPerfectDodge = true;
    }

    // 完美闪避接口方法
    // 外部可以调用这个方法来尝试触发完美闪避
    public void PerfectDodgeInterface()
    {
        // 判断当前是否可以触发完美闪避
        if (canTriggerPerfectDodge)
        {
            // 执行完美闪避的逻辑

            // 触发后立刻关闭触发权限，防止短时间内重复触发
            canTriggerPerfectDodge = false;

            // 调用玩家战斗控制器中的 PerfectDodge 方法
            // 真正的完美闪避效果应该在 PlayerCombatController.PerfectDodge() 里面实现
            playerCombatController.PerfectDodge();

            // 开启协程，等待一段时间后重新允许触发完美闪避
            // 时间长度由 playerCombatController.GetCanPerfectDodgeTime() 返回
            StartCoroutine(
                IE_CanPerfectDodgeTimeCount(
                    playerCombatController.GetCanPerfectDodgeTime()
                )
            );
        }
    }

    // 协程：用于计算完美闪避的冷却时间
    // duration 表示需要等待的时间，单位是秒
    IEnumerator IE_CanPerfectDodgeTimeCount(float duration)
    {
        // 等待指定秒数
        // WaitForSecondsRealtime 使用真实时间，不受 Time.timeScale 影响
        // 也就是说，即使游戏暂停或者慢动作，这个计时仍然会继续
        yield return new WaitForSecondsRealtime(duration);

        // 等待结束后，重新允许触发完美闪避
        canTriggerPerfectDodge = true;
    }
}