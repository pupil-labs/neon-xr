using System;
using System.Runtime.InteropServices;

namespace PupilLabs
{
    public static class Live555Wrapper
    {
        public delegate void LogCallback([MarshalAs(UnmanagedType.LPStr)] string message);
        public delegate void RawDataCallback(long timestampMs, uint dataSize, IntPtr data);

        private static LogCallback _logCallback;
        private static RawDataCallback _gazeCallback;
        private static RawDataCallback _worldCallback;

        [DllImport("Live555Wrapper")]
        static extern void CStart([MarshalAs(UnmanagedType.LPStr)] string url, LogCallback logCallback, RawDataCallback gazeCallback, RawDataCallback worldCallback);

        [DllImport("Live555Wrapper")]
        static extern void CStop();

        public static void Start(string url, LogCallback logCallback, RawDataCallback gazeCallback, RawDataCallback worldCallback)
        {
            _logCallback = logCallback;
            _gazeCallback = gazeCallback;
            _worldCallback = worldCallback;

            LogCallback lc = _logCallback == null ? null : InvokeLogCallback;
            RawDataCallback gc = _gazeCallback == null ? null : InvokeGazeCallback;
            RawDataCallback wc = _worldCallback == null ? null : InvokeWorldCallback;

            CStart(url, lc, gc, wc);
        }

        public static void Stop()
        {
            CStop();
        }

        [AOT.MonoPInvokeCallback(typeof(LogCallback))]
        static void InvokeLogCallback(string message)
        {
            _logCallback.Invoke(message);
        }

        [AOT.MonoPInvokeCallback(typeof(RawDataCallback))]
        static void InvokeGazeCallback(long timestampMs, uint dataSize, IntPtr data)
        {
            _gazeCallback.Invoke(timestampMs, dataSize, data);
        }

        [AOT.MonoPInvokeCallback(typeof(RawDataCallback))]
        static void InvokeWorldCallback(long timestampMs, uint dataSize, IntPtr data)
        {
            _worldCallback.Invoke(timestampMs, dataSize, data);
        }
    }
}
