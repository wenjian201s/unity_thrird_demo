// 定义武器类型枚举
public enum E_WeaponType
{
    Empty,      // 空武器 / 无武器状态
    Katana,     // 武士刀
    GreatSword, // 大剑
    Bow         // 弓
}

// 定义攻击类型枚举
public enum E_AttackType
{
    Common,     // 普通攻击
    Skill,      // 技能攻击
    Ultimate,   // 大招 / 终极技能
}

// 定义攻击力度枚举
public enum E_AttackForce
{
    Easy, // 轻攻击 / 轻力度
    Mid,  // 中等攻击 / 中等力度
    Hard  // 重攻击 / 高力度
}

// 定义位移补偿方向枚举
public enum E_MoveOffsetDirection
{
    Forward, // 向前位移
    Up       // 向上位移
}