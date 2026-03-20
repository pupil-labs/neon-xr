using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace PupilLabs
{
    public class RTSPClientLive555 : RTSPClient
    {
        private readonly string ip;
        private readonly int port;

        public RTSPClientLive555(string ip, int port)
        {
            this.ip = ip;
            this.port = port;
        }

        public override async Task RunAsync()
        {
            string url = $"rtsp://{ip}:{port}";

            using (stopCts = new CancellationTokenSource())
            {
                using (RTSPWorker worker = RTSPServiceWrapper.StartWorker<RTSPWorker>(url, 1 << (int)StreamId.Gaze))
                {
                    worker.DataReceived += DataCallback;
                    worker.LogMessageReceived += LogCallback;
                    await Task.Delay(Timeout.Infinite, stopCts.Token).NoThrow();
                }
                //stopCts must be valid till end due to callback, on C side there is thread.join(), might need rework
            }
        }

        public void LogCallback(string message)
        {
            Debug.Log($"[RTSPClientLive555] {message}");
        }

        public void DataCallback(long timestampMs, bool rtcpSynchronized, byte streamId, byte payloadFormat, uint dataSize, IntPtr data)
        {
            OnDataReceived(timestampMs, rtcpSynchronized, streamId, payloadFormat, dataSize, data);
        }
    }
}
