using System;
using System.Threading.Tasks;
using UnityEngine;

namespace PupilLabs
{
    public struct EyeState
    {
        public EyeState(float pdl, Vector3 ecl, Vector3 oal, float pdr, Vector3 ecr, Vector3 oar)
        {
            pupilDiameterLeft = pdl;
            eyeballCenterLeft = ecl;
            opticalAxisLeft = oal;

            pupilDiameterRight = pdr;
            eyeballCenterRight = ecr;
            opticalAxisRight = oar;
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

        public abstract Task RunAsync();

        public abstract void Stop();

        public abstract Vector2 GazePoint { get; }
        public abstract Vector2 SmoothGazePoint { get; }

        public abstract bool EyeStateAvailable { get; }
        public abstract EyeState EyeState { get; }
        public abstract EyeState SmoothEyeState { get; }

        protected virtual void OnGazeDataReceived()
        {
            GazeDataReceived?.Invoke(this, EventArgs.Empty);
        }
    }
}
