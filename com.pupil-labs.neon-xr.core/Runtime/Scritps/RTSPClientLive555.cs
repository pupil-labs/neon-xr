using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace PupilLabs
{
    public class RTSPClientLive555 : RTSPClient
    {
        private float[] gazePoint = new float[2];
        private bool worn;
        private float[] gazePointDualRight = new float[2];
        private float[] eyeStateLeft = new float[7];
        private float[] eyeStateRight = new float[7];
        private float[] eyelidLeft = new float[3];
        private float[] eyelidRight = new float[3];
        private EtDataType etDataType = EtDataType.Unknown;
        long timestampMs = 0;
        bool rtcpSynchronized = false;

        private readonly object dataLock = new object();
        private GazeData gazeData = new GazeData();
        private readonly string ip;
        private readonly int port;

        private const int readTimeout = 7500;
        private const int msgsPerTimer = 200;
        private const int msgsPerLog = msgsPerTimer * 10;
        private int msgCounter = 0;

        public override GazeData GazeData
        {
            get
            {
                lock (dataLock)
                {
                    gazeData.SetData(etDataType, gazePoint, worn, gazePointDualRight, eyeStateLeft, eyeStateRight, eyelidLeft, eyelidRight, timestampMs, rtcpSynchronized);
                    return gazeData;
                }
            }
        }

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
                    stopCts.CancelAfter(readTimeout);
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
            StreamId sid = (StreamId)streamId;
            if (sid == StreamId.Gaze)
            {
                GazeCallback(timestampMs, rtcpSynchronized, dataSize, data);
            }
        }

        public void GazeCallback(long timestampMs, bool rtcpSynchronized, uint dataSize, IntPtr data)
        {
            if (msgCounter % msgsPerTimer == 0)
            {
                stopCts.CancelAfter(readTimeout);
            }

            lock (dataLock)
            {
                this.timestampMs = timestampMs;
                this.rtcpSynchronized = rtcpSynchronized;
                etDataType = RTSPServiceWrapper.BytesToGazeData(
                    data, dataSize, 0,
                    gazePoint, out worn, gazePointDualRight,
                    eyeStateLeft, eyeStateRight,
                    eyelidLeft, eyelidRight
                );
            }
            OnGazeDataReceived();

            if (++msgCounter == msgsPerLog)
            {
                Debug.Log($"[RTSPClientLive555] {msgsPerLog} messages processed");
                msgCounter = 0;
            }
        }
    }
}
