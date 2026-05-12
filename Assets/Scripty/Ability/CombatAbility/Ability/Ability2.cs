using System.Collections; // 引入协程相关命名空间。当前代码中没有实际使用，可以删除
using System.Collections.Generic; // 引入泛型集合命名空间。当前代码中没有实际使用，可以删除
using UnityEngine; // 引入 Unity 核心命名空间，例如 ScriptableObject、CreateAssetMenu、Animator、Time 等

// CreateAssetMenu 用于让这个 ScriptableObject 可以在 Unity Project 面板中创建资源
//
// fileName = "Ability2"
// 表示默认创建出来的资源文件名叫 Ability2
//
// menuName = "Abilities/上刺横扫"
// 表示可以在右键菜单中通过：
// Create -> Abilities -> 上刺横扫
// 创建这个技能资源
[CreateAssetMenu(fileName = "Ability2", menuName = "Abilities/上刺横扫")]
public class Ability2 : CombatAbilityBase
{
    /// <summary>
    /// 技能逻辑
    /// </summary>
    //
    // InvokeAbility 是 CombatAbilityBase 中定义的抽象方法
    // 每一个具体技能都必须重写这个方法
    //
    // 这个方法的作用：
    // 定义“上刺横扫”这个技能在被敌人 AI 选择后，应该如何执行
    public override void InvokeAbility()
    {
        // 判断当前是否允许释放技能
        //
        // animator.GetCurrentAnimatorStateInfo(0)
        // 获取 Animator 第 0 层，也就是 Base Layer 当前播放的动画状态信息
        //
        // IsTag("Motion")
        // 判断当前动画状态是否带有 Motion 标签
        //
        // 这里的意思是：
        // 只有敌人当前处于可行动状态时，才能执行技能逻辑
        //
        // 例如：
        // Idle、Walk、Run 可以设置为 Motion
        // 受击、死亡、击倒、攻击中则不应该是 Motion
        //
        // abilitiyIsAvailable 表示技能当前是否可用
        // true：技能不在冷却中，可以释放
        // false：技能正在冷却，不能释放
        if (animator.GetCurrentAnimatorStateInfo(0).IsTag("Motion") && abilitiyIsAvailable)
        {
            // 判断敌人与当前目标之间的距离是否大于技能释放距离
            //
            // combatController.GetCurrentTargetDistance()
            // 返回敌人与当前目标之间的距离
            //
            // abilityUseDistance
            // 是这个技能允许释放的距离
            //
            // 如果当前距离大于技能释放距离，
            // 说明敌人离玩家太远，还不能释放该技能
            if (combatController.GetCurrentTargetDistance() > abilityUseDistance)
            {
                // 设置 Animator 的 Vertical 参数为 1
                //
                // Vertical 通常用于控制前后移动动画
                // Vertical = 1 一般表示向前移动或向前奔跑
                //
                // 参数解释：
                // verticalHash：Animator 参数 Hash，对应 "Vertical"
                // 1f：目标值
                // 0.1f：平滑过渡时间
                // Time.deltaTime：当前帧时间
                //
                // 作用：
                // 让敌人的动画状态机进入向前移动状态
                animator.SetFloat(verticalHash, 1f, 0.1f, Time.deltaTime);

                // 设置 Animator 的 Horizontal 参数为 0
                //
                // Horizontal 通常表示左右移动
                // 0 表示不向左也不向右，只向前
                animator.SetFloat(horizontalHash, 0f, 0.1f, Time.deltaTime);

                // 设置 Animator 的 MoveSpeed 参数为敌人的奔跑速度
                //
                // enemyParameter.runSpeed
                // 来自 EnemyBase，表示敌人的奔跑速度
                //
                // 作用：
                // 让动画状态机知道敌人当前应该以奔跑速度移动
                // 可能会从 Idle / Walk 切换到 Run
                animator.SetFloat(moveSpeedHash, enemyParameter.runSpeed, 0.1f, Time.deltaTime);   
            }
            // 如果敌人与目标之间的距离小于技能释放距离
            //
            // 说明玩家已经进入这个技能的攻击范围
            // 此时可以正式释放技能
            else if (combatController.GetCurrentTargetDistance() < abilityUseDistance)
            {
                // 调用父类 CombatAbilityBase 的 UseAbility()
                //
                // UseAbility() 内部会做几件事：
                // 1. 播放 abilityName 对应的技能动画
                // 2. 将技能设置为不可用
                // 3. 从 availableAbilityList 中移除当前技能
                // 4. 开始技能 CD
                //
                // 也就是说，这一句才是真正释放“上刺横扫”技能
                UseAbility();
            }
        }
    }
}