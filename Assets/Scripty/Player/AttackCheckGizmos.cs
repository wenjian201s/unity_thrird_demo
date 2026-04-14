using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.PlayerLoop;


//根据武器上的攻击检测点，在攻击期间用射线检测敌人是否被命中，并在命中后调用敌人的受击逻辑。
// 管理不同武器的攻击检测点
// 在攻击过程中持续做命中检测
// 检测到敌人后触发 OnHit()
// 顺便处理攻击顿帧效果
public class AttackCheckGizmos : MonoBehaviour// 攻击检测脚本，继承 MonoBehaviour
{
    private Animator animator;// 当前角色身上的动画组件，用于实现命中顿帧
    
    //敌人层级
    [SerializeField] protected LayerMask enemyLayer; // 射线检测时只检测敌人所在层
    //是否正在攻击
    [SerializeField] protected bool isAttacking = false;// 当前是否处于攻击检测状态
    //当前的武器类型
    [SerializeField] public E_WeaponType weaponType = E_WeaponType.Empty;// 当前使用的武器
    //对应武器和攻击检测点组的字典   // 用字典管理“武器类型 -> 攻击检测点数组”的映射关系
    protected Dictionary<E_WeaponType, Transform[]> attackCheckPointsOfWeapon = new Dictionary<E_WeaponType, Transform[]>();
    //太刀攻击检测点  
    public Transform[] katanaCheckPoints;// 太刀武器上的多个攻击检测点
    //大剑攻击检测点
    public Transform[] greatSwordCheckPoints;// 大剑武器上的多个攻击检测点
    
    //武器上的攻击检测点
    [SerializeField] protected Transform[] attackCheckPoints; // 当前真正参与检测的点
    //上一次检测时检测点的位置
    [SerializeField] protected Vector3[] lastCheckPointsPosition; // 用来记录上一次 Check 时每个攻击点的位置
    //检测时间间隔
    public float timeBetweenCheck;// 每次攻击检测之间的时间间隔
    //计时器
    protected float timeCounter; // 用于累计时间，控制检测频率
    //是否是第一次检测
    protected bool isFirstCheck = true;
    protected RaycastHit[] enemiesRaycastHits;// 第一次检测时没有“上一帧位置”，所以只记录位置不检测
    
    //本次攻击的交互数据
    protected ComboInteractionConfig comboInteractionConfig; // 当前攻击的命中信息，比如伤害、攻击力度、武器类型等
    //本次攻击的反馈数据
    protected AttackFeedbackConfig attackFeedbackConfig; // 当前攻击的反馈信息，比如顿帧速度、顿帧时长等

    protected virtual void Start()
    {
        animator = GetComponent<Animator>();// 获取当前物体上的 Animator 组件
        //注册不同武器的攻击检测点  
        attackCheckPointsOfWeapon.Add(E_WeaponType.Katana, katanaCheckPoints);
        attackCheckPointsOfWeapon.Add(E_WeaponType.GreatSword, greatSwordCheckPoints);
    }

    protected virtual void Update()
    {
        if (isAttacking)// 如果当前处于攻击状态
        {
            timeCounter += Time.deltaTime;// 累计时间，用于控制检测频率
        }
        SwitchAttackCheckPoints();// 根据当前武器类型切换攻击检测点
    }

    protected virtual void FixedUpdate()
    {
        AttackCheck();// 在物理帧中执行攻击检测
    }

    protected virtual void SwitchAttackCheckPoints()
    {
        switch (weaponType)// 根据当前武器类型切换不同的检测点组
        {
            case E_WeaponType.Empty: // 没有武器
                break;
            
            case E_WeaponType.Katana:// 当前武器是太刀
                attackCheckPoints = attackCheckPointsOfWeapon[E_WeaponType.Katana];// 使用太刀检测点
                enemiesRaycastHits = new RaycastHit[attackCheckPoints.Length]; // 创建对应长度的命中数组
                break;
            
            case E_WeaponType.GreatSword:// 当前武器是大剑
                attackCheckPoints = attackCheckPointsOfWeapon[E_WeaponType.GreatSword]; // 使用大剑检测点
                enemiesRaycastHits = new RaycastHit[attackCheckPoints.Length]; // 创建对应长度的命中数组
                break;
            
            case E_WeaponType.Bow: // 当前武器是弓
                //功能待添加
                break;
        }
    }
    
    public virtual void AttackCheck()
    {
        if(weaponType == E_WeaponType.Empty)// 如果当前没有武器
            return;
        
        //若当时处于攻击状态
        if (isAttacking)
        {
            if (timeCounter >= timeBetweenCheck)// 达到一次检测所需时间间隔
            {
                //如果是第一次检查，则不进行检测
                if (isFirstCheck) // 如果是第一次检查，则不进行命中检测，只记录当前位置
                {
                    lastCheckPointsPosition = new Vector3[attackCheckPoints.Length];
                    //将isFirstCheck置false
                    isFirstCheck = false;// 将第一次检测标记关闭，下一次才能真正做检测
                }
                //不是第一次检查，则进行检测 // 不是第一次检查，则进行命中检测
                else
                {
                    for (int i = 0; i < attackCheckPoints.Length; i++) // 遍历所有攻击检测点
                    {
                        //进行射线检测     // 从“上一次记录的位置”朝“当前点的位置”发出一条射线
                        Ray ray = new Ray(lastCheckPointsPosition[i], (attackCheckPoints[i].position - lastCheckPointsPosition[i]).normalized);
                        // 射线非分配检测，检测从上次位置到当前位置的线段范围内是否打到敌人
                        int length = Physics.RaycastNonAlloc(ray, enemiesRaycastHits, Vector3.Distance(attackCheckPoints[i].position, lastCheckPointsPosition[i]), enemyLayer);
                        //若检测到了敌人
                        if (length > 0)
                        {
                            foreach (RaycastHit enemy in enemiesRaycastHits) // 遍历命中结果数组
                            {
                                if (enemy.transform)// 如果当前命中结果有效
                                {    // 获取敌人的战斗控制器
                                    //EnemyCombatController enemyHit = enemy.transform.gameObject.GetComponent<EnemyCombatController>();
                                    // if (enemyHit)// 如果目标身上有敌人战斗控制器
                                    // {    // 调用敌人的受击函数
                                    //     enemyHit.OnHit(comboInteractionConfig, attackFeedbackConfig, this.transform); //调用受击函数   
                                    //     SetAnimatorSpeed(attackFeedbackConfig.animatorSpeed); //对玩家进行顿帧 // 设置自己动画速度，实现顿帧效果
                                    //     // 在 stopFrameTime 秒后恢复动画速度
                                    //     Invoke(nameof(ResetAnimatorSpeed), attackFeedbackConfig.stopFrameTime); //结束顿帧
                                    // }
                                }
                            }
                        }
                        // 绘制调试射线，方便在 Scene 视图查看攻击检测轨迹
                        //绘制从上一次记录的该点的位置到当前该点的位置的线段
                        Debug.DrawRay(lastCheckPointsPosition[i], (attackCheckPoints[i].position - lastCheckPointsPosition[i]).normalized * Vector3.Distance(attackCheckPoints[i].position, lastCheckPointsPosition[i]), Color.red, 2f);
                    }
                }
                // 无论是否第一次检测，都记录当前所有检测点的位置，作为下次检测的“上一帧位置”
                //记录上一次Check时攻击判定点的位置
                for (int i = 0; i < attackCheckPoints.Length; i++)
                {
                    lastCheckPointsPosition[i] = attackCheckPoints[i].position;
                }
                timeCounter = 0f; //计时器归零，重新开始计时
            }
        }
        else
        {    // 如果当前不在攻击状态，说明攻击结束，需要重置状态
            isFirstCheck = true;  // 下次重新攻击时仍然从第一次检测开始
            lastCheckPointsPosition = null;// 清空上一次检测点位置
        }
    }

    private void SetAnimatorSpeed(float speed) => animator.speed = speed;// 设置动画播放速度

    private void ResetAnimatorSpeed() => animator.speed = 1f;// 恢复动画速度为默认值
    
    public void StartAttacking(ComboInteractionConfig comboConfig, AttackFeedbackConfig feedbackConfig)
    {
        isAttacking = true;  // 开始攻击检测
        comboInteractionConfig = comboConfig;// 记录当前攻击交互数据
        attackFeedbackConfig = feedbackConfig;// 记录当前攻击反馈数据
    }

    public void EndAttacking()
    {
        isAttacking = false; // 停止攻击检测
        comboInteractionConfig = null;// 清空当前攻击交互数据
        attackFeedbackConfig = null;// 清空当前攻击反馈数据
    }
}
