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

using System;
using System.Threading;

namespace FiftyOne.Pipeline.Engines.Configuration
{
    /// <summary>
    /// Used to store configuration values relating to lazy loading
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/advanced-features/lazy-loading.md">Specification</see>
    /// </summary>
    public class LazyLoadingConfiguration
    {
        /// <summary>
        /// The timeout in milliseconds to use when waiting for 
        /// processing to complete in order to retrieve property values.
        /// If the timeout is exceeded then a 
        /// <see cref="TimeoutException"/> will be thrown.
        /// </summary>
        public int PropertyTimeoutMs { get; set; }

        /// <summary>
        /// The <see cref="System.Threading.CancellationToken"/> to use 
        /// when waiting for processing to complete in order to retrieve 
        /// property values.
        /// If the cancellation token is triggered then the call to the 
        /// property will return immediately with a null value. 
        /// </summary>
        public CancellationToken? CancellationToken { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="propertyTimeoutMs">
        /// The timeout in milliseconds to use when waiting for 
        /// processing to complete in order to retrieve property values.
        /// If the timeout is exceeded then a 
        /// <see cref="TimeoutException"/> will be thrown.
        /// </param>
        /// <param name="cancellationToken">
        /// The <see cref="System.Threading.CancellationToken"/> to use 
        /// when waiting for processing to complete in order to retrieve 
        /// property values.
        /// If the cancellation token is triggered then the call to the 
        /// property will return immediately with a null value. 
        /// </param>
        public LazyLoadingConfiguration(
            int propertyTimeoutMs = Constants.LAZY_LOADING_DEFAULT_TIMEOUT_MS,
            CancellationToken? cancellationToken = null)
        {
            PropertyTimeoutMs = propertyTimeoutMs;
            CancellationToken = cancellationToken;
        }
    }
}
