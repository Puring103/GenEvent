using System;
using System.Collections.Generic;
using System.Diagnostics;
using GenEvent;
using UnityEngine;

/// <summary>
/// 事件性能测试脚本
/// 测试 EventExample 事件的发布和订阅性能
/// </summary>
public class EventPerformanceTest : MonoBehaviour
{
    [Header("测试配置")]
    [SerializeField] private int subscriberCount = 100;
    [SerializeField] private int publishCount = 10000;
    [SerializeField] private bool runOnStart = false;

    [Header("每帧测试配置")]
    [SerializeField] private bool enableUpdateTest = false;
    [SerializeField] private bool logEachUpdate = false;

    private List<GameObject> _subscriberObjects = new List<GameObject>();
    private List<TestSubscriber> _subscribers = new List<TestSubscriber>();
    public int _eventReceivedCount = 0;
    private bool _isInitialized = false;
    private ExampleEvent _cachedExampleEvent;

    private void Start()
    {
        if (runOnStart)
        {
            RunPerformanceTest();
        }

        if (enableUpdateTest)
        {
            InitializeSubscribers();
        }
    }

    private void Update()
    {
        if (enableUpdateTest && _isInitialized)
        {
            TestSinglePublish();
        }
    }

    [ContextMenu("运行性能测试")]
    public void RunPerformanceTest()
    {
        UnityEngine.Debug.Log("========== 事件性能测试开始 ==========");

        // 清理之前的测试
        Cleanup();

        // 测试1: 订阅和取消订阅性能
        TestSubscribeUnsubscribe();

        // 测试2: 单订阅者事件发布性能
        TestSingleSubscriber();

        // 测试3: 多订阅者事件发布性能
        TestMultipleSubscribers();

        // 测试4: GC分配测试
        TestGCAllocation();

        UnityEngine.Debug.Log("========== 事件性能测试完成 ==========");
    }

    private void TestSubscribeUnsubscribe()
    {
        UnityEngine.Debug.Log($"\n[测试1] 订阅/取消订阅性能测试 (订阅者数量: {subscriberCount})");

        var sw = Stopwatch.StartNew();
        long gcBefore = GC.CollectionCount(0);

        // 创建并订阅
        for (int i = 0; i < subscriberCount; i++)
        {
            var go = new GameObject($"TestSubscriber_{i}");
            var subscriber = go.AddComponent<TestSubscriber>();
            subscriber.SetTest(this);
            subscriber.StartListening();
            _subscriberObjects.Add(go);
            _subscribers.Add(subscriber);
        }

        sw.Stop();
        long gcAfter = GC.CollectionCount(0);

        UnityEngine.Debug.Log($"  订阅耗时: {sw.ElapsedMilliseconds}ms ({sw.ElapsedTicks} ticks)");
        UnityEngine.Debug.Log($"  平均每个订阅: {sw.ElapsedTicks / (double)subscriberCount:F2} ticks");
        UnityEngine.Debug.Log($"  GC次数: {gcAfter - gcBefore}");

        // 取消订阅
        sw.Restart();
        gcBefore = GC.CollectionCount(0);

        foreach (var subscriber in _subscribers)
        {
            subscriber.StopListening();
        }

        sw.Stop();
        gcAfter = GC.CollectionCount(0);

        UnityEngine.Debug.Log($"  取消订阅耗时: {sw.ElapsedMilliseconds}ms ({sw.ElapsedTicks} ticks)");
        UnityEngine.Debug.Log($"  平均每个取消订阅: {sw.ElapsedTicks / (double)subscriberCount:F2} ticks");
        UnityEngine.Debug.Log($"  GC次数: {gcAfter - gcBefore}");

        CleanupSubscribers();
    }

    private void TestSingleSubscriber()
    {
        UnityEngine.Debug.Log($"\n[测试2] 单订阅者事件发布性能测试 (发布次数: {publishCount})");

        var go = new GameObject("TestSubscriber_Single");
        var subscriber = go.AddComponent<TestSubscriber>();
        subscriber.SetTest(this);
        subscriber.StartListening();
        _subscriberObjects.Add(go);
        _subscribers.Add(subscriber);

        _eventReceivedCount = 0;
        var sw = Stopwatch.StartNew();
        long gcBefore = GC.CollectionCount(0);

        var exampleEvent = new ExampleEvent { Message = "Test Message" };

        for (int i = 0; i < publishCount; i++)
        {
            exampleEvent.Publish();
        }

        sw.Stop();
        long gcAfter = GC.CollectionCount(0);

        UnityEngine.Debug.Log($"  发布耗时: {sw.ElapsedMilliseconds}ms ({sw.ElapsedTicks} ticks)");
        UnityEngine.Debug.Log($"  平均每次发布: {sw.ElapsedTicks / (double)publishCount:F2} ticks");
        UnityEngine.Debug.Log($"  每秒可发布: {publishCount * 1000.0 / sw.ElapsedMilliseconds:F0} 次");
        UnityEngine.Debug.Log($"  收到事件数: {_eventReceivedCount}");
        UnityEngine.Debug.Log($"  GC次数: {gcAfter - gcBefore}");

        CleanupSubscribers();
    }

    private void TestMultipleSubscribers()
    {
        UnityEngine.Debug.Log($"\n[测试3] 多订阅者事件发布性能测试 (订阅者: {subscriberCount}, 发布次数: {publishCount})");

        // 创建多个订阅者
        for (int i = 0; i < subscriberCount; i++)
        {
            var go = new GameObject($"TestSubscriber_{i}");
            var subscriber = go.AddComponent<TestSubscriber>();
            subscriber.SetTest(this);
            subscriber.StartListening();
            _subscriberObjects.Add(go);
            _subscribers.Add(subscriber);
        }

        _eventReceivedCount = 0;
        var sw = Stopwatch.StartNew();
        long gcBefore = GC.CollectionCount(0);

        var exampleEvent = new ExampleEvent { Message = "Test Message" };

        for (int i = 0; i < publishCount; i++)
        {
            exampleEvent.Publish();
        }

        sw.Stop();
        long gcAfter = GC.CollectionCount(0);

        long totalEvents = (long)subscriberCount * publishCount;

        UnityEngine.Debug.Log($"  发布耗时: {sw.ElapsedMilliseconds}ms ({sw.ElapsedTicks} ticks)");
        UnityEngine.Debug.Log($"  平均每次发布: {sw.ElapsedTicks / (double)publishCount:F2} ticks");
        UnityEngine.Debug.Log($"  平均每个订阅者处理: {sw.ElapsedTicks / (double)totalEvents:F2} ticks");
        UnityEngine.Debug.Log($"  每秒可发布: {publishCount * 1000.0 / sw.ElapsedMilliseconds:F0} 次");
        UnityEngine.Debug.Log($"  每秒可处理事件: {totalEvents * 1000.0 / sw.ElapsedMilliseconds:F0} 次");
        UnityEngine.Debug.Log($"  收到事件数: {_eventReceivedCount} (期望: {totalEvents})");
        UnityEngine.Debug.Log($"  GC次数: {gcAfter - gcBefore}");

        CleanupSubscribers();
    }

    private void TestGCAllocation()
    {
        UnityEngine.Debug.Log($"\n[测试4] GC分配测试 (订阅者: {subscriberCount}, 发布次数: {publishCount})");

        // 创建订阅者
        for (int i = 0; i < subscriberCount; i++)
        {
            var go = new GameObject($"TestSubscriber_{i}");
            var subscriber = go.AddComponent<TestSubscriber>();
            subscriber.SetTest(this);
            subscriber.StartListening();
            _subscriberObjects.Add(go);
            _subscribers.Add(subscriber);
        }

        // 强制GC收集基线
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long gcBefore = GC.GetTotalMemory(false);
        long gcCountBefore = GC.CollectionCount(0);

        _eventReceivedCount = 0;
        var sw = Stopwatch.StartNew();

        // 使用固定字符串避免字符串插值产生的GC分配
        var exampleEvent = new ExampleEvent { Message = "Test Message" };

        for (int i = 0; i < publishCount; i++)
        {
            exampleEvent.Publish();
        }

        sw.Stop();

        long gcAfter = GC.GetTotalMemory(false);
        long gcCountAfter = GC.CollectionCount(0);
        long allocatedBytes = gcAfter - gcBefore;

        UnityEngine.Debug.Log($"  发布耗时: {sw.ElapsedMilliseconds}ms");
        UnityEngine.Debug.Log($"  内存分配: {allocatedBytes} bytes ({allocatedBytes / 1024.0:F2} KB)");
        UnityEngine.Debug.Log($"  每次发布平均分配: {allocatedBytes / (double)publishCount:F2} bytes");
        UnityEngine.Debug.Log($"  GC次数: {gcCountAfter - gcCountBefore}");
        UnityEngine.Debug.Log($"  收到事件数: {_eventReceivedCount}");

        if (allocatedBytes == 0)
        {
            UnityEngine.Debug.Log($"  ✓ 0GC 目标达成！");
        }
        else
        {
            UnityEngine.Debug.LogWarning($"  ⚠ 检测到内存分配: {allocatedBytes} bytes");
        }

        CleanupSubscribers();
    }

    private void CleanupSubscribers()
    {
        foreach (var subscriber in _subscribers)
        {
            if (subscriber != null)
            {
                subscriber.StopListening();
            }
        }
        _subscribers.Clear();

        foreach (var go in _subscriberObjects)
        {
            if (go != null)
            {
                DestroyImmediate(go);
            }
        }
        _subscriberObjects.Clear();

        _eventReceivedCount = 0;
    }

    private void Cleanup()
    {
        CleanupSubscribers();
    }

    /// <summary>
    /// 初始化订阅者（只创建一次）
    /// </summary>
    private void InitializeSubscribers()
    {
        if (_isInitialized)
        {
            return;
        }

        UnityEngine.Debug.Log($"[每帧测试] 初始化订阅者 (数量: {subscriberCount})");

        // 创建多个订阅者
        for (int i = 0; i < subscriberCount; i++)
        {
            var go = new GameObject($"TestSubscriber_{i}");
            var subscriber = go.AddComponent<TestSubscriber>();
            subscriber.SetTest(this);
            subscriber.StartListening();
            _subscriberObjects.Add(go);
            _subscribers.Add(subscriber);
        }

        // 缓存事件对象，避免每次创建
        _cachedExampleEvent = new ExampleEvent { Message = "Update Test Message" };
        _isInitialized = true;

        UnityEngine.Debug.Log($"[每帧测试] 订阅者初始化完成，开始每帧测试");
    }

    /// <summary>
    /// 每帧发布测试（在Update中调用）
    /// 按照 publishCount 执行多次发布
    /// </summary>
    private void TestSinglePublish()
    {
        for (int i = 0; i < publishCount; i++)
        {
            _cachedExampleEvent.Publish();
        }

        if (logEachUpdate)
        {
            long totalEvents = (long)subscriberCount * publishCount;
            UnityEngine.Debug.Log($"[每帧测试] 已发布 {publishCount} 次事件，收到事件数: {_eventReceivedCount} (期望: {totalEvents})");
        }
    }

    private void OnDestroy()
    {
        Cleanup();
    }
}