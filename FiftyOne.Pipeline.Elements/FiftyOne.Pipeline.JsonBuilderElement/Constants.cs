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

namespace FiftyOne.Pipeline.JsonBuilder
{
    /// <summary>
    /// Static class containing various constants that are used by the 
    /// Pipeline and/or are helpful to callers. 
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming",
        "CA1707:Identifiers should not contain underscores",
        Justification = "51Degrees coding style is for constant names " +
            "to be all-caps with an underscore to separate words.")]
    public static class Constants
    {
        /// <summary>
        /// The maximum number of times that the JavaScript produced
        /// by the JavaScriptBuilderElement will be allowed to call
        /// back to the server to get new JSON data.
        /// </summary>
        /// <remarks>
        /// When the JavaScript runs on the client device, it can optionally
        /// include a callback to an endpoint on the server that gets new 
        /// JSON data.
        /// If that JSON data contains further JavaScript properties then
        /// they are executed again on the client and the process repeats.
        /// To prevent accidental infinite loops of requests, we limit 
        /// the maximum number of iterations.
        /// After this number of iterations is reached, the 
        /// JsonBuilderElement will simply not build the list of 
        /// JavaScript properties and so none will get executed on the
        /// client device.
        /// </remarks>
        public const int MAX_JAVASCRIPT_ITERATIONS = 10;
    }
}
