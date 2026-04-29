using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CachePoolObject : MonoBehaviour
{
    [SerializeField] private float timeToDestroy = 1f;//时间叠加
    private float timer;  //时间
    void OnEnable()
    {
        Invoke(nameof(PushIntoCachePool), timeToDestroy);  //回调内存池
        timer = timeToDestroy;
        StartCoroutine(IE_PushIntoCachePool()); //开启携程函数
    }

    IEnumerator IE_PushIntoCachePool()
    {
        while (timer > 0)
        {
            yield return null;
            timer -= Time.deltaTime;
        }
        //将对象放入缓存池中
        PushIntoCachePool();
    }

    private void PushIntoCachePool()
    {
        CachePoolManager.Instance.PushObject(this.gameObject);
    }
}