using GenEvent.Runtime.gen;
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
        public void OnEvent(EventExample eventExample)
        {
            if (_test != null)
            {
                _test._eventReceivedCount++;
            }
        }
        
        private void Start()
        {
            this.Bind();
        }

        private void OnDestroy()
        {
            this.Unbind();
        }
    }
}