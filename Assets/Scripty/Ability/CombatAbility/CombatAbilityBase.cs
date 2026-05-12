using System.Collections; // 引入协程相关命名空间。当前代码中没有直接使用，可以删除
using System.Collections.Generic; // 引入泛型集合命名空间。当前代码中没有直接使用，可以删除
using UnityEngine; // 引入 Unity 核心命名空间，例如 ScriptableObject、Animator、SerializeField 等
using UnityEngine.Serialization; // Unity 序列化兼容相关命名空间。当前代码中没有使用，可以删除

// CombatAbilityBase 是所有敌人技能 / 招式的抽象基类
//
// abstract 表示这个类不能直接实例化
// 它只能作为父类，让具体技能继承它
//
// ScriptableObject 表示它不是普通 MonoBehaviour 脚本
// 它可以作为 Unity 资源文件存在，例如：
// 右键 Create 一个 KatanaAttackAbility.asset
// 然后把这个技能资源配置到 EnemyCombatController 的 abilityList 中
//
// 主要作用：
// 1. 保存技能基础数据，例如技能名、技能 ID、CD、使用距离
// 2. 保存技能运行状态，例如技能是否可用
// 3. 保存技能执行时需要用到的组件引用
// 4. 提供统一的技能释放逻辑 UseAbility()
// 5. 提供统一的技能冷却逻辑 AbilityCoolDown()
// 6. 让子类通过 InvokeAbility() 实现具体技能行为
public abstract class CombatAbilityBase : ScriptableObject
{
    // =========================
    // 技能基础数据
    // =========================

    // 技能名称
    //
    // 这个名称通常要和 Animator 动画状态名对应
    // 因为 UseAbility() 中会执行：
    // animator.CrossFade(abilityName, 0.1f);
    //
    // 例如：
    // abilityName = "Katana_Attack_01"
    // 那么 Animator 中需要有同名动画状态
    [SerializeField] protected string abilityName;

    // 技能 ID
    //
    // 用于通过数字编号查找技能
    // 比如 EnemyCombatController.GetAbilityByID(int abilityID)
    [SerializeField] protected int abilityID;

    // 技能冷却时间
    //
    // 释放技能后，会进入 CD
    // CD 结束后，该技能重新变为可用
    [SerializeField] protected float abilityCD;

    // 技能使用距离
    //
    // 用于判断敌人和玩家之间的距离是否适合释放该技能
    //
    // 例如：
    // 普通砍击技能 abilityUseDistance = 2
    // 突刺技能 abilityUseDistance = 5
    //
    // 敌人 AI 可以根据这个值判断当前是否能释放技能
    [SerializeField] protected float abilityUseDistance;

    // 技能是否可用
    //
    // true 表示技能当前可以释放
    // false 表示技能正在冷却中，不能释放
    //
    // 注意：
    // 变量名 abilitiyIsAvailable 拼写有误
    // 推荐改成 abilityIsAvailable
    [SerializeField] protected bool abilitiyIsAvailable;


    #region 组件

    // =========================
    // 技能运行时需要的组件引用
    // =========================

    // 敌人的 Animator 动画机
    //
    // 技能释放时通过 Animator 播放技能动画
    protected Animator animator;

    // 敌人战斗控制器
    //
    // 用于访问敌人的技能列表、当前目标、攻击配置等战斗逻辑
    protected EnemyCombatController combatController;

    // 敌人移动控制器
    //
    // 技能内部可能需要控制敌人位移
    // 例如突刺、冲锋、攻击前移、后撤等
    protected EnemyMovementController enemyMovementController;

    // 敌人基础属性
    //
    // 用于读取敌人血量、耐力、转身速度、移动速度等参数
    protected EnemyBase enemyParameter;

    #endregion


    #region 动画状态机哈希值

    // =========================
    // Animator 参数 Hash
    // =========================
    //
    // Animator.StringToHash 的作用：
    // 把字符串参数名转换成 int 类型
    // 之后 SetFloat / SetBool 时使用 Hash 可以减少字符串查找开销
    //
    // 这些参数当前基类中没有使用，
    // 但子类技能中可能会使用，例如控制移动动画混合树

    // Animator 中的 Vertical 参数
    // 通常用于前后方向动画混合
    protected int verticalHash = Animator.StringToHash("Vertical");

    // Animator 中的 Horizontal 参数
    // 通常用于左右方向动画混合
    protected int horizontalHash = Animator.StringToHash("Horizontal");

    // Animator 中的 MoveSpeed 参数
    // 通常用于控制移动速度动画
    protected int moveSpeedHash = Animator.StringToHash("MoveSpeed");

    #endregion


    /// <summary>
    /// 调用技能
    /// </summary>
    //
    // 抽象方法，子类必须实现
    //
    // 这个方法用于定义“具体技能如何释放”
    //
    // 例如：
    // 普通攻击技能的 InvokeAbility() 里可能直接调用 UseAbility()
    // 冲锋技能的 InvokeAbility() 里可能先判断距离，再调用 UseAbility()
    // 远程技能的 InvokeAbility() 里可能生成投射物，再调用 UseAbility()
    public abstract void InvokeAbility();


    /// <summary>
    /// 使用技能
    /// </summary>
    //
    // protected 表示：
    // 只有当前类和子类可以调用
    //
    // 也就是说：
    // 外部不能直接调用 UseAbility()
    // 外部应该调用子类实现的 InvokeAbility()
    //
    // 这是一种常见设计：
    // 子类负责判断能不能释放技能
    // 父类负责执行通用释放逻辑
    protected void UseAbility()
    {
        // 判断当前 Animator 的 Base Layer 是否处于 Tag 为 "Motion" 的状态
        //
        // Animator Tag 是 Unity Animator 状态上的标签
        // 这里的意思可能是：
        // 只有敌人当前处于可行动状态时，才允许切换到技能动画
        //
        // 例如：
        // Idle、Walk、Run 等动画状态可以设置为 Motion Tag
        // 受击、死亡、击倒等状态不设置为 Motion Tag
        //
        // 这样可以防止敌人在死亡、受击、击倒时强行释放技能
        if (animator.GetCurrentAnimatorStateInfo(0).IsTag("Motion"))
        {
            // 播放技能动画
            //
            // abilityName 应该对应 Animator 中的动画状态名
            // 0.1f 表示动画过渡时间
            //
            // 原理：
            // CrossFade 会从当前动画平滑过渡到目标动画
            animator.CrossFade(abilityName, 0.1f);
        }

        // 将技能设为不可用
        //
        // 释放技能后进入冷却状态
        abilitiyIsAvailable = false;

        // 将当前技能从可用技能列表中移除
        //
        // combatController.availableAbilityList 是敌人当前可释放技能列表
        // 移除后，敌人 AI 在随机选择技能时不会再选到这个正在冷却的技能
        combatController.availableAbilityList.Remove(this);

        // 开始技能冷却
        AbilityCoolDown();
    }


    /// <summary>
    /// 技能 CD
    /// </summary>
    //
    // 作用：
    // 创建一个计时器
    // 等待 abilityCD 秒后，让技能重新变为可用
    public void AbilityCoolDown()
    {
        // 从对象池中获取一个 Timer 对象
        //
        // CachePoolManager.Instance.GetObject("Tool/Timer")
        // 说明你的项目中有一个对象池系统
        //
        // 原理：
        // 不直接 new Timer，而是从池中拿
        // 可以减少频繁创建和销毁对象带来的性能开销
        Timer timer = CachePoolManager.Instance
            .GetObject("Tool/Timer")
            .GetComponent<Timer>();

        // 创建计时器
        //
        // abilityCD：
        // 冷却时间
        //
        // () => { ... }：
        // Lambda 回调函数
        // 当计时结束后，会执行里面的逻辑
        timer.CreateTime(abilityCD, () =>
        {
            // CD 结束后，将技能重新设为可用
            abilitiyIsAvailable = true;

            // 把当前技能重新加入敌人的可用技能列表
            //
            // 这样 EnemyCombatController.GetRandomAvailableAbility()
            // 又可以选到这个技能
            combatController.availableAbilityList.Add(this);
        });
    }
    
    #region 公共调用接口

    /// <summary>
    /// 初始化
    /// </summary>
    //
    // 这个方法由 EnemyCombatController.InitAllAbilities() 调用
    //
    // 由于 CombatAbilityBase 是 ScriptableObject，
    // 它自己不能像 MonoBehaviour 那样直接 GetComponent
    // 所以需要外部把运行时组件引用传进来
    //
    // 参数解释：
    // animator：敌人的动画机
    // combatController：敌人战斗控制器
    // enemyMovementController：敌人移动控制器
    // enemyParameter：敌人基础属性
    public void Init(
        Animator animator,
        EnemyCombatController combatController, 
        EnemyMovementController enemyMovementController,
        EnemyBase enemyParameter
    )
    {
        // 保存 Animator 引用
        this.animator = animator;

        // 保存敌人战斗控制器引用
        this.combatController = combatController;

        // 保存敌人移动控制器引用
        this.enemyMovementController = enemyMovementController;

        // 保存敌人基础属性引用
        this.enemyParameter = enemyParameter;
    }
    
    
    // 获取技能名称
    public string GetAbilityName() => abilityName;

    // 获取技能 ID
    public int GetAbilityID() => abilityID;

    // 获取技能冷却时间
    public float GetAbilityCD() => abilityCD;

    // 获取技能使用距离
    public float GetAbilityUseDistance() => abilityUseDistance;

    // 获取技能是否可用
    public bool GetAbilityAvailable() => abilitiyIsAvailable;
    
    // 设置技能是否可用
    //
    // isDone 为 true：技能可用
    // isDone 为 false：技能不可用
    public void SetAbilityAvailable(bool isDone)
    {
        abilitiyIsAvailable = isDone;
    }

    #endregion
}