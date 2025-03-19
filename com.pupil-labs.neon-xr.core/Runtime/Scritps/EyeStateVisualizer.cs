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
        protected Transform leftEyelidTop;
        [SerializeField]
        protected Transform leftEyelidBottom;
        [SerializeField]
        protected Transform rightEye;
        [SerializeField]
        protected Transform rightPupil;
        [SerializeField]
        protected Transform rightEyelidTop;
        [SerializeField]
        protected Transform rightEyelidBottom;

        protected bool eyeStateAvailable = false;
        protected EyeState eyeState;
        protected bool eyelidAvailable = false;
        protected Eyelid eyelid;

        public void OnGazeDataReady(GazeDataProvider gazeDataProvider)
        {
            eyeStateAvailable = gazeDataProvider.EyeStateAvailable;
            eyeState = gazeDataProvider.EyeState;
            eyelidAvailable = gazeDataProvider.EyelidAvailable;
            eyelid = gazeDataProvider.Eyelid;
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

            if (eyelidAvailable == false)
            {
                leftEyelidTop.gameObject.SetActive(false);
                leftEyelidBottom.gameObject.SetActive(false);
                rightEyelidTop.gameObject.SetActive(false);
                rightEyelidBottom.gameObject.SetActive(false);
                return;
            }

            leftEyelidTop.localEulerAngles = new Vector3(-eyelid.eyelidAngleTopLeft, 0, 0);
            leftEyelidBottom.localEulerAngles = new Vector3(-eyelid.eyelidAngleBottomLeft, 0, 0);
            rightEyelidTop.localEulerAngles = new Vector3(-eyelid.eyelidAngleTopRight, 0, 0);
            rightEyelidBottom.localEulerAngles = new Vector3(-eyelid.eyelidAngleBottomRight, 0, 0);
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
