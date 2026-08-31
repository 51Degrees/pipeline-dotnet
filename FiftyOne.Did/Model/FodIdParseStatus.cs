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

using Owid.Client.Model;

namespace FiftyOne.Did.Model
{
    /// <summary>
    /// Why a parse of a 51Did succeeded or failed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A 51Did is read from whatever a caller was given, which on a public
    /// endpoint means anything at all, so every one of these outcomes is an
    /// ordinary result rather than a fault. The vocabulary is the OWID one,
    /// <see cref="OwidParseStatus"/>, carried through unchanged with the
    /// same names and values, plus the two outcomes that belong to the
    /// 51Did payload rules. A failure the OWID reader found is reported
    /// exactly as the OWID reader named it and is never collapsed into a
    /// more general one.
    /// </para>
    /// <para>
    /// <see cref="Parsed"/> says the bytes form a 51Did. It says nothing
    /// about the signature, which is a separate question answered by
    /// <c>SignatureStatus</c> on the parsed value or by
    /// <c>DidClient.VerifySignatureDetailedAsync</c>.
    /// </para>
    /// </remarks>
    public enum FodIdParseStatus
    {
        /// <summary>
        /// The bytes form a structurally valid 51Did whose payload meets the
        /// minimum for its identifier type. The signature has not been
        /// checked.
        /// </summary>
        Parsed = (int)OwidParseStatus.Parsed,

        /// <summary>
        /// Nothing was supplied to parse.
        /// </summary>
        MissingInput = (int)OwidParseStatus.MissingInput,

        /// <summary>
        /// The input was supplied in a form the surface cannot read.
        /// </summary>
        InvalidInputType = (int)OwidParseStatus.InvalidInputType,

        /// <summary>
        /// The string is not valid base64 in either alphabet, so there are
        /// no bytes to read.
        /// </summary>
        InvalidBase64 = (int)OwidParseStatus.InvalidBase64,

        /// <summary>
        /// The first byte names an OWID version this implementation does
        /// not know.
        /// </summary>
        UnsupportedVersion = (int)OwidParseStatus.UnsupportedVersion,

        /// <summary>
        /// The data stopped in the middle of an envelope field.
        /// </summary>
        UnexpectedEnd = (int)OwidParseStatus.UnexpectedEnd,

        /// <summary>
        /// The creator domain is not terminated, or is longer than the
        /// published maximum.
        /// </summary>
        InvalidDomainEncoding = (int)OwidParseStatus.InvalidDomainEncoding,

        /// <summary>
        /// The declared payload byte count disagrees with the bytes
        /// actually present.
        /// </summary>
        ByteCountMismatch = (int)OwidParseStatus.ByteCountMismatch,

        /// <summary>
        /// The envelope is structurally consistent but larger than this
        /// runtime can hold.
        /// </summary>
        ImplementationCapacityExceeded =
            (int)OwidParseStatus.ImplementationCapacityExceeded,

        /// <summary>
        /// The envelope is malformed in a way no other status describes.
        /// </summary>
        MalformedEnvelope = (int)OwidParseStatus.MalformedEnvelope,

        /// <summary>
        /// The OWID version 0 marker, which stands for an absent node and
        /// is never a 51Did.
        /// </summary>
        AbsentNode = (int)OwidParseStatus.AbsentNode,

        /// <summary>
        /// The envelope is a valid OWID but its payload is shorter than the
        /// five byte 51Did header (one byte of flags and a four byte
        /// licence id), so the identifier type cannot be read.
        /// </summary>
        PayloadTooShort = 100,

        /// <summary>
        /// The payload holds the header but is shorter than the minimum for
        /// the identifier type the header names, being the header plus 16
        /// GUID bytes for Random and the header plus 32 hash bytes for
        /// Probabilistic and HashedEmail.
        /// </summary>
        InvalidTypePayloadLength = 101,
    }
}
