using System; // 引入基础系统命名空间，提供 Serializable 等基础功能
using System.Collections; // 引入非泛型集合和协程相关支持
using System.Collections.Generic; // 引入泛型集合，如 Dictionary
using UnityEngine; // 引入 Unity 核心功能
using UnityEngine.InputSystem; // 引入 Unity 新输入系统
using UnityEngine.InputSystem.Interactions; // 引入输入交互类型，例如 TapInteraction
using Random = UnityEngine.Random; // 给 UnityEngine.Random 起别名 Random，避免和 System.Random 冲突

[Serializable] // 让该结构体可以被 Unity 序列化，并显示在 Inspector 面板中
public struct ComboDictStruct // 定义一个结构体，用于保存“武器类型-连招表”的对应关系
{
    public E_WeaponType weaponType; // 当前结构体中保存的武器类型
    public ComboList comboList; // 当前武器类型对应的连招列表
}

public class PlayerCombatController : CombatControllerBase // 定义玩家战斗控制器类，继承自战斗基类
{
    #region 组件 // 组件引用区域
    
    private CharacterController controller; // 角色控制器组件引用，用于角色移动碰撞
    private ThirdPersonController thirdPersonController; // 第三人称控制器组件引用
    private AttackCheckGizmos attackCheck; // 攻击检测 Gizmos 组件引用
    
    private InputAction movementInputAction; // 玩家移动输入动作引用
    private InputAction attackAction; // 玩家攻击输入动作引用
    
    #endregion // 结束组件区域

    public float attack = 1f; // 玩家攻击力，默认值为 1
    private E_AttackType attackType = E_AttackType.Common; // 当前攻击类型，默认普通攻击
    public E_WeaponType weaponType = E_WeaponType.Empty; // 当前武器类型，默认空武器
    [SerializeField] private ComboDictStruct[] comboDictStructs; // 在 Inspector 中配置的“武器类型-连招表”数组
    private Dictionary<E_WeaponType, ComboList> comboListDict; // 运行时使用的字典，存储武器类型到连招表的映射
    [SerializeField] private bool canPlayHitAnim; // 是否允许播放受击动画
    
    [Header("无敌帧")] // 在 Inspector 中显示“无敌帧”标题
    [SerializeField] private int invincibleFrame; // 无敌帧持续的帧数
    private int countInvincibleFrame; // 当前剩余无敌帧计数
    private bool startToCountInvincibleFrame; // 是否开始计算无敌帧
    
    [Header("完美闪避")] // 在 Inspector 中显示“完美闪避”标题
    [SerializeField] private float perfectDodgeTime; // 完美闪避持续时间
    [SerializeField] private float canPerfectDodgeTime; // 可触发完美闪避的时间窗口
    [SerializeField][Range(0f, 1f)] private float perfectDodgeTimeScale; // 完美闪避时的时间缩放倍率，范围 0~1
    [SerializeField] public bool isPerfectDodging; // 当前是否处于完美闪避状态
    [SerializeField] private string perfectDodgeAudioClipPath; // 完美闪避音效资源路径
    
    private int rollHash; // 翻滚动画参数的哈希值，优化 Animator 参数访问

    void Awake() // Unity 生命周期：对象初始化时调用
    {
        comboListDict = new Dictionary<E_WeaponType, ComboList>(); // 初始化武器类型到连招表的字典
        //将对应的连招表添加到字典中
        foreach (ComboDictStruct comboDict in comboDictStructs) // 遍历 Inspector 中配置的连招映射数组
        {
            comboListDict.Add(comboDict.weaponType, comboDict.comboList); // 将每个武器类型和对应连招表加入字典
        }
    }
    
    void Start() // Unity 生命周期：游戏开始时调用
    {
        base.Start(); // 调用父类的 Start 方法，初始化基类逻辑
        controller = GetComponent<CharacterController>(); // 获取当前对象上的 CharacterController 组件
        thirdPersonController = GetComponent<ThirdPersonController>(); // 获取当前对象上的 ThirdPersonController 组件
        attackCheck = GetComponent<AttackCheckGizmos>(); // 获取当前对象上的 AttackCheckGizmos 组件
        
        movementInputAction = GetComponent<PlayerInput>().actions["PlayerMovement"]; // 从 PlayerInput 中获取“移动”输入动作
        attackAction = GetComponent<PlayerInput>().actions["Attack"]; // 从 PlayerInput 中获取“攻击”输入动作
        
        rollHash = Animator.StringToHash("Roll"); // 将动画参数名 "Roll" 转为哈希值，提高访问效率

        canPlayHitAnim = true; // 初始化为允许播放受击动画
        startToCountInvincibleFrame = false; // 初始化为不开始计算无敌帧
        countInvincibleFrame = invincibleFrame; // 初始化当前无敌帧计数为设定值
        isPerfectDodging = false; // 初始化为不处于完美闪避状态
    }
    
    void Update() // Unity 生命周期：每帧调用一次
    {
        base.Update(); // 调用父类的 Update 方法，执行基类每帧逻辑
    }

    void FixedUpdate() // Unity 生命周期：固定时间间隔调用，常用于物理或稳定计时
    {
        CountInvincibleFrame(); // 在固定更新中计算无敌帧倒计时
    }

    public void SwitchComboList(E_WeaponType _weaponType) // 根据武器类型切换当前使用的连招表
    {
        if (!comboListDict.ContainsKey(_weaponType)) // 如果字典中不存在该武器类型
            return; // 直接返回，不做任何处理
        currentComboList = comboListDict[_weaponType]; // 将当前连招表切换为该武器类型对应的连招表
    }

    // /// <summary>
    // /// 玩家受击逻辑
    // /// </summary>
    // public void PlayerOnHit(EnemyAttackDetectionConfig attackConfig, Transform attackerTransform) // 玩家被击中时调用的方法
    // {
    //     if(!canBeHit) // 如果当前不能被击中
    //         return; // 直接返回，避免重复受击
    //     canBeHit = false; // 标记当前暂时不能再被击中
    //     
    //     //停止攻击检测（防止攻击被打断时攻击检测一直开启）
    //     attackCheckSystem.EndAttacking(); // 结束当前攻击检测，防止攻击中断后检测残留
    //     
    //     //禁用玩家移动和攻击输入
    //     movementInputAction.Disable(); // 禁用移动输入
    //     attackAction.Disable(); // 禁用攻击输入
    //     
    //     int damage = attackConfig.damage + Random.Range(-10, 10); // 根据攻击配置计算最终伤害，并附加一个随机浮动值
    //     //TODO: 扣除生命值等逻辑
    //     Debug.Log("玩家受到了" + damage + "点伤害!"); // 在控制台打印玩家受到的伤害数值
    //
    //     //播放切换装备动画、大剑的攻击动画时不播放受击动画（大剑攻击有硬直）
    //     if (canPlayHitAnim && !animator.GetCurrentAnimatorStateInfo(0).IsTag("GSAttack") && // 如果允许播放受击动画，且当前不是大剑攻击状态
    //         !animator.GetCurrentAnimatorStateInfo(0).IsTag("Equip")) // 并且当前不是切换装备状态
    //     {
    //         Vector3 dir = (attackerTransform.position - this.transform.position).normalized; // 计算攻击者相对玩家的方向向量并归一化
    //     
    //         // 计算与前方和右侧的夹角
    //         float angleForward = Vector3.Angle(dir, transform.forward); // 计算攻击方向与玩家前方的夹角
    //         float angleRight = Vector3.Angle(dir, transform.right); // 计算攻击方向与玩家右侧的夹角
    //
    //         // 判断方位
    //         if (angleForward <= 45f) // 如果攻击来自玩家前方 90 度范围内
    //         {
    //             animator.Play("Hit_Front_" + weaponType.ToString()); // 播放对应武器的前方受击动画
    //         }
    //         else if (angleForward >= 135f) // 如果攻击来自玩家后方 90 度范围内
    //         {
    //             animator.Play("Hit_Back_" + weaponType.ToString()); // 播放对应武器的后方受击动画
    //         }
    //         else if (angleRight <= 45f) // 如果攻击来自玩家右侧 90 度范围内
    //         {
    //             animator.Play("Hit_Right_" + weaponType.ToString()); // 播放对应武器的右侧受击动画
    //         }
    //         else if (angleRight >= 135f) // 如果攻击来自玩家左侧 90 度范围内
    //         {
    //             animator.Play("Hit_Left_" + weaponType.ToString()); // 播放对应武器的左侧受击动画
    //         }
    //     }
    //     
    //     //生成受击特效
    //     string hitFXName = hitFXList[0].TryGetHitFXName(); // 获取第一个受击特效配置对应的特效名称
    //     FXManager.Instance.PlayOneHitFX(hitFXName, hitTransform.position, hitFXScale); // 在受击位置播放受击特效
    //     
    //     //无敌时间计时
    //     StartCoroutine(IE_HitCoolDown(hitCoolDown)); // 启动受击冷却协程，处理短暂无敌和输入恢复
    // }
    
    private IEnumerator IE_HitCoolDown(float coolDownTime) // 受击冷却协程，控制短时间内不可再次受击
    {
        while (coolDownTime > 0) // 当冷却时间还没结束时循环
        {
            yield return null; // 等待下一帧
            coolDownTime -= Time.deltaTime; // 每帧减去经过的时间
        }
        canBeHit = true; // 冷却结束后允许再次受击
        //启用玩家移动和攻击输入
        movementInputAction.Enable(); // 重新启用移动输入
        attackAction.Enable(); // 重新启用攻击输入
    }

    /// <summary>
    /// 计算无敌帧
    /// </summary>
    private void CountInvincibleFrame() // 计算无敌帧的方法
    {
        if (startToCountInvincibleFrame) // 如果已经开始计算无敌帧
        {
            if (countInvincibleFrame > 0) // 如果当前剩余无敌帧大于 0
            {
                countInvincibleFrame--; // 每次固定更新减少 1 帧
                if (countInvincibleFrame <= 0) // 如果无敌帧已经耗尽
                {
                    canBeHit = true; // 无敌结束，角色可以再次被攻击
                    startToCountInvincibleFrame = false; // 停止无敌帧计时
                    countInvincibleFrame = invincibleFrame; // 将无敌帧计数重置为初始值，便于下次使用
                }
            }   
        }
    }

    /// <summary>
    /// 完美闪避函数，在角色的完美闪避碰撞体与EnemyWeapon碰撞时调用
    /// </summary>
    public void PerfectDodge() // 处理完美闪避逻辑的方法
    {
        if (isPerfectDodging || canBeHit || !startToCountInvincibleFrame) // 如果已经在完美闪避中，或当前可被击中，或还没进入无敌计时
            return; // 则不触发完美闪避，直接返回

        isPerfectDodging = true; // 标记当前进入完美闪避状态

        Time.timeScale = perfectDodgeTimeScale; // 调整全局时间缩放，制造慢动作效果

        //播放完美闪避音效
        audioSource.PlayOneShot(Resources.Load<AudioClip>(perfectDodgeAudioClipPath), 0.5f); // 加载并播放完美闪避音效，音量为 0.5

        StartCoroutine(IE_CountPerfectDodge(perfectDodgeTime)); // 启动协程，在指定时间后结束完美闪避
    }
    
    IEnumerator IE_CountPerfectDodge(float duration) // 完美闪避计时协程
    {
        yield return new WaitForSecondsRealtime(duration); // 按真实时间等待指定时长，不受 timeScale 影响
        isPerfectDodging = false; // 完美闪避状态结束
        Debug.Log("完美闪避结束"); // 在控制台输出完美闪避结束信息
        Time.timeScale = 1f; // 恢复全局时间流速为正常值
    }

    #region 公共接口 // 对外公开的方法区域

    public float GetCanPerfectDodgeTime() => canPerfectDodgeTime; // 返回可完美闪避的判定时间窗口

    #endregion // 结束公共接口区域
    
    #region 动画事件 // 动画事件调用的方法区域

    public void StartInvincibleFrame() // 由动画事件调用，开启无敌帧
    {
        canBeHit = false; // 设置当前角色不可被击中
        startToCountInvincibleFrame = true; // 开始无敌帧计时
    }

    #endregion // 结束动画事件区域

    #region 玩家输入相关 // 玩家输入处理区域

    public void GetAttackInput(InputAction.CallbackContext ctx) // 接收玩家攻击输入
    {
        if (ctx.started && weaponType != E_WeaponType.Empty) // 如果输入刚开始触发，并且当前装备了武器
        {
            ExecuteCombo();  // 执行连招逻辑
        }
    }
    
    //获取玩家闪避输入
    public void GetSlideInput(InputAction.CallbackContext ctx) // 接收玩家闪避输入
    {
        if (ctx.interaction is TapInteraction && canExecuteCombo) // 如果当前输入是点击交互，并且当前允许执行动作
        {
            animator.SetTrigger(rollHash); // 触发翻滚动画参数，执行闪避动作
        }
    }
    
    #endregion // 结束玩家输入相关区域
    
} // 类定义结束