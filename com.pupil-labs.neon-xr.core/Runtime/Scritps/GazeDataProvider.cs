using UnityEngine;

namespace PupilLabs
{
    public abstract class GazeDataProvider : MonoBehaviour
    {
        public abstract Vector3 RawGazeDir { get; }
        public abstract Vector2 RawGazePoint { get; }
        public abstract bool EyeStateAvailable { get; }
        public abstract EyeState RawEyeState { get; }
        public abstract bool EyelidAvailable { get; }
        public abstract Eyelid RawEyelid { get; }

        public Pose GazeOrigin { get { return gazeOrigin; } }
        protected Pose gazeOrigin;

        public virtual Ray GazeRay { get { return new Ray(gazeOrigin.position, gazeOrigin.rotation * RawGazeDir); } }
        public virtual EyeState EyeState
        {
            get
            {
                EyeState state = RawEyeState;
                state.eyeballCenterLeft = gazeOrigin.rotation * state.eyeballCenterLeft + gazeOrigin.position;
                state.opticalAxisLeft = gazeOrigin.rotation * state.opticalAxisLeft;
                state.eyeballCenterRight = gazeOrigin.rotation * state.eyeballCenterRight + gazeOrigin.position;
                state.opticalAxisRight = gazeOrigin.rotation * state.opticalAxisRight;
                return state;
            }
        }
        public virtual Eyelid Eyelid
        {
            get
            {
                return RawEyelid;
            }
        }

        public Serializable.GazeDataProviderEvent gazeDataReady;

        protected virtual void OnGazeDataReady()
        {
            gazeDataReady.Invoke(this);
        }

        public virtual void SetGazeOrigin(Vector3 pos, Vector3 rot)
        {
            gazeOrigin.position = pos;
            gazeOrigin.rotation = Quaternion.Euler(rot);
        }
    }
}

