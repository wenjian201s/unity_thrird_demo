using System; // 引入 System 命名空间，用于 Serializable、Enum.Parse 等
using System.Collections.Generic; // 引入泛型集合命名空间。当前代码中没有使用，可以删除
using UnityEngine; // 引入 Unity 核心命名空间，例如 MonoBehaviour、GameObject、Collider、SerializeField 等

// EnemySwapWeapon 敌人武器切换控制器
// 这个脚本通常挂载在敌人根物体上
//
// 主要作用：
// 1. 保存敌人拥有的所有武器配置
// 2. 记录当前正在使用的武器
// 3. 切换敌人手上和背上的武器显示状态
// 4. 同步 EnemyAttackDetection 中的 weaponType
// 5. 提供接口，让其他系统获取当前武器配置
public class EnemySwapWeapon : MonoBehaviour
{
    // 敌人拥有的所有武器配置
    //
    // 每个 WeaponConfig 中包含：
    // 1. 武器名称
    // 2. 武器类型枚举
    // 3. 手上的武器模型
    // 4. 背上的武器模型
    // 5. 武器碰撞体
    //
    // 可以在 Inspector 面板中配置多个武器
    // 例如 Katana、GreatSword、Spear 等
    [SerializeField] private WeaponConfig[] weapons;

    // 当前敌人正在使用的武器
    //
    // 例如：
    // 当前拿着武士刀，则 currentActiveWeapon 就是 Katana 的 WeaponConfig
    // 当前拿大剑，则 currentActiveWeapon 就是 GreatSword 的 WeaponConfig
    [SerializeField] private WeaponConfig currentActiveWeapon;

    // 敌人攻击检测组件
    //
    // 切换武器时，需要同步它里面的 weaponType
    // 因为 EnemyAttackDetection 会根据 weaponType 切换攻击检测点
    [SerializeField] private EnemyAttackDetection enemyAttackDetection;
    
    // Start 会在脚本启用后的第一帧之前执行
    // 用于初始化默认武器和组件引用
    void Start()
    {
        // 默认手上拿的是 weapons 数组中的第一个武器
        //
        // 例如：
        // weapons[0] 如果配置的是 Katana，
        // 那么敌人初始武器就是 Katana
        //
        // 注意：
        // 这里要求 weapons 数组至少有一个元素，
        // 否则 weapons[0] 会报错
        currentActiveWeapon = weapons[0];

        // 获取当前敌人身上的 EnemyAttackDetection 组件
        // 用于切换武器类型
        enemyAttackDetection = GetComponent<EnemyAttackDetection>();
    }
    
    // 敌人切换武器方法
    //
    // 参数 weaponName：
    // 要切换到的武器名称
    //
    // 例如：
    // EnemySwapWeapons("Katana")
    // EnemySwapWeapons("GreatSword")
    //
    // 注意：
    // weaponName 必须和 E_WeaponType 枚举名称一致，
    // 同时也必须和 WeaponConfig.weaponName 一致
    public void EnemySwapWeapons(string weaponName)
    {
        // 把字符串 weaponName 转换成 E_WeaponType 枚举
        //
        // 例如：
        // weaponName = "Katana"
        // Enum.Parse<E_WeaponType>("Katana") 会得到 E_WeaponType.Katana
        //
        // 然后把敌人攻击检测系统中的 weaponType 切换为对应武器类型
        //
        // 原理：
        // EnemyAttackDetection 内部会根据 weaponType 判断当前使用哪组攻击检测点
        // 这样不同武器可以使用不同的攻击判定范围
        enemyAttackDetection.weaponType = Enum.Parse<E_WeaponType>(weaponName);
            
        // 隐藏当前手上的武器模型
        //
        // 比如敌人当前手上拿着武士刀，
        // 准备切换成大剑，
        // 那么先把手上的武士刀隐藏
        currentActiveWeapon.weaponInHand.SetActive(false);

        // 显示当前武器背上的模型
        //
        // 也就是说：
        // 当前武器不再拿在手上，
        // 而是显示为背在背上
        currentActiveWeapon.weaponOnBack.SetActive(true);

        // 遍历所有武器配置
        // 查找名字等于 weaponName 的武器
        for (int i = 0; i < weapons.Length; i++)
        {
            // 如果找到目标武器
            if (weapons[i].weaponName == weaponName)
            {
                // 将当前激活武器切换为这个武器
                currentActiveWeapon = weapons[i];

                // 找到后跳出循环
                break;
            }
        }

        // 隐藏新武器背上的模型
        //
        // 例如切换到大剑后，
        // 大剑不应该还显示在背上，
        // 因为它接下来要显示在手上
        currentActiveWeapon.weaponOnBack.SetActive(false);

        // 显示新武器手上的模型
        //
        // 例如切换到大剑后，
        // 显示敌人手中的大剑模型
        currentActiveWeapon.weaponInHand.SetActive(true);
    }

    #region 公共接口

    // 获取当前正在使用的武器配置
    //
    // 其他脚本可以通过这个接口获取当前武器
    // 例如 EnemyAttackDetection 中开启或关闭当前武器 Collider：
    //
    // enemySwapWeapon.GetCurrentActiveWeapon().weaponCollider.enabled = true;
    public WeaponConfig GetCurrentActiveWeapon() => currentActiveWeapon;

    #endregion
}

// Serializable 表示这个结构体可以在 Unity Inspector 面板中显示和编辑
//
// WeaponConfig 是单个武器的完整配置数据
// 一个敌人可以有多个 WeaponConfig
[Serializable]
public struct WeaponConfig
{
    // 武器名称
    //
    // 这个名字用于字符串查找
    // 例如 "Katana"、"GreatSword"
    //
    // 注意：
    // 当前代码中 weaponName 需要和 E_WeaponType 枚举名称保持一致
    public string weaponName;

    // 武器类型枚举
    //
    // 例如：
    // E_WeaponType.Katana
    // E_WeaponType.GreatSword
    //
    // 当前 EnemySwapWeapons 中没有直接使用这个字段，
    // 而是通过 Enum.Parse 从 weaponName 转换枚举
    public E_WeaponType weaponType;

    // 该武器拿在手上的模型
    //
    // 当这个武器是当前武器时：
    // weaponInHand.SetActive(true)
    //
    // 当这个武器不是当前武器时：
    // weaponInHand.SetActive(false)
    public GameObject weaponInHand;

    // 该武器背在背上的模型
    //
    // 当这个武器不是当前手持武器时：
    // weaponOnBack.SetActive(true)
    //
    // 当这个武器被拿到手上时：
    // weaponOnBack.SetActive(false)
    public GameObject weaponOnBack;

    // 武器碰撞体
    //
    // 通常用于攻击检测
    // 攻击开始时开启：
    // weaponCollider.enabled = true
    //
    // 攻击结束时关闭：
    // weaponCollider.enabled = false
    public Collider weaponCollider;
}
