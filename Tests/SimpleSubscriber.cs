using GenEvent;

namespace Tests
{
    /// <summary>
    /// 订阅 SimpleEvent 的订阅者，使用 void 返回类型的 [OnEvent] 方法
    /// </summary>
    public class SimpleSubscriber
    {
        /// <summary>
        /// 事件被调用的次数，用于测试验证
        /// </summary>
        public int EventCallCount { get; private set; }

        /// <summary>
        /// 最后接收到的事件消息
        /// </summary>
        public string LastMessage { get; private set; } = string.Empty;

        [OnEvent]
        public void OnSimpleEvent(SimpleEvent e)
        {
            EventCallCount++;
            LastMessage = e.Message;
        }
        
        [OnEvent]
        public void OnSimpleEvent(SimpleEvent2 e)
        {
            EventCallCount++;
            LastMessage = e.Message;
        }
    }
}
