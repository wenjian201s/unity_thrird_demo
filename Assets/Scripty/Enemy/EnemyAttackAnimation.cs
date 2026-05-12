using System; // 引入 System 命名空间，用于 Serializable 特性
using System.Collections; // 引入 IEnumerator，用于协程
using System.Collections.Generic; // 引入 List 集合
using UnityEngine; // 引入 Unity 核心命名空间，例如 MonoBehaviour、Animator、Transform、AudioSource 等

/// <summary>
/// 存储敌人攻击时的动画事件函数
/// </summary>
/// 
/// EnemyAttackAnimation 的作用：
/// 1. 接收攻击动画事件
/// 2. 根据动画事件传入的 count 参数，判断当前是哪个技能、哪一段攻击
/// 3. 开启敌人的攻击检测
/// 4. 延迟关闭攻击检测
/// 5. 根据技能配置播放攻击音效
/// 6. 根据技能配置播放攻击特效
/// 7. 把当前技能配置同步给 EnemyCombatController
///
/// 这个脚本本质上是：
/// “动画事件驱动的攻击表现与攻击判定控制器”
public class EnemyAttackAnimation : MonoBehaviour
{
    // AICombat 引用
    // 当前代码中没有实际使用
    // 可能是预留给 AI 战斗状态机或敌人技能选择系统使用
    public AICombat aiCombat;

    // 敌人的 Animator 动画机
    // 用于判断当前动画状态的 Tag
    [SerializeField] private Animator animator;

    // 敌人战斗控制器
    // 用于同步当前技能配置和当前攻击段索引
    // 这样敌人命中玩家时，可以知道应该使用哪一段攻击的伤害配置
    [SerializeField] private EnemyCombatController enemyCombatController;

    // 敌人攻击检测组件
    // 用于开启和关闭攻击判定
    // 例如挥刀有效帧开始时 StartAttacking()
    // 挥刀有效帧结束时 EndAttacking()
    [SerializeField] private EnemyAttackDetection enemyAttackDetection;

    // 敌人自身 Transform
    // 用于计算特效生成位置和旋转
    [SerializeField] private Transform enemyTransform;

    // 音源组件
    // 用于播放攻击音效
    [SerializeField] private AudioSource audioSource;

    // 每个技能的配置信息
    //
    // 一个 AbilityConfig 代表一个技能的完整配置：
    // 1. 技能 ID
    // 2. 技能引用
    // 3. 攻击检测配置
    // 4. 音效配置
    // 5. 特效配置
    [SerializeField] private List<AbilityConfig> abilityConfigs;

    // 当前正在执行的技能配置
    //
    // 当动画事件触发时，会根据技能 ID 找到对应的 AbilityConfig，
    // 然后赋值给 currentAbilityConfig
    [SerializeField] private AbilityConfig currentAbilityConfig;
    
    // Start 会在脚本启用后第一帧之前执行
    // 用于获取当前物体上的组件引用
    void Start()
    {
        // 获取 Animator
        animator = GetComponent<Animator>();

        // 获取敌人战斗控制器
        enemyCombatController = GetComponent<EnemyCombatController>();

        // 获取敌人攻击检测组件
        enemyAttackDetection = GetComponent<EnemyAttackDetection>();

        // 获取自身 Transform
        enemyTransform = GetComponent<Transform>();

        // 获取 AudioSource 音源组件
        audioSource = GetComponent<AudioSource>();
    }

    /// <summary>
    /// 在动画播放时调用的事件函数
    /// </summary>
    /// <param name="count">
    /// count 表示当前技能的第几个攻击。
    ///
    /// 当前代码用 count 同时编码了两个信息：
    /// 1. 当前技能 ID
    /// 2. 当前技能的第几段攻击
    ///
    /// 例如：
    /// count = 21
    /// currentAbilityID = 21 / 10 = 2
    /// configIndex = 21 % 10 = 1
    ///
    /// 表示：
    /// 技能 ID 为 2，
    /// 使用该技能中的第 1 段攻击配置。
    /// </param>
    public void OnAttackAnimationEnter(int count)
    {
        // 如果技能配置列表为空，直接返回
        // 防止后面查找配置时报错
        if (abilityConfigs == null)
            return;

        // 判断当前动画状态是否是攻击类动画
        //
        // Animator 的 Tag 用于给动画状态分类。
        //
        // 这里允许三种 Tag：
        // Ability：普通技能
        // GSAbility：大剑技能 / 特殊技能
        // Attack：普通攻击
        //
        // 如果当前动画状态不是这些攻击相关状态，
        // 就不应该触发攻击检测、音效和特效
        if (!(animator.GetCurrentAnimatorStateInfo(0).IsTag("Ability") ||
              animator.GetCurrentAnimatorStateInfo(0).IsTag("GSAbility") ||
              animator.GetCurrentAnimatorStateInfo(0).IsTag("Attack")))
            return;
        
        // 从 count 中解析当前技能 ID
        //
        // 例如 count = 21
        // 21 / 10 = 2
        // 当前技能 ID 就是 2
        int currentAbilityID = count / 10;

        // 从 count 中解析当前技能的攻击段索引
        //
        // 例如 count = 21
        // 21 % 10 = 1
        // 表示当前使用 detectionConfigs[1]、clipConfigs[1]、fxConfigs[1]
        int configIndex = count % 10;
        
        // 根据技能 ID 找到对应的技能配置
        currentAbilityConfig = GetAbilityConfigByID(currentAbilityID);

        // 把当前技能配置同步给 EnemyCombatController
        //
        // 作用：
        // 当敌人攻击检测命中玩家时，
        // EnemyCombatController.HitPlayer() 可以知道当前使用哪个技能配置。
        enemyCombatController.SetCurrentAbilityConfig(currentAbilityConfig);

        // 把当前攻击段索引同步给 EnemyCombatController
        //
        // 例如三连击中：
        // 第 0 段攻击用 detectionConfigs[0]
        // 第 1 段攻击用 detectionConfigs[1]
        // 第 2 段攻击用 detectionConfigs[2]
        enemyCombatController.SetAttackConfigCount(configIndex);
        
        // 执行攻击检测事件
        // 根据当前攻击段配置开启攻击判定，并在指定时间后关闭
        AttackDetectionEvent(configIndex);

        // 执行音效事件
        // 根据当前攻击段配置延迟播放音效
        PlayClipEvent(configIndex);

        // 执行特效事件
        // 根据当前攻击段配置延迟播放攻击特效
        PlayFXEvent(configIndex);
    }

    // 攻击检测事件
    //
    // count 是当前技能攻击段索引
    // 例如 count = 0 表示第一段攻击
    // count = 1 表示第二段攻击
    private void AttackDetectionEvent(int count)
    {
        // 如果当前攻击段索引超过检测配置数组长度，直接返回
        // 防止数组越界
        if (count >= currentAbilityConfig.detectionConfigs.Length)
            return;

        // 开启攻击检测
        //
        // EnemyAttackDetection.StartAttacking() 会：
        // 1. 设置 isAttacking = true
        // 2. 开启当前武器 Collider
        // 3. 让攻击检测系统开始做射线扫掠检测
        enemyAttackDetection.StartAttacking();

        // 开启协程倒计时
        //
        // detectionTime 表示这一次攻击判定持续多久
        // 倒计时结束后自动调用 EndAttacking()
        StartCoroutine(
            IE_AttackDetectionCount(
                currentAbilityConfig.detectionConfigs[count].detectionTime
            )
        );
    }
    
    /// <summary>
    /// 倒计时结束后，结束攻击判定
    /// </summary>
    /// <param name="timer">攻击检测持续时间</param>
    /// <returns></returns>
    IEnumerator IE_AttackDetectionCount(float timer)
    {
        // 当 timer 大于 0 时，每帧等待并减少时间
        while (timer > 0)
        {
            // 等待下一帧
            yield return null;

            // 使用游戏时间减少计时
            // 这个计时会受到 Time.timeScale 影响
            timer -= Time.deltaTime;
        }

        // 计时结束后关闭攻击检测
        //
        // EnemyAttackDetection.EndAttacking() 会：
        // 1. 设置 isAttacking = false
        // 2. 关闭当前武器 Collider
        // 3. 停止攻击判定
        enemyAttackDetection.EndAttacking();
    }

    // 播放特效事件
    //
    // 根据当前攻击段索引读取 fxConfigs 中的配置
    private void PlayFXEvent(int count)
    {
        // 如果索引超过特效配置数组长度，直接返回
        if (count >= currentAbilityConfig.fxConfigs.Length)
            return;

        // 开启协程
        // 按照 fxConfig.startTime 延迟播放特效
        StartCoroutine(IE_FXCount(currentAbilityConfig.fxConfigs[count]));
    }
    
    // 延迟播放特效协程
    IEnumerator IE_FXCount(EnemyFXConfig fxConfig)
    {
        // 特效开始时间
        // 表示动画事件触发后，再等多少秒播放特效
        float timer = fxConfig.startTime;

        // 等待指定时间
        while (timer > 0)
        {
            yield return null;
            timer -= Time.deltaTime;
        }

        // 如果特效名称不为空，则播放特效
        //
        // FXName 通常用于对象池或特效管理器查找特效
        if (fxConfig.FXName != null)
        {
            // 根据敌人的朝向计算特效生成位置
            //
            // fxConfig.position 是局部偏移。
            //
            // enemyTransform.forward * fxConfig.position.z
            // 表示在敌人前方偏移多少
            //
            // enemyTransform.up * fxConfig.position.y
            // 表示在敌人上方偏移多少
            //
            // enemyTransform.right * fxConfig.position.x
            // 表示在敌人右方偏移多少
            //
            // 三个方向相加后，就得到一个相对于敌人自身坐标系的偏移位置
            Vector3 fxPosition =
                enemyTransform.forward * fxConfig.position.z +
                enemyTransform.up * fxConfig.position.y +
                enemyTransform.right * fxConfig.position.x;

            // 通过 FXManager 播放攻击特效
            //
            // 参数解释：
            // fxConfig：特效配置
            // fxPosition + transform.position：世界坐标中的特效生成位置
            // fxConfig.rotation + transform.eulerAngles：特效旋转，叠加敌人当前旋转
            // fxConfig.scale：特效缩放
            FXManager.Instance.PlayOneFX(
                fxConfig,
                fxPosition + transform.position,
                fxConfig.rotation + transform.eulerAngles,
                fxConfig.scale
            );
        }
    }

    // 播放音效事件
    //
    // 根据当前攻击段索引读取 clipConfigs 中的音效配置
    private void PlayClipEvent(int count)
    {
        // 如果索引超过音效配置数组长度，直接返回
        if (count >= currentAbilityConfig.clipConfigs.Length)
            return;

        // 开启协程
        // 按照 clipConfig.startTime 延迟播放音效
        StartCoroutine(IE_ClipCount(currentAbilityConfig.clipConfigs[count]));
    }
    
    // 延迟播放音效协程
    IEnumerator IE_ClipCount(EnemyClipConfig clipConfig)
    {
        // 音效开始时间
        // 表示动画事件触发后，再等多少秒播放音效
        float timer = clipConfig.startTime;

        // 等待指定时间
        while (timer > 0)
        {
            yield return null;
            timer -= Time.deltaTime;
        }

        // 如果音效资源存在，则播放音效
        if (clipConfig.audioClip)
        {
            // 使用 AudioSource 播放一次音效
            //
            // audioClip：要播放的声音
            // volume：播放音量
            audioSource.PlayOneShot(clipConfig.audioClip, clipConfig.volume);
        }
    }
    
    // 根据技能 ID 查找技能配置
    private AbilityConfig GetAbilityConfigByID(int abilityID)
    {
        // 遍历所有技能配置
        for (int i = 0; i < abilityConfigs.Count; i++)
        {
            // 如果配置中的 abilityID 与传入 ID 相同
            if (abilityConfigs[i].abilityID == abilityID)
                return abilityConfigs[i];
        }

        // 没找到则返回 null
        return null;
    }
    
    #region 公共接口

    // 改变当前技能配置
    //
    // 这个函数可以由外部 AI 或技能系统调用
    // 作用是根据当前 CombatAbilityBase，更新 currentAbilityConfig
    public void ChangeCurrentAbilityConfig(CombatAbilityBase currentAbility)
    {
        // 如果当前技能为空，直接返回
        if (!currentAbility)
            return;

        // 如果没有配置任何技能数据，直接返回
        if (abilityConfigs.Count == 0)
            return;

        // 如果当前配置已经是这个技能对应的配置，就不用重复更新
        if (currentAbilityConfig != null &&
            currentAbilityConfig.abilityID == currentAbility.GetAbilityID())
            return;

        Debug.Log("更新了技能配置信息");

        // 遍历所有技能配置
        for (int i = 0; i < abilityConfigs.Count; i++)
        {
            // 如果找到和当前技能 ID 相同的配置
            if (abilityConfigs[i].abilityID == currentAbility.GetAbilityID())
            {
                // 更新当前技能配置
                currentAbilityConfig = abilityConfigs[i];
                return;
            }
        }
    }

    #endregion
}

/// <summary>
/// 存储一个技能中多段攻击的配置信息
/// </summary>
///
/// 一个 AbilityConfig 对应一个完整技能。
/// 一个技能里可以有多段攻击。
///
/// 例如一个三连击技能：
/// detectionConfigs[0] 第一刀攻击判定
/// detectionConfigs[1] 第二刀攻击判定
/// detectionConfigs[2] 第三刀攻击判定
///
/// clipConfigs 和 fxConfigs 也是类似逻辑。
[Serializable]
public class AbilityConfig
{
    [Header("技能信息")]

    // 技能 ID
    //
    // 用于和 CombatAbilityBase.GetAbilityID() 对应
    // 也用于动画事件中的 count / 10 解析
    [SerializeField] public int abilityID;

    // 技能资源引用
    //
    // 指向具体的 CombatAbilityBase 技能
    [SerializeField] public CombatAbilityBase ability;

    [Header("攻击检测")]

    // 攻击检测配置数组
    //
    // 每一项代表该技能的一段攻击判定
    [SerializeField] public EnemyAttackDetectionConfig[] detectionConfigs;

    [Header("音效数据")]

    // 音效配置数组
    //
    // 每一项代表该技能某一段攻击对应的音效播放配置
    [SerializeField] public EnemyClipConfig[] clipConfigs;

    [Header("特效数据")]

    // 特效配置数组
    //
    // 每一项代表该技能某一段攻击对应的特效播放配置
    [SerializeField] public EnemyFXConfig[] fxConfigs;
}

[Serializable]
public class EnemyAttackDetectionConfig
{
    // 攻击检测持续时间
    //
    // 动画事件触发后，攻击判定会持续 detectionTime 秒
    // 时间结束后自动关闭攻击判定
    public float detectionTime;

    // 攻击伤害
    //
    // 当前代码中这个 damage 字段没有直接被 EnemyAttackAnimation 使用
    // 但是后续可以被 EnemyCombatController 或 PlayerCombatController 使用
    public int damage;
}

[Serializable]
public class EnemyFXConfig
{
    // 特效延迟开始时间
    //
    // 动画事件触发后，等待 startTime 秒再播放特效
    public float startTime;

    // 特效预制体
    //
    // 当前代码中没有直接 Instantiate 这个预制体，
    // 而是交给 FXManager 处理
    public GameObject FXPrefab;

    // 特效名称
    //
    // 一般用于对象池或 FXManager 根据名字查找特效
    public string FXName;

    // 特效相对于敌人的局部位置偏移
    //
    // x：相对敌人右方向偏移
    // y：相对敌人上方向偏移
    // z：相对敌人前方向偏移
    public Vector3 position;

    // 特效旋转偏移
    //
    // 会与敌人的当前欧拉角相加
    public Vector3 rotation;

    // 特效缩放
    public Vector3 scale;
}

[Serializable]
public class EnemyClipConfig
{
    // 音效延迟开始时间
    //
    // 动画事件触发后，等待 startTime 秒再播放音效
    public float startTime;

    // 要播放的音频片段
    public AudioClip audioClip;

    // 播放音量
    public float volume;

    // 音效持续时间
    //
    // 当前代码中没有使用
    // 未来可以用于控制循环音效、延迟停止音效等
    public float duration;
}