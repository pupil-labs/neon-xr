using System;
using System.Threading.Tasks;
using UnityEngine;

namespace PupilLabs
{
    public struct EyeLid
    {
        public EyeLid(float tl_angle, float bl_angle, float apl, float tr_angle, float br_angle, float apr)
        {
            eyelid_angle_top_left = tl_angle;
            eyelid_angle_bottom_left = bl_angle;
            eyelid_aperture_left = apl;

            eyelid_angle_top_right = tr_angle;
            eyelid_angle_bottom_right = br_angle;
            eyelid_aperture_right = apr;
        }

        public float eyelid_angle_top_left;
        public float eyelid_angle_bottom_left;
        public float eyelid_aperture_left;
        public float eyelid_angle_top_right;
        public float eyelid_angle_bottom_right;
        public float eyelid_aperture_right;
    }

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

        public abstract bool EyeLidAvailable { get; }
        public abstract EyeLid EyeLid { get; }
        public abstract EyeLid SmoothEyeLid { get; }
        public abstract bool EyeStateAvailable { get; }
        public abstract EyeState EyeState { get; }
        public abstract EyeState SmoothEyeState { get; }

        protected virtual void OnGazeDataReceived()
        {
            GazeDataReceived?.Invoke(this, EventArgs.Empty);
        }
    }
}
