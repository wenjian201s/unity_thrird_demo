using Unity.VisualScripting; // 引入 Unity 可视化脚本命名空间。当前代码中没有实际使用，可以删除
using UnityEngine; // 引入 Unity 核心命名空间，例如 MonoBehaviour、Transform、Physics、LayerMask、Gizmos 等

// EnemyView 敌人视野检测类
// 这个脚本通常挂载在敌人对象身上
// 作用：
// 1. 检测玩家是否进入敌人的感知范围
// 2. 判断玩家是否在敌人正前方的视野角度内
// 3. 判断敌人和玩家之间是否有障碍物遮挡
// 4. 如果玩家满足条件，则设置为 currentTarget
// 5. 如果玩家离开一定范围，则丢失目标
public class EnemyView : MonoBehaviour
{
    // =========================
    // 战斗 / 视野检测相关参数
    // =========================

    // 视野检测中心点
    // 一般可以设置为敌人的头部、眼睛、胸口位置
    // 用于作为 OverlapSphere 和 Raycast 的起点
    [SerializeField] private Transform detectionCenter;

    // 视野检测半径
    // 代表敌人最多能感知多远范围内的玩家
    // 例如 detectionRadius = 10，表示敌人能检测 10 米内的玩家
    [SerializeField] private float detectionRadius;

    // 目标丢失半径倍率
    // 范围限制在 0.5 到 1
    //
    // 作用：
    // 玩家被发现后，如果暂时不在视野内，
    // 只要玩家还在 detectionRadius * detectionRadiusMultiplier 的距离以内，
    // 敌人就不会立刻丢失目标
    //
    // 举例：
    // detectionRadius = 10
    // detectionRadiusMultiplier = 0.8
    // 那么玩家在 8 米内，即使暂时不满足视野检测，也不会立刻被清空
    [SerializeField]
    [Range(0.5f, 1f)]
    private float detectionRadiusMultiplier;

    // 玩家所在的 Layer
    // Physics.OverlapSphereNonAlloc 只会检测这个 Layer 上的对象
    // 通常玩家对象需要设置为 Player Layer
    [SerializeField] private LayerMask playerLayer;

    // 障碍物所在的 Layer
    // Physics.Raycast 会检测这个 Layer
    // 如果敌人与玩家之间有 obstacleLayer 的物体，就认为视线被遮挡
    [SerializeField] private LayerMask obstacleLayer;

    // 用于存储检测到的玩家 Collider
    //
    // 注意：
    // 这里数组长度是 1
    // 说明这个敌人一次只关心一个目标
    // 如果场景中有多个玩家或多个可攻击对象，只会保存检测到的第一个
    //
    // 使用 Collider[] 缓存数组的原因：
    // 搭配 Physics.OverlapSphereNonAlloc 使用，可以避免每帧产生 GC 垃圾
    [SerializeField] private Collider[] targets = new Collider[1];

    // 当前攻击目标
    // 如果敌人看到玩家，就会把玩家 Transform 赋值给 currentTarget
    // 如果玩家离开视野或距离过远，就会把 currentTarget 设为 null
    [SerializeField, Header("攻击目标")]
    private Transform currentTarget = null;

    // 对外提供只读属性
    // 其他脚本可以读取敌人当前目标，但不能直接修改它
    // 例如 EnemyAI、EnemyCombatController 可以通过 enemyView.CurrentTarget 获取玩家
    public Transform CurrentTarget => currentTarget;

    // 敌人的视野角度
    // 范围 0 到 360 度
    //
    // 举例：
    // detectAngle = 90，表示敌人正前方左右各 45 度范围内可以看到玩家
    // detectAngle = 180，表示敌人前方半圆范围都能看到玩家
    // detectAngle = 360，表示敌人全方向感知
    [SerializeField, Range(0f, 360f)]
    private float detectAngle;

    // Update 每帧执行一次
    // 每一帧都重新检测敌人是否能看到玩家
    void Update()
    {
        // 执行视野检测逻辑
        View();
    }

    // =========================
    // 视野检测核心逻辑
    // =========================

    // 视野检测函数
    // 作用：
    // 1. 先用球形范围检测玩家是否进入感知半径
    // 2. 再用射线检测玩家是否被障碍物挡住
    // 3. 再用点乘判断玩家是否在敌人正前方视野角度内
    // 4. 满足条件则设置 currentTarget
    // 5. 不满足条件并且距离过远，则清空 currentTarget
    private void View()
    {
        // 使用非分配版本的球形检测
        //
        // Physics.OverlapSphereNonAlloc 的作用：
        // 在 detectionCenter.position 位置创建一个半径为 detectionRadius 的球形检测范围
        // 检测范围内属于 playerLayer 的 Collider
        // 检测结果存入 targets 数组
        //
        // 返回值 targetCount 表示检测到了多少个目标
        //
        // 使用 NonAlloc 的好处：
        // 不会每帧 new 一个 Collider 数组，减少 GC，适合 Update 中频繁调用
        int targetCount = Physics.OverlapSphereNonAlloc(
            detectionCenter.position,
            detectionRadius,
            targets,
            playerLayer
        );

        // 当前这一帧是否真正看到了目标
        // false 表示本帧没有确认看到玩家
        // true 表示本帧玩家满足范围、无遮挡、角度条件
        bool isInView = false;

        // 如果球形范围内检测到了玩家
        if (targetCount > 0)
        {
            // 第一步：检测玩家是否被障碍物遮挡
            //
            // IsInView 返回 true 表示：
            // 从敌人的检测中心点向玩家身体部分位置发射射线时，
            // 没有被 obstacleLayer 障碍物挡住
            if (IsInView(targets[0].transform))
            {
                // 第二步：检测玩家是否在敌人的视野角度内
                //
                // 这里计算的是：
                // 从敌人位置指向玩家位置的方向
                //
                // targets[0].transform.position + new Vector3(0, 1f, 0)
                // 表示玩家大约胸口或身体中部的位置
                //
                // transform.position + new Vector3(0, 1.2f, 0)
                // 表示敌人眼睛或头部附近的位置
                //
                // 两者相减，得到“敌人看向玩家”的方向
                Vector3 directionToTarget =
                    (
                        (targets[0].transform.position + new Vector3(0, 1f, 0))
                        - (transform.position + new Vector3(0, 1.2f, 0))
                    ).normalized;

                // Vector3.Dot 点乘用于判断两个方向的夹角
                //
                // directionToTarget：敌人指向玩家的方向
                // transform.forward：敌人自己的正前方方向
                //
                // 点乘结果：
                // 1 表示两个方向完全相同，玩家在敌人正前方
                // 0 表示两个方向垂直，玩家在敌人侧面
                // -1 表示两个方向完全相反，玩家在敌人背后
                //
                // Mathf.Cos(Mathf.Deg2Rad * detectAngle / 2)
                // 用于把视野半角转换成点乘阈值
                //
                // 如果点乘值大于这个阈值，说明玩家在视野角度内
                if (
                    Vector3.Dot(directionToTarget, transform.forward)
                    > Mathf.Cos(Mathf.Deg2Rad * detectAngle / 2)
                )
                {
                    // 玩家满足：
                    // 1. 在检测半径内
                    // 2. 没有被障碍物挡住
                    // 3. 在敌人正前方视野角度内
                    //
                    // 所以设置为当前攻击目标
                    currentTarget = targets[0].transform;

                    // 标记本帧确实看到了目标
                    isInView = true;
                }
            }
        }

        // 如果当前已经有攻击目标
        if (currentTarget)
        {
            // 如果本帧没有看到目标
            // 并且目标距离敌人已经超过 detectionRadius * detectionRadiusMultiplier
            // 那么丢失目标
            //
            // 这个逻辑的作用：
            // 玩家被敌人发现后，不会因为短暂离开视野或者被遮挡就立刻丢失目标
            // 只有当玩家离开一定距离之后，敌人才会真正放弃追踪
            if (
                !isInView
                && Vector3.Distance(transform.position, currentTarget.position)
                > detectionRadius * detectionRadiusMultiplier
            )
            {
                // 清空当前攻击目标
                currentTarget = null;

                // 清空检测数组中的目标缓存
                targets[0] = null;
            }
        }
    }

    /// <summary>
    /// 检测玩家对象在视野中是否可见
    /// </summary>
    /// <param name="target">需要检测的目标，一般是玩家</param>
    /// <returns>
    /// true：目标可见，中间没有障碍物完全遮挡
    /// false：目标不可见，被障碍物挡住
    /// </returns>
    private bool IsInView(Transform target)
    {
        // 循环两次：
        // i = 5  时 offset = 0.5
        // i = 10 时 offset = 1.0
        //
        // 也就是说：
        // 从敌人的 detectionCenter 向玩家身体的两个高度位置发射射线
        // 一个大约是腰部/胸口
        // 一个大约是头部附近
        for (int i = 5; i <= 10; i += 5)
        {
            // 把 i 转换成高度偏移
            // 5 / 10f = 0.5
            // 10 / 10f = 1.0
            float offset = i / 10f;

            // 从敌人的检测中心向玩家身体某个高度点发射射线
            //
            // 射线起点：
            // detectionCenter.position
            //
            // 射线方向：
            // target.position + target.up * offset - detectionCenter.position
            //
            // 射线距离：
            // 敌人检测中心到目标检测点之间的距离
            //
            // 检测层：
            // obstacleLayer，只检测障碍物
            //
            // 原理：
            // 如果射线没有打到障碍物，说明敌人和玩家之间无遮挡，可以看到玩家
            // 如果射线打到了障碍物，说明这条视线被挡住
            //
            // 当前逻辑：
            // 只要有一条射线没有被障碍物挡住，就认为玩家可见
            if (
                Physics.Raycast(
                    detectionCenter.position,
                    ((target.position + target.up * offset) - detectionCenter.position).normalized,
                    out RaycastHit hit,
                    Vector3.Distance(
                        detectionCenter.position,
                        target.position + target.up * offset
                    ),
                    obstacleLayer
                ) == false
            )
            {
                // 如果没有检测到障碍物
                // 说明从敌人视野中心到玩家该部位之间是通的
                // 返回 true，表示玩家可见
                return true;
            }
        }

        // 如果两条射线都被障碍物挡住
        // 说明玩家不可见
        return false;
    }

    #region Gizmos绘图

    // Gizmos 绘图函数
    // 用于在 Scene 视图中可视化敌人的检测范围和视线方向
    // 只用于编辑器调试，不影响游戏运行逻辑
    private void OnDrawGizmos()
    {
        // 设置 Gizmos 颜色为蓝色
        Gizmos.color = Color.blue;

        // 绘制敌人的检测范围球
        // 这个球对应 Physics.OverlapSphereNonAlloc 的检测范围
        Gizmos.DrawWireSphere(detectionCenter.position, detectionRadius);

        // 如果检测数组里有目标，并且当前攻击目标不为空
        if (targets[0] != null && currentTarget != null)
        {
            // 绘制从敌人检测中心到目标 root 位置的射线
            // 注意：
            // 当前 DrawRay 传入的是 normalized 方向
            // 所以绘制出来的射线长度只有 1
            // 如果想画到玩家身上，需要乘以距离
            Gizmos.DrawRay(
                detectionCenter.position,
                ((targets[0].transform.root.position + targets[0].transform.root.up * 0f)
                 - detectionCenter.position).normalized
            );

            // 绘制从检测中心到目标 root 高度 0.5 的射线方向
            Gizmos.DrawRay(
                detectionCenter.position,
                ((targets[0].transform.root.position + targets[0].transform.root.up * 0.5f)
                 - detectionCenter.position).normalized
            );

            // 绘制从检测中心到目标 root 高度 1.0 的射线方向
            Gizmos.DrawRay(
                detectionCenter.position,
                ((targets[0].transform.root.position + targets[0].transform.root.up * 1f)
                 - detectionCenter.position).normalized
            );

            // 绘制从检测中心到目标 root 高度 1.5 的射线方向
            Gizmos.DrawRay(
                detectionCenter.position,
                ((targets[0].transform.root.position + targets[0].transform.root.up * 1.5f)
                 - detectionCenter.position).normalized
            );
        }
    }

    #endregion
}