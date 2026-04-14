using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.UI;
using UnityEngine.UIElements;
//对象池 / 缓存池系统
/// <summary>
/// 缓存池中的每一列    
/// </summary>
/// /// 缓存池中的每一列
/// 表示“某一种对象”的缓存列表
/// 例如：某个特效、某个子弹、某个预制体对应的一组缓存对象
public class CachePoolData 
{
    public List<GameObject> cachePoolList;// 当前这一类对象的缓存列表

    public CachePoolData(GameObject obj, GameObject cachePoolRoot)
    {
        cachePoolList = new List<GameObject>() { };// 初始化缓存列表
        PushObject(obj, cachePoolRoot); // 把第一个对象存入缓存池
    }

    /// <summary>
    /// 向缓存池中添加对象
    /// </summary>
    public void PushObject(GameObject obj, GameObject cachePoolRoot)
    {
        if (obj)
        {
            //存入缓存池中
            cachePoolList.Add(obj);
            //失活
            obj.SetActive(false);
        }
        else
        {
            Debug.Log("Object is null");   
        }
    }

    /// <summary>
    /// 从缓存池中获取对象
    /// </summary>
    /// <returns></returns>
    public GameObject GetObject()
    {
        GameObject result = null;
        //取出第一个缓存对象
        if (cachePoolList.Count > 0) // 如果缓存池中还有对象
        {
            result = cachePoolList[0]; // 取出第一个缓存对象
            //将其从缓存池中移除
            cachePoolList.RemoveAt(0); // 将该对象从缓存列表中移除
            //激活，令其显示
            //TEST: 测试代码
            if (result)
            {
                result.SetActive(true);// 激活对象，使其重新显示/参与场景逻辑
                //断开父子关系
                result.transform.parent = null; // 断开父子关系
            }
        }
        return result;
    }
}


/// <summary>
/// 缓存池管理模块
/// 负责统一管理所有对象池
/// </summary>
public class CachePoolManager : SingletonPatternBase<CachePoolManager>
{
    // 字典：key = 对象类型名/资源路径名，value = 对应这一类对象的缓存池
    public Dictionary<string, CachePoolData> cachePoolDic = new Dictionary<string, CachePoolData>();

    private GameObject cachePoolRoot;// 缓存池根节点，用于统一管理池中的对象

    private CachePoolManager()
    {
        cachePoolDic = new Dictionary<string, CachePoolData>(); // 初始化字典
        cachePoolDic.Clear();// 清空字典（这里其实有点重复）
    }

    /// <summary>
    /// 从缓存池中获取对象
    /// </summary>
    /// <param name="key">缓存池名,为资源的文件路径名</param>
    /// <returns>被获取的对象</returns>
    public GameObject GetObject(string key)
    {
        GameObject result = null;
        //缓存池中存在该类型对象的缓存 // 如果缓存池中存在该 key，并且该池里还有对象可取
        if (cachePoolDic.ContainsKey(key) && cachePoolDic[key].cachePoolList.Count > 0)
        {
            result = cachePoolDic[key].GetObject();// 先从缓存池中取对象
            if (!result)// 如果取出来为空，则重新实例化一个
            {
                result = GameObject.Instantiate(Resources.Load<GameObject>(key));
                //把对象名改成缓存池名
                result.name = key; // 把对象名改成缓存池名
            }
        }
        else
        {    // 如果缓存池中没有该对象，或者池里没有可用对象，则直接从 Resources 加载并实例化
            result = GameObject.Instantiate(Resources.Load<GameObject>(key));
            //把对象名改成缓存池名
            result.name = key;
        }
        
        return result;
    }

    /// <summary>
    /// 向缓存池中添加对象,显示指定Key
    /// </summary>
    /// <param name="key">缓存池名</param>
    /// <param name="obj">被添加的对象</param>
    public void PushObject(string key, GameObject obj)
    {
        if (!cachePoolRoot) // 如果缓存池根节点还没创建，就创建一个
        {
            cachePoolRoot = new GameObject("CachePool");
        }

        //缓存池中存在该类型对象的缓存
        if (cachePoolDic.ContainsKey(key))// 如果该 key 对应的缓存池已经存在
        {
            cachePoolDic[key].PushObject(obj, cachePoolRoot); // 直接把对象加进去
        }
        else
        {
            cachePoolDic.Add(key, new CachePoolData(obj, cachePoolRoot));// 如果不存在，则新建一个缓存池
        }
    }

    /// <summary>
    /// 向缓存池中添加对象,忽略Key(此时Key为obj.name)
    /// </summary>
    /// <param name="obj">被添加的对象</param>
    public void PushObject(GameObject obj)
    {
        PushObject(obj.name, obj);
    }

    /// <summary>
    /// 清空缓存池,主要用于场景切换时
    /// </summary>
    public void ClearCachePool()
    {
        cachePoolDic.Clear();
        cachePoolRoot = null;
    }
}
