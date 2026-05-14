using UnityEngine; // 引入 Unity 核心命名空间，例如 ScriptableObject、Vector3、Time、Destroy 等

// CreateAssetMenu 表示这个 ScriptableObject 可以在 Unity Project 面板中创建成资源
//
// fileName = "AIDead"
// 默认创建出来的资源文件名叫 AIDead
//
// menuName = "StateMachine/State/AIDead"
// 创建路径为：
// Create -> StateMachine -> State -> AIDead
//
// AIDead 继承 StateActionSO，说明它是敌人有限状态机中的一个状态。
// 这个状态代表敌人死亡后的行为逻辑。
[CreateAssetMenu(fileName = "AIDead", menuName = "StateMachine/State/AIDead")]
public class AIDead : StateActionSO
{
    // TODO: 将来应该重构 UI 逻辑
    //
    // Boss 血条和耐力条 UI 控制器
    //
    // 当前死亡状态中会调用：
    // 1. AppearText() 显示死亡文本
    // 2. DisappearBar() 隐藏 Boss 血条
    //
    // 从架构上来说，死亡状态直接控制 UI 可以运行，
    // 但更好的做法是通过事件通知 UI 系统：
    // 敌人死亡 -> UI 自己处理显示和隐藏。
    private BossHealthAndEndurance bossHealthAndEndurance;
    
    // 角色控制器
    //
    // 敌人死亡后需要关闭 CharacterController，
    // 防止角色控制器继续参与移动、碰撞、地面检测等逻辑。
    private CharacterController characterController;

    // 敌人视野检测组件
    //
    // 当前代码中声明了 enemyView，但是没有实际使用。
    // 可以删除，或者未来用于死亡后关闭敌人视野。
    private EnemyView enemyView;

    // 当前敌人身上的所有 MonoBehaviour 脚本组件
    //
    // 死亡后会遍历这些组件，把大部分脚本禁用。
    //
    // 目的：
    // 敌人死亡后不应该继续执行移动、攻击、视野检测、AI 判断等逻辑。
    private MonoBehaviour[] components;

    // 死亡后延迟销毁敌人对象的时间
    //
    // 例如 destoryTime = 5，
    // 表示敌人死亡 5 秒后销毁整个敌人 GameObject。
    //
    // 注意：
    // 这里变量名 destoryTime 拼写错误，
    // 推荐改成 destroyTime。
    [SerializeField] private float destoryTime = 5f;

    // 死亡动画播放到一定进度后，敌人向地下沉的速度
    //
    // 数值越大，下沉越快。
    [SerializeField] private float downVelocity = 5f;
    
    
    // 初始化死亡状态
    //
    // 当状态机切换到 AIDead 状态时，
    // OnEnter 会调用 base.OnEnter(stateMachineSystem)，
    // base.OnEnter 内部会调用 Init。
    protected override void Init(StateMachineSystem stateMachineSystem)
    {
        // 调用父类 StateActionSO 的 Init
        //
        // 父类会获取：
        // animator
        // enemyCombatController
        // enemyMovementController
        // enemyParameter
        // transform
        //
        // 这里的 transform 指向当前敌人的 Transform。
        base.Init(stateMachineSystem);

        // 获取当前敌人 GameObject 上的所有 MonoBehaviour 组件
        //
        // 例如可能包括：
        // StateMachineSystem
        // EnemyCombatController
        // EnemyMovementController
        // EnemyAttackDetection
        // EnemyAttackAnimation
        // EnemyView
        // BossHealthAndEndurance
        //
        // 注意：
        // GetComponents<MonoBehaviour>() 只会获取当前 GameObject 上的脚本，
        // 不会获取子物体上的脚本。
        components = stateMachineSystem.GetComponents<MonoBehaviour>();

        // 获取子物体中的 CharacterController
        //
        // 如果 CharacterController 挂在敌人根物体或子物体上，都有机会获取到。
        //
        // 死亡后会禁用它，防止角色控制器继续影响敌人的移动和碰撞。
        characterController = stateMachineSystem.GetComponentInChildren<CharacterController>();
        
        // TODO: 将来应该重构 UI 逻辑
        //
        // 获取 Boss 血条 UI 控制器
        bossHealthAndEndurance = stateMachineSystem.GetComponent<BossHealthAndEndurance>();
    }

    // 进入死亡状态
    //
    // 当状态机切换到死亡状态时，只执行一次。
    public override void OnEnter(StateMachineSystem stateMachineSystem)
    {
        // 调用父类 OnEnter
        //
        // 父类 OnEnter 会调用 Init，
        // 初始化当前状态需要的组件引用。
        base.OnEnter(stateMachineSystem);

        // 禁用敌人大部分脚本
        //
        // 作用：
        // 1. 停止敌人 AI
        // 2. 停止敌人攻击检测
        // 3. 停止敌人移动控制
        // 4. 停止敌人视野检测
        // 5. 防止敌人死亡后继续执行战斗逻辑
        DisableAllScripts();

        // 延迟 destoryTime 秒后销毁当前敌人对象
        //
        // 使用 Timer 对象池计时器，而不是协程。
        DelayDestoryThisGameObject();
        
        // TODO: 将来应该重构 UI 逻辑
        //
        // 显示死亡相关文字
        bossHealthAndEndurance.AppearText();

        // 隐藏 Boss 血条和耐力条
        bossHealthAndEndurance.DisappearBar();
    }

    // 死亡状态每帧执行
    public override void OnUpdate()
    {
        // 判断当前动画在第 0 层播放的状态归一化时间是否大于等于 0.7
        //
        // normalizedTime：
        // 0 表示动画刚开始
        // 0.5 表示动画播放到一半
        // 1 表示动画播放完整一次
        //
        // 这里的意思是：
        // 当死亡动画播放到 70% 以后，开始让敌人向下沉。
        //
        // 常见用途：
        // 敌人死亡后先播放倒地动画，
        // 播放到后半段后尸体慢慢沉入地面，
        // 最后销毁对象。
        if (animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 0.7f)
        {
            // 获取敌人当前世界坐标
            Vector3 currentPosition = transform.position;

            // 计算新的 y 坐标
            //
            // Time.deltaTime：
            // 保证下沉速度不受帧率影响。
            //
            // downVelocity：
            // 控制每秒向下移动的速度。
            float newY = currentPosition.y - (Time.deltaTime * downVelocity);

            // 更新敌人的位置
            //
            // x 和 z 保持不变，
            // 只改变 y，让敌人垂直向下沉。
            transform.position = new Vector3(
                currentPosition.x,
                newY,
                currentPosition.z
            );   
        }
        
        // 调试输出
        //
        // 表示当前状态机正在执行死亡状态。
        //
        // 注意：
        // 这个 Debug.Log 每帧都会打印，
        // 实际项目中建议调试完成后删除，
        // 否则会刷屏并影响性能。
        Debug.Log("此时处于死亡状态");
    }

    // 禁用敌人大部分脚本
    //
    // 死亡后敌人不应该继续移动、攻击、检测玩家或执行 AI。
    private void DisableAllScripts()
    {
        // 遍历当前敌人身上的所有 MonoBehaviour 组件
        foreach (var component in components)
        {
            // 默认先禁用该组件
            component.enabled = false;

            // 如果该组件是 StateMachineSystem 或 BossHealthAndEndurance，
            // 则重新启用。
            //
            // 原因：
            // 1. StateMachineSystem 需要继续运行当前 AIDead.OnUpdate()
            //    否则死亡状态的下沉逻辑不会继续执行。
            //
            // 2. BossHealthAndEndurance 需要继续运行 UI 逻辑，
            //    否则死亡文字、血条隐藏等功能可能无法正常执行。
            //
            // 当前代码使用 component.GetType().Name 进行字符串比较。
            // 可以运行，但不够稳。
            // 更推荐使用 component is StateMachineSystem 这种类型判断。
            if (component.GetType().Name == nameof(StateMachineSystem) ||
                component.GetType().Name == nameof(BossHealthAndEndurance))
            {
                component.enabled = true;
            }
        }

        // 禁用 CharacterController
        //
        // 作用：
        // 1. 防止敌人死亡后继续被角色控制器移动
        // 2. 防止 CharacterController 的碰撞影响尸体下沉
        // 3. 允许直接修改 transform.position 实现下沉
        characterController.enabled = false;
    }

    // 延迟销毁当前敌人 GameObject
    //
    // 这里使用对象池中的 Timer 计时器。
    private void DelayDestoryThisGameObject()
    {
        // 从对象池中取出一个 Timer 对象
        //
        // CachePoolManager.Instance.GetObject("Tool/Timer")
        // 表示从对象池中获取路径或名称为 Tool/Timer 的计时器对象。
        Timer timer = CachePoolManager.Instance
            .GetObject("Tool/Timer")
            .GetComponent<Timer>();

        // 创建倒计时
        //
        // destoryTime 秒后执行回调函数。
        timer.CreateTime(destoryTime, () =>
        {
            // 倒计时结束后，销毁当前敌人对象
            //
            // 这里的 transform 来自 StateActionSO.Init，
            // 指向状态机所属敌人的 Transform。
            //
            // 所以 Destroy(transform.gameObject)
            // 销毁的是敌人本体，而不是 AIDead 这个 ScriptableObject 资源。
            Destroy(transform.gameObject);
        });
    }
}