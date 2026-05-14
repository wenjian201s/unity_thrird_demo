using System.Collections; // 引入协程相关命名空间。当前代码中没有直接使用，可以删除
using System.Collections.Generic; // 引入泛型集合命名空间。当前代码中没有直接使用，可以删除
using Unity.VisualScripting; // Unity Visual Scripting 命名空间。当前代码中没有直接使用，可以删除
using UnityEngine; // 引入 Unity 核心命名空间，例如 ScriptableObject、Animator、Transform、Random 等

// CreateAssetMenu 表示这个类可以在 Unity Project 面板中创建成资源文件
//
// fileName = "AICombat"
// 默认创建出来的资源名叫 AICombat
//
// menuName = "StateMachine/State/AICombat"
// 创建路径为：
// Create -> StateMachine -> State -> AICombat
//
// AICombat 继承 StateActionSO，说明它是状态机中的一个“状态”
// 这个状态表示敌人进入战斗后的 AI 行为逻辑
[CreateAssetMenu(fileName = "AICombat", menuName = "StateMachine/State/AICombat")]
public class AICombat : StateActionSO
{
    // TODO: 将来应该重构 UI 逻辑
    //
    // Boss 血条和耐力条 UI 控制器
    // 当前 AICombat 进入时会调用 AppearBar() 显示 Boss UI
    //
    // 注意：
    // 从架构上讲，战斗 AI 状态直接控制 UI 不太理想
    // 更好的做法是让 Boss 进入战斗时发送事件，由 UI 系统监听事件显示血条
    private BossHealthAndEndurance bossHealthAndEndurance;
    
    // 敌人攻击动画事件控制器
    //
    // EnemyAttackAnimation 通常用于在攻击动画事件中：
    // 1. 开启攻击检测
    // 2. 播放攻击音效
    // 3. 播放攻击特效
    // 4. 同步当前技能攻击配置
    //
    // 当前代码中只是获取了该组件，但没有实际使用
    private EnemyAttackAnimation enemyAttackAnimation;
    
    // =========================
    // 战斗距离参数
    // =========================

    // 后退距离
    //
    // 当玩家距离敌人太近时，敌人会向后退
    // 用于防止敌人一直贴脸攻击，让战斗更有节奏
    [SerializeField] private float backwardDistance;

    // 攻击距离
    //
    // 当玩家距离小于 attackDistance 时，敌人会尝试普通攻击
    [SerializeField] private float attackDistance;

    // 追击距离
    //
    // 当玩家距离大于 chaseDistance 但还没到 runDistance 时，
    // 敌人会向玩家走过去
    [SerializeField] private float chaseDistance;

    // 奔跑追击距离
    //
    // 当玩家距离大于 runDistance 时，
    // 敌人会用 runSpeed 奔跑接近玩家
    [SerializeField] private float runDistance;

    // 技能释放间隔，当前被注释掉
    //
    // 这个参数原本可能用于控制敌人释放技能的欲望
    // 比如每隔几秒才允许 AI 重新选技能
    //[SerializeField] private float abilityCooldownTime;

    // 当前是否可以使用技能，当前被注释掉
    //private bool canUseAbility = true;
    
    // 当前被 AI 选中的技能
    //
    // 如果 currentAbility == null：
    // 敌人执行普通战斗移动逻辑，并尝试选择一个可用技能
    //
    // 如果 currentAbility != null：
    // 敌人执行 currentAbility.InvokeAbility()
    //
    // 技能释放后，如果技能进入不可用状态，
    // 就把 currentAbility 清空，等待下次重新选择技能
    [SerializeField] private CombatAbilityBase currentAbility;
    
    // Animator 参数 Hash
    //
    // StringToHash 可以把字符串参数名转换成 int
    // 使用 int 设置 Animator 参数比每帧使用字符串更高效
    private int verticalHash = Animator.StringToHash("Vertical");
    private int horizontalHash = Animator.StringToHash("Horizontal");
    private int moveSpeedHash = Animator.StringToHash("MoveSpeed");

    // 随机横向移动方向
    //
    // 取值为 1 或 -1
    // 1 表示向右横移
    // -1 表示向左横移
    private int randomHorizontal;

    // 初始化状态
    //
    // 该方法重写自 StateActionSO.Init()
    // 当状态进入时，OnEnter 会调用 base.OnEnter()
    // base.OnEnter() 内部会调用 Init()
    //
    // 作用：
    // 1. 先执行父类 Init，获取 Animator、EnemyCombatController、EnemyBase 等组件
    // 2. 再获取 AICombat 自己额外需要的组件
    protected override void Init(StateMachineSystem stateMachineSystem)
    {
        // 调用父类初始化
        //
        // 父类会从 StateMachineSystem 中获取：
        // animator
        // enemyCombatController
        // enemyMovementController
        // enemyParameter
        // transform
        base.Init(stateMachineSystem);

        // 获取敌人攻击动画事件控制器
        //
        // 当前代码中没有使用它
        // 但未来可以通过它更新技能配置或连接动画事件逻辑
        enemyAttackAnimation = stateMachineSystem.GetComponent<EnemyAttackAnimation>();
        
        // TODO: 将来应该重构 UI 逻辑
        //
        // 获取 Boss 血条和耐力条 UI 控制器
        bossHealthAndEndurance = stateMachineSystem.GetComponent<BossHealthAndEndurance>();
    }

    // 进入战斗状态时调用
    //
    // 当状态机切换到 AICombat 状态时，会执行这个函数
    public override void OnEnter(StateMachineSystem stateMachineSystem)
    {
        // 执行父类 OnEnter
        //
        // 父类 OnEnter 会调用 Init(stateMachineSystem)
        // 也就是初始化当前状态所需的组件引用
        base.OnEnter(stateMachineSystem);

        // 播放 Boss / 敌人的苏醒动画
        //
        // 注意：
        // Animator 中需要存在名为 "WakeUp" 的状态
        animator.Play("WakeUp");
        
        // TODO: UI 逻辑需要重构
        //
        // 显示 Boss 血条和耐力条
        bossHealthAndEndurance.AppearBar();
    }

    // 战斗状态每帧执行
    public override void OnUpdate()
    {
        // 执行敌人战斗行为逻辑
        //
        // 包括：
        // 1. 没有技能时执行普通移动 / 普通攻击
        // 2. 尝试获取技能
        // 3. 有技能时调用技能
        CombatAction();

        // 每帧让敌人转向当前目标
        LookAtTarget();
    }

    // 退出战斗状态时调用
    //
    // 当前没有写逻辑
    // 以后可以在这里关闭 UI、清空当前技能、重置 Animator 参数等
    public override void OnExit()
    {

    }
    
    // 没有当前技能时的普通战斗移动逻辑
    //
    // 这个函数根据敌人与玩家之间的距离，
    // 决定敌人是普通攻击、后退、横移、走近还是跑近
    private void NoCombatMove()
    {
        // 如果当前动画处于不可打断状态，则不执行普通移动逻辑
        //
        // Roll：翻滚 / 闪避状态
        // Ability：技能状态
        // GSAbililty：大剑技能状态，注意这里单词疑似拼写错误
        // animator.IsInTransition(0)：动画正在过渡中
        //
        // 这样做的目的：
        // 防止敌人在攻击、翻滚、技能播放过程中被普通移动逻辑打断
        if (animator.GetCurrentAnimatorStateInfo(0).IsTag("Roll") || 
            animator.GetCurrentAnimatorStateInfo(0).IsTag("Ability") ||
            animator.IsInTransition(0) || 
            animator.GetCurrentAnimatorStateInfo(0).IsTag("GSAbililty"))
            return;
        
        // 判断当前是否处于 Motion 标签的动画状态
        //
        // Motion 一般表示敌人可以自由移动的状态，
        // 例如 Idle、Walk、Run、CombatMove 等
        //
        // enemyCombatController.GetCurrentTargetDistance() != -1f
        // 表示当前存在目标
        // 因为你的 EnemyCombatController 中没有目标时会返回 -1f
        if (animator.GetCurrentAnimatorStateInfo(0).IsTag("Motion") && 
            !Mathf.Approximately(enemyCombatController.GetCurrentTargetDistance(), -1f))
        {
            // 如果玩家距离小于攻击距离，则进行普通攻击
            if (enemyCombatController.GetCurrentTargetDistance() < attackDistance)
            {
                // 判断当前不是攻击、技能、大剑技能状态时，才播放普通攻击
                //
                // 注意：
                // 这里原代码使用的是 ||，逻辑上有问题。
                // 因为一个动画状态不可能同时拥有 Attack、Ability、GSAbility 三个 Tag，
                // 所以这个判断大概率永远为 true。
                //
                // 如果你的目的是“当前不属于这三种状态中的任意一种”，
                // 应该使用 &&。
                if (!animator.GetCurrentAnimatorStateInfo(0).IsTag("Attack") || 
                    !animator.GetCurrentAnimatorStateInfo(0).IsTag("Ability") ||
                    !animator.GetCurrentAnimatorStateInfo(0).IsTag("GSAbililty"))
                {
                    // TODO: 待添加更完整的普通攻击逻辑
                    //
                    // 播放普通攻击动画 Normal01
                    // Animator 中需要有名为 "Normal01" 的动画状态
                    animator.Play("Normal01");
                }
            }
            // 如果玩家距离小于后退距离，则敌人向后退
            //
            // 注意：
            // 由于上一个 if 是 distance < attackDistance，
            // 这里是 else if distance < backwardDistance。
            // 如果 attackDistance 比 backwardDistance 大，
            // 那么 backwardDistance 这一段可能永远进不来。
            else if (enemyCombatController.GetCurrentTargetDistance() < backwardDistance)
            {
                // Vertical = -1 表示向后移动
                animator.SetFloat(verticalHash, -1f, 0.1f, Time.deltaTime);

                // Horizontal = 0 表示不横移
                animator.SetFloat(horizontalHash, 0f, 0.1f, Time.deltaTime);

                // 使用 walkSpeed 作为移动速度
                animator.SetFloat(moveSpeedHash, enemyParameter.walkSpeed, 0.1f, Time.deltaTime);

                // 随机下一次横移方向
                randomHorizontal = GetRandomHorizontal();
            }
            // 如果玩家距离大于后退距离，并且小于追击距离，则敌人进行横向移动
            //
            // 这会让敌人围绕玩家左右移动，
            // 增加战斗中的压迫感和灵活性
            else if (enemyCombatController.GetCurrentTargetDistance() > backwardDistance && 
                     enemyCombatController.GetCurrentTargetDistance() < chaseDistance)
            {
                // Vertical = 0 表示不前进不后退
                animator.SetFloat(verticalHash, 0f, 0.1f, Time.deltaTime);

                // Horizontal = randomHorizontal
                // 表示向左或向右横移
                animator.SetFloat(horizontalHash, randomHorizontal, 0.1f, Time.deltaTime);

                // 横移使用步行速度
                animator.SetFloat(moveSpeedHash, enemyParameter.walkSpeed, 0.1f, Time.deltaTime);
            }
            // 如果玩家距离大于追击距离，并且小于奔跑距离，则敌人走向玩家
            else if (enemyCombatController.GetCurrentTargetDistance() > chaseDistance && 
                     enemyCombatController.GetCurrentTargetDistance() < runDistance)
            {
                // Vertical = 1 表示向前移动
                animator.SetFloat(verticalHash, 1f, 0.1f, Time.deltaTime);

                // Horizontal = 0 表示直线接近，不横移
                animator.SetFloat(horizontalHash, 0f, 0.1f, Time.deltaTime);

                // 使用 walkSpeed 走向玩家
                animator.SetFloat(moveSpeedHash, enemyParameter.walkSpeed, 0.1f, Time.deltaTime);
              
                // 随机下一次横移方向
                randomHorizontal = GetRandomHorizontal();
            }
            // 如果玩家距离大于奔跑追击距离，则敌人奔跑接近玩家
            else if (enemyCombatController.GetCurrentTargetDistance() > runDistance)
            {
                // 向前移动
                animator.SetFloat(verticalHash, 1f, 0.1f, Time.deltaTime);

                // 不横移
                animator.SetFloat(horizontalHash, 0f, 0.1f, Time.deltaTime);

                // 使用 runSpeed 奔跑追击
                animator.SetFloat(moveSpeedHash, enemyParameter.runSpeed, 0.1f, Time.deltaTime);
            }
        }
        else
        {
            // 如果当前不是 Motion 状态，或者没有目标，
            // 则停止移动动画参数
            //
            // Vertical = 0
            // Horizontal = 0
            // MoveSpeed = 0
            //
            // 让敌人回到待机 / 停止移动状态
            animator.SetFloat(verticalHash, 0f, 0.1f, Time.deltaTime);
            animator.SetFloat(horizontalHash, 0f, 0.1f, Time.deltaTime);
            animator.SetFloat(moveSpeedHash, 0f, 0.1f, Time.deltaTime);
        }
    }
    
    // 让敌人朝向当前目标
    private void LookAtTarget()
    {
        // 从战斗控制器中获取当前目标
        Transform target = enemyCombatController.GetCurrentTarget();

        // 如果没有目标，则不旋转
        if (!target)
            return;

        // 平滑过渡到目标方向
        //
        // target.transform.position - transform.position
        // 表示从敌人指向玩家的方向
        //
        // Vector3.Lerp(a, b, t)
        // 表示从当前 forward 慢慢插值到目标方向
        //
        // Time.deltaTime * enemyParameter.rotationSpeed
        // 控制旋转速度
        //
        // 作用：
        // 让敌人持续面向玩家，而不是瞬间转向
        transform.forward = Vector3.Lerp(
            transform.forward,
            target.transform.position - transform.position,
            Time.deltaTime * enemyParameter.rotationSpeed
        );
    }

    /// <summary>
    /// 敌人战斗行为
    /// </summary>
    //
    // 这是 AICombat 的核心函数
    //
    // 分两种情况：
    //
    // 1. 当前没有技能 currentAbility == null
    //    执行普通战斗移动逻辑，并尝试从可用技能列表中获取技能
    //
    // 2. 当前有技能 currentAbility != null
    //    执行技能逻辑 currentAbility.InvokeAbility()
    private void CombatAction()
    {
        // 如果当前没有技能，则执行普通战斗移动
        if (!currentAbility)
        {
            // 根据距离执行：
            // 普通攻击 / 后退 / 横移 / 走近 / 跑近
            NoCombatMove();

            // 如果当前目标距离为 -1，
            // 说明当前没有目标，或者玩家不在敌人视野内
            if (Mathf.Approximately(enemyCombatController.GetCurrentTargetDistance(), -1f))
            {
                // TODO: 补充玩家不在敌人视野中的情况
                //
                // 例如：
                // 1. 切回巡逻状态
                // 2. 切回搜索状态
                // 3. 停止战斗 UI
                // 4. 清空当前技能
            }

            // 尝试获取一个可用技能
            GetAbility();
        }
        // 如果当前已经有技能，则执行该技能
        else if (currentAbility)
        {
            // 调用技能逻辑
            //
            // 具体行为由 CombatAbilityBase 的子类决定
            // 例如：
            // Ability2 会先接近玩家，
            // 进入技能距离后调用 UseAbility()
            currentAbility.InvokeAbility();

            // 如果当前技能变为不可用，说明技能已经释放并进入冷却
            //
            // CombatAbilityBase.UseAbility() 中会执行：
            // abilitiyIsAvailable = false;
            //
            // 所以这里检测到 false 后，
            // 说明 AI 不应该继续持有这个技能，
            // 要把 currentAbility 清空，等待下次重新选择技能
            if (currentAbility.GetAbilityAvailable() == false)
            {
                // 丢弃当前技能
                currentAbility = null;
            }
        }
    }

    /// <summary>
    /// 获取技能
    /// </summary>
    private void GetAbility()
    {
        // TODO: 可以添加一个“释放技能间隔 CD”
        //
        // 这样可以控制敌人 AI 的攻击欲望，
        // 避免敌人每次 currentAbility 为空时都立刻拿一个技能释放
        //
        // 当前条件：
        // 1. 当前没有技能
        // 2. 当前动画不是 Ability 标签
        // 3. 当前 Animator 不在过渡中
        if (!currentAbility && 
            !animator.GetCurrentAnimatorStateInfo(0).IsTag("Ability") &&
            !animator.IsInTransition(0))
        {
            // 从 EnemyCombatController 的可用技能列表中随机获取一个技能
            //
            // availableAbilityList 中通常只包含当前不在 CD 的技能
            //
            // 获取后赋值给 currentAbility
            // 后续 CombatAction 会执行 currentAbility.InvokeAbility()
            currentAbility = enemyCombatController.GetRandomAvailableAbility();
        }
    }
    
    
    #region 工具方法

    // 随机获取横向移动方向
    //
    // Random.Range(0, 100)
    // 随机生成 0 到 99 之间的整数
    //
    // 如果随机数大于 50，返回 1
    // 否则返回 -1
    //
    // 作用：
    // 让敌人在近距离时随机向左或向右横移
    private int GetRandomHorizontal()
    {
        int randomNum = Random.Range(0, 100);
        return randomNum > 50 ? 1 : -1;
    }

    // Gizmos 调试绘制
    //
    // 在 Scene 视图中画出一条红色射线
    // 用于辅助观察敌人与目标方向
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        
        // 绘制方向射线
        //
        // transform.position + Vector3.up：
        // 从敌人位置上方一点开始画
        //
        // -enemyCombatController.GetDirectionForTarget()：
        // 当前写的是目标方向的反方向
        //
        // 如果你想画“敌人指向玩家”的方向，
        // 通常应该去掉负号：
        // enemyCombatController.GetDirectionForTarget()
        Gizmos.DrawRay(
            transform.position + Vector3.up,
            -enemyCombatController.GetDirectionForTarget()
        );
    }

    #endregion
}