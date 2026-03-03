using UnityEngine;
using UnityEngine.Networking;

namespace PupilLabs
{
    public class HttpApi : MonoBehaviour
    {
        [SerializeField]
        private DeviceManager deviceManager;

        private long timeOffset = 0;
        
        protected virtual void OnEnable()
        {
            deviceManager.timeOffsetEstimated.AddListener(OnOffsetReceived);
        }

        void OnOffsetReceived(string ip, long offset)
        {
            timeOffset = offset;
            Debug.Log($"Offset updated for {ip}: {offset}");
        }

        public System.Collections.IEnumerator SendEvent(string name)
        {
            long beforeMs = RTSPServiceWrapper.UnixTimeMs();
            long time_in_neon = beforeMs - timeOffset;

            Debug.Log("Sending Neon Event...");

            using (UnityWebRequest www = UnityWebRequest.Post($"http://{deviceManager.SelectedDeviceIp}:8080/api/event", $"{{\"name\": {name}, \"timestamp\": {time_in_neon}}}", "application/json"))
            {
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

            Debug.Log("Neon Event sent.");
        }

        public System.Collections.IEnumerator RecordingStart()
        {
            long beforeMs = RTSPServiceWrapper.UnixTimeMs();
            long time_in_neon = beforeMs - timeOffset;

            Debug.Log("Starting Neon recording...");

            using (UnityWebRequest www = UnityWebRequest.Post($"http://{deviceManager.SelectedDeviceIp}:8080/api/event", "", "application/json"))
            {
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

            Debug.Log("Neon recording started.");
        }

        public System.Collections.IEnumerator RecordingStopAndSave()
        {
            long beforeMs = RTSPServiceWrapper.UnixTimeMs();
            long time_in_neon = beforeMs - timeOffset;

            Debug.Log("Stopping and saving Neon recording...");

            using (UnityWebRequest www = UnityWebRequest.Post($"http://{deviceManager.SelectedDeviceIp}:8080/api/event", "", "application/json"))
            {
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

            Debug.Log("Neon recording stopped and saved.");
        }

        public System.Collections.IEnumerator RecordingCancel()
        {
            long beforeMs = RTSPServiceWrapper.UnixTimeMs();
            long time_in_neon = beforeMs - timeOffset;

            Debug.Log("Cancelling Neon recording...");

            using (UnityWebRequest www = UnityWebRequest.Post($"http://{deviceManager.SelectedDeviceIp}:8080/api/event", "", "application/json"))
            {
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

            Debug.Log("Neon recording cancelled.");
        }
    }
}
