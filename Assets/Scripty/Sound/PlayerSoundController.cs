using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class PlayerSoundController : MonoBehaviour //玩家音效控制器
{
    private AudioSource audioSource; //播放音效组件
    [Header("基础动作音效")]
    // 脚步声音效数组（放入多个音效是为了随机播放，避免听感单调）
    public AudioClip[] footSteps;
    public AudioClip[] jumpEfforts; // 起跳时的发力/呼喝音效数组
    public AudioClip[] landing;// 落地时的音效数组
    [Header("装备音效")]
    public AudioClip equip; // 装备武器的音效（单一音效即可）
    public AudioClip unEquip;// 卸下武器的音效（单一音效即可）
    [Header("攻击音效")]
    public AudioClip[] commonAttack;// 普通攻击的武器挥舞/命中音效数组（按连击段数对应，例如索引0对应第一段攻击）
    public AudioClip[] playerCommonAttack;// 玩家普通攻击时的呼喝声/风声数组（用于增加打击感）
    
    void Start()
    {
        audioSource = GetComponent<AudioSource>(); //获取玩家身上的音效组件
    }

    public void PlayFootStepSound() //播放走路音效
    {
        int i = Random.Range(0, footSteps.Length); // 随机生成一个数组索引，从脚步声数组中随机抽取一个播放
        audioSource.PlayOneShot(footSteps[i]);
    }
    
    /// 播放起跳发力音效
    public void PlayJumpEffortSound()
    {
        int i = Random.Range(0, jumpEfforts.Length);
        audioSource.PlayOneShot(jumpEfforts[i]);
    }
    /// 播放落地音效
    public void PlayLandingSound()
    {
        int i = Random.Range(0, landing.Length);
        audioSource.PlayOneShot(landing[i]);
    }
    /// 播放装备武器音效
    public void PlayEquipSound()
    {
        audioSource.PlayOneShot(equip);
    }
    /// 播放卸下武器音效
    public void PlayUnEquipSound()
    {
        audioSource.PlayOneShot(unEquip);
    }
    /// 播放普通攻击音效（通常由动画事件Animation Event调用）
    public void PlayCommonAttackSound(int currentAttack)//当前是第几段连击（通常从1开始）
    {
        audioSource.PlayOneShot(commonAttack[currentAttack - 1]); // 播放对应段数的武器攻击音效（传入参数为1，数组索引为0，所以减1）
        PlayPlayerCommonAttackSound(currentAttack);// 同时触发玩家自身的攻击发声（呼喝声）
    }
    /// 播放玩家普通攻击时的自身发声（呼喝声/气声）
    public void PlayPlayerCommonAttackSound(int currentAttack)//当前是第几段连击
    {    // 随机抽取一个玩家发声音效
        int randomIndex = Random.Range(0, playerCommonAttack.Length);
        //连击第四下（重击）必定播放音效
        if(currentAttack == 4)
        {
            audioSource.PlayOneShot(playerCommonAttack[randomIndex]);
        }
        else
        {    // 前三下普通攻击：有概率播放音效
            // 【注意】：Random.Range(0f, 1f) >= 0.2f 实际上是 80% 的概率会进入此分支。
            // 如果你原本的设计是 "20%的概率播放"，建议将 >= 改为 < 。
            //20%的概率播放音效
            if (Random.Range(0f, 1f) >= 0.2f)
            {
                audioSource.PlayOneShot(playerCommonAttack[randomIndex]);
            }
        }
    }
}