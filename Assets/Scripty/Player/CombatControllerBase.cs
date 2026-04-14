using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.PlayerLoop;
using Random = UnityEngine.Random;

public class CombatControllerBase : MonoBehaviour  // 战斗控制基类，继承 MonoBehaviour
{
    #region 组件
    
    protected AttackCheckGizmos attackCheckSystem; // 攻击检测系统，用于攻击范围判定
    protected AudioSource audioSource; // 音频组件，用于播放攻击音效
    protected Animator animator;    // 动画组件，用于播放攻击/受击动画
    
    #endregion
    //连招
    [SerializeField] protected ComboList currentComboList; // 当前使用的连招配置列表
    protected int currentComboIndex; // 当前正在执行的连招索引
    protected int nextComboIndex;// 下一次将要执行的连招索引
    protected bool canExecuteCombo; // 是否允许继续执行连招
    public bool CanExecuteCombo => canExecuteCombo; // 对外只读属性，返回是否可以执行连招
    
    [SerializeField] protected float multiplier = 1.2f;// 连招重置时间倍率，影响 stop combo 的判定时间
    [SerializeField] protected bool canBeHit;// 是否可以被击中
    [SerializeField] protected float hitCoolDown = 0.25f; // 受击冷却时间，防止短时间内重复受击
    [SerializeField] protected HitFXConfig[] hitFXList; // 受击特效配置数组，不同受击力度对应不同特效
    public Transform hitTransform; //播放受击特效的位置
    [SerializeField] protected Vector3 hitFXScale;// 受击特效缩放
    
    protected virtual void Start()
    {
        runningEventIndex = new RunningEventIndex();// 初始化运行中的事件索引记录器
        animator = GetComponent<Animator>();         // 获取当前物体上的 Animator 组件
        attackCheckSystem = GetComponent<AttackCheckGizmos>();// 获取攻击检测组件
        audioSource = GetComponent<AudioSource>();  // 获取 AudioSource 组件
        canExecuteCombo = true;  // 初始状态允许出招
        canBeHit = true;  // 初始状态允许被击中
    }

    protected virtual void Update()
    {
        RunEvent(); // 每帧检测当前攻击动画中是否有事件需要触发  按动画时间触发事件
    }
    
    //播放攻击动画
    protected void ExecuteCombo()  //执行连招 判断当前能不能出招 设置当前连招索引播放对应攻击动画更新下一段连招索引进入攻击冷却 开启“超时重置连招”的协程
    {
        if(!canExecuteCombo)// 如果当前不允许继续出招
            return;
        runningEventIndex.Reset();//重置攻击的事件计数
        currentComboIndex = nextComboIndex; // 当前攻击索引 = 下一段要执行的连招索引
        //播放攻击动画   // 获取当前连招对应的动画名  // 动画过渡时间  // 动画层级 0  // 从动画起始时间开始播放
        animator.CrossFadeInFixedTime(currentComboList.TryGetComboName(currentComboIndex), 0.1555f, 0, 0);
        //更新攻击计数
        UpdateComboIndex();
        canExecuteCombo = false; //后摇 / 进入后摇/冷却，暂时不能再次出招
        // 开启攻击冷却协程，到时间后重新允许出招
        StartCoroutine(IE_ExecuteComboCoolDown(currentComboList.TryGetCoolDownTime(currentComboIndex)));
        if (stopComboCoroutine != null) // 如果之前已经有重置连招的协程在跑
        {
            StopCoroutine(stopComboCoroutine); // 停止之前的协程，避免重复计时
        }
        // 开启一个新的协程，用于在一定时间后重置连招索引
        stopComboCoroutine = StartCoroutine(IE_StopCombo(currentComboList.TryGetCoolDownTime(currentComboIndex)));
    }
    
    private Coroutine stopComboCoroutine;
    
    //协程计算后摇时间    // 协程：计算连招中断/重置时间
    IEnumerator IE_StopCombo(float coolDownTime)
    {
        float time = coolDownTime * multiplier;// 实际重置时间 = 冷却时间 * 倍率
        while (time > 0)// 只要时间还没走完
        {
            yield return null; // 等待下一帧
            time -= Time.deltaTime;// 扣除一帧时间
        }
        //重置连招
        nextComboIndex = 0;
    }
    // 协程：控制当前攻击的后摇/冷却
    IEnumerator IE_ExecuteComboCoolDown(float coolDownTime)
    {
        while (coolDownTime > 0)// 冷却还没结束
        {
            yield return null;// 等待下一帧
            coolDownTime -= Time.deltaTime; // 扣除一帧时间
        }
        canExecuteCombo = true;// 冷却结束，可以再次出招
    }

    //更新攻击计数
    private void UpdateComboIndex()
    {
        nextComboIndex++; // 下一次攻击索引 +1
        //重置攻击计数  // 如果超出连招数量，则回到第 0 段
        if (nextComboIndex >= currentComboList.TryGetComboListCount())
        {
            nextComboIndex = 0;
        }
    }

    private RunningEventIndex runningEventIndex;// 当前动画事件执行到第几个的记录器
    //事件检测
    private void RunEvent() // 事件检测
    {
        if(!currentComboList)// 如果没有连招配置 // 直接返回
            return;
        // 如果当前动画层 0 的状态名不是当前连招动画，或者动画正在过渡中
        if (!animator.GetCurrentAnimatorStateInfo(0).IsName(currentComboList.TryGetComboName(currentComboIndex)) || 
            animator.IsInTransition(0))
            return;// 直接返回，不执行事件
        //攻击检测部分
        //攻击检测   // 当前连招段数   // 当前第几个攻击检测事件
        ComboInteractionConfig comboInteractionConfig = currentComboList.TryGetComboInteractionConfig(currentComboIndex, runningEventIndex.attackDetectionIndex);
        if (comboInteractionConfig != null)// 如果取到了攻击检测配置
        {    // 如果当前动画播放进度超过攻击开始时间
            if (animator.GetCurrentAnimatorStateInfo(0).normalizedTime > comboInteractionConfig.startTime)
            {
                //获得攻击反馈配置信息  / 获取攻击反馈配置（如停顿、震屏等）               // 当前连招段数            // 当前第几个反馈事件
                AttackFeedbackConfig attackFeedbackConfig = currentComboList.TryGetAttackFeedbackConfig(currentComboIndex, runningEventIndex.attackFeedbackIndex);
                //执行攻击检测
                attackCheckSystem.StartAttacking(comboInteractionConfig, attackFeedbackConfig); //开始进行攻击检测
            }   
            // 如果当前动画播放进度超过攻击结束时间
            if (animator.GetCurrentAnimatorStateInfo(0).normalizedTime > comboInteractionConfig.endTime)
            {
                attackCheckSystem.EndAttacking();// 结束攻击检测
                //执行一次事件后  // 执行完一次攻击检测事件后，索引加一，准备读下一段事件
                runningEventIndex.attackDetectionIndex++;
                runningEventIndex.attackFeedbackIndex++;
            }
        }
        
        //特效部分
        //生成特效                                           // 当前连招段数      // 当前第几个特效事件
        FXConfig fxConfig = currentComboList.TryGetFXConfig(currentComboIndex, runningEventIndex.FXIndex);
        if (fxConfig != null)// 如果存在特效配置
        {    // 如果当前动画播放进度超过特效触发时间
            if (animator.GetCurrentAnimatorStateInfo(0).normalizedTime > fxConfig.startTime)
            {
                //修改位置  // 计算特效相对于角色的位置偏移
                Vector3 fxPosition = transform.forward * fxConfig.position.z  // 前后偏移
                                     + transform.up * fxConfig.position.y // 上下偏移
                                     + transform.right * fxConfig.position.x; // 左右偏移
                // 播放一次特效
                FXManager.Instance.PlayOneFX(fxConfig, // 特效配置 
                    fxPosition + transform.position,     // 世界坐标位置
                    fxConfig.rotation + transform.eulerAngles, fxConfig.scale); // 旋转  // 缩放
                runningEventIndex.FXIndex++;// 特效事件执行完毕，索引加一
            }
        }
        
        //音效 部分
        //播放音效
        ClipConfig clipConfig = currentComboList.TryGetClipConfig(currentComboIndex// 当前连招段数
            , runningEventIndex.clipIndex);// 当前第几个音效事件
        if (clipConfig != null)// 如果存在音效配置
        {   // 如果动画进度超过音效触发时间
            if (animator.GetCurrentAnimatorStateInfo(0).normalizedTime > clipConfig.startTime)
            {
                //播放音效
                if (clipConfig.audioClip)
                {
                    audioSource.PlayOneShot(clipConfig.audioClip, clipConfig.volume);// 播放一次音效
                    runningEventIndex.clipIndex++;// 音效事件执行完后，索引加一
                }
            }
        }
    }

    [SerializeField] private float rotationSpeed; // 受击转向速度
    //受击函数  // 受击函数：当被攻击命中时调用  //传入攻击者位置
    public virtual void OnHit(ComboInteractionConfig interactionConfig, AttackFeedbackConfig attackFeedbackConfig, Transform attacker)
    {
        //看向攻击者
        // 获取当前的旋转和目标旋转
        Quaternion fromRotation = transform.rotation;
        // 获取目标旋转
        Quaternion toRotation = Quaternion.LookRotation(-attacker.position, Vector3.up);
        // 平滑过渡到目标旋转
        transform.rotation = Quaternion.Lerp(fromRotation, toRotation, Time.deltaTime * rotationSpeed);
        
        if(!canBeHit) // 如果当前不可被击中
            return; // 直接返回

        canBeHit = false;// 标记为暂时不可再次受击
        // 开启受击冷却协程，冷却结束后才能再次受击
        StartCoroutine(IE_HitCoolDown(attackFeedbackConfig, hitCoolDown));
        //生成受击特效
        // 根据攻击力度选择受击特效名
        string hitFXName = hitFXList[(int)interactionConfig.attackForce].TryGetHitFXName();
        // 播放受击特效
        FXManager.Instance.PlayOneHitFX(hitFXName, hitTransform.position, hitFXScale);
    }
    // 受击冷却协程
    IEnumerator IE_HitCoolDown( AttackFeedbackConfig attackFeedbackConfig, float coolDownTime)
    {   // 冷却时间 = 基础受击冷却 + 停帧时间
        coolDownTime = coolDownTime + attackFeedbackConfig.stopFrameTime;
        Debug.Log(coolDownTime);// 打印实际受击冷却时间，便于调试
        while (coolDownTime > 0)// 冷却中
        {
            yield return null;// 等待下一帧
            coolDownTime -= Time.deltaTime;   // 扣除一帧时间
        }
        canBeHit = true;// 冷却结束，可以再次受击
    }
    // 设置动画播放速度
    protected void SetAnimatorSpeed(float speed) => animator.speed = speed;
    // 重置动画播放速度为默认值 1
    protected void ResetAnimatorSpeed() => animator.speed = 1f;
}
// 用于记录当前动画事件执行到哪个索引
public class RunningEventIndex
{
    public int attackDetectionIndex = 0;// 当前攻击检测事件索引
    public int FXIndex = 0;   // 当前特效事件索引
    public int clipIndex = 0; // 当前音效事件索引
    public int attackFeedbackIndex = 0;// 当前攻击反馈事件索引

    public void Reset()
    {
        attackDetectionIndex = 0; // 重置攻击检测索引
        FXIndex = 0;              // 重置特效索引
        clipIndex = 0;            // 重置音效索引
        attackFeedbackIndex = 0;  // 重置攻击反馈索引
    }
}
