using System.Collections; 
using System.Collections.Generic; 
using UnityEngine; 

[CreateAssetMenu(fileName = "ComboList", menuName = "ScriptableObjects/Combat/ComboList")] // 允许在 Project 窗口通过右键菜单创建 ComboList 配置资源。
public class ComboList : ScriptableObject // 定义连招配置表，继承 ScriptableObject，方便把连招数据做成 Unity 资源。
{ 
    [SerializeField] private ComboConfig[] comboList; // 在 Inspector 中序列化显示连招数组，每个元素代表一段攻击/连招配置。
    
    public int TryGetComboListCount() => comboList.Length; // 返回当前连招数组的长度，用于判断连招总段数。

    // 获取指定连招段的动画/连招名称。
    public string TryGetComboName(int comboIndex) // 根据连招索引读取 comboName。
    { 
        return comboIndex >= comboList.Length ? null : comboList[comboIndex].comboName; // 索引越界时返回 null，否则返回对应连招名。
    } 

    // 获取指定连招段的攻击冷却时间。
    public float TryGetCoolDownTime(int comboIndex) // 根据连招索引读取 coolDownTime。
    { 
        return comboIndex >= comboList.Length ? 0 : comboList[comboIndex].coolDownTime; // 索引越界时返回 0，否则返回对应冷却时间。
    } 

    // 获取指定连招段中的攻击交互/判定配置。
    public ComboInteractionConfig TryGetComboInteractionConfig(int comboIndex, int eventIndex) // 根据连招索引和事件索引读取攻击判定配置。
    { 
        if(comboIndex >= comboList.Length) // 如果连招索引超出连招数组长度。
            return null; // 没有对应连招配置，返回 null。
        if(eventIndex >= comboList[comboIndex].comboInteractionConfigs.Length) // 如果事件索引超出当前连招的攻击判定配置数组长度。
            return null; // 没有对应攻击判定配置，返回 null。
        return comboList[comboIndex].comboInteractionConfigs[eventIndex]; // 返回当前连招中指定序号的攻击交互/判定配置。
    } 
    
    // 获取指定连招段中的特效配置。
    public FXConfig TryGetFXConfig(int comboIndex, int eventIndex) // 根据连招索引和事件索引读取特效配置。
    { 
        if(comboIndex >= comboList.Length) // 如果连招索引超出连招数组长度。
            return null; // 没有对应连招配置，返回 null。
        if(eventIndex >= comboList[comboIndex].fxConfigs.Length) // 如果事件索引超出当前连招的特效配置数组长度。
            return null; // 没有对应特效配置，返回 null。
        return comboList[comboIndex].fxConfigs[eventIndex]; // 返回当前连招中指定序号的特效配置。
    } 
    
    // 获取指定连招段中的音效配置。
    public ClipConfig TryGetClipConfig(int comboIndex, int eventIndex) // 根据连招索引和事件索引读取音效配置。
    { 
        if(comboIndex >= comboList.Length) // 如果连招索引超出连招数组长度。
            return null; // 没有对应连招配置，返回 null。
        if(eventIndex >= comboList[comboIndex].clipConfigs.Length) // 如果事件索引超出当前连招的音效配置数组长度。
            return null; // 没有对应音效配置，返回 null。
        return comboList[comboIndex].clipConfigs[eventIndex]; // 返回当前连招中指定序号的音效配置。
    } 
    
    // 获取指定连招段中的攻击反馈配置。
    public AttackFeedbackConfig TryGetAttackFeedbackConfig(int comboIndex, int eventIndex) // 根据连招索引和事件索引读取攻击反馈配置。
    { 
        if(comboIndex >= comboList.Length) // 如果连招索引超出连招数组长度。
            return null; // 没有对应连招配置，返回 null。
        if(eventIndex >= comboList[comboIndex].attackFeedbackConfigs.Length) // 如果事件索引超出当前连招的攻击反馈配置数组长度。
            return null; // 没有对应攻击反馈配置，返回 null。
        return comboList[comboIndex].attackFeedbackConfigs[eventIndex]; // 返回当前连招中指定序号的攻击反馈配置。
    } 
    
    // 获取指定连招段中的自身位移补偿配置。
    public SelfMoveOffsetConfig TryGetSelfMoveOffsetConfig(int comboIndex, int eventIndex) // 根据连招索引和事件索引读取自身位移补偿配置。
    { 
        if(comboIndex >= comboList.Length) // 如果连招索引超出连招数组长度。
            return null; // 没有对应连招配置，返回 null。
        if(eventIndex >= comboList[comboIndex].selfMoveOffsetConfigsConfigs.Length) // 如果事件索引超出当前连招的自身位移补偿配置数组长度。
            return null; // 没有对应自身位移补偿配置，返回 null。
        return comboList[comboIndex].selfMoveOffsetConfigsConfigs[eventIndex]; // 返回当前连招中指定序号的自身位移补偿配置。
    } 
    
    // 获取指定连招段中的目标位移补偿配置。
    public TargetMoveOffsetConfig TryGetTargetMoveOffsetConfig(int comboIndex, int eventIndex) // 根据连招索引和事件索引读取目标位移补偿配置。
    {
        if(comboIndex >= comboList.Length) // 如果连招索引超出连招数组长度。
            return null; // 没有对应连招配置，返回 null。
        if(eventIndex >= comboList[comboIndex].targetMoveOffsetConfigsConfigs.Length) // 如果事件索引超出当前连招的目标位移补偿配置数组长度。
            return null; // 没有对应目标位移补偿配置，返回 null。
        return comboList[comboIndex].targetMoveOffsetConfigsConfigs[eventIndex]; // 返回当前连招中指定序号的目标位移补偿配置。
    } 
} 
