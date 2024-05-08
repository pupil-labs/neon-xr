using UnityEngine;

namespace PupilLabs
{
    public class EyeStateVisualizer : MonoBehaviour
    {
        [SerializeField]
        protected bool registerOnEnable = false;

        [SerializeField]
        protected Transform reference;
        [SerializeField]
        protected Transform leftEye;
        [SerializeField]
        protected Transform leftPupil;
        [SerializeField]
        protected Transform rightEye;
        [SerializeField]
        protected Transform rightPupil;

        protected bool eyeStateAvailable = false;
        protected EyeState eyeState;

        public void OnGazeDataReady(GazeDataProvider gazeDataProvider)
        {
            eyeStateAvailable = gazeDataProvider.EyeStateAvailable;
            eyeState = gazeDataProvider.EyeState;
        }

        protected virtual void LateUpdate()
        {
            if (eyeStateAvailable == false)
            {
                return;
            }

            if (reference == null)
            {
                reference = transform;
            }

            leftEye.position = reference.TransformPoint(eyeState.eyeballCenterLeft);
            leftEye.forward = reference.TransformDirection(eyeState.opticalAxisLeft);
            Vector3 tmp = leftPupil.localScale;
            tmp.x = tmp.z = eyeState.pupilDiameterLeft;
            leftPupil.localScale = tmp;

            rightEye.position = reference.TransformPoint(eyeState.eyeballCenterRight);
            rightEye.forward = reference.TransformDirection(eyeState.opticalAxisRight);
            tmp = rightPupil.localScale;
            tmp.x = tmp.z = eyeState.pupilDiameterRight;
            rightPupil.localScale = tmp;
        }

        protected virtual void OnEnable()
        {
            if (registerOnEnable)
            {
                ServiceLocator.Instance.GazeDataProvider.gazeDataReady.AddListener(OnGazeDataReady);
            }
        }

        protected virtual void OnDisable()
        {
            if (registerOnEnable)
            {
                ServiceLocator.Instance.GazeDataProvider.gazeDataReady.RemoveListener(OnGazeDataReady);
            }
        }
    }
}
