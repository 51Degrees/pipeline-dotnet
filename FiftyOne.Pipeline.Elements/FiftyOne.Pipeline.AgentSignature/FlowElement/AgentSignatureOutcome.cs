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

using FiftyOne.Pipeline.Engines.Data;
using System;

namespace FiftyOne.Pipeline.AgentSignature.FlowElement
{
    /// <summary>
    /// What the element worked out about one request, before it is written
    /// into the element data.
    /// </summary>
    internal sealed class AgentSignatureOutcome
    {
        /// <summary>
        /// One of the STATUS_ constants.
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// One of the REASON_ constants.
        /// </summary>
        public string Reason { get; set; }

        /// <summary>
        /// The 'Signature-Agent' member value as the agent sent it.
        /// </summary>
        public string Agent { get; set; }

        /// <summary>
        /// The 'keyid' signature parameter.
        /// </summary>
        public string KeyId { get; set; }

        /// <summary>
        /// The algorithm name to report.
        /// </summary>
        public string Algorithm { get; set; }

        /// <summary>
        /// When the signature was made.
        /// </summary>
        public DateTimeOffset? Created { get; set; }

        /// <summary>
        /// When the signature stops being valid.
        /// </summary>
        public DateTimeOffset? Expires { get; set; }

        /// <summary>
        /// The 'nonce' signature parameter.
        /// </summary>
        public string Nonce { get; set; }

        /// <summary>
        /// What the agent says the keys are for.
        /// </summary>
        public string Purpose { get; set; }

        /// <summary>
        /// The name from the agent card.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The robots.txt product token from the agent card.
        /// </summary>
        public string ProductToken { get; set; }

        /// <summary>
        /// The URL of the agent card.
        /// </summary>
        public string CardUrl { get; set; }

        /// <summary>
        /// True once the key directory has been read, which decides what a
        /// missing purpose is explained by.
        /// </summary>
        public bool DirectoryWasRead { get; set; }

        /// <summary>
        /// The message a detail property carries when it has no value.
        /// </summary>
        public string MissingDetailMessage { get; set; } =
            Messages.NoValueDetailAbsent;
    }

    /// <summary>
    /// The property values that say a property has no value, together with
    /// the values that never change.
    /// </summary>
    /// <remarks>
    /// These instances are shared by every request. A request with no
    /// signature, which is nearly every request, is answered entirely from
    /// them, so that answering it costs no property values at all and the
    /// element does no parsing and makes no request.
    /// <para>
    /// Nothing in this element writes to them. They are handed out through
    /// <see cref="IAspectPropertyValue{T}"/>, whose Value setter is public,
    /// so an element or a caller further along the pipeline that wrote to
    /// one would change what every later request with no signature reports.
    /// Nothing in this repository does that, and the pipeline gives no
    /// element a reason to write to another element's values.
    /// </para>
    /// </remarks>
    internal static class SharedValues
    {
        /// <summary>The 'Absent' status.</summary>
        public static readonly IAspectPropertyValue<string> StatusAbsent =
            new AspectPropertyValue<string>(Constants.STATUS_ABSENT);

        /// <summary>The 'NoSignature' reason.</summary>
        public static readonly IAspectPropertyValue<string> ReasonNoSignature =
            new AspectPropertyValue<string>(Constants.REASON_NO_SIGNATURE);

        /// <summary>
        /// A text property with no value, because the request carried no
        /// signature headers.
        /// </summary>
        public static readonly IAspectPropertyValue<string> AbsentText =
            NoText(Messages.NoValueDetailAbsent);

        /// <summary>
        /// A time property with no value, because the request carried no
        /// signature headers.
        /// </summary>
        public static readonly IAspectPropertyValue<DateTimeOffset>
            AbsentTime = NoTime(Messages.NoValueDetailAbsent);

        /// <summary>
        /// The purpose with no value, because the key directory was never
        /// read.
        /// </summary>
        public static readonly IAspectPropertyValue<string>
            PurposeNotRead = NoText(Messages.NoValuePurposeNotRead);

        /// <summary>
        /// The purpose with no value, because the key directory was read and
        /// does not say.
        /// </summary>
        public static readonly IAspectPropertyValue<string>
            PurposeNotStated = NoText(Messages.NoValuePurposeNotStated);

        /// <summary>
        /// An agent card property with no value, because no card was found.
        /// </summary>
        public static readonly IAspectPropertyValue<string> NoCard =
            NoText(Messages.NoValueNoCard);

        /// <summary>
        /// Make a text property that has no value.
        /// </summary>
        /// <param name="message">Why it has no value.</param>
        /// <returns>The property value.</returns>
        public static IAspectPropertyValue<string> NoText(string message)
        {
            return new AspectPropertyValue<string>
            {
                NoValueMessage = message,
            };
        }

        /// <summary>
        /// Make a time property that has no value.
        /// </summary>
        /// <param name="message">Why it has no value.</param>
        /// <returns>The property value.</returns>
        public static IAspectPropertyValue<DateTimeOffset> NoTime(
            string message)
        {
            return new AspectPropertyValue<DateTimeOffset>
            {
                NoValueMessage = message,
            };
        }
    }
}
