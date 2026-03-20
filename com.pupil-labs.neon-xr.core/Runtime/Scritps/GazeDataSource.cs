using System;
using UnityEngine;

namespace PupilLabs
{
    public abstract class GazeDataSource : MonoBehaviour, IGazeDataSource
    {
        public event Action GazeDataReceived;

        public abstract GazeData GazeData { get; }

        protected virtual void OnGazeDataReceived()
        {
            GazeDataReceived?.Invoke();
        }
    }
}
