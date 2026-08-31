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

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;

namespace FiftyOne.Did.Client
{
    /// <summary>
    /// The typed answer from the cloud's redeem endpoint, built by
    /// <see cref="DidClient.RedeemAsync(string, string, string, System.Threading.CancellationToken)"/>
    /// from the JSON body. <see cref="Raw"/> keeps the body as received and
    /// <see cref="StatusCode"/> the HTTP status, so nothing the cloud said
    /// is lost in the mapping.
    /// </summary>
    public sealed class RedeemResult
    {
        /// <summary>
        /// The format the cloud writes <c>verifiedAt</c> in, ISO 8601 UTC to
        /// the second.
        /// </summary>
        public const string VerifiedAtFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";

        /// <summary>
        /// Creates a result from its parts. Callers normally get one from
        /// <see cref="FromResponse"/> rather than building one.
        /// </summary>
        /// <param name="context">The mapped context verdict.</param>
        /// <param name="contextValue">The raw <c>context</c> string.</param>
        /// <param name="signature">The mapped signature outcome.</param>
        /// <param name="factors">The per-factor outcomes, or null.</param>
        /// <param name="verifiedAt">When the result was sealed, or null.</param>
        /// <param name="secondsSinceVerified">The result's age, or null.</param>
        /// <param name="statusCode">The HTTP status.</param>
        /// <param name="raw">The response body.</param>
        public RedeemResult(
            ContextOutcome context,
            string? contextValue,
            SignatureOutcome signature,
            IReadOnlyDictionary<string, FactorOutcome>? factors,
            DateTime? verifiedAt,
            int? secondsSinceVerified,
            int statusCode,
            string raw)
        {
            Context = context;
            ContextValue = contextValue;
            Signature = signature;
            Factors = factors;
            VerifiedAt = verifiedAt;
            SecondsSinceVerified = secondsSinceVerified;
            StatusCode = statusCode;
            Raw = raw ?? string.Empty;
        }

        /// <summary>
        /// The creator context verdict, mapped from the <c>context</c>
        /// string. A string this client does not recognise maps to
        /// <see cref="ContextOutcome.Unreadable"/>, so an unexpected answer
        /// never reads as a pass.
        /// </summary>
        public ContextOutcome Context { get; }

        /// <summary>
        /// The <c>context</c> string exactly as the cloud sent it, or null
        /// when the body carried none.
        /// </summary>
        public string? ContextValue { get; }

        /// <summary>
        /// The signature outcome, mapped from the <c>signature</c> string.
        /// <see cref="SignatureOutcome.Unknown"/> when the field is absent,
        /// which it is on every outcome other than a redeemed verdict.
        /// </summary>
        public SignatureOutcome Signature { get; }

        /// <summary>
        /// The outcome of each creator context factor by name
        /// (<c>transport</c>, <c>device</c>, <c>browserip</c>,
        /// <c>connectionip</c>, <c>asn</c>, <c>browser</c>), present only
        /// when the cloud sent <c>factors</c>, which it does for a
        /// <see cref="ContextOutcome.Mismatch"/>.
        /// </summary>
        public IReadOnlyDictionary<string, FactorOutcome>? Factors { get; }

        /// <summary>
        /// When the verify endpoint checked the context and sealed the
        /// result, UTC. Present on the redeemed and expired outcomes.
        /// </summary>
        public DateTime? VerifiedAt { get; }

        /// <summary>
        /// How long before this redemption the verification happened, in
        /// whole seconds by the cloud's clock. Present on the redeemed and
        /// expired outcomes.
        /// </summary>
        public int? SecondsSinceVerified { get; }

        /// <summary>
        /// The HTTP status the cloud answered with, 200 for every verdict
        /// and 503 for <see cref="ContextOutcome.Unconfirmed"/>.
        /// </summary>
        public int StatusCode { get; }

        /// <summary>The response body as received.</summary>
        public string Raw { get; }

        /// <summary>
        /// Builds a result from a redeem response. A body that is not a
        /// JSON object, or carries no <c>context</c>, gives
        /// <see cref="ContextOutcome.Unreadable"/> with the body kept in
        /// <see cref="Raw"/>.
        /// </summary>
        /// <param name="statusCode">The HTTP status.</param>
        /// <param name="body">The response body.</param>
        /// <returns>The typed result.</returns>
        public static RedeemResult FromResponse(int statusCode, string body)
        {
            body ??= string.Empty;
            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(body);
            }
            catch (JsonException)
            {
                return Unreadable(statusCode, body);
            }
            using (document)
            {
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    return Unreadable(statusCode, body);
                }
                var contextValue = ReadString(root, "context");
                int? seconds = null;
                if (root.TryGetProperty("secondsSinceVerified", out var age)
                    && age.ValueKind == JsonValueKind.Number
                    && age.TryGetInt32(out var parsedAge))
                {
                    seconds = parsedAge;
                }
                return new RedeemResult(
                    ParseContext(contextValue),
                    contextValue,
                    ParseSignature(ReadString(root, "signature")),
                    ReadFactors(root),
                    ReadVerifiedAt(root),
                    seconds,
                    statusCode,
                    body);
            }
        }

        /// <summary>
        /// Maps a <c>context</c> string to its outcome. Anything not
        /// recognised, including null, is
        /// <see cref="ContextOutcome.Unreadable"/>.
        /// </summary>
        /// <param name="value">The <c>context</c> string.</param>
        /// <returns>The outcome.</returns>
        public static ContextOutcome ParseContext(string? value)
        {
            switch (value?.ToLowerInvariant())
            {
                case "verified": return ContextOutcome.Verified;
                case "mismatch": return ContextOutcome.Mismatch;
                case "nocontext": return ContextOutcome.NoContext;
                case "notcheckable": return ContextOutcome.NotCheckable;
                case "expired": return ContextOutcome.Expired;
                case "replayed": return ContextOutcome.Replayed;
                case "unconfirmed": return ContextOutcome.Unconfirmed;
                default: return ContextOutcome.Unreadable;
            }
        }

        /// <summary>
        /// The <c>context</c> string the cloud uses for an outcome, the
        /// inverse of <see cref="ParseContext"/>.
        /// </summary>
        /// <param name="outcome">The outcome.</param>
        /// <returns>The cloud's word for it.</returns>
        public static string ToContextValue(ContextOutcome outcome)
        {
            switch (outcome)
            {
                case ContextOutcome.Verified: return "verified";
                case ContextOutcome.Mismatch: return "mismatch";
                case ContextOutcome.NoContext: return "nocontext";
                case ContextOutcome.NotCheckable: return "notcheckable";
                case ContextOutcome.Expired: return "expired";
                case ContextOutcome.Replayed: return "replayed";
                case ContextOutcome.Unconfirmed: return "unconfirmed";
                default: return "unreadable";
            }
        }

        /// <summary>
        /// Maps a <c>signature</c> string to its outcome. Anything not
        /// recognised, including null, is
        /// <see cref="SignatureOutcome.Unknown"/>.
        /// </summary>
        /// <param name="value">The <c>signature</c> string.</param>
        /// <returns>The outcome.</returns>
        public static SignatureOutcome ParseSignature(string? value)
        {
            switch (value?.ToLowerInvariant())
            {
                case "verified": return SignatureOutcome.Verified;
                case "invalid": return SignatureOutcome.Invalid;
                default: return SignatureOutcome.Unknown;
            }
        }

        private static RedeemResult Unreadable(int statusCode, string body)
            => new RedeemResult(
                ContextOutcome.Unreadable, null, SignatureOutcome.Unknown,
                null, null, null, statusCode, body);

        private static IReadOnlyDictionary<string, FactorOutcome>? ReadFactors(
            JsonElement root)
        {
            if (root.TryGetProperty("factors", out var element) == false
                || element.ValueKind != JsonValueKind.Object)
            {
                return null;
            }
            var factors = new Dictionary<string, FactorOutcome>(
                StringComparer.Ordinal);
            foreach (var factor in element.EnumerateObject())
            {
                // Anything other than the one word verified is a mismatch,
                // so an unexpected value never reads as a pass.
                var verified = factor.Value.ValueKind == JsonValueKind.String
                    && string.Equals(
                        factor.Value.GetString(), "verified",
                        StringComparison.OrdinalIgnoreCase);
                factors[factor.Name] = verified
                    ? FactorOutcome.Verified
                    : FactorOutcome.Mismatch;
            }
            return factors;
        }

        private static DateTime? ReadVerifiedAt(JsonElement root)
        {
            var value = ReadString(root, "verifiedAt");
            if (value is null)
            {
                return null;
            }
            return DateTime.TryParse(
                value, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                out var parsed)
                ? parsed
                : null;
        }

        private static string? ReadString(JsonElement element, string name)
            => element.TryGetProperty(name, out var value)
                && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
    }
}
