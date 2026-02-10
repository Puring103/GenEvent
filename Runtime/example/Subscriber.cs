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

        [OnGameEvent]
        public void OnEvent(ExampleEvent exampleEvent)
        {
            if (_test != null)
            {
                _test._eventReceivedCount++;
            }
        }
        
        [OnGameEvent]
        public void OnEvent3(ExampleEvent2 eventExample)
        {
            if (_test != null)
            {
                eventExample.number++;
            }
        }
    }
}