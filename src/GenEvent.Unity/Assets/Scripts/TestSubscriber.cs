// using UnityEngine;
//
// namespace GenEvent.Runtime.example
// {
//     public class TestSubscriber : MonoBehaviour
//     {
//         private EventPerformanceTest _test;
//
//         public void SetTest(EventPerformanceTest test)
//         {
//             _test = test;
//         }
//
//         [OnEvent]
//         public void OnEvent(ExampleEvent exampleEvent)
//         {
//             if (_test != null)
//             {
//                 _test._eventReceivedCount++;
//             }
//         }
//
//         // [OnEvent]
//         // public bool OnEvent3(ExampleEvent2 eventExample)
//         // {
//         //     if (_test != null)
//         //     {
//         //         // 使用 _eventReceivedCount 来演示可取消事件：
//         //         // 当返回 false 时后续订阅者不会再被调用
//         //         _test._eventReceivedCount++;
//         //
//         //         // 小于 10 次时允许继续传播，大于等于 10 次时中止后续订阅者
//         //         return _test._eventReceivedCount < 10;
//         //     }
//         //
//         //     // 未设置测试脚本时不拦截事件
//         //     return true;
//         // }
//     }
// }