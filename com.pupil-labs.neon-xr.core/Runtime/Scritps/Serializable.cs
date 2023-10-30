using System;
using UnityEngine;
using UnityEngine.Events;

namespace PupilLabs.Serializable
{
    [Serializable]
    public class DVector3Event : UnityEvent<Vector3, Vector3> { }

    [Serializable]
    public class Vector3Event : UnityEvent<Vector3> { }

    [Serializable]
    public class Vector2Event : UnityEvent<Vector3> { }

    [Serializable]
    public class RayEvent : UnityEvent<Ray> { }

    [Serializable]
    public class GazeDataProviderEvent : UnityEvent<GazeDataProvider> { };

    [Serializable]
    public struct SVector3
    {
        public float x;
        public float y;
        public float z;

        public Vector3 ToVector3()
        {
            return new Vector3(x, y, z);
        }
    }

    [Serializable]
    public struct Pose
    {
        public SVector3 position;
        public SVector3 rotation;
    }

    [Serializable]
    public class AppConfig
    {
        public RTSPSettings rtspSettings;
        public SensorCalibration sensorCalibration;
    }

    [Serializable]
    public class RTSPSettings
    {
        public bool autoIp;
        public string deviceName;
        public string ip;
        public int port;
        public int dnsPort;
    }

    [Serializable]
    public class SensorCalibration
    {
        public Pose offset;
    }
}
