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

using FiftyOne.Pipeline.AgentSignature.Verification;
using System;
using System.Collections.Generic;

namespace FiftyOne.Pipeline.AgentSignature.Keys
{
    /// <summary>
    /// One public key from a key directory, as RFC 7517 defines a JSON Web
    /// Key.
    /// </summary>
    internal sealed class JsonWebKey
    {
        /// <summary>
        /// The point above which a 'nbf' or 'exp' value is read as
        /// milliseconds rather than the seconds the drafts specify. The
        /// Cloudflare research directory served milliseconds on
        /// 1 September 2026, and a seconds value this large is a date more
        /// than a thousand years away.
        /// </summary>
        public const long MillisecondThreshold = 100000000000L;

        /// <summary>
        /// The key type, for example 'OKP' for Ed25519, 'RSA' or 'EC'.
        /// </summary>
        public string KeyType { get; private set; }

        /// <summary>
        /// The key identifier the directory gives the key.
        /// </summary>
        public string KeyId { get; private set; }

        /// <summary>
        /// What the key is for, normally 'sig'.
        /// </summary>
        public string Use { get; private set; }

        /// <summary>
        /// The algorithm the key is for, written in the JOSE names that
        /// RFC 7518 registers, for example 'PS512'.
        /// </summary>
        public string Algorithm { get; private set; }

        /// <summary>
        /// The curve, for example 'Ed25519' or 'P-256'.
        /// </summary>
        public string Curve { get; private set; }

        /// <summary>
        /// The 'x' parameter, holding the public key for an OKP key and the
        /// x coordinate for an EC key, base64url encoded.
        /// </summary>
        public string X { get; private set; }

        /// <summary>
        /// The 'y' parameter, holding the y coordinate for an EC key,
        /// base64url encoded.
        /// </summary>
        public string Y { get; private set; }

        /// <summary>
        /// The RSA modulus, base64url encoded.
        /// </summary>
        public string Modulus { get; private set; }

        /// <summary>
        /// The RSA public exponent, base64url encoded.
        /// </summary>
        public string Exponent { get; private set; }

        /// <summary>
        /// The point in time from which the key is valid, or null when the
        /// directory did not say.
        /// </summary>
        public DateTimeOffset? NotBefore { get; private set; }

        /// <summary>
        /// The point in time at which the key stops being valid, or null
        /// when the directory did not say.
        /// </summary>
        public DateTimeOffset? Expires { get; private set; }

        /// <summary>
        /// True when a 'nbf' or 'exp' value had to be read as milliseconds.
        /// </summary>
        public bool TimesWereInMilliseconds { get; private set; }

        /// <summary>
        /// The thumbprint of the key, being the short fingerprint that the
        /// 'keyid' signature parameter carries. This is computed the first
        /// time it is asked for.
        /// </summary>
        public string Thumbprint
        {
            get
            {
                if (_thumbprint == null)
                {
                    _thumbprint = JwkThumbprint.Compute(this) ?? string.Empty;
                }
                return _thumbprint;
            }
        }

        private string _thumbprint;

        /// <summary>
        /// Read a key from the parsed directory JSON.
        /// </summary>
        /// <param name="source">The key object.</param>
        /// <returns>
        /// The key, or null when the object carries no key type.
        /// </returns>
        public static JsonWebKey Parse(IDictionary<string, object> source)
        {
            var keyType = JsonReader.GetString(source, "kty");
            if (string.IsNullOrEmpty(keyType))
            {
                return null;
            }
            var key = new JsonWebKey
            {
                KeyType = keyType,
                KeyId = JsonReader.GetString(source, "kid"),
                Use = JsonReader.GetString(source, "use"),
                Algorithm = JsonReader.GetString(source, "alg"),
                Curve = JsonReader.GetString(source, "crv"),
                X = JsonReader.GetString(source, "x"),
                Y = JsonReader.GetString(source, "y"),
                Modulus = JsonReader.GetString(source, "n"),
                Exponent = JsonReader.GetString(source, "e"),
            };
            key.NotBefore = key.ReadTime(JsonReader.GetLong(source, "nbf"));
            key.Expires = key.ReadTime(JsonReader.GetLong(source, "exp"));
            return key;
        }

        /// <summary>
        /// Answer whether the key was valid at the given point in time.
        /// </summary>
        /// <param name="when">The point in time to test.</param>
        /// <returns>
        /// True when the directory placed no limits on the key, or when the
        /// limits include the point in time given.
        /// </returns>
        public bool IsValidAt(DateTimeOffset when)
        {
            if (NotBefore.HasValue && when < NotBefore.Value)
            {
                return false;
            }
            if (Expires.HasValue && when > Expires.Value)
            {
                return false;
            }
            return true;
        }

        private DateTimeOffset? ReadTime(long? value)
        {
            if (value.HasValue == false)
            {
                return null;
            }
            var seconds = value.Value;
            if (seconds > MillisecondThreshold)
            {
                seconds /= 1000;
                TimesWereInMilliseconds = true;
            }
            try
            {
                return DateTimeOffset.FromUnixTimeSeconds(seconds);
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
        }
    }

    /// <summary>
    /// A key directory, being the JWKS an agent publishes together with the
    /// two fields the drafts add to it.
    /// </summary>
    internal sealed class KeyDirectory
    {
        /// <summary>
        /// The keys the directory publishes.
        /// </summary>
        public IList<JsonWebKey> Keys { get; }

        /// <summary>
        /// What the agent says it uses the keys for, for example 'ai',
        /// 'rag' or 'tdm'. Null when the directory did not say.
        /// </summary>
        public string Purpose { get; }

        /// <summary>
        /// The signature agent the directory names, or null when it named
        /// none.
        /// </summary>
        public string SignatureAgent { get; }

        /// <summary>
        /// True when any key in the directory carried its times in
        /// milliseconds rather than the seconds the drafts specify.
        /// </summary>
        public bool TimesWereInMilliseconds { get; }

        private KeyDirectory(
            IList<JsonWebKey> keys,
            string purpose,
            string signatureAgent,
            bool timesWereInMilliseconds)
        {
            Keys = keys;
            Purpose = purpose;
            SignatureAgent = signatureAgent;
            TimesWereInMilliseconds = timesWereInMilliseconds;
        }

        /// <summary>
        /// Read a key directory from its JSON.
        /// </summary>
        /// <param name="json">The directory document.</param>
        /// <param name="directory">The directory read.</param>
        /// <returns>
        /// True when the document is an object with a 'keys' array holding
        /// at least one key.
        /// </returns>
        public static bool TryParse(string json, out KeyDirectory directory)
        {
            directory = null;
            if (JsonReader.TryParseObject(json, out var root) == false)
            {
                return false;
            }
            return TryParse(root, out directory);
        }

        /// <summary>
        /// Read a key directory from an already parsed JSON object. An
        /// agent card carries its keys this way in its 'jwks' field.
        /// </summary>
        /// <param name="root">The directory object.</param>
        /// <param name="directory">The directory read.</param>
        /// <returns>
        /// True when the object has a 'keys' array holding at least one
        /// key.
        /// </returns>
        public static bool TryParse(
            IDictionary<string, object> root,
            out KeyDirectory directory)
        {
            directory = null;
            var keysValue = JsonReader.GetArray(root, "keys");
            if (keysValue == null)
            {
                return false;
            }
            var keys = new List<JsonWebKey>(keysValue.Count);
            var milliseconds = false;
            foreach (var item in keysValue)
            {
                if (item is IDictionary<string, object> keyObject)
                {
                    var key = JsonWebKey.Parse(keyObject);
                    if (key != null)
                    {
                        keys.Add(key);
                        milliseconds |= key.TimesWereInMilliseconds;
                    }
                }
            }
            if (keys.Count == 0)
            {
                return false;
            }
            directory = new KeyDirectory(
                keys,
                JsonReader.GetString(root, "purpose"),
                JsonReader.GetString(root, "signature_agent"),
                milliseconds);
            return true;
        }

        /// <summary>
        /// Find the key the given key id names. The key id is matched
        /// against both the 'kid' the directory gives the key and the
        /// thumbprint computed from the key itself, because the protocol
        /// draft says 'keyid' carries the thumbprint whilst some directories
        /// also use the thumbprint as the 'kid'.
        /// </summary>
        /// <param name="keyId">The key id from the signature.</param>
        /// <returns>The key, or null when the directory has no such key.</returns>
        public JsonWebKey FindKey(string keyId)
        {
            if (string.IsNullOrEmpty(keyId))
            {
                return null;
            }
            foreach (var key in Keys)
            {
                if (string.Equals(
                    key.Thumbprint, keyId, StringComparison.Ordinal))
                {
                    return key;
                }
            }
            foreach (var key in Keys)
            {
                if (string.Equals(
                    key.KeyId, keyId, StringComparison.Ordinal))
                {
                    return key;
                }
            }
            return null;
        }
    }
}
