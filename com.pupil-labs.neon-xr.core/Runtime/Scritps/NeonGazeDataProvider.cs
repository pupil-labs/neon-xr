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
        private bool gazeSmoothing = true;
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

            using (rtspClient = new RTSPClientWs(storage.Config.rtspSettings, rtspAutoReconnect, smoothingWindowSize))
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
                }

                rawGazeDir = CameraUtils.ImgPointToDir(rawGazePoint, storage.CameraIntrinsics.cameraMatrix, storage.CameraIntrinsics.distortionCoefficients);
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
