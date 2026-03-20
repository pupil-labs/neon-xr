using PupilLabs.Serializable;
using System;
using UnityEngine;

namespace PupilLabs
{
    public class EyeTrackingDataReceiver : GazeDataSource
    {
        [SerializeField]
        protected DataStorage storage;
        [SerializeField]
        protected DeviceManager deviceManager;
        [SerializeField]
        protected bool rtspAutoReconnect = true;

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

        protected const int readTimeout = 7500;
        protected const int msgsPerTimer = 200;
        protected const int msgsPerLog = msgsPerTimer * 10;
        protected int msgCounter = 0;

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

        protected virtual async void Awake()
        {
            await storage.WhenReady();
            do
            {
                RTSPSettings rtspSettings = storage.Config.rtspSettings;
                string ip = rtspSettings.ip;
                if (rtspSettings.autoIp)
                {
                    if (await deviceManager.Discover() && deviceManager.SelectAnyDevice())
                    {
                        ip = deviceManager.SelectedDeviceIp;
                    }
                    else
                    {
                        Debug.Log("[EyeTrackingDataReceiver] no device discovered");
                        continue;
                    }
                }

                using (
                    rtspClient = rtspSettings.useUdp ?
                        new RTSPClientLive555(ip, rtspSettings.port) :
                        new RTSPClientWs(ip, rtspSettings.port)
                )
                {
                    rtspClient.DataReceived += OnDataReceived;
                    await rtspClient.RunAsync();
                }
                rtspClient = null;
            } while (rtspAutoReconnect == true);
        }

        protected virtual void OnDataReceived(long timestampMs, bool rtcpSynchronized, byte streamId, byte payloadFormat, uint dataSize, IntPtr data)
        {
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

            if (++msgCounter == msgsPerLog)
            {
                Debug.Log($"[EyeTrackingDataReceiver] {msgsPerLog} messages with gaze data processed");
                msgCounter = 0;
            }
        }

        private void OnDestroy()
        {
            rtspAutoReconnect = false;
            rtspClient?.Stop();
        }
    }

    //TODO handle autoreconnect and timetout only here, fire own event since rtspclient can be replaced over time
}
