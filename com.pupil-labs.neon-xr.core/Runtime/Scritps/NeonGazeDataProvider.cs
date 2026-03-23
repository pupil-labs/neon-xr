using UnityEngine;
using UnityEngine.InputSystem;

namespace PupilLabs
{
    public sealed class NeonGazeDataProvider : GazeDataProvider
    {
        [SerializeField]
        private DataStorage storage;
        [SerializeField]
        private GazeDataSource gazeDataSource;
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

        private volatile bool dataReceived = false;
        private IGazeDataSource lockedGazeDataSource = null;

        public GazeData RawGazeData { get { return rawGazeData; } }
        private GazeData rawGazeData;
        public bool Ready { get; private set; } = false;
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
            if (gazeDataSource != null)
            {
                lockedGazeDataSource = gazeDataSource;
            }

            rawGazePoint = simulatedResolution * 0.5f;

            await storage.WhenReady();
            Serializable.Pose offset = storage.Config.sensorCalibration.offset;
            SetGazeOrigin(offset.position.ToVector3(), offset.rotation.ToVector3());

            Ready = true;
        }

        private void OnGazeDataReceived()
        {
            dataReceived = true;
        }

        public override Vector3 PointToDir(Vector2 gazePoint)
        {
            if (Ready == false)
            {
                return Vector3.forward;
            }
            return CameraUtils.ImgPointToDir(gazePoint, storage.CameraIntrinsics.cameraMatrix, storage.CameraIntrinsics.distortionCoefficients);
        }

        private void Update()
        {
            if (Ready == false)
            {
                return;
            }
            if (dataReceived || simulationEnabled)
            {
                if (simulationEnabled)
                {
                    rawGazePoint = new Vector2(
                        Mathf.Clamp(rawGazePoint.x + simulatedLookHorizontal.action.ReadValue<float>() * simulationSensitivity, 0, simulatedResolution.x),
                        Mathf.Clamp(rawGazePoint.y - simulatedLookVertical.action.ReadValue<float>() * simulationSensitivity, 0, simulatedResolution.y)
                    );
                }
                else if (lockedGazeDataSource != null)
                {
                    rawGazeData = lockedGazeDataSource.GazeData;
                    rawGazePoint = rawGazeData.gazePoint;
                    eyeStateAvailable = rawGazeData.type >= EtDataType.EyeStateGazeData;
                    rawEyeState = rawGazeData.eyeState;
                    eyelidAvailable = rawGazeData.type >= EtDataType.EyeStateEyelidGazeData;
                    rawEyelid = rawGazeData.eyelid;
                    dataReceived = false;
                }

                if (simulationEnabled && simulateEyeState)
                {
                    eyeStateAvailable = true;

                    Vector3 gazePoint = RawGazeDir.normalized * simulatedGazeDistance;

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

        private void OnEnable()
        {
            dataReceived = false;
            if (lockedGazeDataSource != null)
            {
                lockedGazeDataSource.GazeDataReceived += OnGazeDataReceived;
            }
        }

        private void OnDisable()
        {
            if (lockedGazeDataSource != null)
            {
                lockedGazeDataSource.GazeDataReceived -= OnGazeDataReceived;
            }
        }
    }
}
