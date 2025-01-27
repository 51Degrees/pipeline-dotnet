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

using FiftyOne.Pipeline.Core.Attributes;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines;

namespace FiftyOne.Pipeline.Core.Tests.HelperClasses
{
    /// <summary>
    /// The builder to use when creating <see cref="EnumPerfProfileElement"/>
    /// </summary>
    [AlternateName("EnumPerfProfile")]
    public class EnumPerfProfileElementBuilder
    {

        /// <summary>
        /// The performance profile to use for the device detection engine.
        /// </summary>
        private PerformanceProfiles _performanceProfile;

        public EnumPerfProfileElementBuilder SetPerformanceProfile(
            PerformanceProfiles profile)
        {
            _performanceProfile = profile;
            return this;
        }

        /// <summary>
        /// The builder has one parameter and it is mandatory.
        /// </summary>
        /// <param name="multiple"></param>
        /// <returns></returns>
        public IFlowElement Build()
        {
            return new EnumPerfProfileElement(_performanceProfile);
        }
    }
}
