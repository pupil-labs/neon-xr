using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace PupilLabs
{
    public struct GazeData
    {
        public void SetData(EtDataType type, float[] gazePoint, bool worn, float[] gazePointDualRight, float[] eyeStateLeft, float[] eyeStateRight, float[] eyelidLeft, float[] eyelidRight, long timestampMs = 0, bool rtcpSynchronized = false)
        {
            this.type = type;
            this.gazePoint.x = gazePoint[0];
            this.gazePoint.y = gazePoint[1];
            this.worn = worn;
            this.gazePointDualRight.x = gazePointDualRight[0];
            this.gazePointDualRight.y = gazePointDualRight[1];
            this.eyeState.SetData(eyeStateLeft, eyeStateRight);
            this.eyelid.SetData(eyelidLeft, eyelidRight);
            this.timestampMs = timestampMs;
            this.rtcpSynchronized = rtcpSynchronized;
        }

        long timestampMs;
        bool rtcpSynchronized;
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
            eyeballCenterLeft.x = eyeStateLeft[1];
            eyeballCenterLeft.y = eyeStateLeft[2];
            eyeballCenterLeft.z = eyeStateLeft[3];
            opticalAxisLeft.x = eyeStateLeft[4];
            opticalAxisLeft.y = eyeStateLeft[5];
            opticalAxisLeft.z = eyeStateLeft[6];

            pupilDiameterRight = eyeStateRight[0];
            eyeballCenterRight.x = eyeStateRight[1];
            eyeballCenterRight.y = eyeStateRight[2];
            eyeballCenterRight.z = eyeStateRight[3];
            opticalAxisRight.x = eyeStateRight[4];
            opticalAxisRight.y = eyeStateRight[5];
            opticalAxisRight.z = eyeStateRight[6];
        }

        public float pupilDiameterLeft;
        public Vector3 eyeballCenterLeft;
        public Vector3 opticalAxisLeft;

        public float pupilDiameterRight;
        public Vector3 eyeballCenterRight;
        public Vector3 opticalAxisRight;
    }

    public abstract class RTSPClient : Disposable
    {
        public event EventHandler GazeDataReceived;

        protected CancellationTokenSource stopCts = null;

        public abstract Task RunAsync();

        public abstract GazeData GazeData { get; }

        public virtual void Stop()
        {
            try
            {
                stopCts?.Cancel();
            }
            catch (ObjectDisposedException e)
            {
                Debug.Log(e.Message);
            }
        }

        protected virtual void OnGazeDataReceived()
        {
            GazeDataReceived?.Invoke(this, EventArgs.Empty);
        }
    }
}
