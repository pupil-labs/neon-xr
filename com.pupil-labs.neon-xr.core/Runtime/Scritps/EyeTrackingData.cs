using UnityEngine;

namespace PupilLabs
{
    public struct GazeData
    {
        public void SetData(EtDataType type, float[] gazePoint, bool worn, float[] gazePointDualRight, float[] eyeStateLeft, float[] eyeStateRight, float[] eyelidLeft, float[] eyelidRight, long timestampMs = 0, bool rtcpSynchronized = false)
        {
            this.type = type;
            this.gazePoint.Set(gazePoint[0], gazePoint[1]);
            this.worn = worn;
            this.gazePointDualRight.Set(gazePointDualRight[0], gazePointDualRight[1]);
            this.eyeState.SetData(eyeStateLeft, eyeStateRight);
            this.eyelid.SetData(eyelidLeft, eyelidRight);
            this.timestampMs = timestampMs;
            this.rtcpSynchronized = rtcpSynchronized;
        }

        public override string ToString()
        {
            return $"GazeData(type={type}, gazePoint={gazePoint}, worn={worn}), timestampMs={timestampMs}, rtcpSynchronized={rtcpSynchronized}";
        }

        public long timestampMs;
        public bool rtcpSynchronized;
        public EtDataType type;
        public Vector2 gazePoint;
        public bool worn;
        public Vector2 gazePointDualRight;
        public EyeState eyeState;
        public Eyelid eyelid;
    }

    public struct Eyelid
    {
        public void SetData(float[] eyelidLeft, float[] eyelidRight)
        {
            eyelidAngleTopLeft = eyelidLeft[0];
            eyelidAngleBottomLeft = eyelidLeft[1];
            eyelidApertureLeft = eyelidLeft[2];

            eyelidAngleTopRight = eyelidRight[0];
            eyelidAngleBottomRight = eyelidRight[1];
            eyelidApertureRight = eyelidRight[2];
        }

        public float eyelidAngleTopLeft;
        public float eyelidAngleBottomLeft;
        public float eyelidApertureLeft;

        public float eyelidAngleTopRight;
        public float eyelidAngleBottomRight;
        public float eyelidApertureRight;
    }

    public struct EyeState
    {
        public void SetData(float[] eyeStateLeft, float[] eyeStateRight)
        {
            pupilDiameterLeft = eyeStateLeft[0];
            eyeballCenterLeft.Set(eyeStateLeft[1], eyeStateLeft[2], eyeStateLeft[3]);
            opticalAxisLeft.Set(eyeStateLeft[4], eyeStateLeft[5], eyeStateLeft[6]);

            pupilDiameterRight = eyeStateRight[0];
            eyeballCenterRight.Set(eyeStateRight[1], eyeStateRight[2], eyeStateRight[3]);
            opticalAxisRight.Set(eyeStateRight[4], eyeStateRight[5], eyeStateRight[6]);
        }

        public float pupilDiameterLeft;
        public Vector3 eyeballCenterLeft;
        public Vector3 opticalAxisLeft;

        public float pupilDiameterRight;
        public Vector3 eyeballCenterRight;
        public Vector3 opticalAxisRight;
    }

    public struct EyeEventData
    {
        public long timestampMs;
        public bool rtcpSynchronized;
        public EyeEventDataType type;
        public EyeEventType eventType;
        public long startTimeNs;
        public long endTimeNs;
        public Vector2 startGazePoint;
        public Vector2 endGazePoint;
        public Vector2 meanGazePoint;
        public float amplitudePixels;
        public float amplitudeAngleDeg;
        public float meanVelocity;
        public float maxVelocity;

        public void SetData(EyeEventDataType type, EyeEventType eventType, long startTimeNs, long endTimeNs, float[] gazeEvent, long timestampMs = 0, bool rtcpSynchronized = false)
        {
            this.timestampMs = timestampMs;
            this.rtcpSynchronized = rtcpSynchronized;
            this.type = type;
            this.eventType = eventType;
            this.startTimeNs = startTimeNs;
            this.endTimeNs = endTimeNs;
            this.startGazePoint.Set(gazeEvent[0], gazeEvent[1]);
            this.endGazePoint.Set(gazeEvent[2], gazeEvent[3]);
            this.meanGazePoint.Set(gazeEvent[4], gazeEvent[5]);
            this.amplitudePixels = gazeEvent[6];
            this.amplitudeAngleDeg = gazeEvent[7];
            this.meanVelocity = gazeEvent[8];
            this.maxVelocity = gazeEvent[9];
        }

        public override string ToString()
        {
            return $"EyeEventData(type={type}, eventType={eventType}, startTimeNs={startTimeNs}, timestampMs={timestampMs}, rtcpSynchronized={rtcpSynchronized})";
        }
    }

    public struct ImuData
    {
        public ImuDataType type; //currently always 0
        public long timestampMs;
        public bool rtcpSynchronized;
        public ulong timestampNs;
        public Vector3 accelData;
        public Vector3 gyroData;
        public Quaternion quatData;

        public void SetData(ImuDataType type, ulong timestampNs, float[] accelData, float[] gyroData, float[] quatData, long timestampMs = 0, bool rtcpSynchronized = false)
        {
            this.timestampMs = timestampMs;
            this.rtcpSynchronized = rtcpSynchronized;
            this.type = type;
            this.timestampNs = timestampNs;
            this.accelData.Set(accelData[0], accelData[1], accelData[2]);
            this.gyroData.Set(gyroData[0], gyroData[1], gyroData[2]);
            this.quatData.Set(quatData[1], quatData[2], quatData[3], quatData[0]);
        }

        public override string ToString()
        {
            return $"ImuData(type={type}, timestampNs={timestampNs} accelData={accelData}, gyroData={gyroData}, quatData={quatData}, timestampMs={timestampMs}, rtcpSynchronized={rtcpSynchronized})";
        }
    }
}