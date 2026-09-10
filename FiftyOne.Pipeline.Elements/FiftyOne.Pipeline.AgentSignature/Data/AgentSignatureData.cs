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
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.Data;
using Microsoft.Extensions.Logging;
using System;

namespace FiftyOne.Pipeline.AgentSignature.Data
{
    /// <summary>
    /// What the agent signature element found out about the signature an
    /// automated agent sent with its request.
    /// </summary>
    public class AgentSignatureData : ElementDataBase, IAgentSignatureData
    {
        /// <summary>
        /// Construct an instance.
        /// </summary>
        /// <param name="logger">The logger for this instance.</param>
        /// <param name="pipeline">
        /// The pipeline this instance was created by.
        /// </param>
        public AgentSignatureData(
            ILogger<AgentSignatureData> logger,
            IPipeline pipeline)
            : base(logger, pipeline)
        { }

        /// <inheritdoc/>
        public IAspectPropertyValue<string> AgentSignature
        {
            get => GetText(Constants.PROPERTY_STATUS);
            set => this[Constants.PROPERTY_STATUS] = value;
        }

        /// <inheritdoc/>
        public IAspectPropertyValue<string> AgentSignatureReason
        {
            get => GetText(Constants.PROPERTY_REASON);
            set => this[Constants.PROPERTY_REASON] = value;
        }

        /// <inheritdoc/>
        public IAspectPropertyValue<string> AgentSignatureAgent
        {
            get => GetText(Constants.PROPERTY_AGENT);
            set => this[Constants.PROPERTY_AGENT] = value;
        }

        /// <inheritdoc/>
        public IAspectPropertyValue<string> AgentSignatureKeyId
        {
            get => GetText(Constants.PROPERTY_KEY_ID);
            set => this[Constants.PROPERTY_KEY_ID] = value;
        }

        /// <inheritdoc/>
        public IAspectPropertyValue<string> AgentSignatureAlgorithm
        {
            get => GetText(Constants.PROPERTY_ALGORITHM);
            set => this[Constants.PROPERTY_ALGORITHM] = value;
        }

        /// <inheritdoc/>
        public IAspectPropertyValue<DateTimeOffset> AgentSignatureCreated
        {
            get => GetTime(Constants.PROPERTY_CREATED);
            set => this[Constants.PROPERTY_CREATED] = value;
        }

        /// <inheritdoc/>
        public IAspectPropertyValue<DateTimeOffset> AgentSignatureExpires
        {
            get => GetTime(Constants.PROPERTY_EXPIRES);
            set => this[Constants.PROPERTY_EXPIRES] = value;
        }

        /// <inheritdoc/>
        public IAspectPropertyValue<string> AgentSignatureNonce
        {
            get => GetText(Constants.PROPERTY_NONCE);
            set => this[Constants.PROPERTY_NONCE] = value;
        }

        /// <inheritdoc/>
        public IAspectPropertyValue<string> AgentSignaturePurpose
        {
            get => GetText(Constants.PROPERTY_PURPOSE);
            set => this[Constants.PROPERTY_PURPOSE] = value;
        }

        /// <inheritdoc/>
        public IAspectPropertyValue<string> AgentSignatureName
        {
            get => GetText(Constants.PROPERTY_NAME);
            set => this[Constants.PROPERTY_NAME] = value;
        }

        /// <inheritdoc/>
        public IAspectPropertyValue<string> AgentSignatureProductToken
        {
            get => GetText(Constants.PROPERTY_PRODUCT_TOKEN);
            set => this[Constants.PROPERTY_PRODUCT_TOKEN] = value;
        }

        /// <inheritdoc/>
        public IAspectPropertyValue<string> AgentSignatureCardUrl
        {
            get => GetText(Constants.PROPERTY_CARD_URL);
            set => this[Constants.PROPERTY_CARD_URL] = value;
        }

        private IAspectPropertyValue<string> GetText(string name) =>
            this[name] as IAspectPropertyValue<string>;

        private IAspectPropertyValue<DateTimeOffset> GetTime(string name) =>
            this[name] as IAspectPropertyValue<DateTimeOffset>;
    }
}
