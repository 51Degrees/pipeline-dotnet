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

using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Engines.Data;
using System;

namespace FiftyOne.Pipeline.AgentSignature.Data
{
    /// <summary>
    /// What the agent signature element found out about the signature an
    /// automated agent sent with its request.
    /// </summary>
    /// <remarks>
    /// Every property is an
    /// <see cref="IAspectPropertyValue{T}"/>, so a property that has no
    /// value says why rather than returning something that looks like an
    /// answer. Only the status and the reason always have a value.
    /// </remarks>
    public interface IAgentSignatureData : IElementData
    {
        /// <summary>
        /// The outcome, which is one of 'Absent', 'Invalid', 'Unverified',
        /// 'Timeout' and 'Verified'. See the STATUS_ constants on
        /// <see cref="Constants"/>. Always has a value.
        /// </summary>
        IAspectPropertyValue<string> AgentSignature { get; }

        /// <summary>
        /// Why the outcome is what it is. See the REASON_ constants on
        /// <see cref="Constants"/>. Always has a value.
        /// </summary>
        IAspectPropertyValue<string> AgentSignatureReason { get; }

        /// <summary>
        /// The 'Signature-Agent' member value exactly as the agent sent it,
        /// for example 'https://chatgpt.com'. Has a value whenever the
        /// header was present and could be read.
        /// </summary>
        IAspectPropertyValue<string> AgentSignatureAgent { get; }

        /// <summary>
        /// The 'keyid' signature parameter, being the thumbprint (a short
        /// fingerprint) of the public key the agent signed with.
        /// </summary>
        IAspectPropertyValue<string> AgentSignatureKeyId { get; }

        /// <summary>
        /// The signature algorithm that was used, or the algorithm the
        /// agent named when this element does not verify it.
        /// </summary>
        IAspectPropertyValue<string> AgentSignatureAlgorithm { get; }

        /// <summary>
        /// When the agent made the signature.
        /// </summary>
        IAspectPropertyValue<DateTimeOffset> AgentSignatureCreated { get; }

        /// <summary>
        /// When the signature stops being valid.
        /// </summary>
        IAspectPropertyValue<DateTimeOffset> AgentSignatureExpires { get; }

        /// <summary>
        /// The 'nonce' signature parameter, when the agent sent one.
        /// Checking that a nonce is never reused is the customer's job,
        /// because only the customer knows how long to remember one for.
        /// </summary>
        IAspectPropertyValue<string> AgentSignatureNonce { get; }

        /// <summary>
        /// What the agent says it uses the pages it fetches for, taken from
        /// the key directory or from the agent card.
        /// </summary>
        IAspectPropertyValue<string> AgentSignaturePurpose { get; }

        /// <summary>
        /// The name the agent gives itself in its agent card.
        /// </summary>
        IAspectPropertyValue<string> AgentSignatureName { get; }

        /// <summary>
        /// The robots.txt product token from the agent card, being the name
        /// the agent answers to in a robots.txt file. A customer can join
        /// this to the CrawlerProductTokens property.
        /// </summary>
        IAspectPropertyValue<string> AgentSignatureProductToken { get; }

        /// <summary>
        /// The URL of the agent card.
        /// </summary>
        IAspectPropertyValue<string> AgentSignatureCardUrl { get; }
    }
}
