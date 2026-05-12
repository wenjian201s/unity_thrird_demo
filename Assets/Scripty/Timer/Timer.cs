using UnityEngine; // 引入 Unity 核心命名空间，例如 MonoBehaviour、Time、GameObject 等
using System; // 引入 System 命名空间，用于 Action 委托

// Timer 计时器类
// 继承 MonoBehaviour，说明它需要挂载在 GameObject 上运行
//
// 主要作用：
// 1. 创建一个倒计时
// 2. 每帧减少计时时间
// 3. 时间结束后执行回调函数
// 4. 执行完成后把自身回收到对象池
//
// 这个脚本通常不会直接手动放在场景中使用
// 而是通过对象池 CachePoolManager 获取：
// CachePoolManager.Instance.GetObject("Tool/Timer")
public class Timer : MonoBehaviour
{
    // 当前剩余计时时间
    //
    // 例如：
    // timer = 3
    // 表示 3 秒后执行回调
    private float timer;

    // 计时结束后要执行的回调函数
    //
    // Action 是 C# 中的委托类型
    // 可以理解成“保存一段之后要执行的代码”
    //
    // 例如：
    // () => { 技能重新可用; }
    private Action action;

    // 计时是否已经完成
    //
    // false 表示计时还没完成
    // true 表示计时完成，可以回收对象
    private bool timeIsDone;

    // Update 每帧执行一次
    private void Update()
    {
        // 执行计时逻辑
        OnUpdate();

        // 检查是否需要回收到对象池
        RecycleObject();
    }

    // 每帧更新计时器
    private void OnUpdate()
    {
        // 如果当前 GameObject 没有激活，则不执行计时
        //
        // activeSelf 表示当前物体自身是否激活
        // 如果对象池把这个 Timer 关闭了，就不应该继续计时
        if (!this.gameObject.activeSelf) return;

        // 如果计时时间还大于 0，并且计时还没有完成
        if (timer > 0 && !timeIsDone)
        {
            // 每帧减少时间
            //
            // Time.deltaTime 表示上一帧到当前帧经过的时间
            // 用它来减少 timer，可以实现按真实游戏时间倒计时
            timer -= Time.deltaTime;

            // 如果时间小于 0，说明倒计时结束
            if (timer < 0)
            {
                // 执行回调函数
                //
                // action?.Invoke() 是安全调用写法
                // 如果 action 不为空，就执行
                // 如果 action 是 null，就不会报错
                action?.Invoke();

                // 标记计时器已经完成
                // Update 中的 RecycleObject() 会根据这个标记把对象回收到对象池
                timeIsDone = true; 
            }
        }
    }

    /// <summary>
    /// 创建计时器
    /// </summary>
    /// <param name="timer">计时时间，单位是秒</param>
    /// <param name="cllBackAction">计时结束后执行的回调函数</param>
    /// <param name="timeIsDone">计时器初始是否完成，默认 false</param>
    public void CreateTime(float timer, Action cllBackAction, bool timeIsDone = false) 
    {
        // 设置计时时间
        //
        // 例如外部传入 5f，
        // 那么这个计时器会在 5 秒后执行回调
        this.timer = timer;

        // 保存计时结束后的回调函数
        //
        // 例如：
        // 技能 CD 结束后，把技能重新设置为可用
        this.action = cllBackAction;

        // 设置计时器是否已经完成
        //
        // 默认 false，表示开始计时
        this.timeIsDone = timeIsDone;
    }

    // 回收对象
    //
    // 作用：
    // 当计时器完成后，把当前 Timer 所在的 GameObject 放回对象池
    //
    // 原理：
    // 对象池不是销毁对象，而是把对象隐藏并保存起来
    // 下次需要 Timer 时再次取出复用
    public void RecycleObject()
    {
        // 如果计时已经完成
        if (timeIsDone) 
        {
            // 清空回调函数引用
            //
            // 这样可以避免旧回调残留
            // 也可以减少对象引用导致的内存泄漏风险
            action = null;

            // 回收到对象池中
            //
            // CachePoolManager.Instance.PushObject(this.gameObject)
            // 表示把当前 Timer 对象交还给对象池
            //
            // 对象池内部通常会 SetActive(false)
            // 下次需要时再 SetActive(true)
            CachePoolManager.Instance.PushObject(this.gameObject);
        }
    }
}