using System; // 引入 C# 基础命名空间。当前代码中没有明显使用，可以删除
using System.Collections; // 引入 IEnumerator，用于协程，例如 IE_HitAudioSound
using System.Collections.Generic; // 引入 List 集合，用于存储敌人技能列表
using System.Linq; // 引入 LINQ 查询扩展。当前代码中没有使用，可以删除
using Cinemachine; // 引入 Cinemachine，用于屏幕震动，例如 CinemachineImpulseSource
using Unity.VisualScripting; // 引入 Unity 可视化脚本命名空间。当前代码中没有使用，可以删除
using UnityEngine; // 引入 Unity 核心命名空间，例如 MonoBehaviour、Transform、Animator、Physics 等
using UnityEngine.Serialization; // Unity 序列化相关命名空间。当前代码中没有使用，可以删除
using UnityEngine.UIElements; // UIElements 命名空间。当前代码中没有使用，可以删除
using Assembly = System.Reflection.Assembly; // 给 System.Reflection.Assembly 起别名。当前代码中没有使用，可以删除
using Random = UnityEngine.Random; // 给 UnityEngine.Random 起别名，避免和 System.Random 混淆


// EnemyCombatController 敌人战斗控制器
// 继承 CombatControllerBase，说明它是基于通用战斗控制器扩展出来的敌人专用战斗逻辑
//
// 主要作用：
// 1. 处理敌人受击逻辑
// 2. 处理敌人血量、耐力、死亡
// 3. 播放受击、击倒、死亡动画
// 4. 处理攻击反馈：屏幕震动、音效、顿帧
// 5. 维护当前攻击目标
// 6. 初始化敌人的所有技能
// 7. 提供技能查询接口
// 8. 当敌人攻击命中玩家时，把伤害信息传给玩家
public class EnemyCombatController : CombatControllerBase
{
    // =========================
    // 组件引用
    // =========================

    // 敌人视野检测组件
    // 用于获取当前敌人看到的目标
    // currentTarget 会从 enemyView.CurrentTarget 同步过来
    private EnemyView enemyView;

    // 敌人基础属性组件
    // 一般存储敌人的血量、耐力、转身速度等基础数值
    private EnemyBase enemyParameter;

    // 敌人移动控制器
    // 技能初始化时会传给 CombatAbilityBase
    // 技能可以通过它控制敌人位移、冲刺、攻击前移等
    private EnemyMovementController enemyMovementController;

    // Cinemachine 震动源
    // 用于在敌人受到攻击时触发屏幕震动
    private CinemachineImpulseSource cinemachineImpulseSource;

    // 敌人攻击检测组件
    // 用于控制敌人攻击判定的开启和关闭
    // 受击时会调用 EndAttacking()，防止攻击检测残留
    private EnemyAttackDetection enemyAttackDetection;


    // =========================
    // 战斗相关
    // =========================

    [Header("战斗相关")]

    // 玩家所在 Layer
    // 用于 FindTarget() 中通过 Physics.OverlapBoxNonAlloc 搜索附近玩家
    [SerializeField] protected LayerMask playerLayer;

    // 当前敌人的目标
    // 例如玩家 Transform
    // 通常由 EnemyView 检测后传入
    [SerializeField] protected Transform currentTarget = null;

    // 攻击者对象
    // 当前代码中没有实际使用
    // 可能是预留字段，用来记录攻击自己的对象
    [SerializeField] protected GameObject attacker;


    // =========================
    // 技能 / 招式相关
    // =========================

    [Header("技能")]

    // 敌人拥有的全部技能列表
    // 可以在 Inspector 中配置多个 CombatAbilityBase 派生技能
    // 例如普通攻击、突刺、重击、连击、跳劈等
    [SerializeField] private List<CombatAbilityBase> abilityList = new List<CombatAbilityBase>();

    // 当前可用技能列表
    // 可用通常表示技能不在冷却中
    // 初始化时会把所有技能加入这个列表
    [SerializeField] public List<CombatAbilityBase> availableAbilityList = new List<CombatAbilityBase>();


    // =========================
    // 攻击检测相关
    // =========================

    [Header("攻击检测")]

    // 当前攻击配置索引
    // 一个技能可能有多段攻击判定
    // 例如三连击：
    // detectionConfigs[0] 第一刀
    // detectionConfigs[1] 第二刀
    // detectionConfigs[2] 第三刀
    //
    // attackConfigCount 就表示当前使用第几个攻击检测配置
    private int attackConfigCount;

    // 当前技能配置
    // 里面应该包含该招式的伤害、攻击检测、打击反馈等数据
    private AbilityConfig currentAbilityConfig;


    // =========================
    // 玩家锁定点相关
    // =========================

    [Header("玩家射线检测")]

    // 锁定点 Transform
    // 可能用于玩家锁定敌人时，相机或锁定系统瞄准的位置
    // 例如锁定敌人的胸口、头部
    [SerializeField] private Transform lockOnTransform;


    // Animator 参数 Hash
    // 对应 Animator 里的 "LockOn" 参数
    // 用于告诉动画状态机：敌人当前是否锁定了目标
    private int lockOnHash;


    // Start 在脚本启用后的第一帧之前执行
    // 这里负责初始化组件引用、动画参数 Hash、技能列表
    private void Start()
    {
        // 调用父类 CombatControllerBase 的 Start
        // 父类中可能初始化了 animator、audioSource、canBeHit 等通用战斗变量
        base.Start();

        // 获取 EnemyView 组件，用于获得敌人当前看到的目标
        enemyView = GetComponent<EnemyView>();

        // 获取 EnemyBase 组件，用于读取/修改敌人血量、耐力、转身速度等属性
        enemyParameter = GetComponent<EnemyBase>();

        // 获取 EnemyMovementController 组件，技能执行时可能需要控制敌人移动
        enemyMovementController = GetComponent<EnemyMovementController>();

        // 获取 CinemachineImpulseSource 组件，用于受击时触发屏幕震动
        cinemachineImpulseSource = GetComponent<CinemachineImpulseSource>();

        // 获取 EnemyAttackDetection 组件，用于控制攻击检测的开始和结束
        enemyAttackDetection = GetComponent<EnemyAttackDetection>();

        // 把 Animator 参数名 "LockOn" 转换成 Hash，提高运行时设置参数的效率
        lockOnHash = Animator.StringToHash("LockOn");

        // 初始化所有技能
        InitAllAbilities();
    }


    // Update 每帧执行一次
    private void Update()
    {
        // 每帧从 EnemyView 同步当前目标
        // 如果 EnemyView 看到了玩家，则 currentTarget = 玩家
        // 如果 EnemyView 没看到玩家，则 currentTarget = null
        UpdateCurrentTarget();
    }


    // =========================
    // 敌人公共方法：受到攻击
    // =========================

    // 敌人受击逻辑
    //
    // interactionConfig：
    // 攻击交互配置，通常包含伤害、削韧、武器类型、受击动画名等
    //
    // attackFeedbackConfig：
    // 攻击反馈配置，通常包含屏幕震动、音效、顿帧时间、动画速度等
    //
    // attacker：
    // 攻击者，一般是玩家或者玩家武器的 Transform
    public override void OnHit(
        ComboInteractionConfig interactionConfig,
        AttackFeedbackConfig attackFeedbackConfig,
        Transform attacker
    )
    {
        // 如果敌人当前不能受击，或者敌人血量已经小于等于 0，则直接返回
        //
        // canBeHit 可能来自 CombatControllerBase
        // 用于防止敌人在硬直、无敌、死亡状态下重复受击
        if (!canBeHit || enemyParameter.health <= 0)
            return;

        // 调用父类的受击逻辑
        // 父类可能处理通用的受击标记、无敌帧、硬直状态等
        base.OnHit(interactionConfig, attackFeedbackConfig, attacker);

        // 受击时强制停止敌人的攻击检测
        //
        // 原因：
        // 敌人正在攻击时如果被玩家打断，
        // 攻击判定可能还处于开启状态。
        // 如果不关闭，可能出现敌人被打断后仍然能打到玩家的问题。
        enemyAttackDetection.EndAttacking();

        // 计算攻击者相对于敌人的方向
        // attacker.position - this.transform.position
        // 表示从敌人指向攻击者的方向
        Vector3 dir = (attacker.position - this.transform.position).normalized;

        // 计算攻击者方向和敌人正前方方向之间的夹角
        //
        // angleForward < 90：
        // 攻击者在敌人前半区
        //
        // angleForward >= 90：
        // 攻击者在敌人后半区
        //
        // 后面会根据这个角度决定播放前倒死亡还是后倒死亡
        float angleForward = Vector3.Angle(dir, transform.forward);

        // =========================
        // 处理血量伤害
        // =========================

        // 计算血量伤害
        // interactionConfig.healthDamage 是基础伤害
        // Random.Range(-10, 10) 添加随机浮动
        //
        // 注意：
        // UnityEngine.Random.Range(int, int) 对整数来说，最大值是不包含的
        // 所以这里随机范围是 -10 到 9
        int healthDamage = interactionConfig.healthDamage + Random.Range(-10, 10);

        // 输出受击日志，显示武器类型和伤害
        Debug.Log("受击了!受到了来自" + interactionConfig.weaponType + "的" + healthDamage + "点伤害!");

        // 扣除敌人血量
        enemyParameter.health -= healthDamage;

        // 如果扣血后敌人死亡
        if (enemyParameter.health <= 0)
        {
            // 把血量限制为 0，避免出现负数
            enemyParameter.health = 0;

            // 输出死亡日志
            Debug.Log("敌人似了!");

            // 播放死亡动画
            //
            // 如果攻击者在敌人前方，则播放正面死亡动画
            // 如果攻击者在敌人后方，则播放背面死亡动画
            if (angleForward < 90f)
            {
                // CrossFadeInFixedTime 参数含义：
                // "Die_Front"：目标动画状态名
                // 0.15f：过渡时间
                // 0：动画层，0 一般是 Base Layer
                // 0：从目标动画的开头开始播放
                animator.CrossFadeInFixedTime("Die_Front", 0.15f, 0, 0);
            }
            else
            {
                // 播放背后死亡动画
                animator.CrossFadeInFixedTime("Die_Back", 0.15f, 0, 0);
            }

            // 死亡后直接返回，不再处理耐力、受击动画、反馈之后的逻辑
            return;
        }

        // =========================
        // 处理耐力 / 韧性
        // =========================

        // 如果敌人还有耐力
        if (enemyParameter.endurance > 0)
        {
            // 计算耐力伤害
            // interactionConfig.enduranceDamage 是基础削韧值
            // Random.Range(-10, 10) 添加随机浮动
            int enduranceDamage = interactionConfig.enduranceDamage + Random.Range(-10, 10);

            // 输出耐力减少日志
            // 注意：这里输出的是 interactionConfig.enduranceDamage，
            // 不是最终的 enduranceDamage，日志可能和实际扣除值不一致
            Debug.Log(
                string.Format(
                    "减少了{0}点耐力，当前耐力为{1}点!",
                    interactionConfig.enduranceDamage,
                    enemyParameter.endurance
                )
            );

            // 扣除耐力
            enemyParameter.endurance -= enduranceDamage;

            // 如果耐力小于等于 0，敌人进入大硬直 / 击倒状态
            if (enemyParameter.endurance <= 0)
            {
                // 耐力归零，防止负数
                enemyParameter.endurance = 0;

                Debug.Log("敌人耐力清空");

                // TODO:
                // 播放耐力清零动画，出大硬直
                // 可以添加子弹时间效果

                // 根据攻击方向播放正面击倒或背面击倒
                if (angleForward < 90f)
                {
                    // 正面击倒动画
                    animator.CrossFadeInFixedTime("KnockDown_Front", 0.15f, 0, 0);
                }
                else
                {
                    // 背面击倒动画
                    animator.CrossFadeInFixedTime("KnockDown_Back", 0.15f, 0, 0);
                }
            }
            else
            {
                // 如果耐力没有被清空，只播放普通受击动画
                //
                // 这里播放的是 Hit 层，也就是 Animator 的第 1 层
                // 好处：
                // 可以只让上半身播放受击动作，
                // 下半身仍然保持移动或其他基础动画
                animator.CrossFadeInFixedTime(interactionConfig.hitName, 0.1555f, 1, 0);
            }
        }
        else
        {
            // 如果敌人当前已经没有耐力
            // 说明敌人可能处于破防、硬直或者特殊可打断状态

            // 如果敌人当前基础层动画带有 GSAbility 或 HitStun 标签
            //
            // GSAbility 可能代表 GreatSword Ability，大剑招式
            // HitStun 代表硬直状态
            //
            // 这里逻辑表示：
            // 如果敌人正在释放大剑攻击，或者已经处于硬直，
            // 就只在 Hit 层播放受击动画，不打断 Base Layer 主动作
            if (
                animator.GetCurrentAnimatorStateInfo(0).IsTag("GSAbility")
                || animator.GetCurrentAnimatorStateInfo(0).IsTag("HitStun")
            )
            {
                // 在第 1 层播放受击动画
                // 主要影响上半身，不打断基础层动作
                animator.CrossFadeInFixedTime(interactionConfig.hitName, 0.1555f, 1, 0);
            }
            else
            {
                // 如果不是大剑攻击，也不是大硬直
                // 则在 Base Layer 播放受击动画
                //
                // Base Layer 受击动画会打断当前动作
                // 适合普通敌人被攻击时中断当前行为
                animator.CrossFadeInFixedTime(interactionConfig.hitName, 0.1555f, 0, 0);
            }
        }

        // =========================
        // 处理敌人朝向
        // =========================

        // 如果当前没有目标，则在附近寻找玩家
        FindTarget();

        // 让敌人朝向当前目标
        LookAtTarget();

        // =========================
        // 处理攻击反馈
        // =========================

        // 如果攻击反馈配置不为空
        if (attackFeedbackConfig != null)
        {
            // 根据配置产生屏幕震动
            //
            // velocity 代表震动方向和强度
            cinemachineImpulseSource.GenerateImpulseWithVelocity(attackFeedbackConfig.velocity);

            // 延迟播放受击音效
            // audioStartTime 用来控制音效在攻击命中后的第几秒播放
            StartCoroutine(
                IE_HitAudioSound(
                    attackFeedbackConfig.audioStartTime,
                    attackFeedbackConfig.audioClip
                )
            );

            // 设置 Animator 播放速度
            //
            // 如果 animatorSpeed 很小，例如 0.05，
            // 可以制造“顿帧”效果
            SetAnimatorSpeed(attackFeedbackConfig.animatorSpeed);

            // 在 stopFrameTime 秒后恢复动画速度
            Invoke(nameof(ResetAnimatorSpeed), attackFeedbackConfig.stopFrameTime);
        }
    }


    // 受击音效延迟播放协程
    //
    // countTime：
    // 等待时间
    //
    // audioClip：
    // 要播放的音效
    IEnumerator IE_HitAudioSound(float countTime, AudioClip audioClip)
    {
        // 当等待时间还大于 0 时，每帧减少 Time.deltaTime
        while (countTime > 0)
        {
            // 等待下一帧
            yield return null;

            // 减少计时
            countTime -= Time.deltaTime;
        }

        // 播放一次受击音效
        // audioSource 应该来自 CombatControllerBase
        // 0.1f 是音量
        audioSource.PlayOneShot(audioClip, 0.1f);
    }


    // 查找目标
    //
    // 作用：
    // 当敌人受击但当前没有目标时，
    // 在敌人附近用盒形检测寻找玩家
    private void FindTarget()
    {
        // 如果已经有目标，直接返回
        if (currentTarget)
            return;

        // 创建一个长度为 1 的数组，用于接收检测到的玩家 Collider
        Collider[] target = new Collider[1];

        // 使用盒形检测搜索玩家
        //
        // transform.position：
        // 检测盒中心
        //
        // new Vector3(4, 4, 4)：
        // 半尺寸，实际检测盒大小是 8 x 8 x 8
        //
        // target：
        // 接收检测结果
        //
        // Quaternion.identity：
        // 检测盒不旋转
        //
        // playerLayer：
        // 只检测玩家层
        var size = Physics.OverlapBoxNonAlloc(
            transform.position,
            new Vector3(4, 4, 4),
            target,
            Quaternion.identity,
            playerLayer
        );

        // 如果检测到玩家
        if (size != 0)
        {
            // 设置当前目标为检测到的玩家
            currentTarget = target[0].transform;
        }
    }


    // 让敌人看向当前目标
    //
    // 作用：
    // 受击后让敌人转向玩家，
    // 或者在有目标时逐渐朝向玩家
    private void LookAtTarget()
    {
        // 如果没有目标，直接返回
        if (!currentTarget)
            return;

        // 计算敌人指向目标的方向
        Vector3 dir = currentTarget.position - transform.position;

        // 直接设置朝向的写法，当前被注释
        // transform.forward = dir.normalized;

        // 根据方向创建目标旋转
        //
        // Quaternion.LookRotation(dir.normalized)
        // 表示让物体的 forward 朝向 dir 方向
        Quaternion targetRotation = Quaternion.LookRotation(dir.normalized);

        // 使用 Lerp 平滑旋转
        //
        // transform.rotation：
        // 当前旋转
        //
        // targetRotation：
        // 目标旋转
        //
        // enemyParameter.rotationSpeed * Time.deltaTime：
        // 插值速度
        //
        // 这样敌人不会瞬间转向玩家，而是平滑转身
        transform.rotation = Quaternion.Lerp(
            transform.rotation,
            targetRotation,
            enemyParameter.rotationSpeed * Time.deltaTime
        );
    }


    // 更新当前目标
    //
    // 作用：
    // 每帧从 EnemyView 获取当前视野目标，
    // 并根据是否有目标设置 Animator 的 LockOn 参数
    private void UpdateCurrentTarget()
    {
        // 从敌人视野系统中同步当前目标
        currentTarget = enemyView.CurrentTarget;

        // 如果有目标
        if (currentTarget)
        {
            // 设置 LockOn = 1
            // 动画状态机可以根据这个值切换到战斗待机、锁定移动、战斗状态
            animator.SetFloat(lockOnHash, 1f);
        }
        else
        {
            // 没有目标时设置 LockOn = 0
            // 动画状态机可以回到普通待机、巡逻状态
            animator.SetFloat(lockOnHash, 0f);
        }
    }


    /// <summary>
    /// 攻击到玩家时执行的逻辑
    /// </summary>
    /// <param name="playerCollider">被攻击检测命中的玩家 Collider</param>
    public void HitPlayer(Collider playerCollider)
    {
        // 获取玩家身上的 PlayerCombatController
        // 然后调用 PlayerOnHit，让玩家执行受击逻辑
        //
        // currentAbilityConfig.detectionConfigs[attackConfigCount]
        // 表示当前敌人招式中，第 attackConfigCount 段攻击的检测/伤害配置
        //
        // this.transform
        // 把敌人自身作为攻击者传给玩家
        playerCollider
            .GetComponent<PlayerCombatController>()
            .PlayerOnHit(
                currentAbilityConfig.detectionConfigs[attackConfigCount],
                this.transform
            );
    }


    #region 公共接口

    // 获取当前目标
    // 其他 AI 状态、技能、移动逻辑可以通过这个函数读取当前目标
    public Transform GetCurrentTarget()
    {
        // 如果没有目标，返回 null
        if (!currentTarget)
            return null;

        // 返回当前目标
        return currentTarget;
    }


    // 获取敌人与当前目标之间的距离
    public float GetCurrentTargetDistance()
    {
        // 如果没有目标，返回 -1
        // -1 表示无效距离
        if (!currentTarget)
            return -1f;

        // 返回敌人和目标之间的距离
        return Vector3.Distance(transform.position, currentTarget.position);
    }


    // 获取敌人指向当前目标的方向
    public Vector3 GetDirectionForTarget()
    {
        // 如果没有目标，返回零向量
        if (!currentTarget)
            return Vector3.zero;

        // 返回从敌人到目标的单位方向
        return (currentTarget.position - transform.position).normalized;
    }


    // 获取锁定点
    // 玩家锁定系统或相机系统可以用它作为锁定位置
    public Transform GetLockOnTransform() => lockOnTransform;


    // 设置当前攻击配置索引
    //
    // 一般由动画事件调用
    // 例如第一刀开始时 SetAttackConfigCount(0)
    // 第二刀开始时 SetAttackConfigCount(1)
    public void SetAttackConfigCount(int count) => attackConfigCount = count;


    // 设置当前技能配置
    //
    // 一般在敌人释放某个技能时调用
    // 用来告诉攻击检测系统当前使用哪个 AbilityConfig
    public void SetCurrentAbilityConfig(AbilityConfig abilityConfig) => currentAbilityConfig = abilityConfig;

    #endregion


    #region 技能

    /// <summary>
    /// 初始化所有技能
    /// </summary>
    private void InitAllAbilities()
    {
        // 如果技能列表为空，直接返回
        if (abilityList.Count == 0)
            return;

        // 遍历所有技能
        for (int i = 0; i < abilityList.Count; i++)
        {
            // 初始化每个技能
            //
            // 把敌人动画器、战斗控制器、移动控制器、敌人参数传进去
            // 这样技能对象内部就可以：
            // 1. 播放动画
            // 2. 控制攻击逻辑
            // 3. 控制敌人移动
            // 4. 读取敌人属性
            abilityList[i].Init(
                animator,
                this,
                enemyMovementController,
                enemyParameter
            );

            // 将技能设为可用
            // true 表示技能当前不在冷却中，可以被 AI 选择
            abilityList[i].SetAbilityAvailable(true);

            // 将技能加入可用技能列表中
            // 之后敌人 AI 可以从 availableAbilityList 中选择技能释放
            availableAbilityList.Add(abilityList[i]);
        }
    }


    /// <summary>
    /// 获得一个可用的，不在冷却中的技能
    /// </summary>
    /// <returns>返回第一个可用技能，如果没有则返回 null</returns>
    public CombatAbilityBase GetAnAvailableAbility()
    {
        // 遍历所有技能
        for (int i = 0; i < abilityList.Count; i++)
        {
            // 如果技能可用，就返回这个技能
            if (abilityList[i].GetAbilityAvailable())
                return abilityList[i];
        }

        // 没有可用技能，返回 null
        return null;
    }


    /// <summary>
    /// 随机返回一个可用技能
    /// </summary>
    /// <returns>返回随机技能，如果没有可用技能则返回 null</returns>
    public CombatAbilityBase GetRandomAvailableAbility()
    {
        // 如果可用技能列表为空，返回 null
        if (availableAbilityList.Count == 0)
            return null;

        // 在可用技能列表中随机选择一个技能
        return availableAbilityList[
            UnityEngine.Random.Range(0, availableAbilityList.Count)
        ];
    }


    /// <summary>
    /// 根据技能名，获得指定的可用技能
    /// </summary>
    /// <param name="abilityName">技能名</param>
    /// <returns>找到则返回技能，否则返回 null</returns>
    public CombatAbilityBase GetAbilityByName(string abilityName)
    {
        // 遍历全部技能
        for (int i = 0; i < abilityList.Count; i++)
        {
            // 如果技能名匹配，则返回该技能
            //
            // 注意：
            // 当前代码只判断名字是否匹配，
            // 没有判断技能是否可用。
            // 如果真的要求“冷却中返回 null”，这里还需要加 GetAbilityAvailable 判断。
            if (abilityList[i].GetAbilityName() == abilityName)
                return abilityList[i];
        }

        // 没找到则返回 null
        return null;
    }


    /// <summary>
    /// 根据技能 ID，获得指定的可用技能
    /// </summary>
    /// <param name="abilityID">技能 ID</param>
    /// <returns>找到则返回技能，否则返回 null</returns>
    public CombatAbilityBase GetAbilityByID(int abilityID)
    {
        // 遍历全部技能
        for (int i = 0; i < abilityList.Count; i++)
        {
            // 如果技能 ID 匹配，则返回该技能
            //
            // 注意：
            // 当前代码只判断 ID 是否匹配，
            // 没有判断技能是否可用。
            // 如果真的要求“冷却中返回 null”，这里还需要判断 GetAbilityAvailable。
            if (abilityList[i].GetAbilityID() == abilityID)
                return abilityList[i];
        }

        // 没找到则返回 null
        return null;
    }

    #endregion
}