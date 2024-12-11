/* *********************************************************************
 * This Original Work is copyright of 51 Degrees Mobile Experts Limited.
 * Copyright 2023 51 Degrees Mobile Experts Limited, Davidson House,
 * Forbury Square, Reading, Berkshire, United Kingdom RG1 3EU.
 *
 * This Original Work is licensed under the European Union Public Licence
 * (EUPL) v.1.2 and is subject to its terms as set out below.
 *
 * If a copy of the EUPL was not distributed with this file, You can obtain
 * one at https://opensource.org/licenses/EUPL-1.2.
 *
 * The 'Compatible Licences' set out in the Appendix to the EUPL (as may be
 * amended by the European Commission) shall be deemed incompatible for
 * the purposes of the Work and the provisions of the compatibility
 * clause in Article 5 of the EUPL shall not apply.
 *
 * If using the Work as, or as part of, a network application, by
 * including the attribution notice(s) required under Article 5 of the EUPL
 * in the end user terms of the application under an appropriate heading,
 * such notice(s) shall fulfill the requirements of that article.
 * ********************************************************************* */

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
