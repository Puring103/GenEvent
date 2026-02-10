using System;
using UnityEngine;

namespace GenEvent.Runtime.example
{
    public struct EventExample : IGameEvent
    {
        public string Message { get; set; }
    }
}