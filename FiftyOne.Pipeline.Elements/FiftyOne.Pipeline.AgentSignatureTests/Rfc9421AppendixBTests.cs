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
using FiftyOne.Pipeline.AgentSignature.Parsing;
using FiftyOne.Pipeline.AgentSignature.Tests.Helpers;
using FiftyOne.Pipeline.AgentSignature.Verification;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace FiftyOne.Pipeline.AgentSignature.Tests
{
    /// <summary>
    /// One of the six worked examples of RFC 9421 Appendix B.2, read from
    /// the fixture that holds them.
    /// </summary>
    public class AppendixBVector
    {
        /// <summary>The name the appendix gives the example.</summary>
        public string Name { get; set; }

        /// <summary>The section number, for example 'B.2.4'.</summary>
        public string Section { get; set; }

        /// <summary>
        /// The RFC 9421 registry name of the algorithm the example uses.
        /// </summary>
        public string Algorithm { get; set; }

        /// <summary>The label the two signature headers share.</summary>
        public string Label { get; set; }

        /// <summary>
        /// True when this element verifies signatures made with the
        /// algorithm of this example.
        /// </summary>
        public bool Supported { get; set; }

        /// <summary>
        /// The signature base the appendix prints, unfolded.
        /// </summary>
        public string SignatureBase { get; set; }

        /// <summary>
        /// The value of the 'Signature-Input' header the appendix prints,
        /// unfolded.
        /// </summary>
        public string SignatureInputHeader { get; set; }

        /// <summary>
        /// The value of the 'Signature' header the appendix prints,
        /// unfolded.
        /// </summary>
        public string SignatureHeader { get; set; }

        /// <summary>
        /// The text of the signature parameters, being everything the
        /// appendix prints on the '@signature-params' line after the colon
        /// and the space.
        /// </summary>
        public string SignatureParams { get; set; }

        /// <summary>
        /// The covered components of the example, each holding the
        /// identifier and the value the appendix prints on that line of the
        /// signature base.
        /// </summary>
        public IList<AppendixBComponent> Components { get; } =
            new List<AppendixBComponent>();

        /// <summary>
        /// The name the appendix gives the key, for example
        /// 'test-key-ecc-p256'.
        /// </summary>
        public string KeyName { get; set; }

        /// <summary>The public part of the key, as JSON.</summary>
        public string KeyJson { get; set; }

        /// <inheritdoc/>
        public override string ToString() =>
            Section + " " + Label + " " + Algorithm;
    }

    /// <summary>
    /// One covered component of a worked example, being one line of the
    /// signature base the appendix prints.
    /// </summary>
    public class AppendixBComponent
    {
        /// <summary>
        /// The component identifier with its parameters, written exactly as
        /// the appendix writes it, for example '"@query-param";name="Pet"'.
        /// </summary>
        public string Identifier { get; set; }

        /// <summary>
        /// The value the appendix prints for the component on that line.
        /// </summary>
        public string Value { get; set; }
    }

    /// <summary>
    /// Pins this element to the worked examples that RFC 9421 Appendix B
    /// prints. The appendix is the strongest check available on the
    /// signature base builder, because the base it prints is the standard's
    /// own output rather than ours, so a base built any other way is wrong
    /// no matter how self consistent the rest of the code is.
    /// </summary>
    /// <remarks>
    /// These are unit tests of the signature base builder and of the
    /// verifier, not of the flow element. Several of the examples cover
    /// components the element cannot rebuild from pipeline evidence, being
    /// '@method', '@path', '@target-uri', '@query' and the '@status' of a
    /// response, so a real request shaped like B.2.3 or B.2.6 would read
    /// Unverified with the ComponentUnavailable reason rather than
    /// verifying. The limitation is written up on
    /// AgentSignatureElement.FlowDataComponentResolver in
    /// FiftyOne.Pipeline.AgentSignature/FlowElement/AgentSignatureElement.cs
    /// and the follow-up work is to put the request line into evidence so
    /// that those components can be rebuilt. The tests below feed the
    /// component values in directly instead, which is what lets the base
    /// builder be checked against the standard on its own.
    ///
    /// The values in the fixture were unfolded from the published RFC text
    /// using the convention of RFC 8792, where a line ending in a backslash
    /// continues on the next line and the leading whitespace of that next
    /// line is not part of the value. See Fixtures/SOURCE.txt.
    /// </remarks>
    [TestClass]
    public class Rfc9421AppendixBTests
    {
        /// <summary>
        /// The name of the fixture holding the six worked examples.
        /// </summary>
        private const string FixtureName = "rfc9421_appendix_b.json";

        /// <summary>
        /// The thumbprint of the Appendix B.1.3 P-256 test key, being the
        /// base64url of the SHA-256 of the canonical JSON below. This was
        /// worked out from the standard rather than from this element, so
        /// it fails if the element ever fingerprints an EC key differently.
        /// </summary>
        private const string EcThumbprint =
            "ydQXMtvbsOsZyFir-Y7A8t7fKEM1gbKPvyFkdpu4fvI";

        /// <summary>
        /// The canonical JSON of the Appendix B.1.3 P-256 test key, being
        /// the four members RFC 7638 section 3.2 names, in order of name
        /// and with no whitespace.
        /// </summary>
        private const string EcCanonicalJson =
            "{\"crv\":\"P-256\"," +
            "\"kty\":\"EC\"," +
            "\"x\":\"qIVYZVLCrPZHGHjP17CTW0_-D9Lfw0EkjqF7xB4FivA\"," +
            "\"y\":\"Mc4nN9LTDOBhfoUeg8Ye9WedFRhnZXZJA12Qp0zZ6F0\"}";

        /// <summary>
        /// All six worked examples of Appendix B.2.
        /// </summary>
        /// <returns>The section number and the example.</returns>
        public static IEnumerable<object[]> AllExamples()
        {
            foreach (var vector in ReadVectors())
            {
                yield return new object[] { vector.Section, vector };
            }
        }

        /// <summary>
        /// The worked examples whose algorithm this element verifies, being
        /// the rsa-pss-sha512, ecdsa-p256-sha256 and ed25519 ones.
        /// </summary>
        /// <returns>The section number and the example.</returns>
        public static IEnumerable<object[]> SupportedExamples()
        {
            foreach (var vector in ReadVectors())
            {
                if (vector.Supported)
                {
                    yield return new object[] { vector.Section, vector };
                }
            }
        }

        /// <summary>
        /// The fixture holds all six worked examples, so that a file that
        /// failed to copy or was cut short is reported as such rather than
        /// as a suite that quietly checks nothing.
        /// </summary>
        [TestMethod]
        public void AllSixExamplesArePresent()
        {
            var vectors = ReadVectors();
            Assert.AreEqual(
                6,
                vectors.Count,
                "RFC 9421 Appendix B.2 prints six worked examples, being " +
                    "B.2.1 to B.2.6, so the fixture should hold six.");
            var sections = new List<string>();
            foreach (var vector in vectors)
            {
                sections.Add(vector.Section);
            }
            CollectionAssert.AreEqual(
                new[] { "B.2.1", "B.2.2", "B.2.3", "B.2.4", "B.2.5",
                    "B.2.6" },
                sections,
                "The fixture should hold the six sections of RFC 9421 " +
                    "Appendix B.2 in order.");
        }

        /// <summary>
        /// The signature base built from the covered components of an
        /// example is the exact text that RFC 9421 Appendix B prints for
        /// that example, character for character. This is the headline
        /// check, because the base is what gets signed, so a base that
        /// differs anywhere makes every signature read as forged.
        /// </summary>
        /// <param name="section">The appendix section number.</param>
        /// <param name="vector">The worked example.</param>
        [DataTestMethod]
        [DynamicData(nameof(AllExamples), DynamicDataSourceType.Method)]
        public void SignatureBaseIsTheTextTheAppendixPrints(
            string section,
            AppendixBVector vector)
        {
            var member = ParseSignatureInput(vector);
            var resolver = new PrintedComponentResolver(vector);
            Assert.IsTrue(
                SignatureBase.TryBuild(
                    member.InnerList,
                    member.Raw,
                    resolver,
                    out var text),
                "The signature base of the RFC 9421 Appendix " + section +
                    " example should be built from its " +
                    member.InnerList.Count + " covered components.");
            Assert.AreEqual(
                vector.SignatureBase,
                text,
                "The signature base built for the RFC 9421 Appendix " +
                    section + " example should be the text the appendix " +
                    "prints, character for character.");
            Assert.AreEqual(
                vector.Components.Count,
                resolver.Resolved,
                "Building the signature base of the RFC 9421 Appendix " +
                    section + " example should ask for each of its " +
                    vector.Components.Count + " covered components once.");
        }

        /// <summary>
        /// The covered components read out of the 'Signature-Input' header
        /// are the ones the appendix prints in the signature base, in the
        /// same order and written the same way. This separates a parsing
        /// fault from a base building fault when the check above fails.
        /// </summary>
        /// <param name="section">The appendix section number.</param>
        /// <param name="vector">The worked example.</param>
        [DataTestMethod]
        [DynamicData(nameof(AllExamples), DynamicDataSourceType.Method)]
        public void CoveredComponentsAreTheOnesTheAppendixPrints(
            string section,
            AppendixBVector vector)
        {
            var member = ParseSignatureInput(vector);
            Assert.AreEqual(
                vector.Components.Count,
                member.InnerList.Count,
                "The 'Signature-Input' header of the RFC 9421 Appendix " +
                    section + " example should list the same number of " +
                    "components as its signature base has lines before " +
                    "the '@signature-params' line.");
            for (var i = 0; i < vector.Components.Count; i++)
            {
                Assert.AreEqual(
                    vector.Components[i].Identifier,
                    member.InnerList[i].Raw,
                    "Component " + i + " of the RFC 9421 Appendix " +
                        section + " example should be read as '" +
                        vector.Components[i].Identifier + "'.");
            }
        }

        /// <summary>
        /// The text of the signature parameters kept from the
        /// 'Signature-Input' header is the text the appendix prints on the
        /// '@signature-params' line of its signature base. The two are
        /// wrapped separately in the RFC, so agreeing on them is a check on
        /// the parser keeping the header text exactly as it was sent.
        /// </summary>
        /// <param name="section">The appendix section number.</param>
        /// <param name="vector">The worked example.</param>
        [DataTestMethod]
        [DynamicData(nameof(AllExamples), DynamicDataSourceType.Method)]
        public void SignatureParamsTextIsKeptExactly(
            string section,
            AppendixBVector vector)
        {
            var member = ParseSignatureInput(vector);
            Assert.AreEqual(
                vector.SignatureParams,
                member.Raw,
                "The signature parameters of the RFC 9421 Appendix " +
                    section + " example should be kept as '" +
                    vector.SignatureParams + "'.");
        }

        /// <summary>
        /// The signature the appendix prints checks out against the key the
        /// appendix prints and the base the appendix prints. The B.2.4 case
        /// matters most, because ecdsa-p256-sha256 is reached by no other
        /// test in this suite, so without it that branch of the verifier
        /// has never been run against a known good signature.
        /// </summary>
        /// <param name="section">The appendix section number.</param>
        /// <param name="vector">The worked example.</param>
        [DataTestMethod]
        [DynamicData(nameof(SupportedExamples), DynamicDataSourceType.Method)]
        public void PrintedSignatureVerifies(
            string section,
            AppendixBVector vector)
        {
            var key = ReadKey(vector);
            Assert.IsTrue(
                SignatureVerifier.Verify(
                    vector.Algorithm,
                    key,
                    Encoding.ASCII.GetBytes(vector.SignatureBase),
                    ParseSignature(vector)),
                "The " + vector.Algorithm + " signature of the RFC 9421 " +
                    "Appendix " + section + " example should check out " +
                    "against the '" + vector.KeyName + "' key over the " +
                    "signature base the appendix prints.");
        }

        /// <summary>
        /// Changing one byte of the signature the appendix prints makes it
        /// stop checking out. Without this a verifier that answered true
        /// for everything would pass the check above.
        /// </summary>
        /// <param name="section">The appendix section number.</param>
        /// <param name="vector">The worked example.</param>
        [DataTestMethod]
        [DynamicData(nameof(SupportedExamples), DynamicDataSourceType.Method)]
        public void ChangedSignatureDoesNotVerify(
            string section,
            AppendixBVector vector)
        {
            var key = ReadKey(vector);
            var signature = ParseSignature(vector);
            var last = signature.Length - 1;
            signature[last] = (byte)(signature[last] ^ 0x01);
            Assert.IsFalse(
                SignatureVerifier.Verify(
                    vector.Algorithm,
                    key,
                    Encoding.ASCII.GetBytes(vector.SignatureBase),
                    signature),
                "The " + vector.Algorithm + " signature of the RFC 9421 " +
                    "Appendix " + section + " example should stop " +
                    "checking out once its last byte is changed.");
        }

        /// <summary>
        /// Changing one character of the signature base makes the signature
        /// the appendix prints stop checking out, which is what proves the
        /// signature is over that exact text and not merely over something
        /// near it.
        /// </summary>
        /// <param name="section">The appendix section number.</param>
        /// <param name="vector">The worked example.</param>
        [DataTestMethod]
        [DynamicData(nameof(SupportedExamples), DynamicDataSourceType.Method)]
        public void ChangedSignatureBaseDoesNotVerify(
            string section,
            AppendixBVector vector)
        {
            var key = ReadKey(vector);
            var text = Encoding.ASCII.GetBytes(vector.SignatureBase);
            var last = text.Length - 1;
            text[last] = (byte)(text[last] == (byte)'x'
                ? (byte)'y'
                : (byte)'x');
            Assert.IsFalse(
                SignatureVerifier.Verify(
                    vector.Algorithm,
                    key,
                    text,
                    ParseSignature(vector)),
                "The " + vector.Algorithm + " signature of the RFC 9421 " +
                    "Appendix " + section + " example should stop " +
                    "checking out once the last character of the " +
                    "signature base is changed.");
        }

        /// <summary>
        /// The key of the Appendix B.1.3 P-256 example settles on the
        /// RFC 9421 registry name 'ecdsa-p256-sha256' and is one this
        /// element verifies, which is what routes a signature made with it
        /// to the ECDSA branch of the verifier.
        /// </summary>
        [TestMethod]
        public void EcKeySettlesOnEcdsaP256Sha256()
        {
            var resolution = SignatureVerifier.ResolveAlgorithm(
                ReadKey(FindVector("B.2.4")), null);
            Assert.AreEqual(
                "ecdsa-p256-sha256",
                resolution.Name,
                "The 'test-key-ecc-p256' key of RFC 9421 Appendix B.1.3 " +
                    "is a P-256 key, so it should settle on the registry " +
                    "name 'ecdsa-p256-sha256'.");
            Assert.IsTrue(
                resolution.Supported,
                "This element verifies ecdsa-p256-sha256, so the " +
                    "'test-key-ecc-p256' key of RFC 9421 Appendix B.1.3 " +
                    "should read as supported.");
        }

        /// <summary>
        /// The key of the Appendix B.1.4 example settles on the RFC 9421
        /// registry name 'ed25519' and is one this element verifies.
        /// </summary>
        [TestMethod]
        public void Ed25519KeySettlesOnEd25519()
        {
            var resolution = SignatureVerifier.ResolveAlgorithm(
                ReadKey(FindVector("B.2.6")), null);
            Assert.AreEqual(
                "ed25519",
                resolution.Name,
                "The 'test-key-ed25519' key of RFC 9421 Appendix B.1.4 " +
                    "is an Edwards curve key, so it should settle on the " +
                    "registry name 'ed25519'.");
            Assert.IsTrue(
                resolution.Supported,
                "This element verifies ed25519, so the " +
                    "'test-key-ed25519' key of RFC 9421 Appendix B.1.4 " +
                    "should read as supported.");
        }

        /// <summary>
        /// The RSA key of the Appendix B.1.2 example settles on
        /// 'rsa-pss-sha512' when the signature names that algorithm, and is
        /// one this element verifies. An RSA key does not say on its own
        /// which of the RSA algorithms it is for, so the signature has to,
        /// which the next test covers.
        /// </summary>
        [TestMethod]
        public void RsaKeyWithAlgorithmSettlesOnRsaPssSha512()
        {
            var resolution = SignatureVerifier.ResolveAlgorithm(
                ReadKey(FindVector("B.2.1")), "rsa-pss-sha512");
            Assert.AreEqual(
                "rsa-pss-sha512",
                resolution.Name,
                "The 'test-key-rsa-pss' key of RFC 9421 Appendix B.1.2 " +
                    "with a signature naming rsa-pss-sha512 should settle " +
                    "on the registry name 'rsa-pss-sha512'.");
            Assert.IsTrue(
                resolution.Supported,
                "This element verifies rsa-pss-sha512, so the " +
                    "'test-key-rsa-pss' key of RFC 9421 Appendix B.1.2 " +
                    "with a signature naming that algorithm should read " +
                    "as supported.");
        }

        /// <summary>
        /// The RSA key of the Appendix B.1.2 example settles on nothing
        /// when the signature names no algorithm, so the element refuses it
        /// rather than guessing. RFC 9421 section 3.3 registers several
        /// algorithms that an RSA key can be used with, and the key itself
        /// does not say which. The Appendix B.2.1 to B.2.3 examples carry
        /// no 'alg' parameter, so a real request shaped like them would
        /// read as an algorithm this element cannot settle on.
        /// </summary>
        [TestMethod]
        public void RsaKeyWithoutAlgorithmIsNotSupported()
        {
            var resolution = SignatureVerifier.ResolveAlgorithm(
                ReadKey(FindVector("B.2.1")), null);
            Assert.IsFalse(
                resolution.Supported,
                "An RSA key does not say which of the RSA algorithms it " +
                    "is for, so the 'test-key-rsa-pss' key of RFC 9421 " +
                    "Appendix B.1.2 with a signature naming no algorithm " +
                    "should not read as supported.");
        }

        /// <summary>
        /// The shared secret of the Appendix B.1.5 example settles on the
        /// RFC 9421 registry name 'hmac-sha256' and reads as not supported,
        /// so the element refuses the Appendix B.2.5 signature rather than
        /// checking it. Web Bot Auth forbids shared secrets because anyone
        /// who can check such a signature can also make one.
        /// </summary>
        [TestMethod]
        public void SharedSecretIsNotSupported()
        {
            var resolution = SignatureVerifier.ResolveAlgorithm(
                ReadKey(FindVector("B.2.5")), null);
            Assert.AreEqual(
                "hmac-sha256",
                resolution.Name,
                "The 'test-shared-secret' of RFC 9421 Appendix B.1.5 is a " +
                    "shared secret, so it should settle on the registry " +
                    "name 'hmac-sha256'.");
            Assert.IsFalse(
                resolution.Supported,
                "Web Bot Auth forbids shared secrets, so the " +
                    "'test-shared-secret' of RFC 9421 Appendix B.1.5 " +
                    "should not read as supported.");
        }

        /// <summary>
        /// A signature made with the shared secret of Appendix B.1.5 is
        /// never checked, so handing the Appendix B.2.5 signature to the
        /// verifier answers false rather than accepting it. This is the
        /// promise that follows from the resolution above.
        /// </summary>
        [TestMethod]
        public void SharedSecretSignatureIsNotVerified()
        {
            var vector = FindVector("B.2.5");
            Assert.IsFalse(
                SignatureVerifier.Verify(
                    vector.Algorithm,
                    ReadKey(vector),
                    Encoding.ASCII.GetBytes(vector.SignatureBase),
                    ParseSignature(vector)),
                "The hmac-sha256 signature of the RFC 9421 Appendix " +
                    "B.2.5 example should be refused rather than checked, " +
                    "because Web Bot Auth forbids shared secrets.");
        }

        /// <summary>
        /// The Appendix B.1.3 P-256 key fingerprints to a thumbprint, and
        /// to the same one every time. The canonical JSON and the
        /// thumbprint expected here were worked out from RFC 7638 rather
        /// than from this element, so the check fails if the element ever
        /// changes how it fingerprints an EC key. Nothing else in the suite
        /// takes an EC key from JSON through to a thumbprint.
        /// </summary>
        [TestMethod]
        public void EcKeyFingerprintsToAStableThumbprint()
        {
            var key = ReadKey(FindVector("B.2.4"));
            Assert.AreEqual(
                EcCanonicalJson,
                JwkThumbprint.BuildCanonicalJson(key),
                "The canonical JSON of the 'test-key-ecc-p256' key of " +
                    "RFC 9421 Appendix B.1.3 should be the four members " +
                    "RFC 7638 section 3.2 names, in order of name and " +
                    "with no whitespace.");
            Assert.AreEqual(
                EcThumbprint,
                JwkThumbprint.Compute(key),
                "The 'test-key-ecc-p256' key of RFC 9421 Appendix B.1.3 " +
                    "should fingerprint to '" + EcThumbprint + "'.");
            Assert.AreEqual(
                JwkThumbprint.Compute(key),
                JwkThumbprint.Compute(ReadKey(FindVector("B.2.4"))),
                "Reading the 'test-key-ecc-p256' key of RFC 9421 " +
                    "Appendix B.1.3 a second time should fingerprint to " +
                    "the same thumbprint.");
            Assert.AreEqual(
                EcThumbprint,
                key.Thumbprint,
                "The thumbprint the 'test-key-ecc-p256' key of RFC 9421 " +
                    "Appendix B.1.3 remembers should be the one just " +
                    "computed.");
        }

        /// <summary>
        /// Read the 'Signature-Input' header of an example and find the
        /// member that carries its label.
        /// </summary>
        /// <param name="vector">The worked example.</param>
        /// <returns>The member.</returns>
        private static SfMember ParseSignatureInput(AppendixBVector vector)
        {
            Assert.IsTrue(
                StructuredFieldParser.TryParseDictionary(
                    vector.SignatureInputHeader, out var dictionary),
                "The 'Signature-Input' header of the RFC 9421 Appendix " +
                    vector.Section + " example should be read as a " +
                    "dictionary from '" + vector.SignatureInputHeader +
                    "'.");
            Assert.IsTrue(
                dictionary.TryGetValue(vector.Label, out var member),
                "The 'Signature-Input' header of the RFC 9421 Appendix " +
                    vector.Section + " example should carry the label '" +
                    vector.Label + "'.");
            Assert.IsTrue(
                member.IsInnerList,
                "The covered components of the RFC 9421 Appendix " +
                    vector.Section + " example should be read as an inner " +
                    "list.");
            return member;
        }

        /// <summary>
        /// Read the signature bytes out of the 'Signature' header of an
        /// example.
        /// </summary>
        /// <param name="vector">The worked example.</param>
        /// <returns>The signature bytes.</returns>
        private static byte[] ParseSignature(AppendixBVector vector)
        {
            Assert.IsTrue(
                StructuredFieldParser.TryParseDictionary(
                    vector.SignatureHeader, out var dictionary),
                "The 'Signature' header of the RFC 9421 Appendix " +
                    vector.Section + " example should be read as a " +
                    "dictionary from '" + vector.SignatureHeader + "'.");
            Assert.IsTrue(
                dictionary.TryGetValue(vector.Label, out var member),
                "The 'Signature' header of the RFC 9421 Appendix " +
                    vector.Section + " example should carry the label '" +
                    vector.Label + "'.");
            Assert.IsFalse(
                member.IsInnerList,
                "The signature of the RFC 9421 Appendix " + vector.Section +
                    " example should be read as a single item.");
            var signature = member.Item.Value as byte[];
            Assert.IsNotNull(
                signature,
                "The signature of the RFC 9421 Appendix " + vector.Section +
                    " example should be read as a byte sequence.");
            return signature;
        }

        /// <summary>
        /// Read the key of an example the way the element does.
        /// </summary>
        /// <param name="vector">The worked example.</param>
        /// <returns>The key.</returns>
        private static JsonWebKey ReadKey(AppendixBVector vector)
        {
            Assert.IsTrue(
                JsonReader.TryParseObject(vector.KeyJson, out var source),
                "The '" + vector.KeyName + "' key of RFC 9421 Appendix " +
                    "B.1 should be read as a JSON object from '" +
                    vector.KeyJson + "'.");
            var key = JsonWebKey.Parse(source);
            Assert.IsNotNull(
                key,
                "The '" + vector.KeyName + "' key of RFC 9421 Appendix " +
                    "B.1 should be read as a key.");
            return key;
        }

        /// <summary>
        /// Find the worked example of one section.
        /// </summary>
        /// <param name="section">The section number.</param>
        /// <returns>The worked example.</returns>
        private static AppendixBVector FindVector(string section)
        {
            foreach (var vector in ReadVectors())
            {
                if (string.Equals(
                    vector.Section, section, StringComparison.Ordinal))
                {
                    return vector;
                }
            }
            Assert.Fail(
                "The fixture should hold the RFC 9421 Appendix " + section +
                    " example.");
            return null;
        }

        /// <summary>
        /// Read the six worked examples from the fixture.
        /// </summary>
        /// <returns>The worked examples.</returns>
        private static IList<AppendixBVector> ReadVectors()
        {
            var result = new List<AppendixBVector>();
            using (var document = JsonDocument.Parse(
                Fixtures.ReadText(FixtureName)))
            {
                foreach (var item in document.RootElement.EnumerateArray())
                {
                    var vector = new AppendixBVector
                    {
                        Name = item.GetProperty("name").GetString(),
                        Section = item.GetProperty("section").GetString(),
                        Algorithm =
                            item.GetProperty("algorithm").GetString(),
                        Label = item.GetProperty("label").GetString(),
                        Supported =
                            item.GetProperty("supported").GetBoolean(),
                        SignatureBase =
                            item.GetProperty("signature_base").GetString(),
                        SignatureInputHeader = item
                            .GetProperty("signature_input_header")
                            .GetString(),
                        SignatureHeader = item
                            .GetProperty("signature_header").GetString(),
                        SignatureParams = item
                            .GetProperty("signature_params").GetString(),
                        KeyName = item.GetProperty("key_name").GetString(),
                        KeyJson = item.GetProperty("key").GetRawText(),
                    };
                    foreach (var component in
                        item.GetProperty("components").EnumerateArray())
                    {
                        vector.Components.Add(new AppendixBComponent
                        {
                            Identifier = component
                                .GetProperty("identifier").GetString(),
                            Value =
                                component.GetProperty("value").GetString(),
                        });
                    }
                    result.Add(vector);
                }
            }
            return result;
        }

        /// <summary>
        /// Answers each covered component with the value the appendix
        /// prints on that line of the signature base. The element resolves
        /// components from pipeline evidence instead, which it cannot do
        /// for several of these examples, so feeding the printed values in
        /// is what lets the base builder be checked on its own.
        /// </summary>
        private sealed class PrintedComponentResolver : IComponentResolver
        {
            private readonly AppendixBVector _vector;

            /// <summary>
            /// How many components the base builder asked for and was
            /// given a value for.
            /// </summary>
            public int Resolved { get; private set; }

            public PrintedComponentResolver(AppendixBVector vector)
            {
                _vector = vector;
            }

            public bool TryResolve(
                string name,
                SfItem component,
                out string value)
            {
                foreach (var entry in _vector.Components)
                {
                    if (string.Equals(
                        entry.Identifier,
                        component.Raw,
                        StringComparison.Ordinal))
                    {
                        value = entry.Value;
                        Resolved++;
                        return true;
                    }
                }
                value = null;
                return false;
            }
        }
    }
}
