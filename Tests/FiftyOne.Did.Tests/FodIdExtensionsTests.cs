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
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.Exceptions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Owid.Client;
using Owid.Client.Model;
using System;
using System.Security.Cryptography;

namespace FiftyOne.Did.Tests
{
    /// <summary>
    /// Tests for the <see cref="FodIdExtensions"/> one-line resolution helpers
    /// (<c>As51Did</c>), on both <c>IAspectPropertyValue&lt;string&gt;</c> and a
    /// raw <see cref="string"/>.
    /// </summary>
    [TestClass]
    public class FodIdExtensionsTests
    {
        private const string TestDomain = "51degrees.com";
        private const uint CanonicalLicenseId = 0x12345678u;

        /// <summary>
        /// Mint a real signed probabilistic 51Did and return its base64 form.
        /// </summary>
        private static string SignedProbabilistic51Did()
        {
            using var crypto = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var privatePem = new string(PemEncoding.Write(
                "PRIVATE KEY", crypto.ExportPkcs8PrivateKey()));

            using var signer = ECDsa.Create();
            signer.ImportFromPem(privatePem);
            var creator = new Creator(TestDomain, signer);

            var payload = new byte[FodId.PayloadLength];
            // Flags 0x01: bits 6-7 = 00 -> Probabilistic, usage bit set.
            payload[FodId.FlagsOffset] = 0x01;
            payload[FodId.LicenseIdOffset + 0] = 0x78;
            payload[FodId.LicenseIdOffset + 1] = 0x56;
            payload[FodId.LicenseIdOffset + 2] = 0x34;
            payload[FodId.LicenseIdOffset + 3] = 0x12;
            for (var i = 0; i < FodId.HashLength; i++)
            {
                payload[FodId.HashOffset + i] = (byte)(0x20 + i);
            }

            var owid = new Owid.Client.Model.Owid
            {
                Date = DateTime.UtcNow,
                Payload = payload,
            };
            creator.Sign(owid);
            return owid.AsBase64();
        }

        [TestMethod]
        public void StringAs51Did_ParsesAValidIdentifier()
        {
            var fodId = SignedProbabilistic51Did().As51Did();

            Assert.AreEqual(IdType.Probabilistic, fodId.Type);
            Assert.AreEqual(CanonicalLicenseId, fodId.LicenseId);
            Assert.AreEqual(TestDomain, fodId.Domain);
            Assert.AreEqual(FodId.HashLength, fodId.Hash.Length);
        }

        [TestMethod]
        public void StringAs51Did_InvalidValueThrowsFormatException()
        {
            Assert.ThrowsExactly<FormatException>(() =>
            {
                _ = "this is not a valid base64 51Did".As51Did();
            });
        }

        [TestMethod]
        public void PropertyValueAs51Did_ParsesAValidIdentifier()
        {
            IAspectPropertyValue<string> value =
                new AspectPropertyValue<string>(SignedProbabilistic51Did());

            var fodId = value.As51Did();

            Assert.AreEqual(IdType.Probabilistic, fodId.Type);
            Assert.AreEqual(CanonicalLicenseId, fodId.LicenseId);
        }

        [TestMethod]
        public void PropertyValueAs51Did_NoValueThrowsNoValueException()
        {
            // The engine determined no value for this identifier (for example the
            // usage policy did not permit it).
            IAspectPropertyValue<string> value = new AspectPropertyValue<string>
            {
                NoValueMessage = "The usage policy does not permit this identifier.",
            };

            var ex = Assert.ThrowsExactly<NoValueException>(() =>
            {
                _ = value.As51Did();
            });
            Assert.AreEqual(
                "The usage policy does not permit this identifier.",
                ex.Message);
        }

        [TestMethod]
        public void PropertyValueAs51Did_NullThrowsArgumentNullException()
        {
            IAspectPropertyValue<string> value = null!;

            Assert.ThrowsExactly<ArgumentNullException>(() =>
            {
                _ = value.As51Did();
            });
        }
    }
}
