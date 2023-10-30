using UnityEngine;

namespace PupilLabs
{
    public abstract class GazeDataProvider : MonoBehaviour
    {
        public abstract Vector3 RawGazeDir { get; }
        public abstract Vector2 RawGazePoint { get; }

        public UnityEngine.Pose GazeOrigin { get { return gazeOrigin; } }
        protected UnityEngine.Pose gazeOrigin;

        public virtual Ray GazeRay { get { return new Ray(gazeOrigin.position, gazeOrigin.rotation * RawGazeDir); } }

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

