using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// 没有继承MonoBehaviour的单例模式基类
/// </summary>
public abstract class SingletonPatternBase<T> where T : class  //单列基类 通用泛型单例基类
{ // 保存单例对象
    // volatile 用于减少多线程下指令重排带来的问题
    private static volatile T _instance;
    private static readonly object _lock = new object(); //锁对象

    /// <summary>
    /// 通过属性获取单例
    /// </summary>
    public static T Instance
    {
        get
        {
            //双重检查锁保证线程安全// 双重检查锁，避免每次取实例都加锁，提高性能
            if (_instance == null) //避免每次访问都进入 lock，提高性能
            {
                lock (_lock)
                {
                    if (_instance == null)// 防止多个线程同时通过第一次检查后，重复创建实例
                    {
                        //instance = new T();
                        //_instance = (T)System.Activator.CreateInstance(typeof(T));
                        Type type = typeof(T);// 获取泛型 T 的类型信息
                        //获取T类型的私有无参构造函数
                        // 支持 private、protected、public 等实例构造函数
                        ConstructorInfo constructor = type.GetConstructor(
                            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                            null, Type.EmptyTypes, null);
                        //调用该私有无参构造函数
                        if (constructor != null)// 如果找到了无参构造函数，就通过反射创建对象
                        {
                            _instance = constructor.Invoke(null) as T;
                        }
                        else
                        {
                            Debug.LogError("Constructor Not Found");
                        }
                    }
                }
            }
            return _instance;
        }
    }

    /// <summary>
    /// 通过方法获取单例
    /// </summary>
    /// <returns>单例模式对象</returns>
    public static T GetInstance()
    {
        //双重检查锁保证线程安全
        if (_instance == null)
        {
            lock (_lock)
            {
                if (_instance == null)
                {
                    //instance = new T();
                    //_instance = (T)System.Activator.CreateInstance(typeof(T));
                    Type type = typeof(T);// 获取泛型 T 的类型
                    ConstructorInfo constructor = type.GetConstructor( // 获取 T 类型的无参构造函数
                        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                        null, Type.EmptyTypes, null);
                    if (constructor != null) // 如果存在构造函数，则通过反射调用它创建实例
                    {
                        _instance = constructor.Invoke(null) as T;
                    }
                    else
                    {
                        //TODO: 此处可以优化错误提示方式  // 如果找不到无参构造函数，输出错误信息
                        Debug.LogError("Constructor Not Found");
                    }
                }
            }
        }
        return _instance;
    }
}
