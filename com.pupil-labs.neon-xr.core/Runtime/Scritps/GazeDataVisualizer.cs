using UnityEngine;

namespace PupilLabs
{
    public class GazeDataVisualizer : MonoBehaviour
    {
        [SerializeField]
        private bool rayVisible = false;
        [SerializeField]
        private float rayLength = 2f;
        [SerializeField]
        private LineRenderer lineRenderer;
        [SerializeField]
        private bool doRaycast = false;
        [SerializeField]
        private GameObject raycastPointer;
        [SerializeField]
        private LayerMask raycastMask = ~0;
        [SerializeField]
        private float raycastDistance = 10f;
        private Transform reference;
        private Vector3 localGazeOrigin = Vector3.zero;
        private Vector3 localGazeDirection = Vector3.forward;

        public bool RayVisible { get { return rayVisible; } set { rayVisible = value; } }

        public void OnGazeDataReady(GazeDataProvider gazeDataProvider)
        {
            localGazeOrigin = gazeDataProvider.GazeRay.origin;
            localGazeDirection = gazeDataProvider.GazeRay.direction;
        }

        private void Update()
        {
            if (reference == null)
            {
                reference = Camera.main.transform;
            }

            var worldOrigin = reference.TransformPoint(localGazeOrigin);
            var worldDirection = reference.TransformDirection(localGazeDirection);

            raycastPointer.SetActive(false);
            if (doRaycast)
            {
                RaycastHit hit;
                if (Physics.Raycast(worldOrigin, worldDirection, out hit, raycastDistance, raycastMask))
                {
                    raycastPointer.transform.position = hit.point;
                    raycastPointer.SetActive(true);
                }
            }

            if (rayVisible)
            {
                lineRenderer.SetPosition(0, worldOrigin);
                lineRenderer.SetPosition(1, worldOrigin + worldDirection * rayLength);
            }
            lineRenderer.enabled = rayVisible;
        }
    }
}
