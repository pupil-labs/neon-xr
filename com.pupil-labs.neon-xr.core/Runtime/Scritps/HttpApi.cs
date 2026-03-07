using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace PupilLabs
{
    public class HttpApi : MonoBehaviour
    {
        [SerializeField]
        protected DeviceManager deviceManager;
        [SerializeField]
        protected int port = 8080;

        protected long timeOffset = 0;

        public long TimestampNeonNs { get { return (RTSPServiceWrapper.UnixTimeMs() - timeOffset) * 1_000_000; } }
        public bool OffsetReceived { get; private set; } = false;

        public UnityWebRequest CreatePostRequest(string endpoint, byte[] data, bool initDownloadHandler)
        {
            if (deviceManager.SelectedDeviceIp == null)
            {
                Debug.LogWarning("[HttpApi] Cannot create post request, no device selected in deviceManager.");
                return null;
            }

            string url = $"http://{deviceManager.SelectedDeviceIp}:{port}/api/{endpoint}";
            UnityWebRequest www = new UnityWebRequest(url, "POST");
            if (data != null)
            {
                www.uploadHandler = new UploadHandlerRaw(data);
            }
            if (initDownloadHandler)
            {
                www.downloadHandler = new DownloadHandlerBuffer();
            }
            www.SetRequestHeader("Content-Type", "application/json");
            return www;
        }

        public UnityWebRequest CreateEventRequest(byte[] data)
        {
            return CreatePostRequest("event", data, false);
        }

        public UnityWebRequest CreateEventRequest(string name, long timestamp)
        {
            string json = $"{{\"name\": \"{name}\", \"timestamp\": {timestamp}}}";
            byte[] payload = System.Text.Encoding.UTF8.GetBytes(json);

            return CreateEventRequest(payload);
        }

        protected virtual void OnEnable()
        {
            deviceManager.timeOffsetEstimated.AddListener(OnOffsetReceived);
        }

        protected virtual void OnDisable()
        {
            deviceManager.timeOffsetEstimated.RemoveListener(OnOffsetReceived);
        }

        protected virtual void OnOffsetReceived(string ip, long offset)
        {
            timeOffset = offset;
            OffsetReceived = true;
            Debug.Log($"Offset updated for {ip}: {offset}");
        }

        public IEnumerator SendEventRoutine(string name)
        {
            long timestamp = TimestampNeonNs;
            return SendEventRoutine(name, timestamp);
        }

        public IEnumerator SendEventRoutine(string name, long timestamp)
        {
            using (UnityWebRequest eventRequest = CreateEventRequest(name, timestamp))
            {
                if (eventRequest == null)
                {
                    yield break;
                }
                yield return eventRequest.SendWebRequest();
                if (eventRequest.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[HttpApi] Failed to send event '{name}': {eventRequest.error}");
                }
            }
        }

        public void SendEvent(string name)
        {
            StartCoroutine(SendEventRoutine(name));
        }

        public IEnumerator PostRequestRoutine(string endpoint, byte[] data = null)
        {
            using (UnityWebRequest eventRequest = CreatePostRequest(endpoint, data, false))
            {
                if (eventRequest == null)
                {
                    yield break;
                }
                yield return eventRequest.SendWebRequest();
                if (eventRequest.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[HttpApi] Failed to send post request to endpoint '{endpoint}': {eventRequest.error}");
                }
            }
        }

        public void RecordingStart()
        {
            StartCoroutine(PostRequestRoutine("recording:start"));
        }

        public void RecordingStopAndSave()
        {
            StartCoroutine(PostRequestRoutine("recording:stop_and_save"));
        }

        public void RecordingCancel()
        {
            StartCoroutine(PostRequestRoutine("recording:cancel"));
        }
    }
}
