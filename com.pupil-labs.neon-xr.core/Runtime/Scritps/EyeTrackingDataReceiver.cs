using PupilLabs.Serializable;
using System;
using Stopwatch = System.Diagnostics.Stopwatch;
using UnityEngine;
using System.Threading.Tasks;

namespace PupilLabs
{
    public class EyeTrackingDataReceiver : GazeDataSource
    {
        public event RTSPDataHandler DataReceived;

        [SerializeField]
        StreamSelection selectedStreams = new StreamSelection
        {
            Gaze = true,
            Imu = false,
            World = false,
            EyeEvents = false,
            Eyes = false
        };
        [SerializeField]
        protected DataStorage storage;
        [SerializeField]
        protected DeviceManager deviceManager;
        [SerializeField]
        protected bool connectOnAwake = true;
        [SerializeField]
        protected bool autoReconnect = true;

        protected bool shouldReconnect = false;
        protected RTSPClient rtspClient = null;

        protected float[] gazePoint = new float[2];
        protected bool worn;
        protected float[] gazePointDualLeft = new float[2];
        protected float[] gazePointDualRight = new float[2];
        protected float[] eyeStateLeft = new float[7];
        protected float[] eyeStateRight = new float[7];
        protected float[] eyelidLeft = new float[3];
        protected float[] eyelidRight = new float[3];
        protected EtDataType etDataType = EtDataType.Unknown;
        protected long timestampMs = 0;
        protected bool rtcpSynchronized = false;

        protected readonly object dataLock = new object();
        protected GazeData gazeData = new GazeData();

        protected const int gazeMsgsPerLog = 2000;
        protected int gazeMsgCounter = 0;

        protected const int readTimeout = 7500;
        protected Stopwatch timeoutWatch = new Stopwatch();

        public bool IsRunning { get; private set; } = false;

        public override GazeData GazeData
        {
            get
            {
                lock (dataLock)
                {
                    gazeData.SetData(etDataType, gazePoint, worn, gazePointDualLeft, gazePointDualRight, eyeStateLeft, eyeStateRight, eyelidLeft, eyelidRight, timestampMs, rtcpSynchronized);
                    return gazeData;
                }
            }
        }

        protected virtual void Awake()
        {
            if (connectOnAwake)
            {
                Connect();
            }
        }

        public virtual void Connect(string ip = null)
        {
            RunAsync(ip).Forget();
        }

        protected virtual async Task RunAsync(string ip = null)
        {
            if (IsRunning)
            {
                return;
            }
            IsRunning = true;

            shouldReconnect = autoReconnect;

            try
            {
                await storage.WhenReady();
                do
                {
                    RTSPSettings rtspSettings = storage.Config.rtspSettings;
                    string currentIp = ip;
                    if (string.IsNullOrEmpty(currentIp))
                    {
                        currentIp = rtspSettings.ip;
                        if (rtspSettings.autoIp)
                        {
                            if (await deviceManager.Discover(rtspSettings.deviceName) && deviceManager.SelectAnyDevice())
                            {
                                currentIp = deviceManager.SelectedDeviceIp;
                            }
                            else
                            {
                                Debug.Log("[EyeTrackingDataReceiver] no device discovered");
                                await Task.Delay(1000);
                                continue;
                            }
                        }
                    }

                    timeoutWatch.Restart();

                    using (
                        rtspClient = rtspSettings.useUdp ?
                            new RTSPClientLive555(currentIp, rtspSettings.port, selectedStreams.GetMask()) :
                            new RTSPClientWs(currentIp, rtspSettings.port) //only gaze stream supported in ws client and always enabled
                    )
                    {
                        rtspClient.DataReceived += OnDataReceived;
                        await rtspClient.RunAsync();
                        rtspClient.DataReceived -= OnDataReceived;
                    }
                    rtspClient = null;
                    if (shouldReconnect)
                    {
                        await Task.Delay(1000);
                    }
                } while (shouldReconnect == true);
            }
            finally
            {
                IsRunning = false;
                timeoutWatch.Stop();
            }
        }

        protected virtual void OnDataReceived(long timestampMs, bool rtcpSynchronized, byte streamId, byte payloadFormat, uint dataSize, IntPtr data)
        {
            timeoutWatch.Restart();

            DataReceived?.Invoke(timestampMs, rtcpSynchronized, streamId, payloadFormat, dataSize, data);

            StreamId sid = (StreamId)streamId;
            if (sid == StreamId.Gaze)
            {
                GazeCallback(timestampMs, rtcpSynchronized, dataSize, data);
            }
        }

        protected virtual void GazeCallback(long timestampMs, bool rtcpSynchronized, uint dataSize, IntPtr data)
        {
            lock (dataLock)
            {
                this.timestampMs = timestampMs;
                this.rtcpSynchronized = rtcpSynchronized;
                etDataType = RTSPServiceWrapper.BytesToGazeData(
                    data, dataSize, 0,
                    gazePoint, out worn, gazePointDualLeft, gazePointDualRight,
                    eyeStateLeft, eyeStateRight,
                    eyelidLeft, eyelidRight
                );
            }
            OnGazeDataReceived();

            if (++gazeMsgCounter == gazeMsgsPerLog)
            {
                Debug.Log($"[EyeTrackingDataReceiver] {gazeMsgsPerLog} messages with gaze data processed");
                gazeMsgCounter = 0;
            }
        }

        protected virtual void Update()
        {
            if (timeoutWatch.IsRunning && timeoutWatch.ElapsedMilliseconds > readTimeout)
            {
                Debug.LogWarning($"[EyeTrackingDataReceiver] Connection timed out after {readTimeout}ms. No messages received.");
                timeoutWatch.Stop();
                rtspClient?.Stop();
            }
        }

        public virtual void Stop()
        {
            shouldReconnect = false;
            timeoutWatch.Stop();
            rtspClient?.Stop();
        }

        protected virtual void OnDestroy()
        {
            Stop();
        }
    }
}
