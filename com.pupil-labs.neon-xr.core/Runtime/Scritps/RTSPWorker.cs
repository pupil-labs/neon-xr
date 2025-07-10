using System;

namespace PupilLabs
{
    public class RTSPWorker : Disposable
    {
        public Action<long, bool, byte, byte, uint, IntPtr> DataReceived;
        public Action<string> LogMessageReceived;

        internal byte Id { get; set; }

        protected override void DisposeUnmanagedResources()
        {
            RTSPServiceWrapper.StopWorker(Id);
        }

        public virtual void LogCallback(string message)
        {
            LogMessageReceived?.Invoke(message);
        }

        public virtual void DataCallback(long timestampMs, bool rtcpSynchronized, byte streamId, byte payloadFormat, uint dataSize, IntPtr data)
        {
            DataReceived?.Invoke(timestampMs, rtcpSynchronized, streamId, payloadFormat, dataSize, data);
        }
    }
}
