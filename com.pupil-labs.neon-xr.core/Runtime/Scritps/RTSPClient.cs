using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace PupilLabs
{
    public abstract class RTSPClient : Disposable
    {
        public event EventHandler GazeDataReceived;

        protected CancellationTokenSource stopCts = null;

        public abstract Task RunAsync();

        public abstract GazeData GazeData { get; }

        public virtual void Stop()
        {
            try
            {
                stopCts?.Cancel();
            }
            catch (ObjectDisposedException e)
            {
                Debug.Log(e.Message);
            }
        }

        protected virtual void OnGazeDataReceived()
        {
            GazeDataReceived?.Invoke(this, EventArgs.Empty);
        }
    }
}
