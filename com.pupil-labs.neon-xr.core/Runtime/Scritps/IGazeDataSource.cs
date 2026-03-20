using System;

namespace PupilLabs
{
    public interface IGazeDataSource
    {
        public event Action GazeDataReceived;
        public abstract GazeData GazeData { get; }
    }
}