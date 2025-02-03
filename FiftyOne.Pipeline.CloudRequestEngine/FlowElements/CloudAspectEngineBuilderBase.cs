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

using FiftyOne.Pipeline.Engines.FlowElements;

namespace FiftyOne.Pipeline.CloudRequestEngine.FlowElements
{
    /// <summary>
    /// Abstract base class that exposes the common options that all
    /// 51Degrees cloud engine builders should make use of.
    /// </summary>
    /// <typeparam name="TBuilder">
    /// The specific builder type to use as the return type from the fluent 
    /// builder methods.
    /// </typeparam>
    /// <typeparam name="TEngine">
    /// The type of the engine that this builder will build
    /// </typeparam>
    public abstract class CloudAspectEngineBuilderBase<TBuilder, TEngine> : 
        AspectEngineBuilderBase<TBuilder, TEngine>
        where TBuilder : CloudAspectEngineBuilderBase<TBuilder, TEngine>
        where TEngine : IAspectEngine
    {
    }
}
