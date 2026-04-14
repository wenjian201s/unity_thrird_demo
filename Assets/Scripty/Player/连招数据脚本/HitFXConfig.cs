using System;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

[Serializable]
public struct HitFXInfo //受击特效数据结构
{
    public GameObject hitFX;  //受击特效预制体对象
    public string hitFXName; //受击特效名称
}


// 允许在 Unity 中通过右键菜单创建 HitFXConfig 资源文件
// fileName = "HitFXConfig" 表示默认创建出来的资源文件名
// menuName = "ScriptableObjects/FX/HitFXConfig" 表示在资源菜单中的创建路径
[CreateAssetMenu(fileName = "HitFXConfig", menuName = "ScriptableObjects/FX/HitFXConfig")]
public class HitFXConfig : ScriptableObject  //受击特效配置文件// 定义一个受击特效配置类，继承 ScriptableObject
{
    
    // 受击特效信息数组
    // 每个元素保存一个受击特效对象和对应名称
    // private + SerializeField 表示字段私有，但仍可在 Inspector 面板中配置
    [SerializeField] private HitFXInfo[] hitFXInfoList;

    public string TryGetHitFXName()// 随机获取一个受击特效名称
    {// 从 hitFXInfoList 数组中随机选一个元素，并返回它的 hitFXName
        return hitFXInfoList[Random.Range(0, hitFXInfoList.Length)].hitFXName;
    }
    
    public GameObject TryGetHitFXObj()// 随机获取一个受击特效对象
    {     // 从 hitFXInfoList 数组中随机选一个元素，并返回它的 hitFX 对象
        return hitFXInfoList[Random.Range(0, hitFXInfoList.Length)].hitFX;
    }
}