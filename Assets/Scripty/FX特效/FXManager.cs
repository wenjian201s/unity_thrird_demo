using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
// 统一播放普通攻击特效
//     统一播放受击特效
// 统一播放敌人特效
//     通过对象池 CachePoolManager 取出特效对象，避免频繁 Instantiate 和 Destroy
public class FXManager : SingletonPatternBase<FXManager>  //特效管理器
{
    
    // 播放普通特效
    // 参数：
    // fxConfig  : 特效配置数据，里面包含特效名称等信息
    // position  : 特效生成位置
    // rotation  : 特效旋转角度
    // scale     : 特效缩放
    public void PlayOneFX(FXConfig fxConfig, Vector3 position, Vector3 rotation, Vector3 scale)
    {
        // 从对象池中根据特效名字获取一个特效对象
        GameObject FX = CachePoolManager.Instance.GetObject(fxConfig.FXName);
        if (!FX) // 如果对象池没有取到特效对象，则报错并结束
        {
            Debug.LogError("FX Manager is NULL");
            return;
        }
        FX.transform.position = position;// 设置特效的位置
        FX.transform.eulerAngles = rotation;// 设置特效的旋转
        FX.transform.localScale = scale;// 设置特效的缩放
        // 获取该特效对象上的粒子系统组件
        ParticleSystem particleSystem = FX.GetComponent<ParticleSystem>();
        particleSystem.Play(); //播放特效
    }
    // 播放受击特效
    // 参数：
    // FXName    : 受击特效名称
    // position  : 受击特效播放位置
    // scale     : 特效缩放
    public void PlayOneHitFX(string FXName, Vector3 position, Vector3 scale)
    {
        GameObject FX = CachePoolManager.Instance.GetObject(FXName); // 从对象池中根据特效名字获取受击特效对象
        if (!FX)
        {
            Debug.LogError("FX Manager is NULL"); // 如果没有获取到对象，则报错
            return;
        }
        //设置特效位置
        FX.transform.position = position;
        FX.transform.localScale = scale;
        ParticleSystem particleSystem = FX.GetComponent<ParticleSystem>();
        particleSystem.Play();
        Debug.Log("播放了特效" + FXName); // 输出调试日志，表示该受击特效已播放
    }
    // // 播放敌人特效
    // // 这是 PlayOneFX 的重载版本，参数改为 EnemyFXConfig
    // // 用于播放敌人专用特效
    // public void PlayOneFX(EnemyFXConfig fxConfig, Vector3 position, Vector3 rotation, Vector3 scale)
    // {
    //     GameObject FX = CachePoolManager.Instance.GetObject(fxConfig.FXName);  // 从对象池中根据敌人特效名称获取特效对象
    //     if (!FX)
    //     {
    //         Debug.LogError("FX Manager is NULL");
    //         return;
    //     }
    //     FX.transform.position = position;
    //     FX.transform.eulerAngles = rotation;
    //     FX.transform.localScale = scale;
    //     ParticleSystem particleSystem = FX.GetComponent<ParticleSystem>();
    //     particleSystem.Play(); //播放特效
    // }
}