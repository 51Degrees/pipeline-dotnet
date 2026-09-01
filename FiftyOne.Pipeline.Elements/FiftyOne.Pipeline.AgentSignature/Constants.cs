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

namespace FiftyOne.Pipeline.AgentSignature
{
    /// <summary>
    /// Static class containing the constants used by the agent signature
    /// element and helpful to callers of it.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming",
        "CA1707:Identifiers should not contain underscores",
        Justification = "51Degrees coding style is for constant names " +
            "to be all-caps with an underscore to separate words.")]
    public static class Constants
    {
        #region Evidence keys

        /// <summary>
        /// The complete key used when the 'Signature' HTTP header is
        /// passed as evidence.
        /// </summary>
        public const string EVIDENCE_SIGNATURE_KEY =
            Core.Constants.EVIDENCE_HTTPHEADER_PREFIX +
            Core.Constants.EVIDENCE_SEPERATOR +
            "signature";

        /// <summary>
        /// The complete key used when the 'Signature-Input' HTTP header is
        /// passed as evidence.
        /// </summary>
        public const string EVIDENCE_SIGNATURE_INPUT_KEY =
            Core.Constants.EVIDENCE_HTTPHEADER_PREFIX +
            Core.Constants.EVIDENCE_SEPERATOR +
            "signature-input";

        /// <summary>
        /// The complete key used when the 'Signature-Agent' HTTP header is
        /// passed as evidence.
        /// </summary>
        public const string EVIDENCE_SIGNATURE_AGENT_KEY =
            Core.Constants.EVIDENCE_HTTPHEADER_PREFIX +
            Core.Constants.EVIDENCE_SEPERATOR +
            "signature-agent";

        /// <summary>
        /// The complete key used when the 'Host' HTTP header is passed as
        /// evidence. The '@authority' derived component is built from it.
        /// </summary>
        public const string EVIDENCE_HOST_KEY =
            Core.Constants.EVIDENCE_HTTPHEADER_PREFIX +
            Core.Constants.EVIDENCE_SEPERATOR +
            "host";

        #endregion

        #region Element data

        /// <summary>
        /// The element data key used by default for this element.
        /// </summary>
        public const string DEFAULT_ELEMENT_DATA_KEY = "agent-signature";

        #endregion

        #region Property names

        /// <summary>
        /// The name of the property holding the signature status, which is
        /// one of the five values in the AGENT_SIGNATURE_ constants.
        /// </summary>
        public const string PROPERTY_STATUS = "agentsignature";

        /// <summary>
        /// The name of the property holding the reason code, which is one
        /// of the REASON_ constants.
        /// </summary>
        public const string PROPERTY_REASON = "agentsignaturereason";

        /// <summary>
        /// The name of the property holding the 'Signature-Agent' member
        /// value exactly as the agent sent it.
        /// </summary>
        public const string PROPERTY_AGENT = "agentsignatureagent";

        /// <summary>
        /// The name of the property holding the 'keyid' signature parameter.
        /// </summary>
        public const string PROPERTY_KEY_ID = "agentsignaturekeyid";

        /// <summary>
        /// The name of the property holding the signature algorithm.
        /// </summary>
        public const string PROPERTY_ALGORITHM = "agentsignaturealgorithm";

        /// <summary>
        /// The name of the property holding the 'created' signature
        /// parameter as a point in time.
        /// </summary>
        public const string PROPERTY_CREATED = "agentsignaturecreated";

        /// <summary>
        /// The name of the property holding the 'expires' signature
        /// parameter as a point in time.
        /// </summary>
        public const string PROPERTY_EXPIRES = "agentsignatureexpires";

        /// <summary>
        /// The name of the property holding the 'nonce' signature
        /// parameter.
        /// </summary>
        public const string PROPERTY_NONCE = "agentsignaturenonce";

        /// <summary>
        /// The name of the property holding the purpose the agent
        /// publishes for the key.
        /// </summary>
        public const string PROPERTY_PURPOSE = "agentsignaturepurpose";

        /// <summary>
        /// The name of the property holding the agent name from the
        /// signature agent card.
        /// </summary>
        public const string PROPERTY_NAME = "agentsignaturename";

        /// <summary>
        /// The name of the property holding the robots.txt product token
        /// from the signature agent card.
        /// </summary>
        public const string PROPERTY_PRODUCT_TOKEN =
            "agentsignatureproducttoken";

        /// <summary>
        /// The name of the property holding the URL of the signature agent
        /// card.
        /// </summary>
        public const string PROPERTY_CARD_URL = "agentsignaturecardurl";

        #endregion

        #region Status values

        /// <summary>
        /// The request carried no signature headers. This is the normal
        /// value, because only a handful of agents sign today.
        /// </summary>
        public const string STATUS_ABSENT = "Absent";

        /// <summary>
        /// The request carried a signature and the signature, or one of the
        /// headers carrying it, is wrong.
        /// </summary>
        public const string STATUS_INVALID = "Invalid";

        /// <summary>
        /// The request carried a signature that could not be checked. This
        /// is not evidence against the agent.
        /// </summary>
        public const string STATUS_UNVERIFIED = "Unverified";

        /// <summary>
        /// The key directory was still being fetched when the wait budget
        /// ran out. The fetch continues, so a later request from the same
        /// agent finds the result.
        /// </summary>
        public const string STATUS_TIMEOUT = "Timeout";

        /// <summary>
        /// The signature checked against a key the agent publishes.
        /// </summary>
        public const string STATUS_VERIFIED = "Verified";

        #endregion

        #region Reason codes

        /// <summary>
        /// No 'Signature' and no 'Signature-Input' header was present.
        /// </summary>
        public const string REASON_NO_SIGNATURE = "NoSignature";

        /// <summary>
        /// One of the two headers was present without the other, or one of
        /// the three headers could not be parsed.
        /// </summary>
        public const string REASON_MALFORMED = "Malformed";

        /// <summary>
        /// No signature carried the 'web-bot-auth' tag.
        /// </summary>
        public const string REASON_TAG_MISMATCH = "TagMismatch";

        /// <summary>
        /// One of the required 'created', 'expires' and 'keyid' parameters
        /// was missing.
        /// </summary>
        public const string REASON_MISSING_PARAMETER = "MissingParameter";

        /// <summary>
        /// The signature had expired, or it was valid for longer than the
        /// configured maximum lifetime.
        /// </summary>
        public const string REASON_EXPIRED = "Expired";

        /// <summary>
        /// The signature was created further in the future than the
        /// configured clock skew allows.
        /// </summary>
        public const string REASON_NOT_YET_VALID = "NotYetValid";

        /// <summary>
        /// The signature named no agent, so there was nowhere to fetch a
        /// key from.
        /// </summary>
        public const string REASON_NO_AGENT = "NoAgent";

        /// <summary>
        /// The signature covered a component that cannot be rebuilt from
        /// the evidence the pipeline holds.
        /// </summary>
        public const string REASON_COMPONENT_UNAVAILABLE =
            "ComponentUnavailable";

        /// <summary>
        /// The key directory was still being fetched when the wait budget
        /// ran out.
        /// </summary>
        public const string REASON_DIRECTORY_PENDING = "DirectoryPending";

        /// <summary>
        /// The key directory could not be fetched or could not be read.
        /// </summary>
        public const string REASON_DIRECTORY_UNAVAILABLE =
            "DirectoryUnavailable";

        /// <summary>
        /// The key directory was read and holds no key with the key id the
        /// signature names, which is evidence the key was withdrawn.
        /// </summary>
        public const string REASON_UNKNOWN_KEY = "UnknownKey";

        /// <summary>
        /// The key itself was not valid at the time the signature was
        /// created.
        /// </summary>
        public const string REASON_KEY_EXPIRED = "KeyExpired";

        /// <summary>
        /// The signature uses an algorithm this element does not verify.
        /// </summary>
        public const string REASON_UNSUPPORTED_ALGORITHM =
            "UnsupportedAlgorithm";

        /// <summary>
        /// The signature did not check out against the key the agent
        /// publishes.
        /// </summary>
        public const string REASON_SIGNATURE_MISMATCH = "SignatureMismatch";

        /// <summary>
        /// The signature checked against a key the agent publishes.
        /// </summary>
        public const string REASON_VERIFIED = "Verified";

        #endregion

        #region Protocol values

        /// <summary>
        /// The tag a Web Bot Auth request signature must carry.
        /// </summary>
        public const string TAG_WEB_BOT_AUTH = "web-bot-auth";

        /// <summary>
        /// The tag the signature over a key directory response carries.
        /// </summary>
        public const string TAG_DIRECTORY = "http-message-signatures-directory";

        /// <summary>
        /// The path a key directory is served from, relative to the origin
        /// a 'Signature-Agent' member of type 'directory' names.
        /// </summary>
        public const string DIRECTORY_PATH =
            "/.well-known/http-message-signatures-directory";

        /// <summary>
        /// The media type a key directory is served with.
        /// </summary>
        public const string DIRECTORY_MEDIA_TYPE =
            "application/http-message-signatures-directory+json";

        /// <summary>
        /// The media type a JWKS or an agent card is served with.
        /// </summary>
        public const string JSON_MEDIA_TYPE = "application/json";

        /// <summary>
        /// The 'Signature-Agent' member type naming an origin that serves a
        /// key directory. This is the default when no type is given.
        /// </summary>
        public const string AGENT_TYPE_DIRECTORY = "directory";

        /// <summary>
        /// The 'Signature-Agent' member type naming a JWKS URL directly.
        /// </summary>
        public const string AGENT_TYPE_JWKS_URI = "jwks_uri";

        /// <summary>
        /// The 'Signature-Agent' member type naming a Client ID Metadata
        /// Document, which this element calls the signature agent card.
        /// </summary>
        public const string AGENT_TYPE_CIMD = "cimd";

        #endregion

        #region Algorithm names

        /// <summary>
        /// The RFC 9421 registry name for Ed25519.
        /// </summary>
        public const string ALGORITHM_ED25519 = "ed25519";

        /// <summary>
        /// The RFC 9421 registry name for RSASSA-PSS using SHA-512.
        /// </summary>
        public const string ALGORITHM_RSA_PSS_SHA512 = "rsa-pss-sha512";

        /// <summary>
        /// The RFC 9421 registry name for ECDSA over the P-256 curve using
        /// SHA-256.
        /// </summary>
        public const string ALGORITHM_ECDSA_P256_SHA256 =
            "ecdsa-p256-sha256";

        /// <summary>
        /// The RFC 9421 registry name for HMAC using SHA-256. The Web Bot
        /// Auth protocol forbids shared secrets, so this element never
        /// verifies a signature that uses it.
        /// </summary>
        public const string ALGORITHM_HMAC_SHA256 = "hmac-sha256";

        #endregion

        #region Builder defaults

        /// <summary>
        /// The default number of key directories held in the cache.
        /// </summary>
        public const int DEFAULT_CACHE_SIZE = 1000;

        /// <summary>
        /// The default period a fetched key directory is reused for, which
        /// is the 'max-age' the drafts recommend a directory is served
        /// with.
        /// </summary>
        public static readonly TimeSpan DEFAULT_CACHE_LIFETIME =
            TimeSpan.FromHours(24);

        /// <summary>
        /// The default period a failed fetch is remembered for, so an
        /// outage at one agent does not cause a fetch per request.
        /// </summary>
        public static readonly TimeSpan DEFAULT_NEGATIVE_CACHE_LIFETIME =
            TimeSpan.FromMinutes(5);

        /// <summary>
        /// The default period a request waits for a key directory fetch
        /// before the status becomes Timeout.
        /// </summary>
        public static readonly TimeSpan DEFAULT_WAIT_BUDGET =
            TimeSpan.FromMilliseconds(350);

        /// <summary>
        /// The default time limit on a single key directory fetch.
        /// </summary>
        public static readonly TimeSpan DEFAULT_FETCH_TIMEOUT =
            TimeSpan.FromSeconds(5);

        /// <summary>
        /// The default tolerance on the 'created' and 'expires' signature
        /// parameters, which allows for clocks that differ a little.
        /// </summary>
        public static readonly TimeSpan DEFAULT_CLOCK_SKEW =
            TimeSpan.FromSeconds(60);

        /// <summary>
        /// The default maximum lifetime of a signature. Zero means no
        /// limit, because the protocol draft only recommends one.
        /// </summary>
        public static readonly TimeSpan DEFAULT_MAX_LIFETIME =
            TimeSpan.Zero;

        /// <summary>
        /// The default for whether the bare quoted string form of the
        /// 'Signature-Agent' header is accepted.
        /// </summary>
        public const bool DEFAULT_ALLOW_LEGACY_SIGNATURE_AGENT = true;

        #endregion
    }
}
