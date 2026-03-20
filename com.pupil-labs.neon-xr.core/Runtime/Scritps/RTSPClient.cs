using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace PupilLabs
{
    public delegate void DataHandler(
        long timestampMs,
        bool rtcpSynchronized,
        byte streamId,
        byte payloadFormat,
        uint dataSize,
        IntPtr data);

    public abstract class RTSPClient : Disposable
    {
        public event DataHandler DataReceived;

        protected CancellationTokenSource stopCts = null;

        public abstract Task RunAsync();

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

        protected virtual unsafe void OnDataReceived(long timestampMs, bool rtcpSynchronized, byte streamId, byte payloadFormat, uint dataSize, uint dataOffset, byte[] data)
        {
            fixed (byte* p = &data[dataOffset])
            {
                OnDataReceived(timestampMs, rtcpSynchronized, streamId, payloadFormat, dataSize - dataOffset, (IntPtr)p);
            }
        }

        protected virtual void OnDataReceived(long timestampMs, bool rtcpSynchronized, byte streamId, byte payloadFormat, uint dataSize, IntPtr data)
        {
            DataHandler handler = DataReceived;
            if (handler != null)
            {
                handler(timestampMs, rtcpSynchronized, streamId, payloadFormat, dataSize, data);
            }
        }
    }
}
