using PupilLabs.Serializable;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PupilLabs
{
    public class StreamingExample : MonoBehaviour
    {
        [SerializeField]
        private TextMeshProUGUI selectedDeviceLabel;
        [SerializeField]
        private GameObject itemPrefab;
        [SerializeField]
        private Transform itemContainer;
        [SerializeField]
        private TextMeshProUGUI timeOffset;
        [SerializeField]
        private Toggle[] streamSelectionToggles;
        [SerializeField]
        private EyeStateVisualizer eyeStateVisualizer;
        [SerializeField]
        private Transform gazePointMarker;
        [SerializeField]
        private Transform gazePointDualLeftMarker;
        [SerializeField]
        private Transform gazePointDualRightMarker;
        [SerializeField]
        private GameObject fixationMarker;
        [SerializeField]
        private Vector2Int worldCamRes = new Vector2Int(1600, 1200);

        [SerializeField]
        Transform imuPitch;
        [SerializeField]
        Transform imuYaw;
        [SerializeField]
        Transform imuRoll;
        [SerializeField]
        LineRenderer accel;
        [SerializeField]
        Transform quat;

        private Transform quatInitial = null;
        private volatile bool imuDataReceived = false;
        private volatile bool gazeDataReceived = false;
        private volatile bool fixationOnSet = false;
        private string selectedDeviceIp = null;
        private RTSPWorker worker;

        //data buffers
        private float[] gazePoint = new float[2];
        private float[] gazePointDualLeft = new float[2];
        private float[] gazePointDualRight = new float[2];
        private float[] eyeStateLeft = new float[7];
        private float[] eyeStateRight = new float[7];
        private float[] eyelidLeft = new float[3];
        private float[] eyelidRight = new float[3];
        private float[] gazeEvent = new float[10];
        private float[] accelData = new float[3];
        private float[] gyroData = new float[3];
        private float[] quatData = new float[4];

        //reuse structs
        private GazeData gazeData;
        private EyeEventData eyeEventData;
        private ImuData imuData;

        object imuLock = new object();
        object gazeLock = new object();

        public void OnDeviceSelected(string label, string ipAddress)
        {
            selectedDeviceLabel.SetText($"{label}@{ipAddress}");
            selectedDeviceIp = ipAddress;
        }

        public void OnDiscoveryFinished(DeviceManager manager)
        {
            foreach (Transform child in itemContainer)
            {
                Destroy(child.gameObject);
            }

            var discoveredDevices = manager.DiscoveredDevices;
            if (discoveredDevices != null)
            {
                foreach (string key in discoveredDevices.Keys)
                {
                    GameObject item = Instantiate(itemPrefab, itemContainer);
                    item.transform.Find("Label").GetComponent<TextMeshProUGUI>().SetText($"{key}@{discoveredDevices[key]}");
                    item.GetComponentInChildren<Button>().onClick.AddListener(() =>
                    {
                        manager.SelectDevice(key);
                    });
                }
            }
        }

        public void OnTimeOffsetEstimated(string ipAddress, long offsetMs)
        {
            timeOffset.text = $"{offsetMs}ms";
        }

        public void OnStreamClicked()
        {
            DataStorage storage = ServiceLocator.Instance.GetComponentInChildren<DataStorage>(true);
            if (storage == null || storage.Ready == false)
            {
                Debug.Log("DataStorage is not ready yet, cannot start RTSP worker.");
                return;
            }
            RTSPSettings rtspSettings = storage.Config.rtspSettings;
            if (rtspSettings.useUdp == false)
            {
                Debug.Log("This functionality is only supported with UDP");
                return;
            }
            byte streamMask = 0;
            string url = $"rtsp://{selectedDeviceIp}:{rtspSettings.port}";
            for (int i = 0; i < streamSelectionToggles.Length; i++)
            {
                if (streamSelectionToggles[i].isOn)
                {
                    streamMask |= (byte)(1 << i);
                }
            }
            if (worker != null)
            {
                worker.Dispose();
            }
            worker = RTSPServiceWrapper.StartWorker<RTSPWorker>(url, streamMask);
            worker.DataReceived += OnDataReceived;
            worker.LogMessageReceived += (message) =>
            {
                Debug.Log($"Worker Log: {message}");
            };
        }

        private void OnDestroy()
        {
            worker?.Dispose();
        }

        private void Update()
        {
            if (imuDataReceived)
            {
                ImuData imuDataCopy;
                lock (imuLock)
                {
                    imuDataCopy = imuData;
                }
                if (quatInitial == null)
                {
                    quatInitial = quat.parent;
                    quatInitial.localRotation = Quaternion.Inverse(imuDataCopy.quatData);
                    Debug.Log($"Initial rotation set to: {imuData.quatData.eulerAngles}");
                }
                imuPitch.localEulerAngles = new Vector3(imuDataCopy.gyroData.x, 0, 0);
                imuRoll.localEulerAngles = new Vector3(0, imuDataCopy.gyroData.y, 0);
                imuYaw.localEulerAngles = new Vector3(0, 0, imuDataCopy.gyroData.z);
                accel.SetPosition(1, imuDataCopy.accelData);
                quat.localRotation = imuDataCopy.quatData;
            }
            if (gazeDataReceived)
            {
                GazeData gazeDataCopy;
                lock (gazeLock)
                {
                    gazeDataCopy = gazeData;
                }
                gazePointMarker.localPosition = new Vector3(gazeDataCopy.gazePoint[0] / worldCamRes.x, gazeDataCopy.gazePoint[1] / worldCamRes.y, 0);
                gazePointDualLeftMarker.localPosition = new Vector3(gazeDataCopy.gazePointDualLeft[0] / worldCamRes.x, gazeDataCopy.gazePointDualLeft[1] / worldCamRes.y, 0);
                gazePointDualRightMarker.localPosition = new Vector3(gazeDataCopy.gazePointDualRight[0] / worldCamRes.x, gazeDataCopy.gazePointDualRight[1] / worldCamRes.y, 0);
                eyeStateVisualizer.SetGazeData(gazeDataCopy.type >= EtDataType.EyeStateEyelidGazeData, gazeDataCopy.eyeState, gazeDataCopy.type >= EtDataType.EyeStateEyelidGazeData, gazeDataCopy.eyelid);

                if (fixationOnSet)
                {
                    fixationMarker.SetActive(true);
                }
                else
                {
                    fixationMarker.SetActive(false);
                }
            }
        }

        private void OnDataReceived(long timestampMs, bool rtcpSynchronized, byte streamId, byte payloadFormat, uint dataSize, IntPtr data)
        {
            Debug.Log($"Data received: {timestampMs}ms, Synchronized: {rtcpSynchronized}, Stream ID: {streamId}, Payload Format: {payloadFormat}, Data Size: {dataSize}");
            if (streamId == (int)StreamId.Gaze)
            {
                bool worn = false;
                EtDataType dataType = RTSPServiceWrapper.BytesToGazeData(data, dataSize, 0, gazePoint, out worn, gazePointDualLeft, gazePointDualRight, eyeStateLeft, eyeStateRight, eyelidLeft, eyelidRight);
                lock (gazeLock)
                {
                    gazeData.SetData(dataType, gazePoint, worn, gazePointDualLeft, gazePointDualRight, eyeStateLeft, eyeStateRight, eyelidLeft, eyelidRight, timestampMs, rtcpSynchronized);
                }
                gazeDataReceived = true;
                Debug.Log($"Decoded gaze data: {gazeData}");
            }
            else if (streamId == (int)StreamId.EyeEvents)
            {
                EyeEventType eventType;
                long startTimeNs;
                long endTimeNs;
                EyeEventDataType dataType = RTSPServiceWrapper.BytesToEyeEventData(data, dataSize, 0, out eventType, out startTimeNs, out endTimeNs, gazeEvent);
                eyeEventData.SetData(dataType, eventType, startTimeNs, endTimeNs, gazeEvent, timestampMs, rtcpSynchronized);
                Debug.Log($"Decoded eye event data: {eyeEventData}");
                if (dataType == EyeEventDataType.FixationOnsetData)
                {
                    if (eventType == EyeEventType.FixationOnset)
                    {
                        fixationOnSet = true;
                    }
                    else if (eventType == EyeEventType.SaccadeOnset)
                    {
                        fixationOnSet = false;
                    }
                }
            }
            else if (streamId == (int)StreamId.Imu)
            {
                ulong timestampNs;
                ImuDataType imuDataType = RTSPServiceWrapper.BytesToImuData(data, dataSize, 0, out timestampNs, accelData, gyroData, quatData);
                lock (imuLock)
                {
                    imuData.SetData(imuDataType, timestampNs, accelData, gyroData, quatData, timestampMs, rtcpSynchronized);
                }
                imuDataReceived = true;
                Debug.Log($"Decoded IMU data: {imuData}");
            }
        }
    }
}
