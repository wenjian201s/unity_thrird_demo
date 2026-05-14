using UnityEngine; // 引入 Unity 核心命名空间，例如 MonoBehaviour、AudioSource、AudioClip、Random 等

// EnemyNormalAttackClip 敌人普通攻击音效控制脚本
//
// 主要作用：
// 1. 获取敌人身上的 AudioSource 音源组件
// 2. 在普通攻击动画事件触发时，随机播放一个攻击音效
// 3. 让敌人的普通攻击音效不重复，增加打击表现的变化
public class EnemyNormalAttackClip : MonoBehaviour
{
    // AudioSource 音源组件
    //
    // AudioSource 负责真正播放声音。
    // 这个脚本只是选择要播放哪个 AudioClip，
    // 最终声音是通过 audioSource.PlayOneShot() 播放出来的。
    private AudioSource audioSource;
    
    // 普通攻击音效数组
    //
    // 可以在 Inspector 面板中配置多个 AudioClip。
    //
    // 例如：
    // audioClips[0] = 普通挥刀音效 1
    // audioClips[1] = 普通挥刀音效 2
    // audioClips[2] = 普通挥刀音效 3
    //
    // 播放时会从数组中随机选择一个音效，
    // 避免每次攻击声音完全一样。
    [SerializeField] private AudioClip[] audioClips;

    // Start 会在脚本启用后的第一帧之前执行
    private void Start()
    {
        // 获取当前 GameObject 上的 AudioSource 组件
        //
        // 注意：
        // 这个脚本所在的物体上必须挂有 AudioSource，
        // 否则 audioSource 会是 null，
        // 后面调用 PlayOneShot 时会报错。
        audioSource = GetComponent<AudioSource>();
    }

    // 播放随机普通攻击音效
    //
    // 这个方法通常不是由 Update 调用，
    // 而是由动画事件 Animation Event 调用。
    //
    // 例如普通攻击动画播放到挥刀那一帧时，
    // 动画事件调用 PlayRandomClip()，
    // 此时随机播放一个挥刀音效。
    public void PlayRandomClip()
    {
        // Random.Range(0, audioClips.Length)
        //
        // 当参数是 int 时：
        // 最小值包含，最大值不包含。
        //
        // 例如 audioClips.Length = 3，
        // Random.Range(0, 3) 只会返回 0、1、2。
        //
        // 然后用这个随机索引从 audioClips 数组中取出一个 AudioClip。
        //
        // PlayOneShot(audioClip, 0.5f)
        // 表示播放一次指定音效，音量为 0.5。
        //
        // PlayOneShot 的特点：
        // 1. 不会打断 AudioSource 正在播放的其他声音
        // 2. 适合播放攻击、受击、脚步声这类短音效
        audioSource.PlayOneShot(
            audioClips[Random.Range(0, audioClips.Length)],
            0.5f
        );
    }
}