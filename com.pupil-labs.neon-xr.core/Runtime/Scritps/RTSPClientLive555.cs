using PupilLabs.Serializable;
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
        private readonly RTSPSettings settings;

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

        public RTSPClientLive555(RTSPSettings settings)
        {
            this.settings = settings;
        }

        public override async Task RunAsync()
        {
            string ip = settings.ip;
            if (settings.autoIp)
            {
                string discovered = await TryDiscoverOneDevice(settings.dnsPort, settings.deviceName);
                if (discovered != null)
                {
                    ip = discovered;
                }
                else
                {
                    Debug.Log("[RTSPClientWs] no device discovered, using fallback ip");
                }
            }
            string url = $"rtsp://{ip}:{settings.port}";

            Live555Wrapper.RawDataCallback gazeCallback = (long timestamp, uint dataSize, IntPtr data) =>
            {
                byte[] bytes = new byte[dataSize];
                Marshal.Copy(data, bytes, 0, (int)dataSize);
                lock (gazePointLock)
                {
                    DataUtils.DecodeGazePoint(bytes, ref gazePoint);
                }
                OnGazeDataReceived();
            };

            Live555Wrapper.Start(url, LogCallback, gazeCallback, null);
            using (stopCts = new CancellationTokenSource())
            {
                await Task.Delay(Timeout.Infinite, stopCts.Token).NoThrow();
            }
            Live555Wrapper.Stop();
        }
    }
}
