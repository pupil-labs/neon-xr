using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace PupilLabs
{
    public class RTSPClientLive555 : RTSPClient
    {
        private Vector2 gazePoint = Vector2.zero;
        private readonly object gazePointLock = new object();
        private Eyelid eyelid = new Eyelid();
        private EyeState eyeState = new EyeState();
        private readonly string ip;
        private readonly int port;

        public override Vector2 GazePoint
        {
            get
            {
                lock (gazePointLock)
                {
                    return gazePoint;
                }
            }
        }

        public override Vector2 SmoothGazePoint => GazePoint;

        public override bool EyelidAvailable => false;

        public override Eyelid Eyelid => eyelid;

        public override Eyelid SmoothEyelid => eyelid;

        public override bool EyeStateAvailable => false;

        public override EyeState EyeState => eyeState;

        public override EyeState SmoothEyeState => eyeState;

        static void LogCallback(string message)
        {
            Debug.Log($"[RTSPClientLive555] {message}");
        }

        public RTSPClientLive555(string ip, int port)
        {
            this.ip = ip;
            this.port = port;
        }

        public override async Task RunAsync()
        {
            int msgCounter = 0;
            const int readTimeout = 7500;
            const int msgsPerTimer = 200;
            const int msgsPerLog = msgsPerTimer * 10;

            string url = $"rtsp://{ip}:{port}";

            Live555Wrapper.RawDataCallback gazeCallback = (long timestamp, uint dataSize, IntPtr data) =>
            {
                if (msgCounter % msgsPerTimer == 0)
                {
                    stopCts.CancelAfter(readTimeout);
                }

                byte[] bytes = new byte[dataSize];
                Marshal.Copy(data, bytes, 0, (int)dataSize);
                lock (gazePointLock)
                {
                    DataUtils.DecodeGazePoint(bytes, ref gazePoint);
                }
                OnGazeDataReceived();

                if (++msgCounter == msgsPerLog)
                {
                    Debug.Log($"[RTSPClientLive555] {msgsPerLog} messages processed");
                    msgCounter = 0;
                }
            };

            Live555Wrapper.Start(url, LogCallback, gazeCallback, null);
            using (stopCts = new CancellationTokenSource())
            {
                stopCts.CancelAfter(readTimeout);
                await Task.Delay(Timeout.Infinite, stopCts.Token).NoThrow();
                Live555Wrapper.Stop(); //stopCts must be valid till end due to callback, on C side there is thread.join(), might need rework
            }
        }
    }
}
