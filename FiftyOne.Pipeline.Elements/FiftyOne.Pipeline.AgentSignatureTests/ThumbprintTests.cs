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
using FiftyOne.Pipeline.AgentSignature.Tests.Helpers;
using FiftyOne.Pipeline.AgentSignature.Verification;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace FiftyOne.Pipeline.AgentSignature.Tests
{
    /// <summary>
    /// Proves that the thumbprint of a public key is the one RFC 7638 and
    /// RFC 8037 Appendix A.3 define. The 'keyid' signature parameter carries
    /// this thumbprint and the element finds the key by matching it, so a
    /// thumbprint that is off by one character makes every signature from
    /// that agent read as naming a key the agent does not publish.
    /// </summary>
    [TestClass]
    public class ThumbprintTests
    {
        /// <summary>
        /// The RFC 7517 Appendix A.1 EC public key, with the two members
        /// that RFC 7638 says take no part in the thumbprint.
        /// </summary>
        private const string EcKeyJson =
            "{\"kty\":\"EC\"," +
            "\"crv\":\"P-256\"," +
            "\"x\":\"MKBCTNIcKUSDii11ySs3526iDZ8AiTo7Tu6KPAqv7D4\"," +
            "\"y\":\"4Etl6SRW2YiLUrN5vfvVHuhp7x8PxltmWWlbbM4IFyM\"," +
            "\"use\":\"enc\"," +
            "\"kid\":\"1\"}";

        /// <summary>
        /// The canonical JSON of the EC key above, being the four members
        /// RFC 7638 section 3.2 names, in order of name and with no
        /// whitespace.
        /// </summary>
        private const string EcCanonicalJson =
            "{\"crv\":\"P-256\"," +
            "\"kty\":\"EC\"," +
            "\"x\":\"MKBCTNIcKUSDii11ySs3526iDZ8AiTo7Tu6KPAqv7D4\"," +
            "\"y\":\"4Etl6SRW2YiLUrN5vfvVHuhp7x8PxltmWWlbbM4IFyM\"}";

        /// <summary>
        /// The key thumbprint cases the standards publish.
        /// </summary>
        /// <returns>The case name and the case.</returns>
        public static IEnumerable<object[]> ThumbprintCases()
        {
            foreach (var vector in Fixtures.Thumbprints())
            {
                yield return new object[] { vector.Name, vector };
            }
        }

        /// <summary>
        /// Hashing the canonical JSON the standard publishes gives the
        /// thumbprint the standard publishes.
        /// </summary>
        /// <param name="caseName">The name of the case.</param>
        /// <param name="vector">The case.</param>
        [DataTestMethod]
        [DynamicData(nameof(ThumbprintCases), DynamicDataSourceType.Method)]
        public void CanonicalJsonHashesToTheStatedThumbprint(
            string caseName,
            ThumbprintVector vector)
        {
            Assert.AreEqual(
                vector.Thumbprint,
                JwkThumbprint.ComputeFromCanonicalJson(vector.CanonicalJson),
                "Hashing the canonical JSON of the '" + caseName +
                    "' key, being '" + vector.CanonicalJson +
                    "', should give the thumbprint '" + vector.Thumbprint +
                    "'.");
        }

        /// <summary>
        /// Reading a key from its JSON and fingerprinting it gives the
        /// thumbprint the standard publishes, which is the path the element
        /// itself takes for every key an agent publishes.
        /// </summary>
        /// <param name="caseName">The name of the case.</param>
        /// <param name="vector">The case.</param>
        [DataTestMethod]
        [DynamicData(nameof(ThumbprintCases), DynamicDataSourceType.Method)]
        public void KeyReadFromJsonGivesTheStatedThumbprint(
            string caseName,
            ThumbprintVector vector)
        {
            var key = ReadKey(vector.KeyJson, caseName);
            Assert.AreEqual(
                vector.Thumbprint,
                JwkThumbprint.Compute(key),
                "The '" + caseName + "' key should fingerprint to '" +
                    vector.Thumbprint + "'.");
            Assert.AreEqual(
                vector.Thumbprint,
                key.Thumbprint,
                "The thumbprint the key remembers should be the same as " +
                    "the one just computed for the '" + caseName + "' key.");
        }

        /// <summary>
        /// The canonical JSON built from a key is the one the standard
        /// publishes for that key, which is what proves the member order and
        /// the choice of members rather than only the hash of them.
        /// </summary>
        /// <param name="caseName">The name of the case.</param>
        /// <param name="vector">The case.</param>
        [DataTestMethod]
        [DynamicData(nameof(ThumbprintCases), DynamicDataSourceType.Method)]
        public void CanonicalJsonBuiltFromKeyIsTheStatedText(
            string caseName,
            ThumbprintVector vector)
        {
            var key = ReadKey(vector.KeyJson, caseName);
            Assert.AreEqual(
                vector.CanonicalJson,
                JwkThumbprint.BuildCanonicalJson(key),
                "The canonical JSON of the '" + caseName +
                    "' key should be the text the standard publishes, " +
                    "being '" + vector.CanonicalJson + "'.");
        }

        /// <summary>
        /// The Ed25519 key the tests sign with fingerprints to the
        /// thumbprint its vectors name in the 'keyid' parameter.
        /// </summary>
        [TestMethod]
        public void Ed25519TestKeyGivesTheExpectedThumbprint()
        {
            var key = ReadKey(Fixtures.Ed25519Key(), "ed25519 test key");
            Assert.AreEqual(
                Fixtures.Ed25519Thumbprint,
                JwkThumbprint.Compute(key),
                "The Ed25519 test key should fingerprint to '" +
                    Fixtures.Ed25519Thumbprint + "', which is the 'keyid' " +
                    "the architecture vectors signed with it name.");
        }

        /// <summary>
        /// The RSA key the tests sign with fingerprints to the thumbprint
        /// its vectors name in the 'keyid' parameter.
        /// </summary>
        [TestMethod]
        public void RsaTestKeyGivesTheExpectedThumbprint()
        {
            var key = ReadKey(Fixtures.RsaKey(), "rsa test key");
            Assert.AreEqual(
                Fixtures.RsaThumbprint,
                JwkThumbprint.Compute(key),
                "The RSA test key should fingerprint to '" +
                    Fixtures.RsaThumbprint + "', which is the 'keyid' the " +
                    "architecture vectors signed with it name.");
        }

        /// <summary>
        /// The canonical JSON of an OKP key holds the three members
        /// RFC 8037 Appendix A.3 names, in order of name, and nothing else,
        /// even though the key carries a key id and a private part.
        /// </summary>
        [TestMethod]
        public void OkpCanonicalJsonHoldsTheRequiredMembersInOrder()
        {
            var key = ReadKey(Fixtures.Ed25519Key(), "ed25519 test key");
            Assert.AreEqual(
                "{\"crv\":\"Ed25519\"," +
                    "\"kty\":\"OKP\"," +
                    "\"x\":\"JrQLj5P_89iXES9-vFgrIy29clF9CC_oPPsw3c5D0bs\"}",
                JwkThumbprint.BuildCanonicalJson(key),
                "An OKP key should give the members 'crv', 'kty' and 'x' " +
                    "in that order and nothing else, so the 'kid' and the " +
                    "private part the test key carries should be left out.");
        }

        /// <summary>
        /// The canonical JSON of an RSA key holds the three members RFC 7638
        /// section 3.2 names, in order of name, and nothing else, even
        /// though the key carries a key id, an algorithm and a private part.
        /// </summary>
        [TestMethod]
        public void RsaCanonicalJsonHoldsTheRequiredMembersInOrder()
        {
            var key = ReadKey(Fixtures.RsaKey(), "rsa test key");
            var canonical = JwkThumbprint.BuildCanonicalJson(key);
            StringAssert.StartsWith(
                canonical,
                "{\"e\":\"" + key.Exponent + "\",\"kty\":\"RSA\",\"n\":\"",
                "An RSA key should give the members 'e', 'kty' and 'n' in " +
                    "that order, so the canonical JSON should open with " +
                    "the exponent and the key type.");
            Assert.AreEqual(
                "{\"e\":\"" + key.Exponent + "\"," +
                    "\"kty\":\"RSA\"," +
                    "\"n\":\"" + key.Modulus + "\"}",
                canonical,
                "An RSA key should give the members 'e', 'kty' and 'n' and " +
                    "nothing else, so the 'kid', the 'alg' and the private " +
                    "part the test key carries should be left out.");
        }

        /// <summary>
        /// The canonical JSON of an EC key holds the four members RFC 7638
        /// section 3.2 names, in order of name, and leaves out the 'use' and
        /// 'kid' members that the standard's own example carries.
        /// </summary>
        [TestMethod]
        public void EcCanonicalJsonHoldsTheRequiredMembersInOrder()
        {
            var key = ReadKey(EcKeyJson, "ec example key");
            Assert.AreEqual(
                EcCanonicalJson,
                JwkThumbprint.BuildCanonicalJson(key),
                "An EC key should give the members 'crv', 'kty', 'x' and " +
                    "'y' in that order and nothing else, so the 'use' and " +
                    "'kid' the example key carries should be left out.");
        }

        /// <summary>
        /// A key of a type the element cannot fingerprint, and a key missing
        /// a member the standard requires, are answered with no thumbprint
        /// rather than with a wrong one.
        /// </summary>
        [TestMethod]
        public void KeyThatCannotBeFingerprintedGivesNoThumbprint()
        {
            Assert.IsNull(
                JwkThumbprint.BuildCanonicalJson(null),
                "No key at all should give no canonical JSON.");
            var unknown = ReadKey(
                "{\"kty\":\"oct\",\"k\":\"AQID\"}", "shared secret key");
            Assert.IsNull(
                JwkThumbprint.Compute(unknown),
                "A shared secret key is not one RFC 7638 fingerprints, so " +
                    "it should give no thumbprint.");
            var incomplete = ReadKey(
                "{\"kty\":\"OKP\",\"crv\":\"Ed25519\"}", "OKP key with no x");
            Assert.IsNull(
                JwkThumbprint.Compute(incomplete),
                "An OKP key with no 'x' member is missing a member the " +
                    "standard requires, so it should give no thumbprint.");
        }

        /// <summary>
        /// Read a key from its JSON the way the element does.
        /// </summary>
        /// <param name="keyJson">The key as JSON.</param>
        /// <param name="caseName">The name of the case.</param>
        /// <returns>The key.</returns>
        private static JsonWebKey ReadKey(string keyJson, string caseName)
        {
            Assert.IsTrue(
                JsonReader.TryParseObject(keyJson, out var source),
                "The '" + caseName + "' key should be read as a JSON " +
                    "object from '" + keyJson + "'.");
            var key = JsonWebKey.Parse(source);
            Assert.IsNotNull(
                key,
                "The '" + caseName + "' key should be read as a key.");
            return key;
        }
    }
}
