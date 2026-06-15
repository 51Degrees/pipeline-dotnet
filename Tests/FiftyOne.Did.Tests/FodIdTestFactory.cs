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

using FiftyOne.Did.Model;
using Owid.Client;
using Owid.Client.Model;
using System;
using System.Linq;
using System.Security.Cryptography;

namespace FiftyOne.Did.Tests
{
    /// <summary>
    /// Shared test helper for the 51Did tests. Generates a fresh ECDsa P-256
    /// key pair per instance and signs real OWID envelopes with it, and builds
    /// the canonical payloads the tests assert against. Centralising this here
    /// avoids duplicating the key-generation, signing and payload code across
    /// the test classes.
    /// </summary>
    internal sealed class FodIdTestFactory
    {
        /// <summary>The domain stamped into every signed test OWID.</summary>
        public const string TestDomain = "51degrees.com";

        /// <summary>
        /// The canonical flags byte (0xA5): usage bits plus the HashedEmail type
        /// tag in bits 6-7, so the 37-byte payload minimum applies.
        /// </summary>
        public const byte CanonicalFlags = 0b1010_0101;

        /// <summary>The canonical little-endian License Id, 0x12345678.</summary>
        public const uint CanonicalLicenseId = 0x12345678u;

        /// <summary>The canonical 32-byte hash value, bytes 0x20..0x3F.</summary>
        public static readonly byte[] CanonicalHash = Enumerable
            .Range(0, FodId.HashLength)
            .Select(i => (byte)(0x20 + i))
            .ToArray();

        private readonly string _privatePem;

        /// <summary>
        /// The PEM-encoded public key matching the private key used to sign,
        /// for signature-verification tests.
        /// </summary>
        public string PublicPem { get; }

        /// <summary>
        /// Generate a fresh ECDsa P-256 key pair for this instance.
        /// </summary>
        public FodIdTestFactory()
        {
            using var crypto = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            PublicPem = new string(PemEncoding.Write(
                "PUBLIC KEY", crypto.ExportSubjectPublicKeyInfo()));
            _privatePem = new string(PemEncoding.Write(
                "PRIVATE KEY", crypto.ExportPkcs8PrivateKey()));
        }

        /// <summary>
        /// A canonical 37-byte 51Did payload: <see cref="CanonicalFlags"/>,
        /// <see cref="CanonicalLicenseId"/> (little-endian) and
        /// <see cref="CanonicalHash"/>.
        /// </summary>
        public static byte[] CanonicalPayload()
        {
            var payload = new byte[FodId.PayloadLength];
            payload[FodId.FlagsOffset] = CanonicalFlags;
            WriteCanonicalLicenseId(payload);
            Array.Copy(CanonicalHash, 0, payload, FodId.HashOffset, FodId.HashLength);
            return payload;
        }

        /// <summary>
        /// A canonical 21-byte Random payload: the Random type tag in bits 6-7
        /// plus usage bits 0b001, <see cref="CanonicalLicenseId"/>, and a stable
        /// 16-byte GUID block (0x40..0x4F).
        /// </summary>
        public static byte[] CanonicalRandomPayload()
        {
            var payload = new byte[FodId.RandomPayloadLength];
            payload[FodId.FlagsOffset] = (byte)((byte)IdType.Random << 6 | 0b001);
            WriteCanonicalLicenseId(payload);
            for (int i = 0; i < FodId.GuidLength; i++)
            {
                payload[FodId.HashOffset + i] = (byte)(0x40 + i);
            }
            return payload;
        }

        /// <summary>
        /// Create and sign a real OWID with the given payload, using this
        /// instance's key pair.
        /// </summary>
        public Owid.Client.Model.Owid SignedOwid(byte[] payload)
        {
            using var crypto = ECDsa.Create();
            crypto.ImportFromPem(_privatePem);
            var creator = new Creator(TestDomain, crypto);
            var owid = new Owid.Client.Model.Owid
            {
                Date = DateTime.UtcNow,
                Payload = payload,
            };
            creator.Sign(owid);
            return owid;
        }

        /// <summary>
        /// Sign the given payload and return the OWID as base64.
        /// </summary>
        public string SignedOwidBase64(byte[] payload) =>
            SignedOwid(payload).AsBase64();

        private static void WriteCanonicalLicenseId(byte[] payload)
        {
            // Little-endian: low byte first.
            payload[FodId.LicenseIdOffset + 0] = 0x78;
            payload[FodId.LicenseIdOffset + 1] = 0x56;
            payload[FodId.LicenseIdOffset + 2] = 0x34;
            payload[FodId.LicenseIdOffset + 3] = 0x12;
        }
    }
}
