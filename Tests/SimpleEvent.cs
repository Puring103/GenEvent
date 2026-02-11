using GenEvent.Interface;

namespace Tests
{
    /// <summary>
    /// 用于基础测试的简单事件，void 返回类型场景
    /// </summary>
    public struct SimpleEvent : IGenEvent<SimpleEvent>
    {
        public string Message { get; set; }
    }
    
    /// <summary>
    /// 用于基础测试的简单事件，void 返回类型场景
    /// </summary>
    public struct SimpleEvent2 : IGenEvent<SimpleEvent2>
    {
        public string Message { get; set; }
    }
}
