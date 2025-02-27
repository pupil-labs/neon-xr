using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PupilLabs
{
    public sealed class NeonGazeDataProvider : GazeDataProvider
    {
        [SerializeField]
        private DataStorage storage;
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
        private bool simulatEyeLid = false;
        [SerializeField]
        private float simulatedGazeDistance = 1f;
        [SerializeField]
        private float simulatedPupilDiameter = 0.004f;
        [SerializeField]
        private bool gazeSmoothing = true;
        [SerializeField]
        private bool eyeStateSmoothing = true;
        [SerializeField]
        private bool eyeLidSmoothing = true;
        [SerializeField]
        private int smoothingWindowSize = 10;
        [SerializeField]
        private bool rtspAutoReconnect = true;

        private RTSPClient rtspClient;
        private volatile bool dataReceived = false;

        public override Vector3 RawGazeDir { get { return rawGazeDir; } }
        private Vector3 rawGazeDir = Vector3.forward;
        public override Vector2 RawGazePoint { get { return rawGazePoint; } }
        private Vector2 rawGazePoint;
        public override bool EyeStateAvailable { get { return eyeStateAvailable; } }
        private bool eyeStateAvailable = false;
        public override EyeState RawEyeState { get { return rawEyeState; } }
        private EyeState rawEyeState;
        public override bool EyeLidAvailable { get { return eyeLidAvailable; } }
        private bool eyeLidAvailable = false;
        public override EyeLid RawEyeLid { get { return rawEyeLid; } }
        private EyeLid rawEyeLid;


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

            using (rtspClient = new RTSPClientWs(storage.Config.rtspSettings, rtspAutoReconnect, smoothingWindowSize, smoothingWindowSize))
            {
                rtspClient.GazeDataReceived += OnGazeDataReceived;
                await rtspClient.RunAsync();
            }
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
                    rawGazePoint = gazeSmoothing ? rtspClient.SmoothGazePoint : rtspClient.GazePoint;
                    dataReceived = false;

                    eyeStateAvailable = rtspClient.EyeStateAvailable;
                    if (eyeStateAvailable)
                    {
                        rawEyeState = eyeStateSmoothing ? rtspClient.SmoothEyeState : rtspClient.EyeState;
                    }

                    eyeLidAvailable = rtspClient.EyeLidAvailable;
                    if (eyeLidAvailable)
                    {
                        rawEyeLid = eyeLidSmoothing ? rtspClient.SmoothEyeLid : rtspClient.EyeLid;
                    }
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

                if (simulationEnabled && simulatEyeLid)
                {
                    eyeLidAvailable = true;

                    rawEyeLid.eyelid_angle_top_left = 0;
                    rawEyeLid.eyelid_angle_bottom_left = 0;
                    rawEyeLid.eyelid_aperture_left = 0;

                    rawEyeLid.eyelid_angle_top_right = 0;
                    rawEyeLid.eyelid_angle_bottom_right = 0;
                    rawEyeLid.eyelid_aperture_right = 0;
                }

                OnGazeDataReady();
            }
        }

        private void OnDestroy()
        {
            if (rtspClient != null)
            {
                rtspClient.Stop();
            }
        }
    }
}
