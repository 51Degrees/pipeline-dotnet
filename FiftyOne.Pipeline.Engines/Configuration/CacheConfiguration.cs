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

using FiftyOne.Caching;

namespace FiftyOne.Pipeline.Engines.Configuration
{
    /// <summary>
    /// Contains everything needed to build a cache.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/features/caching.md">Specification</see>
    /// </summary>
    public class CacheConfiguration
    {
        /// <summary>
        /// Default constructor uses the LruPutCache
        /// with a size of 1000.
        /// </summary>
        public CacheConfiguration()
        {
            Builder = new LruPutCacheBuilder();
            Size = Constants.CACHE_DEFAULT_SIZE;
        }

        /// <summary>
        /// The builder to use when building a cache.
        /// </summary>
        public ICacheBuilder Builder { get; set; }
        /// <summary>
        /// The maximum size parameter to use when building a cache.
        /// </summary>
        public int Size { get; set; }
    }
}
