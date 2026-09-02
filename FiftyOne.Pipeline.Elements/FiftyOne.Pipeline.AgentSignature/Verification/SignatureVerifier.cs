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

using FiftyOne.Pipeline.AgentSignature.Keys;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using System;
using System.Security.Cryptography;

namespace FiftyOne.Pipeline.AgentSignature.Verification
{
    /// <summary>
    /// The algorithm a signature is to be checked with, together with
    /// whether this element can check it.
    /// </summary>
    internal sealed class AlgorithmResolution
    {
        /// <summary>
        /// The name to report through the AgentSignatureAlgorithm property.
        /// This is the RFC 9421 registry name when one was settled on, and
        /// otherwise whatever the signature or the key said.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// True when this element verifies signatures made with the
        /// algorithm.
        /// </summary>
        public bool Supported { get; }

        /// <summary>
        /// Construct a resolution.
        /// </summary>
        /// <param name="name">The algorithm name to report.</param>
        /// <param name="supported">
        /// True when this element verifies the algorithm.
        /// </param>
        public AlgorithmResolution(string name, bool supported)
        {
            Name = name;
            Supported = supported;
        }
    }

    /// <summary>
    /// Checks a signature against a public key.
    /// </summary>
    /// <remarks>
    /// Ed25519 is not in the .NET base library, so it comes from
    /// BouncyCastle. RSA-PSS and ECDSA use the base library.
    /// </remarks>
    internal static class SignatureVerifier
    {
        /// <summary>
        /// Settle which algorithm a signature is to be checked with.
        /// </summary>
        /// <param name="key">The key from the agent's directory.</param>
        /// <param name="signatureAlgorithm">
        /// The 'alg' signature parameter, which is optional and holds an
        /// RFC 9421 registry name.
        /// </param>
        /// <returns>
        /// The algorithm settled on, or an unsupported resolution when the
        /// key and the signature disagree, when neither says which
        /// algorithm to use, or when the algorithm is one this element does
        /// not verify.
        /// </returns>
        public static AlgorithmResolution ResolveAlgorithm(
            JsonWebKey key,
            string signatureAlgorithm)
        {
            var fromKey = FromKey(key);
            var fromSignature = string.IsNullOrEmpty(signatureAlgorithm)
                ? null
                : signatureAlgorithm.ToLowerInvariant();

            if (fromKey != null &&
                fromSignature != null &&
                string.Equals(
                    fromKey, fromSignature, StringComparison.Ordinal)
                    == false)
            {
                // The key says one thing and the signature says another, so
                // there is nothing safe to check with.
                return new AlgorithmResolution(fromSignature, false);
            }

            var chosen = fromSignature ?? fromKey;
            if (chosen == null)
            {
                return new AlgorithmResolution(
                    key?.Algorithm ?? key?.KeyType ?? string.Empty, false);
            }
            return new AlgorithmResolution(chosen, IsSupported(chosen));
        }

        /// <summary>
        /// Answer whether this element verifies signatures made with the
        /// named algorithm.
        /// </summary>
        /// <param name="algorithm">The RFC 9421 registry name.</param>
        /// <returns>True when the algorithm is one of the three.</returns>
        public static bool IsSupported(string algorithm)
        {
            return string.Equals(
                    algorithm, Constants.ALGORITHM_ED25519,
                    StringComparison.Ordinal) ||
                string.Equals(
                    algorithm, Constants.ALGORITHM_RSA_PSS_SHA512,
                    StringComparison.Ordinal) ||
                string.Equals(
                    algorithm, Constants.ALGORITHM_ECDSA_P256_SHA256,
                    StringComparison.Ordinal);
        }

        /// <summary>
        /// Check a signature against a key.
        /// </summary>
        /// <param name="algorithm">
        /// The RFC 9421 registry name of the algorithm.
        /// </param>
        /// <param name="key">The public key.</param>
        /// <param name="signatureBase">
        /// The bytes that were signed, being the ASCII of the signature
        /// base.
        /// </param>
        /// <param name="signature">The signature bytes.</param>
        /// <returns>
        /// True when the signature checks out. False for any failure,
        /// including a key this element cannot read, because a signature
        /// that cannot be checked has not been checked.
        /// </returns>
        public static bool Verify(
            string algorithm,
            JsonWebKey key,
            byte[] signatureBase,
            byte[] signature)
        {
            if (key == null || signatureBase == null || signature == null)
            {
                return false;
            }
            try
            {
                switch (algorithm)
                {
                    case Constants.ALGORITHM_ED25519:
                        return VerifyEd25519(key, signatureBase, signature);
                    case Constants.ALGORITHM_RSA_PSS_SHA512:
                        return VerifyRsaPss(key, signatureBase, signature);
                    case Constants.ALGORITHM_ECDSA_P256_SHA256:
                        return VerifyEcdsa(key, signatureBase, signature);
                    default:
                        return false;
                }
            }
            catch (CryptographicException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }
        }

        private static string FromKey(JsonWebKey key)
        {
            if (key == null)
            {
                return null;
            }
            // A key that names its own algorithm does so in the JOSE names
            // that RFC 7518 and RFC 8037 register, which are not the
            // RFC 9421 registry names the signature uses.
            if (string.IsNullOrEmpty(key.Algorithm) == false)
            {
                switch (key.Algorithm.ToUpperInvariant())
                {
                    case "EDDSA":
                        return Constants.ALGORITHM_ED25519;
                    case "PS512":
                        return Constants.ALGORITHM_RSA_PSS_SHA512;
                    case "ES256":
                        return Constants.ALGORITHM_ECDSA_P256_SHA256;
                    case "HS256":
                        return Constants.ALGORITHM_HMAC_SHA256;
                    default:
                        // A JOSE algorithm with no RFC 9421 equivalent this
                        // element verifies. Report it as it was written.
                        return key.Algorithm.ToLowerInvariant();
                }
            }
            switch (key.KeyType)
            {
                case "OKP":
                    return string.Equals(
                        key.Curve, "Ed25519", StringComparison.Ordinal)
                        ? Constants.ALGORITHM_ED25519
                        : key.Curve?.ToLowerInvariant();
                case "EC":
                    return string.Equals(
                        key.Curve, "P-256", StringComparison.Ordinal)
                        ? Constants.ALGORITHM_ECDSA_P256_SHA256
                        : key.Curve?.ToLowerInvariant();
                case "oct":
                    // A shared secret, which the Web Bot Auth protocol
                    // forbids for signing requests.
                    return Constants.ALGORITHM_HMAC_SHA256;
                default:
                    // An RSA key on its own does not say which of the RSA
                    // algorithms it is for, so the signature has to.
                    return null;
            }
        }

        private static bool VerifyEd25519(
            JsonWebKey key,
            byte[] signatureBase,
            byte[] signature)
        {
            if (Base64Url.TryDecode(key.X, out var publicKey) == false ||
                publicKey.Length != Ed25519PublicKeyParameters.KeySize)
            {
                return false;
            }
            var parameters = new Ed25519PublicKeyParameters(publicKey, 0);
            var signer = new Ed25519Signer();
            signer.Init(false, parameters);
            signer.BlockUpdate(signatureBase, 0, signatureBase.Length);
            return signer.VerifySignature(signature);
        }

        private static bool VerifyRsaPss(
            JsonWebKey key,
            byte[] signatureBase,
            byte[] signature)
        {
            if (Base64Url.TryDecode(key.Modulus, out var modulus) == false ||
                Base64Url.TryDecode(key.Exponent, out var exponent) == false)
            {
                return false;
            }
            using (var rsa = RSA.Create())
            {
                rsa.ImportParameters(new RSAParameters
                {
                    Modulus = modulus,
                    Exponent = exponent,
                });
                // RFC 9421 section 3.3.1 specifies a salt the same length
                // as the hash, which is what .NET uses for PSS.
                return rsa.VerifyData(
                    signatureBase,
                    signature,
                    HashAlgorithmName.SHA512,
                    RSASignaturePadding.Pss);
            }
        }

        private static bool VerifyEcdsa(
            JsonWebKey key,
            byte[] signatureBase,
            byte[] signature)
        {
            if (Base64Url.TryDecode(key.X, out var x) == false ||
                Base64Url.TryDecode(key.Y, out var y) == false)
            {
                return false;
            }
            var parameters = new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP256,
                Q = new ECPoint { X = x, Y = y },
            };
            using (var ecdsa = ECDsa.Create())
            {
                if (ecdsa == null)
                {
                    return false;
                }
                ecdsa.ImportParameters(parameters);
                // RFC 9421 section 3.3.4 puts r and s side by side, which is
                // the format .NET expects by default.
                return ecdsa.VerifyData(
                    signatureBase, signature, HashAlgorithmName.SHA256);
            }
        }
    }
}
