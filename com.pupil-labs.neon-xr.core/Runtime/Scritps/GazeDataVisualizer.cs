using UnityEngine;

namespace PupilLabs
{
    public class GazeDataVisualizer : MonoBehaviour
    {
        [SerializeField]
        protected bool registerOnEnable = false;

        [SerializeField]
        protected Transform reference;

        [SerializeField]
        protected bool rayVisible = false;
        [SerializeField]
        protected float rayLength = 2f;
        [SerializeField]
        protected LineRenderer lineRenderer;

        [SerializeField]
        protected bool doRaycast = false;
        [SerializeField]
        protected bool raycastPointerVisible = false;
        [SerializeField]
        protected GameObject raycastPointer;
        [SerializeField]
        protected LayerMask raycastMask = ~0;
        [SerializeField]
        protected float raycastDistance = 10f;

        public Serializable.RaycastHitEvent onHit;

        protected Vector3 localGazeOrigin = Vector3.zero;
        protected Vector3 localGazeDirection = Vector3.forward;

        public bool RayVisible { get { return rayVisible; } set { rayVisible = value; } }
        public bool DoRaycast { get { return doRaycast; } set { doRaycast = value; } }
        public bool RaycastPointerVisible { get { return raycastPointerVisible; } set { raycastPointerVisible = value; } }

        protected virtual void OnEnable()
        {
            if (registerOnEnable)
            {
                ServiceLocator.Instance.GazeDataProvider.gazeDataReady.AddListener(OnGazeDataReady);
            }
        }

        public void OnGazeDataReady(GazeDataProvider gazeDataProvider)
        {
            localGazeOrigin = gazeDataProvider.GazeRay.origin;
            localGazeDirection = gazeDataProvider.GazeRay.direction;
        }

        protected virtual void LateUpdate()
        {
            if (reference == null)
            {
                reference = Camera.main.transform;
            }

            var worldOrigin = reference.TransformPoint(localGazeOrigin);
            var worldDirection = reference.TransformDirection(localGazeDirection);

            float dist = UpdateRaycast(worldOrigin, worldDirection);

            if (lineRenderer != null)
            {
                lineRenderer.enabled = false;
            }
            if (rayVisible)
            {
                lineRenderer.SetPosition(0, worldOrigin);
                lineRenderer.SetPosition(1, worldOrigin + worldDirection * (dist > 0 ? dist : rayLength));
                lineRenderer.enabled = true;
            }
        }

        protected virtual float UpdateRaycast(Vector3 worldOrigin, Vector3 worldDirection)
        {
            if (raycastPointer != null)
            {
                raycastPointer.SetActive(false);
            }
            if (doRaycast)
            {
                RaycastHit hit;
                if (Physics.Raycast(worldOrigin, worldDirection, out hit, raycastDistance, raycastMask))
                {
                    onHit.Invoke(hit);
                    raycastPointer.transform.position = hit.point;
                    raycastPointer.SetActive(raycastPointerVisible);
                    return hit.distance;
                }
            }
            return -1f;
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
