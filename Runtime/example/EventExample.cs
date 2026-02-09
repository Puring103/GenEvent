using System;
using UnityEngine;

namespace GenEvent.Runtime.example
{
    public struct EventExample : IGameEvent
    {
        public void OnEvent()
        {
            Debug.Log("EventExample");
        }
    }
}