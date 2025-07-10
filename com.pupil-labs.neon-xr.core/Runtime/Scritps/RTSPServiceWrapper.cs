using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace PupilLabs
{
    public enum StreamId
    {
        Imu = 0,
        World = 1,
        Gaze = 2,
        EyeEvents = 3,
        Eyes = 4
    }

    public enum RtpPayloadFormat
    {
        Video = 96,
        Audio = 97,
        Gaze = 99,
        Imu = 100,
        EyeEvents = 101
    }
    public enum EtDataType
    {
        GazeData = 0,
        DualMonocularGazeData = 1,
        EyeStateGazeData = 2,
        EyeStateEyelidGazeData = 3,
        Unknown = -1
    }

    public static class RTSPServiceWrapper
    {
        delegate void LogCallback([MarshalAs(UnmanagedType.LPStr)] string message, IntPtr userData);
        delegate void RawDataCallback(long timestampMs, bool rtcpSynchronized, byte streamId, byte payloadFormat, uint dataSize, IntPtr data, IntPtr userData);


        static Dictionary<byte, GCHandle> handleDict = new Dictionary<byte, GCHandle>();

        [DllImport("pl-rtsp-service")]
        static extern short pl_acquire_worker();

        [DllImport("pl-rtsp-service")]
        static extern int pl_start_worker(byte workerId, [MarshalAs(UnmanagedType.LPStr)] string url, byte streamMask, LogCallback logCallback, RawDataCallback dataCallback, IntPtr userData);

        [DllImport("pl-rtsp-service")]
        static extern void pl_stop_worker(byte workerId, bool release);

        [DllImport("pl-rtsp-service")]
        static extern void pl_stop_service();

        [DllImport("pl-rtsp-service")]
        static extern EtDataType pl_bytes_to_eye_tracking_data(
            IntPtr data,
            uint dataSize,
            uint offset,
            [Out] float[] gazePoint,          // length 2
            [Out, MarshalAs(UnmanagedType.I1)] out bool worn,
            [Out] float[] gazePointDualRight, // length 2
            [Out] float[] eyeStateLeft,       // length 7
            [Out] float[] eyeStateRight,      // length 7
            [Out] float[] eyelidLeft,         // length 3
            [Out] float[] eyelidRight         // length 3
        );

        public static EtDataType BytesToGazeData(
            IntPtr data,
            uint dataSize,
            uint offset,
            float[] gazePoint,
            out bool worn,
            float[] gazePointDualRight,
            float[] eyeStateLeft,
            float[] eyeStateRight,
            float[] eyelidLeft,
            float[] eyelidRight)
        {
            EtDataType res = pl_bytes_to_eye_tracking_data(
                data, dataSize, offset,
                gazePoint, out worn, gazePointDualRight,
                eyeStateLeft, eyeStateRight,
                eyelidLeft, eyelidRight
            );

            eyeStateLeft[0] *= 0.001f;
            eyeStateLeft[1] *= 0.001f;
            eyeStateLeft[2] *= -0.001f;
            eyeStateLeft[3] *= 0.001f;
            eyeStateLeft[5] *= -1f;

            eyeStateRight[0] *= 0.001f;
            eyeStateRight[1] *= 0.001f;
            eyeStateRight[2] *= -0.001f;
            eyeStateRight[3] *= 0.001f;
            eyeStateRight[5] *= -1f;
            return res;
        }

        public static EtDataType BytesToGazeData(
            byte[] data,
            uint dataSize,
            uint offset,
            float[] gazePoint,
            out bool worn,
            float[] gazePointDualRight,
            float[] eyeStateLeft,
            float[] eyeStateRight,
            float[] eyelidLeft,
            float[] eyelidRight)
        {

            GCHandle handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            try
            {
                return BytesToGazeData(
                    handle.AddrOfPinnedObject(), dataSize, offset,
                    gazePoint, out worn, gazePointDualRight,
                    eyeStateLeft, eyeStateRight,
                    eyelidLeft, eyelidRight
                );
            }
            finally
            {
                handle.Free();
            }
        }

        public static T StartWorker<T>(string url, byte streamMask) where T : RTSPWorker, new()
        {
            short workerId = pl_acquire_worker();
            if (workerId != -1)
            {
                byte bWorkerId = (byte)workerId;
                T worker = new T();
                worker.Id = bWorkerId;
                GCHandle handle = GCHandle.Alloc(worker, GCHandleType.Weak);
                int res = pl_start_worker(bWorkerId, url, streamMask, InvokeLogCallback, InvokeDataCallback, GCHandle.ToIntPtr(handle));
                if (res == 0)
                {
                    handleDict.Add(bWorkerId, handle);
                    return worker;
                }
                handle.Free();
            }
            return null;
        }

        public static void StopWorker(byte id)
        {
            pl_stop_worker(id, true);
            GCHandle handle;
            if (handleDict.TryGetValue(id, out handle))
            {
                handle.Free();
                handleDict.Remove(id);
            }
        }

        public static void Stop()
        {
            pl_stop_service();
        }

        [AOT.MonoPInvokeCallback(typeof(LogCallback))]
        static void InvokeLogCallback(string message, IntPtr userData)
        {
            GCHandle handle = GCHandle.FromIntPtr(userData);
            RTSPWorker worker = (RTSPWorker)handle.Target;
            worker?.LogCallback(message);
        }

        [AOT.MonoPInvokeCallback(typeof(RawDataCallback))]
        static void InvokeDataCallback(long timestampMs, bool rtcpSynchronized, byte streamId, byte payloadFormat, uint dataSize, IntPtr data, IntPtr userData)
        {
            GCHandle handle = GCHandle.FromIntPtr(userData);
            RTSPWorker worker = (RTSPWorker)handle.Target;
            worker?.DataCallback(timestampMs, rtcpSynchronized, streamId, payloadFormat, dataSize, data);
        }
    }
}
