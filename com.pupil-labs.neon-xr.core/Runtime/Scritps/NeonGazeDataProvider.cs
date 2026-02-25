using PupilLabs.Serializable;
using System;
using System.Net;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PupilLabs
{
    public sealed class NeonGazeDataProvider : GazeDataProvider
    {
        [SerializeField]
        private DataStorage storage;
        [SerializeField]
        private DeviceManager deviceManager;
        [SerializeField]
        private bool simulationEnabled = false;
        [SerializeField]
        private InputActionReference simulatedLookHorizontal;
        [SerializeField]
        private InputActionReference simulatedLookVertical;
        [SerializeField]
        private float simulationSensitivity = 100f;
        [SerializeField]
        private Vector2 simulatedResolution = new Vector2(1600, 1200);
        [SerializeField]
        private bool simulateEyeState = false;
        [SerializeField]
        private Vector3 simulatedLeftEyePos = new Vector3(-0.032f, -0.02f, -0.02f);
        [SerializeField]
        private Vector3 simulatedRightEyePos = new Vector3(0.032f, -0.02f, -0.02f);
        [SerializeField]
        private float simulatedGazeDistance = 1f;
        [SerializeField]
        private float simulatedPupilDiameter = 0.004f;
        [SerializeField]
        private bool rtspAutoReconnect = true;

        private volatile bool dataReceived = false;
        private GazeData gazeData;

        public RTSPClient RTSPClient { get { return rtspClient; } }
        private RTSPClient rtspClient;
        public override Vector3 RawGazeDir { get { return rawGazeDir; } }
        private Vector3 rawGazeDir = Vector3.forward;
        public override Vector2 RawGazePoint { get { return rawGazePoint; } }
        private Vector2 rawGazePoint;
        public override bool EyeStateAvailable { get { return eyeStateAvailable; } }
        private bool eyeStateAvailable = false;
        public override EyeState RawEyeState { get { return rawEyeState; } }
        private EyeState rawEyeState;
        public override bool EyelidAvailable { get { return eyelidAvailable; } }
        private bool eyelidAvailable = false;
        public override Eyelid RawEyelid { get { return rawEyelid; } }
        private Eyelid rawEyelid;


        private async void Awake()
        {
            rawGazePoint = simulatedResolution * 0.5f;

            await storage.WhenReady();
            Serializable.Pose offset = storage.Config.sensorCalibration.offset;
            SetGazeOrigin(offset.position.ToVector3(), offset.rotation.ToVector3());

            if (simulationEnabled)
            {
                return;
            }

            do
            {
                RTSPSettings rtspSettings = storage.Config.rtspSettings;
                string ip = rtspSettings.ip;
                if (rtspSettings.autoIp)
                {
                    if (await deviceManager.Discover() && deviceManager.SelectAnyDevice())
                    {
                        ip = deviceManager.SelectedDeviceIp;
                        deviceManager.StartTimeOffsetEstimation();
                    }
                    else
                    {
                        Debug.Log("[NeonGazeDataProvider] no device discovered");
                        continue;
                    }
                }

                using (
                    rtspClient = rtspSettings.useUdp ?
                        new RTSPClientLive555(ip, rtspSettings.port) :
                        new RTSPClientWs(ip, rtspSettings.port)
                )
                {
                    rtspClient.GazeDataReceived += OnGazeDataReceived;
                    await rtspClient.RunAsync();
                }
                rtspClient = null;
            } while (rtspAutoReconnect == true);
        }

        public void OnGazeDataReceived(object sender, EventArgs e)
        {
            dataReceived = true;
        }

        private void Update()
        {
            if (dataReceived || simulationEnabled)
            {
                if (simulationEnabled)
                {
                    rawGazePoint = new Vector2(
                        Mathf.Clamp(rawGazePoint.x + simulatedLookHorizontal.action.ReadValue<float>() * simulationSensitivity, 0, simulatedResolution.x),
                        Mathf.Clamp(rawGazePoint.y - simulatedLookVertical.action.ReadValue<float>() * simulationSensitivity, 0, simulatedResolution.y)
                    );
                }
                else if (rtspClient != null)
                {
                    gazeData = rtspClient.GazeData;
                    rawGazePoint = gazeData.gazePoint;
                    eyeStateAvailable = gazeData.type >= EtDataType.EyeStateGazeData;
                    rawEyeState = gazeData.eyeState;
                    eyelidAvailable = gazeData.type >= EtDataType.EyeStateEyelidGazeData;
                    rawEyelid = gazeData.eyelid;
                    dataReceived = false;
                }

                rawGazeDir = CameraUtils.ImgPointToDir(rawGazePoint, storage.CameraIntrinsics.cameraMatrix, storage.CameraIntrinsics.distortionCoefficients);

                if (simulationEnabled && simulateEyeState)
                {
                    eyeStateAvailable = true;

                    Vector3 gazePoint = rawGazeDir.normalized * simulatedGazeDistance;

                    rawEyeState.eyeballCenterLeft = simulatedLeftEyePos;
                    rawEyeState.opticalAxisLeft = gazePoint - simulatedLeftEyePos;
                    rawEyeState.pupilDiameterLeft = simulatedPupilDiameter;

                    rawEyeState.eyeballCenterRight = simulatedRightEyePos;
                    rawEyeState.opticalAxisRight = gazePoint - simulatedRightEyePos;
                    rawEyeState.pupilDiameterRight = simulatedPupilDiameter;
                }

                OnGazeDataReady();
            }
        }

        private void OnDestroy()
        {
            rtspAutoReconnect = false;
            rtspClient?.Stop();
        }
    }
}
