using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;

public class NativeDataWriter : IDisposable
{
    protected readonly struct Frame
    {
        public readonly byte[] Buffer;
        public readonly int Length;

        public Frame(byte[] buffer, int length)
        {
            Buffer = buffer;
            Length = length;
        }
    }

    private readonly BlockingCollection<Frame> _queue = new BlockingCollection<Frame>();
    private readonly FileStream _fileStream;
    private readonly Thread _writerThread;

    public NativeDataWriter(string filePath, int bufferSize = 65536)
    {
        _fileStream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read, bufferSize);

        _writerThread = new Thread(ProcessQueue)
        {
            IsBackground = true,
            Name = "NativeDataWriterThread"
        };
        _writerThread.Start();
    }

    public void EnqueueData(long timestampMs, uint dataSize, IntPtr data)
    {
        int totalSize = 12 + (int)dataSize;

        byte[] buffer = ArrayPool<byte>.Shared.Rent(totalSize);

        BinaryPrimitives.WriteInt64BigEndian(buffer.AsSpan(0, 8), timestampMs);
        BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(8, 4), dataSize);

        if (data != IntPtr.Zero && dataSize > 0)
        {
            unsafe
            {
                var unmanagedData = new ReadOnlySpan<byte>(data.ToPointer(), (int)dataSize);
                unmanagedData.CopyTo(buffer.AsSpan(12));
            }
        }

        _queue.Add(new Frame(buffer, totalSize));
    }

    private void ProcessQueue()
    {
        foreach (Frame rawFrame in _queue.GetConsumingEnumerable())
        {
            Frame finalFrame = rawFrame;

            try
            {
                finalFrame = PostProcessFrame(rawFrame);

                _fileStream.Write(finalFrame.Buffer, 0, finalFrame.Length);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(finalFrame.Buffer);

                if (finalFrame.Buffer != rawFrame.Buffer)
                {
                    ArrayPool<byte>.Shared.Return(rawFrame.Buffer);
                }
            }
        }
    }

    protected virtual Frame PostProcessFrame(Frame rawFrame)
    {
        //byte[] postProcessedBuffer = ArrayPool<byte>.Shared.Rent(length);
        //return new Frame(postProcessedBuffer, length);
        return rawFrame;
    }

    public virtual void Dispose()
    {
        _queue.CompleteAdding();
        _writerThread.Join(500);
        _fileStream.Dispose();
        _queue.Dispose();
    }
}