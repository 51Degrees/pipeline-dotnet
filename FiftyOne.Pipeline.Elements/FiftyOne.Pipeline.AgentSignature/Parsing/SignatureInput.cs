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

using System.Collections.Generic;

namespace FiftyOne.Pipeline.AgentSignature.Parsing
{
    /// <summary>
    /// One signature offered by the request, being the pairing of a member
    /// of the 'Signature-Input' header with the member of the 'Signature'
    /// header that carries the same label.
    /// </summary>
    internal sealed class SignatureCandidate
    {
        /// <summary>
        /// The label the two headers share, for example 'sig1'.
        /// </summary>
        public string Label { get; }

        /// <summary>
        /// The components the signature covers, in the order the signer
        /// listed them.
        /// </summary>
        public IList<SfItem> CoveredComponents { get; }

        /// <summary>
        /// The strict serialisation of the 'Signature-Input' member value,
        /// being the inner list of covered components with its parameters
        /// written the one way RFC 8941 section 4.1 allows. RFC 9421
        /// section 2.3 has the signer build the last line of the signature
        /// base from this form, so the verifier rebuilds from the same
        /// form rather than from the text as the agent happened to write
        /// it.
        /// </summary>
        public string SignatureParams { get; }

        /// <summary>
        /// The signature bytes from the 'Signature' header.
        /// </summary>
        public byte[] Signature { get; }

        /// <summary>
        /// The 'created' parameter as Unix seconds, or null when absent.
        /// </summary>
        public long? Created { get; }

        /// <summary>
        /// The 'expires' parameter as Unix seconds, or null when absent.
        /// </summary>
        public long? Expires { get; }

        /// <summary>
        /// The 'keyid' parameter, being the thumbprint (a short
        /// fingerprint) of the public key, or null when absent.
        /// </summary>
        public string KeyId { get; }

        /// <summary>
        /// The 'tag' parameter, which Web Bot Auth requires to be
        /// 'web-bot-auth', or null when absent.
        /// </summary>
        public string Tag { get; }

        /// <summary>
        /// The 'nonce' parameter, or null when absent.
        /// </summary>
        public string Nonce { get; }

        /// <summary>
        /// The 'alg' parameter, or null when absent. The parameter is
        /// optional, because the key normally says which algorithm it is
        /// for.
        /// </summary>
        public string Algorithm { get; }

        /// <summary>
        /// Construct a candidate from the two parsed header members.
        /// </summary>
        /// <param name="label">The label the two members share.</param>
        /// <param name="input">
        /// The 'Signature-Input' member, which must hold an inner list.
        /// </param>
        /// <param name="signature">The signature bytes.</param>
        public SignatureCandidate(
            string label,
            SfMember input,
            byte[] signature)
        {
            Label = label;
            CoveredComponents = input.InnerList;
            SignatureParams = StructuredFieldSerializer.Serialize(input);
            Signature = signature;
            Created = input.TryGetLongParameter("created", out var created)
                ? created
                : (long?)null;
            Expires = input.TryGetLongParameter("expires", out var expires)
                ? expires
                : (long?)null;
            KeyId = input.GetStringParameter("keyid");
            Tag = input.GetStringParameter("tag");
            Nonce = input.GetStringParameter("nonce");
            Algorithm = input.GetStringParameter("alg");
        }

        /// <summary>
        /// Build the list of signatures the request offers, being every
        /// label that appears in both headers with an inner list of covered
        /// components and a byte sequence signature.
        /// </summary>
        /// <param name="input">The parsed 'Signature-Input' header.</param>
        /// <param name="signature">The parsed 'Signature' header.</param>
        /// <param name="candidates">The signatures found.</param>
        /// <returns>
        /// False when a label appears in one header without the other, or
        /// when a member is not of the shape the standard requires, which
        /// the element reports as Malformed.
        /// </returns>
        public static bool TryBuild(
            SfDictionary input,
            SfDictionary signature,
            out IList<SignatureCandidate> candidates)
        {
            candidates = null;
            if (input == null ||
                signature == null ||
                input.Count == 0 ||
                input.Count != signature.Count)
            {
                return false;
            }

            var result = new List<SignatureCandidate>(input.Count);
            foreach (var entry in input.Members)
            {
                if (entry.Value.IsInnerList == false)
                {
                    return false;
                }
                if (signature.TryGetValue(
                    entry.Key, out var signatureMember) == false)
                {
                    return false;
                }
                if (signatureMember.IsInnerList ||
                    (signatureMember.Item.Value is byte[]) == false)
                {
                    return false;
                }
                result.Add(new SignatureCandidate(
                    entry.Key,
                    entry.Value,
                    (byte[])signatureMember.Item.Value));
            }

            candidates = result;
            return true;
        }
    }
}
