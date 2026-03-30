using UnityEngine;

namespace PupilLabs
{
    public abstract class GazeDataProvider : MonoBehaviour
    {
        public abstract Vector2 RawGazePoint { get; }
        public abstract bool EyeStateAvailable { get; }
        public abstract EyeState RawEyeState { get; }
        public abstract bool EyelidAvailable { get; }
        public abstract Eyelid RawEyelid { get; }

        public Pose GazeOrigin { get { return gazeOrigin; } }
        protected Pose gazeOrigin = Pose.identity;

        public virtual Vector3 RawGazeDir { get { return PointToDir(RawGazePoint); } }
        public virtual Ray GazeRay { get { return DirToRay(RawGazeDir); } }
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

        public abstract Vector3 PointToDir(Vector2 point);

        public virtual Ray DirToRay(Vector3 rawGazeDir)
        {
            return new Ray(gazeOrigin.position, gazeOrigin.rotation * rawGazeDir.normalized);
        }

        public virtual Ray PointToRay(Vector2 point)
        {
            return DirToRay(PointToDir(point));
        }

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

