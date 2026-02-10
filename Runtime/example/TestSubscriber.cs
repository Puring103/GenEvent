using UnityEngine;

namespace GenEvent.Runtime.example
{
    public class TestSubscriber : MonoBehaviour
    {
        private EventPerformanceTest _test;

        public void SetTest(EventPerformanceTest test)
        {
            _test = test;
        }

        [OnEvent]
        public void OnEvent(ExampleEvent exampleEvent)
        {
            if (_test != null)
            {
                _test._eventReceivedCount++;
            }
        }
        
        [OnEvent]
        public void OnEvent3(ExampleEvent2 eventExample)
        {
            if (_test != null)
            {
                eventExample.number++;
            }
        }
    }
}