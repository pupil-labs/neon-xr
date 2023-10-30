using System;
using System.Threading.Tasks;
using UnityEngine;

namespace PupilLabs
{
    public abstract class RTSPClient : Disposable
    {
        public event EventHandler GazeDataReceived;

        public abstract Task RunAsync();

        public abstract void Stop();

        public abstract Vector2 GazePoint { get; }
        public abstract Vector2 SmoothGazePoint { get; }

        protected virtual void OnGazeDataReceived()
        {
            GazeDataReceived?.Invoke(this, EventArgs.Empty);
        }
    }
}