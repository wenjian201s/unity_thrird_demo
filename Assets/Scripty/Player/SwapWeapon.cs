using UnityEngine; // 引入 Unity 核心命名空间，提供 MonoBehaviour、GameObject、Transform、Animator 等常用类型
using UnityEngine.Animations.Rigging; // 引入 Unity Animation Rigging 命名空间，用于使用 TwoBoneIKConstraint 等 IK 约束组件
using UnityEngine.InputSystem; // 引入 Unity 新输入系统，用于接收玩家输入
using UnityEngine.InputSystem.Interactions; // 引入输入交互类型，例如 TapInteraction 点击交互

public class SwapWeapon : MonoBehaviour // 定义武器切换类，继承 MonoBehaviour，可以挂载到 Unity 游戏对象上
{
    #region 组件 // 组件引用区域
    
    private Animator animator; // 角色 Animator 组件，用于控制动画状态机参数和播放动画
    private ThirdPersonController thirdPersonController; // 第三人称控制器组件，用于同步角色是否处于装备武器状态
    public Transform effectTransform; // 特效生成位置，用于后续在指定位置播放切换武器或攻击特效
    public TwoBoneIKConstraint[] rightHandIKConstraints; // 右手 IK 约束数组，不同武器使用不同的右手 IK
    private TwoBoneIKConstraint currentRightHandIKConstraint; // 当前正在生效的右手 IK 约束
    public TwoBoneIKConstraint[] leftHandIKConstraints; // 左手 IK 约束数组，不同武器使用不同的左手 IK
    private TwoBoneIKConstraint currentLeftHandIKConstraint; // 当前正在生效的左手 IK 约束
    private AttackCheckGizmos attackCheck; // 攻击检测组件，用于同步当前武器类型，影响攻击检测范围或逻辑
    private PlayerCombatController playerCombatController; // 玩家战斗控制器组件，用于同步武器类型和切换连招表
    
    #endregion // 组件引用区域结束
    
    private E_AttackType attackType = E_AttackType.Common; // 当前攻击类型，默认是普通攻击；当前代码中暂时没有实际使用
    private E_WeaponType weaponType = E_WeaponType.Empty; // 当前武器类型，初始为空，表示没有装备武器
    public E_WeaponType WeaponType => weaponType; // 对外暴露只读属性，让其他脚本可以读取当前武器类型
    
    public GameObject[] weaponOnBack; // 背上的武器模型数组，用于显示收起状态的武器
    public GameObject[] weaponInHand; // 手上的武器模型数组，用于显示装备状态的武器

    private int equipHash; // Animator 参数 WeaponType 的哈希值，用于提高设置动画参数的效率
    
    void Start() // Unity 生命周期函数，在脚本启用后的第一帧 Update 前调用
    {
        thirdPersonController = GetComponent<ThirdPersonController>(); // 获取当前物体上的 ThirdPersonController 组件
        currentRightHandIKConstraint = rightHandIKConstraints[0]; // 初始化当前右手 IK 约束为数组第 0 个元素
        currentLeftHandIKConstraint = leftHandIKConstraints[0]; // 初始化当前左手 IK 约束为数组第 0 个元素
        attackCheck = GetComponent<AttackCheckGizmos>(); // 获取当前物体上的 AttackCheckGizmos 攻击检测组件
        playerCombatController = GetComponent<PlayerCombatController>(); // 获取当前物体上的 PlayerCombatController 玩家战斗控制器组件
        animator = GetComponent<Animator>(); // 获取当前物体上的 Animator 动画组件
        
            equipHash = Animator.StringToHash("WeaponType"); // 将 Animator 参数名 WeaponType 转换成哈希值，避免频繁使用字符串
    }
    
    void Update()
    {
        SetAnimator();
        RepairWeaponVisualState();
    }

    private void SetAnimator() // 设置 Animator 参数和 IK 权重的方法
    {
        //装备状态
        animator.SetInteger(equipHash, (int)weaponType); // 将当前武器类型转换为 int，并写入 Animator 的 WeaponType 参数
        //控制掏出武器和收起武器时的右手IK权重
        currentRightHandIKConstraint.weight = animator.GetFloat("Right Hand Weight"); // 从 Animator 中读取右手权重曲线值，并赋给当前右手 IK
        currentLeftHandIKConstraint.weight = animator.GetFloat("Left Hand Weight"); // 从 Animator 中读取左手权重曲线值，并赋给当前左手 IK
    }

    private bool IsWeaponIndexValid(int index)
    {
        return index > 0 &&
               weaponOnBack != null &&
               weaponInHand != null &&
               index < weaponOnBack.Length &&
               index < weaponInHand.Length &&
               weaponOnBack[index] != null &&
               weaponInHand[index] != null;
    }

    private void SetWeaponModelState(int index, bool inHand)
    {
        if (!IsWeaponIndexValid(index))
            return;

        weaponOnBack[index].SetActive(!inHand);
        weaponInHand[index].SetActive(inHand);
    }

    public bool IsWeaponInHand(E_WeaponType requestedWeaponType)
    {
        int index = (int)requestedWeaponType;

        if (requestedWeaponType == E_WeaponType.Empty || !IsWeaponIndexValid(index))
            return false;

        return weaponInHand[index].activeInHierarchy;
    }

    public bool HasEquippedWeaponInHand()
    {
        return weaponType != E_WeaponType.Empty && IsWeaponInHand(weaponType);
    }

    private bool IsEquipAnimationActive()
    {
        AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);

        if (currentState.IsTag("Equip") || currentState.IsTag("EquipMotion"))
            return true;

        if (!animator.IsInTransition(0))
            return false;

        AnimatorStateInfo nextState = animator.GetNextAnimatorStateInfo(0);
        return nextState.IsTag("Equip") || nextState.IsTag("EquipMotion");
    }

    private void RepairWeaponVisualState()
    {
        if (weaponType == E_WeaponType.Empty ||
            thirdPersonController == null ||
            thirdPersonController.armState != ThirdPersonController.ArmState.Equip ||
            IsEquipAnimationActive() ||
            HasEquippedWeaponInHand())
            return;

        SetWeaponModelState((int)weaponType, true);
    }

    
    #region 动画片段调用函数 // 动画事件调用函数区域
    
    /// <summary>
    /// 切换背部武器和手部武器的显示
    /// </summary>
    /// <param name="weaponType">表示武器的位置是在背上0还是手上1\2\3</param>
    public void PutGrabWeapon(int weaponType)
    {
        if (!IsWeaponIndexValid(weaponType))
            return;

        bool shouldBeInHand = this.weaponType != E_WeaponType.Empty && (int)this.weaponType == weaponType;
        SetWeaponModelState(weaponType, shouldBeInHand);
    }
    
    #endregion // 动画事件调用函数区域结束
    
    #region 玩家输入相关 // 玩家输入处理区域
    
    //判断是否接收玩家攻击输入
    private bool IsInputValid() // 判断当前是否允许处理输入
    {
        if (thirdPersonController.armState != ThirdPersonController.ArmState.Equip) // 如果第三人称控制器中的手臂状态不是装备状态
        {
            return false; // 返回 false，表示当前输入无效
        }
        return true; // 返回 true，表示当前可以接收输入
    }
    
    //获取玩家武器装备输入
    public void GetKatanaInput(InputAction.CallbackContext ctx) // 接收武士刀输入的方法，通常绑定到输入系统的某个按键
    {
        if (ctx.started) // 判断输入是否刚刚开始触发，避免 performed 或 canceled 阶段重复执行
        {
            //若当前手上没有武器
            if (weaponType == E_WeaponType.Empty) // 如果当前没有装备任何武器
            {
                weaponType = E_WeaponType.Katana; // 将当前武器类型设置为武士刀
                thirdPersonController.isEquip = true; // 通知第三人称控制器：当前角色处于装备武器状态
                //将当前有效的IK约束设置为Katana的IK约束
                currentRightHandIKConstraint = rightHandIKConstraints[(int)E_WeaponType.Katana]; // 将当前右手 IK 切换为武士刀对应的右手 IK
                currentLeftHandIKConstraint = leftHandIKConstraints[(int)E_WeaponType.Katana]; // 将当前左手 IK 切换为武士刀对应的左手 IK
            }
            //若手上有武器，则收回该武器
            else // 如果当前已经装备了武器
            {
                weaponType = E_WeaponType.Empty; // 将当前武器类型设为空，表示收起武器
                thirdPersonController.isEquip = false; // 通知第三人称控制器：当前角色不再处于装备武器状态
            }
            attackCheck.weaponType = weaponType; // 将当前武器类型同步给攻击检测组件
            
            playerCombatController.weaponType = weaponType; // 将当前武器类型同步给玩家战斗控制器
            //切换连招表
            playerCombatController.SwitchComboList(weaponType); // 根据当前武器类型切换玩家战斗控制器中的连招表
        }
    }

    public void GetGreatSwordInput(InputAction.CallbackContext ctx) // 接收大剑输入的方法，通常绑定到输入系统的某个按键
    {
        if (ctx.started) // 判断输入是否刚刚开始触发
        {
            if (weaponType == E_WeaponType.Empty) // 如果当前没有装备任何武器
            {
                
                weaponType = E_WeaponType.GreatSword; // 将当前武器类型设置为大剑
                thirdPersonController.isEquip = true; // 通知第三人称控制器：当前角色处于装备武器状态
                //将当前有效的IK约束设置为GreatSword的IK约束
                currentRightHandIKConstraint = rightHandIKConstraints[(int)E_WeaponType.GreatSword]; // 将当前右手 IK 切换为大剑对应的右手 IK
                currentLeftHandIKConstraint = leftHandIKConstraints[(int)E_WeaponType.GreatSword]; // 将当前左手 IK 切换为大剑对应的左手 IK
            }
            else // 如果当前已经装备了武器
            {
                weaponType = E_WeaponType.Empty; // 将当前武器类型设为空，表示收起武器
                thirdPersonController.isEquip = false; // 通知第三人称控制器：当前角色不再处于装备武器状态
            }
            attackCheck.weaponType = weaponType; // 将当前武器类型同步给攻击检测组件
            
            playerCombatController.weaponType = weaponType; // 将当前武器类型同步给玩家战斗控制器
            //切换连招表
            playerCombatController.SwitchComboList(weaponType); // 根据当前武器类型切换玩家战斗控制器中的连招表
        }
    }

    public void GetBowInput(InputAction.CallbackContext ctx) // 接收弓箭输入的方法，通常绑定到输入系统的某个按键
    {
        if (ctx.started) // 判断输入是否刚刚开始触发
        {
            if (weaponType == E_WeaponType.Empty) // 如果当前没有装备任何武器
            {
                weaponType = E_WeaponType.Bow; // 将当前武器类型设置为弓
                thirdPersonController.isEquip = true; // 通知第三人称控制器：当前角色处于装备武器状态
                //将当前有效的IK约束设置为Bow的IK约束
                currentRightHandIKConstraint = rightHandIKConstraints[(int)E_WeaponType.Bow]; // 将当前右手 IK 切换为弓对应的右手 IK
                currentLeftHandIKConstraint = leftHandIKConstraints[(int)E_WeaponType.Bow]; // 将当前左手 IK 切换为弓对应的左手 IK
            }
            else // 如果当前已经装备了武器
            {
                weaponType = E_WeaponType.Empty; // 将当前武器类型设为空，表示收起武器
                thirdPersonController.isEquip = false; // 通知第三人称控制器：当前角色不再处于装备武器状态
            }
            attackCheck.weaponType = weaponType; // 将当前武器类型同步给攻击检测组件
            
            playerCombatController.weaponType = weaponType; // 将当前武器类型同步给玩家战斗控制器
            //切换连招表
            playerCombatController.SwitchComboList(weaponType); // 根据当前武器类型切换玩家战斗控制器中的连招表
        }
    }
    
    //获取玩家闪避输入
     public void GetSlideInput(InputAction.CallbackContext ctx) // 已注释掉的闪避输入方法
     {
         if (ctx.interaction is TapInteraction && IsInputValid()) // 如果当前输入是点击交互，并且当前输入状态有效
         {
             animator.SetTrigger("Roll"); // 触发 Animator 中的 Roll 参数，播放翻滚动画
         }
     }
    
    //TEST: 暂停时间（调试用，发布时删除）
    public void StopTime(InputAction.CallbackContext ctx) // 调试用输入方法，用于暂停或恢复游戏时间
    {
        if (ctx.started) // 判断输入是否刚刚开始触发
        {
            if(Time.timeScale == 0f) // 如果当前游戏时间已经暂停
                Time.timeScale = 1f; // 将时间缩放恢复为 1，游戏继续运行
            else if(Time.timeScale == 1f) // 如果当前游戏时间正常运行
                Time.timeScale = 0f; // 将时间缩放设为 0，暂停游戏
        }
    }
    
    #endregion // 玩家输入处理区域结束
    
} // SwapWeapon 类结束
