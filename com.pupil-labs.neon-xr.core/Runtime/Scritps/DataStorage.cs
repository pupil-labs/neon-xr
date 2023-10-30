using PupilLabs.Serializable;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace PupilLabs
{
    public class DataStorage : MonoBehaviour //TODO refactor
    {
        [SerializeField]
        private string configFileName = "config.json";
        [SerializeField]
        private string configAddress = "pl.config";
        [SerializeField]
        private string configDefaultsFileName = "configDefaults.json";
        [SerializeField]
        private string configDefaultsAddress = "pl.config.defaults";
        [SerializeField]
        private string calibrationFileName = "calibration.bin";
        [SerializeField]
        private string calibrationAddress = "pl.calibration";

        private AppConfig config = new AppConfig();
        private AppConfig configDefaults = new AppConfig();
        private CameraIntrinsics cameraIntrinsics = new CameraIntrinsics();

        public bool Ready { get; private set; } = false;
        public AppConfig Config { get { return config; } }
        public AppConfig ConfigDefaults { get { return configDefaults; } }
        public CameraIntrinsics CameraIntrinsics { get { return cameraIntrinsics; } }
        public string ConfigFilePath { get { return DataUtils.GetDataPath(configFileName); } }
        public string ConfigDefaultsFilePath { get { return DataUtils.GetDataPath(configFileName); } }
        public string CalibrationFilePath { get { return DataUtils.GetDataPath(configFileName); } }

        private async void Awake()
        {
            bool success = await LoadAll();
            if (success == false)
            {
                throw new Exception("Cannot load application data");
            }
            Debug.Log("[DataStorage] data loaded successfully");
        }

        public async Task WhenReady()
        {
            //not using Task.WhenAll because this might be called before tasks are set
            while (Ready == false)
            {
                await Task.Delay(100);
            }
        }

        private async Task<bool> LoadAll()
        {
            Ready = false;
            Task<bool>[] tasks = new Task<bool>[]{
                LoadData<AppConfig>(configFileName, config, configAddress),
                LoadData<AppConfig>(configDefaultsFileName, configDefaults, configDefaultsAddress),
                LoadCalibrationData(calibrationFileName, cameraIntrinsics, calibrationAddress)
            };
            bool[] results = await Task.WhenAll(tasks);
            Ready = true;
            return results.All(r => r);
        }

        private static async Task<bool> LoadData<T>(string fileName, T objectToOverwrite, string defaultContentAddress)
        {
            string filePath = await DataUtils.GetDataPath(fileName, defaultContentAddress);
            String json;
            using (StreamReader reader = File.OpenText(filePath))
            {
                json = await reader.ReadToEndAsync();
            }
            JsonUtility.FromJsonOverwrite(json, objectToOverwrite);
            Debug.Log($"[DataStorage] {typeof(T).FullName} object data loaded from: {filePath}");
            return true;
        }

        private static async Task<bool> LoadCalibrationData(string fileName, CameraIntrinsics ci, string defaultContentAddress)
        {
            //version-1B,serial-6B,cameraMatrix-72B,distortionCoeffs-64B
            //seems to be little endian
            byte[] bytes = null;
            string filePath = await DataUtils.GetDataPath(fileName, defaultContentAddress);
            using (FileStream SourceStream = File.Open(filePath, FileMode.Open))
            {
                bytes = new byte[SourceStream.Length];
                await SourceStream.ReadAsync(bytes, 0, bytes.Length);
            }
            if (bytes != null && bytes.Length > 143)
            {
                DataUtils.ParseCalibrationBytes(bytes, ci);
                Debug.Log($"[DataStorage] Calibration data loaded from: {filePath}");
                return true;
            }
            Debug.LogWarning("[DataStorage] Failed to load calibration data");
            return false;
        }
    }
}
