using UnityEngine;
using UnityEngine.Networking;

namespace PupilLabs
{
    public class GazeDataVisualizer : MonoBehaviour
    {
        [SerializeField]
        protected bool registerOnEnable = false;
        [SerializeField]
        private DeviceManager deviceManager;

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

        private long timeOffset = 0;

        protected virtual void OnEnable()
        {
            if (registerOnEnable)
            {
                ServiceLocator.Instance.GazeDataProvider.gazeDataReady.AddListener(OnGazeDataReady);
                deviceManager.timeOffsetEstimated.AddListener(OnOffsetReceived);
            }
        }

        void OnOffsetReceived(string ip, long offset)
        {
            timeOffset = offset;
            Debug.Log($"Offset updated for {ip}: {offset}");

            StartCoroutine(SendEvent(timeOffset, "xr_stream_start"));

            Debug.Log("Sent Event for Stream start");
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
                    onHit.Invoke(hit.transform, hit.point, hit.textureCoord);
                    raycastPointer.transform.position = hit.point;
                    raycastPointer.SetActive(raycastPointerVisible);
                    return hit.distance;
                }
            }
            return -1f;
        }

        System.Collections.IEnumerator SendEvent(long timeOffset, string name)
        {
            long beforeMs = RTSPServiceWrapper.UnixTimeMs();
            long time_in_neon = beforeMs - timeOffset;

            Debug.Log("Sending event...");

            using (UnityWebRequest www = UnityWebRequest.Post($"http://{deviceManager.SelectedDeviceIp}:8080/api/event", $"{{\"name\": {name}, \"field2\": {time_in_neon}}}", "application/json"))
            {
                //www.certificateHandler = new BypassCertificate();
                yield return www.SendWebRequest();

                if (www.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError(www.error);
                }
                else
                {
                    Debug.Log("Form upload complete!");
                }
            }

            Debug.Log("Event sent");
        }

        protected virtual void OnDisable()
        {
            if (registerOnEnable)
            {
                StartCoroutine(SendEvent(timeOffset, "xr_stream_end"));
                Debug.Log("Sent Event for Stream end");

                ServiceLocator.Instance.GazeDataProvider.gazeDataReady.RemoveListener(OnGazeDataReady);
                deviceManager.timeOffsetEstimated.RemoveListener(OnOffsetReceived);
            }
        }
    }
}
