using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UIElements;



//整个招式的总配置表
//基础数据
//    攻击交互
//特效
//    音效
//打击反馈
//    自身位移
//目标位移


// 让这个 ScriptableObject 可以在 Unity 菜单中创建
// fileName = "ComboConfig" 表示默认资源文件名
// menuName = "ScriptableObjects/Combat/ComboConfig" 表示创建菜单路径
[CreateAssetMenu(fileName = "ComboConfig", menuName = "ScriptableObjects/Combat/ComboConfig")]
public class ComboConfig : ScriptableObject  //定义一个连招配置资源类
{
    [Header("基础数据")] 
    public string comboName;              // 连招/招式名称，通常对应动画名或招式名
    public float coolDownTime;            // 该招式的冷却时间/后摇时间
    [Header("交互数据")]
    // 攻击交互配置数组
    // 用来配置攻击在什么时间生效、伤害多少、武器类型、攻击力度等
    public ComboInteractionConfig[] comboInteractionConfigs;
    [Header("特效数据")]
    // 特效配置数组
    // 用来配置攻击过程中在什么时间播放什么特效、位置偏移、旋转、缩放等
    public FXConfig[] fxConfigs;
    [Header("音效数据")]
    // 音效配置数组
    // 用来配置攻击过程中在什么时间播放什么音效、音量多大等
    public ClipConfig[] clipConfigs;
    [Header("攻击反馈数据")] 
    // 攻击反馈配置数组
    // 用来配置命中后的顿帧、受击音效、屏幕震动等反馈内容
    public AttackFeedbackConfig[] attackFeedbackConfigs;
    [Header("自身位移补偿数据")]
    // 自身位移补偿配置数组
    // 用来控制角色出招时自身的位移效果，比如前冲、后撤等
    public SelfMoveOffsetConfig[] selfMoveOffsetConfigsConfigs;
    [Header("目标位移补偿数据")]
    // 目标位移补偿配置数组
    // 用来控制被击中的目标发生位移，比如击退、浮空位移等
    public TargetMoveOffsetConfig[] targetMoveOffsetConfigsConfigs;
}

[System.Serializable]
public class ComboInteractionConfig //连招武器配置
{
    public float startTime;               // 攻击判定开始时间（通常是动画归一化时间）
    public float endTime;                 // 攻击判定结束时间

    public string hitName;                // 命中地面目标时的受击动画名
    public string hitAirName;             // 命中空中目标时的受击动画名

    // 武器类型
    public E_WeaponType weaponType;       // 当前攻击使用的武器类型（枚举，外部定义）

    // 攻击力度
    public E_AttackForce attackForce;     // 当前攻击力度（轻击/重击等，枚举，外部定义）

    public int healthDamage;              // 对生命值造成的伤害
    public int enduranceDamage;           // 对韧性值/耐力值造成的伤害
}

[System.Serializable]
// 这个类一般用于攻击判定区域的配置，比如盒子检测、球形检测等
// 不过从你前面的 CombatControllerBase 看，这个类当前似乎没有直接被使用到
public class AttackDetectionConfig //攻击判断检测
{
    public float startTime;               // 攻击检测开始时间
    public Vector3 position;              // 攻击检测区域的位置偏移
    public Vector3 rotation;              // 攻击检测区域的旋转
    public Vector3 scale;                 // 攻击检测区域的大小/范围
}


[System.Serializable]
public class FXConfig
{
    public float startTime;               // 特效触发时间
    public GameObject FXPrefab;           // 特效预制体
    public string FXName;                 // 特效名称（有些项目会通过对象池/管理器按名字取特效）
    public Vector3 position;              // 特效相对角色的位置偏移
    public Vector3 rotation;              // 特效旋转偏移
    public Vector3 scale;                 // 特效缩放
}
// 这个类的作用：配置攻击过程中播放的视觉特效
// 比如挥刀光效、斩击波、地面冲击火花等

[System.Serializable]
public class ClipConfig
{
    public float startTime;               // 音效触发时间
    public AudioClip audioClip;           // 需要播放的音频片段
    public float volume;                  // 音量大小
    public float duration;                // 音效持续时间（当前代码里可能只是预留字段）
}
// 这个类的作用：配置招式中的音效播放参数
// 比如挥刀声、击中声、技能释放声等

[System.Serializable]
public class AttackFeedbackConfig
{
    public Vector3 velocity;              // 屏幕震动速度 / 震动参数（具体用法取决于外部系统）
    public AudioClip audioClip;           // 受击反馈音效
    public float audioStartTime;          // 反馈音效播放时间
    public float animatorSpeed;           // 顿帧时的动画速度，比如设为 0 或很小值营造停顿感
    public float stopFrameTime;           // 顿帧持续时间
}
// 这个类的作用：定义攻击命中后的“打击感反馈”
// 例如：顿帧、命中音效、震屏等

[System.Serializable]
public class SelfMoveOffsetConfig
{
    public float startTime;                       // 位移补偿开始时间
    public AnimationCurve animationCurve;         // 位移变化曲线，用来控制位移随时间如何变化
    public E_MoveOffsetDirection moveOffsetDirection; // 位移方向（前、后、左、右等，枚举，外部定义）
    public float scale;                           // 位移强度/距离系数
    public float duration;                        // 位移持续时间
}
// 这个类的作用：控制攻击者自己在攻击期间的位移
// 比如角色挥刀时向前滑一步，或者突刺时快速前冲

[System.Serializable]
public class TargetMoveOffsetConfig
{
    public float startTime;                       // 目标位移补偿开始时间
    public AnimationCurve animationCurve;         // 目标位移曲线
    public E_MoveOffsetDirection moveOffsetDirection; // 目标位移方向
    public float scale;                           // 位移强度/击退距离
    public float duration;                        // 位移持续时间
}
// 这个类的作用：控制被打中的目标发生位移
// 比如敌人被击退、被打飞、被拉扯等
