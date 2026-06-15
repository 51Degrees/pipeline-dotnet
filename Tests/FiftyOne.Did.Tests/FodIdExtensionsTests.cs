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
using System;
using static FiftyOne.Did.Tests.FodIdTestFactory;

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
        private FodIdTestFactory _factory = null!;

        [TestInitialize]
        public void TestInitialize()
        {
            _factory = new FodIdTestFactory();
        }

        [TestMethod]
        public void StringAs51Did_ParsesAValidIdentifier()
        {
            var fodId = _factory.SignedOwidBase64(CanonicalPayload()).As51Did();

            // The canonical payload carries the HashedEmail type tag in its flags.
            Assert.AreEqual(IdType.HashedEmail, fodId.Type);
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
                new AspectPropertyValue<string>(_factory.SignedOwidBase64(CanonicalPayload()));

            var fodId = value.As51Did();

            Assert.AreEqual(IdType.HashedEmail, fodId.Type);
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
