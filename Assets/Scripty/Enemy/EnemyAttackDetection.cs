using System; // 引入 C# 基础命名空间。当前代码中没有实际使用，可以删除
using System.Collections; // 引入协程相关命名空间。当前代码中没有实际使用，可以删除
using System.Collections.Generic; // 引入泛型集合命名空间。当前代码中没有实际使用，可以删除
using Unity.VisualScripting; // 引入 Unity 可视化脚本命名空间。当前代码中没有实际使用，可以删除
using UnityEngine; // 引入 Unity 核心命名空间，例如 MonoBehaviour、Ray、Physics、Vector3、Debug 等
using UnityEngine.PlayerLoop; // 引入 Unity PlayerLoop 相关命名空间。当前代码中没有实际使用，可以删除

// EnemyAttackDetection 敌人攻击检测类
// 继承自 AttackCheckGizmos
//
// AttackCheckGizmos 很可能是你项目中封装好的攻击检测基类
// 里面大概率包含：
// 1. attackCheckPoints 攻击检测点数组
// 2. lastCheckPointsPosition 上一次检测点位置
// 3. enemiesRaycastHits 射线检测结果数组
// 4. enemyLayer 可命中的 Layer
// 5. isAttacking 是否处于攻击状态
// 6. timeCounter 检测计时器
// 7. timeBetweenCheck 每次检测之间的时间间隔
// 8. isFirstCheck 是否是第一次检测
// 9. weaponType 当前武器类型
// 10. SwitchAttackCheckPoints() 根据武器切换检测点
//
// 这个类是敌人专用的攻击检测脚本。
// 作用：敌人攻击时检测是否命中玩家，或者是否触发玩家完美闪避。
public class EnemyAttackDetection : AttackCheckGizmos
{
    // 敌人战斗控制器
    // 作用：
    // 当攻击检测命中玩家时，调用 enemyCombatController.HitPlayer()
    // 把当前敌人招式的伤害配置传给玩家受击系统
    private EnemyCombatController enemyCombatController;

    // 敌人武器切换控制器
    // 作用：
    // 获取当前敌人正在使用的武器
    // 攻击开始时开启当前武器 Collider
    // 攻击结束时关闭当前武器 Collider
    private EnemySwapWeapon enemySwapWeapon;
    
    // Start 在脚本启用后的第一帧之前执行
    // 用于初始化组件引用和武器类型
    private void Start()
    {
        // 调用父类 AttackCheckGizmos 的 Start 方法
        // 父类中可能初始化了攻击检测点、射线命中数组、检测参数等
        base.Start();

        // 设置当前武器类型为 Katana
        // 说明这个敌人的默认攻击武器是武士刀
        //
        // weaponType 应该定义在 AttackCheckGizmos 父类中
        // 后续 SwitchAttackCheckPoints() 可能会根据 weaponType 切换不同武器的攻击检测点
        weaponType = E_WeaponType.Katana;

        // 获取当前物体上的 EnemyCombatController 组件
        // 用于在命中玩家时调用 HitPlayer()
        enemyCombatController = GetComponent<EnemyCombatController>();

        // 获取当前物体上的 EnemySwapWeapon 组件
        // 用于获取当前激活的武器，并开启或关闭武器 Collider
        enemySwapWeapon = GetComponent<EnemySwapWeapon>();
    }

    // Update 每帧执行一次
    private void Update()
    {
        // 根据当前武器类型切换攻击检测点
        //
        // 例如：
        // 如果当前武器是 Katana，则使用刀上的检测点
        // 如果当前武器是 GreatSword，则使用大剑上的检测点
        //
        // 这个函数应该来自父类 AttackCheckGizmos
        SwitchAttackCheckPoints();
    }

    // FixedUpdate 按固定物理帧执行
    // 适合做物理检测，例如 Raycast、Rigidbody、Collider 相关逻辑
    private void FixedUpdate()
    {
        // 如果当前正在攻击
        if (isAttacking)
        {
            // 累加固定时间
            // 用于控制攻击检测的频率
            //
            // 例如 timeBetweenCheck = 0.02
            // 那么每隔 0.02 秒进行一次射线扫掠检测
            timeCounter += Time.fixedDeltaTime;
        }

        // 执行攻击检测
        AttackCheck();
    }

    
    // 重写父类的攻击检测方法
    // 作用：
    // 在敌人攻击期间，通过检测点的移动轨迹发射射线，
    // 判断敌人的武器是否扫到玩家或完美闪避判定体
    public override void AttackCheck()
    {
        // 如果当前武器类型为空，则不进行攻击检测
        //
        // TODO 中写着“检查这句是否可以删除”
        // 这句本身是安全保护：
        // 如果敌人没有装备武器，就不应该进行攻击判定
        //
        // 但你在 Start 中固定设置了：
        // weaponType = E_WeaponType.Katana;
        // 所以在当前敌人脚本中，这句一般不会触发
        if (weaponType == E_WeaponType.Empty)
            return;
        
        // 如果当前处于攻击状态
        if (isAttacking)
        {
            // 判断是否到达下一次检测时间
            //
            // 这样做的目的：
            // 不是每一个 FixedUpdate 都检测，而是按照 timeBetweenCheck 控制检测频率
            //
            // 检测频率越高，命中更稳定，但性能开销更大
            // 检测频率越低，性能更好，但快速挥刀可能漏检
            if (timeCounter >= timeBetweenCheck)
            {
                // 如果这是攻击开始后的第一次检测
                if (isFirstCheck)
                {
                    // 创建一个数组，用来保存每个攻击检测点上一次的位置
                    //
                    // attackCheckPoints 是武器上的检测点数组
                    // 例如刀身上可以放多个点：
                    // 刀柄、刀身中段、刀尖
                    //
                    // lastCheckPointsPosition 和 attackCheckPoints 数量一致
                    lastCheckPointsPosition = new Vector3[attackCheckPoints.Length];

                    // 第一次检测只记录位置，不做射线检测
                    //
                    // 原理：
                    // 射线扫掠需要“上一次位置”和“当前位置”
                    // 第一次还没有上一次位置，所以不能检测
                    isFirstCheck = false;
                }
                // 如果不是第一次检测，就可以进行射线扫掠检测
                else
                {
                    // 遍历所有攻击检测点
                    for (int i = 0; i < attackCheckPoints.Length; i++)
                    {
                        // 创建一条射线
                        //
                        // 射线起点：
                        // lastCheckPointsPosition[i]
                        // 也就是该检测点上一次的位置
                        //
                        // 射线方向：
                        // attackCheckPoints[i].position - lastCheckPointsPosition[i]
                        // 也就是该检测点从上次检测到当前检测移动的方向
                        //
                        // 这个做法叫“扫掠检测”。
                        // 它不是只检测刀当前的位置，
                        // 而是检测刀从上一个位置移动到当前位置的整段路径。
                        //
                        // 这样可以避免武器挥动太快时穿过玩家却没有检测到的问题。
                        Ray ray = new Ray(
                            lastCheckPointsPosition[i],
                            (attackCheckPoints[i].position - lastCheckPointsPosition[i]).normalized
                        );

                        // 使用 RaycastNonAlloc 进行非分配射线检测
                        //
                        // 参数解释：
                        // ray：
                        // 要发射的射线
                        //
                        // enemiesRaycastHits：
                        // 存储射线检测结果的数组，应该定义在父类中
                        //
                        // Vector3.Distance(...)：
                        // 射线长度，也就是该检测点从上次位置到当前位置的距离
                        //
                        // enemyLayer：
                        // 要检测的 Layer
                        // 虽然名字叫 enemyLayer，但在敌人攻击检测中，
                        // 它实际应该设置为玩家层或者可受击层
                        //
                        // 返回值 length：
                        // 本次射线实际命中的数量
                        int length = Physics.RaycastNonAlloc(
                            ray,
                            enemiesRaycastHits,
                            Vector3.Distance(attackCheckPoints[i].position, lastCheckPointsPosition[i]),
                            enemyLayer
                        );

                        // 如果射线检测命中了目标
                        if (length > 0)
                        {
                            // 遍历射线命中的结果
                            //
                            // 注意：
                            // 这里当前写法是 foreach 整个 enemiesRaycastHits 数组
                            // 可能会遍历到上一次检测残留的数据。
                            //
                            // 更安全的写法应该是：
                            // for (int j = 0; j < length; j++)
                            //
                            // 后面我会给你优化版本。
                            foreach (RaycastHit enemy in enemiesRaycastHits)
                            {
                                // 如果命中的 Transform 不为空
                                if (enemy.transform)
                                {
                                    // 如果命中的对象 Tag 是 PerfectDodgeCollider
                                    //
                                    // 说明敌人的攻击扫到了玩家的“完美闪避判定体”
                                    // 这通常不是玩家本体，而是一个专门用于检测完美闪避时机的 Collider
                                    if (enemy.transform.CompareTag("PerfectDodgeCollider"))
                                    {
                                        Debug.Log("使用了射线检测");

                                        // 获取 PerfectDodge 脚本
                                        // 该脚本负责判断是否允许触发完美闪避
                                        PerfectDodge perfectDodge =
                                            enemy.transform.gameObject.GetComponent<PerfectDodge>();

                                        // 调用完美闪避接口
                                        // 如果当前可以触发完美闪避，就会执行玩家完美闪避逻辑
                                        perfectDodge.PerfectDodgeInterface();
                                    }
                                    // 如果命中的对象 Tag 是 Player
                                    else if (enemy.transform.CompareTag("Player"))
                                    {
                                        // 获取玩家战斗控制器
                                        PlayerCombatController playerHit =
                                            enemy.transform.gameObject.GetComponent<PlayerCombatController>();

                                        // 如果玩家身上存在 PlayerCombatController
                                        if (playerHit)
                                        {
                                            // 调用敌人战斗控制器的 HitPlayer
                                            //
                                            // HitPlayer 内部会把：
                                            // 当前敌人技能配置
                                            // 当前攻击段数配置
                                            // 攻击者 Transform
                                            // 传递给玩家受击系统
                                            enemyCombatController.HitPlayer(enemy.collider);   
                                        }   
                                    }
                                }
                            }
                        }

                        // 绘制调试射线
                        //
                        // 从上一次检测点位置，画到当前检测点位置
                        // 颜色为黄色，持续 2 秒
                        //
                        // 作用：
                        // 在 Scene 视图中观察武器攻击检测的轨迹
                        // 方便判断攻击检测是否覆盖了刀身挥动路径
                        Debug.DrawRay(
                            lastCheckPointsPosition[i],
                            (attackCheckPoints[i].position - lastCheckPointsPosition[i]).normalized
                            * Vector3.Distance(attackCheckPoints[i].position, lastCheckPointsPosition[i]),
                            Color.yellow,
                            2f
                        );
                    }
                }

                // 本次检测结束后，记录当前所有攻击检测点的位置
                //
                // 下一次检测时，会从这些位置发射到新的位置
                // 形成连续的武器轨迹检测
                for (int i = 0; i < attackCheckPoints.Length; i++)
                {
                    lastCheckPointsPosition[i] = attackCheckPoints[i].position;
                }

                // 计时器归零
                // 等待下一次 timeBetweenCheck 后继续检测
                timeCounter = 0f;
            }
        }
        else
        {
            // 如果当前不在攻击状态

            // 重置第一次检测标记
            // 下次攻击开始时，需要先初始化 lastCheckPointsPosition
            isFirstCheck = true;

            // 清空上一次检测点位置
            // 防止下一次攻击使用旧数据
            lastCheckPointsPosition = null;
        }
    }

    // 攻击开始
    //
    // 通常由攻击动画事件调用
    // 例如敌人挥刀动画到真正有伤害的帧时，调用 StartAttacking()
    public void StartAttacking()
    {
        // 标记当前进入攻击检测状态
        isAttacking = true;

        // 开启当前激活武器的 Collider
        //
        // enemySwapWeapon.GetCurrentActiveWeapon()
        // 获取当前敌人正在使用的武器对象
        //
        // weaponCollider.enabled = true
        // 开启武器触发器
        //
        // 作用：
        // 1. 可以用于 Trigger 检测
        // 2. 可以用于调试武器攻击范围
        // 3. 可以配合其他系统判断武器是否处于有效攻击状态
        enemySwapWeapon.GetCurrentActiveWeapon().weaponCollider.enabled = true;
    }

    // 攻击结束
    //
    // 通常由攻击动画事件调用
    // 例如敌人挥刀动画伤害帧结束后，调用 EndAttacking()
    public void EndAttacking()
    {
        // 标记当前不再攻击
        isAttacking = false;

        // 关闭当前激活武器的 Collider
        //
        // 防止攻击结束后武器碰到玩家还继续造成伤害
        enemySwapWeapon.GetCurrentActiveWeapon().weaponCollider.enabled = false;
    }
}