using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class PlayerAudioController : MonoBehaviour
{
    private AudioSource audioSource;

    public AudioClip[] greetAudio;   // 问候语音
    public AudioClip[] agreeAudio;   // 同意/回应语音
    public AudioClip[] linesAudio;   // 普通台词语音
    
    void Start()
    {
        audioSource = GetComponent<AudioSource>();
    }

    // 播放随机问候语音
    public void PlayGreetAudio()
    {
        if (greetAudio == null || greetAudio.Length == 0)
        {
            Debug.LogWarning("greetAudio 数组为空，无法播放问候语音");
            return;
        }

        int i = Random.Range(0, greetAudio.Length);
        audioSource.PlayOneShot(greetAudio[i]);
    }

    // 播放随机回应语音
    public void PlayAgreeAudio()
    {
        if (agreeAudio == null || agreeAudio.Length == 0)
        {
            Debug.LogWarning("agreeAudio 数组为空，无法播放回应语音");
            return;
        }

        int i = Random.Range(0, agreeAudio.Length);
        audioSource.PlayOneShot(agreeAudio[i]);
    }

    // 播放随机普通台词
    public void PlayLinesAudio()
    {
        if (linesAudio == null || linesAudio.Length == 0)
        {
            Debug.LogWarning("linesAudio 数组为空，无法播放普通台词");
            return;
        }

        int i = Random.Range(0, linesAudio.Length);
        audioSource.PlayOneShot(linesAudio[i]);
    }
}