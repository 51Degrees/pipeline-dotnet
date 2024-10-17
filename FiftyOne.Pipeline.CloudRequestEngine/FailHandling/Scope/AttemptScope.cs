using FiftyOne.Pipeline.CloudRequestEngine.FailHandling.ExceptionCaching;
using System;
using System.Collections.Generic;
using System.Text;

namespace FiftyOne.Pipeline.CloudRequestEngine.FailHandling.Scope
{
    /// <summary>
    /// A scope within which an attempt will be made.
    /// Call <see cref="IAttemptScope.RecordFailure(Exception)"/>
    /// to indicate the failure and cache the reason.
    /// </summary>
    public class AttemptScope: IAttemptScope
    {
        private readonly Action<CachedException> _onDispose;

        private CachedException _exception = null;
        private bool _disposed = false;

        /// <summary>
        /// Designated constuctor.
        /// </summary>
        /// <param name="onDispose">
        /// Action to perform once the scope is disposed.
        /// </param>
        public AttemptScope(Action<CachedException> onDispose)
        {
            if ((_onDispose = onDispose) is null)
            {
                throw new ArgumentNullException(nameof(onDispose));
            }
        }

        /// <summary>
        /// Finalizer for disposable pattern.
        /// see https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/implementing-dispose
        /// </summary>
        ~AttemptScope() => Dispose(false);

        /// <summary>
        /// Signals that attempt failed.
        /// </summary>
        /// <param name="exception">
        /// The cause of failure.
        /// </param>
        public void RecordFailure(Exception exception)
        {
            _exception = new CachedException(exception);
        }

        /// <summary>
        /// <see cref="IDisposable.Dispose"/>
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Implementation method for disposable pattern.
        /// see https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/implementing-dispose
        /// </summary>
        /// <param name="disposing">
        /// `true` if called from <see cref="IDisposable.Dispose"/>,
        /// `false` if called from finalizer.
        /// </param>
        public virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
            _disposed = true;
            _onDispose(_exception);
        }
    }
}
