using System;

namespace PupilLabs
{
    public abstract class Disposable : IDisposable //MRTK based
    {
        protected bool DisposedValue { get; private set; }

        protected virtual void DisposeManagedResources()
        {
            // Dispose managed state (managed objects)
        }

        protected virtual void DisposeUnmanagedResources()
        {
            // Free unmanaged resources (unmanaged objects)
            // Set large fields to null
        }

        private void Dispose(bool disposing)
        {
            if (!DisposedValue)
            {
                if (disposing)
                {
                    DisposeManagedResources();
                }

                DisposeUnmanagedResources();
                DisposedValue = true;
            }
        }

        ~Disposable()
        {
            // Do not change this code. Put cleanup code in 'DisposeManagedResources or DisposeUnmanagedResources' methods
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'DisposeManagedResources or DisposeUnmanagedResources' methods
            Dispose(disposing: true);
            System.GC.SuppressFinalize(this);
        }
    }
}
