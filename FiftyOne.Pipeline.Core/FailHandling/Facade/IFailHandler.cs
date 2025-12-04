/* *********************************************************************
 * This Original Work is copyright of 51 Degrees Mobile Experts Limited.
 * Copyright 2025 51 Degrees Mobile Experts Limited, Davidson House,
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

using FiftyOne.Pipeline.Core.Exceptions;
using FiftyOne.Pipeline.Core.FailHandling.Scope;
using System;
using System.Collections.Generic;
using System.Text;

namespace FiftyOne.Pipeline.Core.FailHandling.Facade
{
    /// <summary>
    /// Tracks failures and throttles requests.
    /// </summary>
    public interface IFailHandler
    {
        /// <summary>
        /// Checks if the strategy indicates that
        /// requests may not be sent now.
        /// </summary>
        /// <param name="exceptionFactory">
        /// Optional factory to wrap an error into a critical path exception.
        /// </param>
        /// <exception cref="Exception">
        /// Underlying exception wrapped by <paramref name="exceptionFactory"/>
        /// </exception>
        /// <returns>
        /// true if recovered, false if not yet.
        /// </returns>
        bool CheckIfRecovered(Func<string, Exception, Exception> exceptionFactory);

        /// <summary>
        /// Lets a consumer to wrap an attempt in `using` scope
        /// to implicitly report success 
        /// or explicitly provide exception on failure.
        /// </summary>
        /// <returns>
        /// Attempt scope that report to this handler once disposed.
        /// </returns>
        IAttemptScope MakeAttemptScope();
    }
}
