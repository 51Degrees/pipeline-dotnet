/* *********************************************************************
 * This Original Work is copyright of 51 Degrees Mobile Experts Limited.
 * Copyright 2026 51 Degrees Mobile Experts Limited, Davidson House,
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

using FiftyOne.Pipeline.Core.FlowElements;

namespace FiftyOne.Pipeline.Core.Data
{
    /// <summary>
    /// Represents something the caller should know about the request that
    /// did not stop it being served. For example, an evidence value that an
    /// element could not use.
    /// </summary>
    /// <remarks>
    /// Warnings are deliberately separate from <see cref="IFlowError"/>.
    /// A non-empty errors collection makes the pipeline throw at the end of
    /// processing unless the host suppresses process exceptions, so an
    /// element cannot use an error to explain a recoverable problem without
    /// risking the whole response. A warning never affects processing.
    /// </remarks>
    public interface IFlowWarning
    {
        /// <summary>
        /// The message for the caller.
        /// </summary>
        string Message { get; }

        /// <summary>
        /// The flow element that the warning relates to. May be null if the
        /// warning did not originate from an element.
        /// </summary>
        IFlowElement FlowElement { get; }
    }
}
