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

namespace FiftyOne.Pipeline.Engines
{
    /// <summary>
    /// The performance profiles to use with the SetPerformanceProfile
    /// method.
    /// </summary>
#pragma warning disable CA1717 // Only FlagsAttribute enums should have plural names
    // This would be a breaking change.
    public enum PerformanceProfiles
#pragma warning restore CA1717 // Only FlagsAttribute enums should have plural names
    {
        /// <summary>
        /// Use as little memory as possible. 
        /// Performance can be significantly impacted but will still be 
        /// fast enough for many scenarios.
        /// Similar to HighPerformance and Balanced but uses much 
        /// smaller cache sizes.
        /// The precise details will vary based on the implementation of 
        /// the engine.
        /// </summary>
        LowMemory,
        /// <summary>
        /// Best possible performance. Everything loaded into memory.
        /// Execution can be optimized to ignore operations relating to
        /// maintaining caches, etc.
        /// The precise details will vary based on the implementation of 
        /// the engine.
        /// </summary>
        MaxPerformance,
        /// <summary>
        /// Load smaller data structures into memory.
        /// Larger data structures are cached to keep the most frequently
        /// used data in memory as well.
        /// Similar to Balanced but uses larger cache sizes.
        /// The precise details will vary based on the implementation of 
        /// the engine.
        /// </summary>
        HighPerformance,
        /// <summary>
        /// Load smaller data structures into memory.
        /// Larger data structures are cached to keep the most frequently
        /// used data in memory as well.
        /// Similar to HighPerformance but uses smaller cache sizes.
        /// The precise details will vary based on the implementation of 
        /// the engine.
        /// </summary>
        Balanced,
        /// <summary>
        /// The precise details will vary based on the implementation of 
        /// the engine.
        /// </summary>
        BalancedTemp
    }
}
