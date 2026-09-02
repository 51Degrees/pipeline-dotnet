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

using FiftyOne.Pipeline.AgentSignature.Data;
using FiftyOne.Pipeline.AgentSignature.Keys;
using FiftyOne.Pipeline.AgentSignature.Tests.Helpers;
using FiftyOne.Pipeline.Engines.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace FiftyOne.Pipeline.AgentSignature.Tests
{
    /// <summary>
    /// Checks the signature agent card, being the JSON document in which an
    /// agent says who it is and where its keys are. These tests prove that
    /// the element reads a card reached through the 'cimd' member type,
    /// that it reads a card reached through a registry, that it rejects the
    /// two cards the registry draft forbids, and that a card which cannot be
    /// fetched leaves the signature itself unaffected.
    /// </summary>
    [TestClass]
    public class AgentCardTests
    {
        /// <summary>
        /// The URL the fixture cards are served from.
        /// </summary>
        private const string CardUrl = "https://example.com/bot";

        /// <summary>
        /// The key URL the fixture card with a 'jwks_uri' names.
        /// </summary>
        private const string CardKeyUrl =
            "https://example.com" + Constants.DIRECTORY_PATH;

        /// <summary>
        /// The origin a 'directory' member names to reach
        /// <see cref="CardKeyUrl"/>.
        /// </summary>
        private const string CardOrigin = "https://example.com";

        /// <summary>
        /// The URL a fake registry of agent cards is served from.
        /// </summary>
        private const string RegistryUrl =
            "https://registry.example.com/agents.txt";

        /// <summary>
        /// The label the 'Signature-Agent' member is written with.
        /// </summary>
        private const string AgentLabel = "agent1";

        /// <summary>
        /// A card reached through a 'cimd' member, which names its keys with
        /// a 'jwks_uri', verifies the signature and puts the name, the
        /// product token, the purpose and the card URL on the result.
        /// </summary>
        [TestMethod]
        public void CimdCardWithKeyUrlIsVerifiedAndCarriesCardDetail()
        {
            using (var harness = ElementHarness.Create())
            {
                harness.Handler.Add(
                    CardUrl,
                    FixtureCard("jwks-uri-card").CardJson,
                    Constants.JSON_MEDIA_TYPE);
                harness.Handler.AddDirectory(
                    CardKeyUrl,
                    RequestSigner.PublicPart(Fixtures.Ed25519Key()));

                var result = ProcessCimdRequest(harness);

                AssertVerified(result);
                AssertValue(
                    "Example Bot", result.AgentSignatureName, "name", result);
                AssertValue(
                    "ExampleBot",
                    result.AgentSignatureProductToken,
                    "product token",
                    result);
                AssertValue(
                    "tdm", result.AgentSignaturePurpose, "purpose", result);
                AssertValue(
                    CardUrl, result.AgentSignatureCardUrl, "card URL", result);
            }
        }

        /// <summary>
        /// A card reached through a 'cimd' member, which carries its keys
        /// inline in a 'jwks' field, verifies the signature and puts the name
        /// and the card URL on the result. That fixture card states no
        /// product token and no purpose, so those two properties have no
        /// value and say why.
        /// </summary>
        [TestMethod]
        public void CimdCardWithInlineKeysIsVerifiedAndCarriesCardDetail()
        {
            using (var harness = ElementHarness.Create())
            {
                harness.Handler.Add(
                    CardUrl,
                    FixtureCard("jwks-card").CardJson,
                    Constants.JSON_MEDIA_TYPE);

                var result = ProcessCimdRequest(harness);

                AssertVerified(result);
                AssertValue(
                    "Example Bot", result.AgentSignatureName, "name", result);
                AssertValue(
                    CardUrl, result.AgentSignatureCardUrl, "card URL", result);
                Assert.IsFalse(
                    result.AgentSignatureProductToken.HasValue,
                    "Expected no product token, because the inline key card " +
                    "states none. " + Describe(result));
                Assert.IsFalse(
                    result.AgentSignaturePurpose.HasValue,
                    "Expected no purpose, because neither the inline key " +
                    "card nor its keys state one. " + Describe(result));
                Assert.AreEqual(
                    1,
                    harness.Handler.CallCount,
                    "Expected the card alone to be fetched, because it " +
                    "carries the keys itself. " + Describe(result));
            }
        }

        /// <summary>
        /// A card carrying both a 'jwks' field and a 'jwks_uri' field, which
        /// the registry draft forbids, is refused, so the signature reads
        /// Unverified because no key could be obtained.
        /// </summary>
        [TestMethod]
        public void CimdCardWithBothKeySourcesIsUnverified()
        {
            AssertCardIsRefused("jwks-and-jwks-uri");
        }

        /// <summary>
        /// A card whose 'client_id' is not the URL the card was fetched from
        /// is refused, so the signature reads Unverified because no key could
        /// be obtained.
        /// </summary>
        [TestMethod]
        public void CimdCardWithMismatchedClientIdIsUnverified()
        {
            AssertCardIsRefused("client-id-mismatch");
        }

        /// <summary>
        /// The card reader accepts the two fixture cards marked valid and
        /// reads the fields the element later reports.
        /// </summary>
        [TestMethod]
        public void TryParseReadsTheValidFixtureCards()
        {
            var withKeyUrl = FixtureCard("jwks-uri-card");
            Assert.IsTrue(
                AgentCard.TryParse(
                    withKeyUrl.CardJson, withKeyUrl.Url, out var keyUrlCard),
                "Expected the 'jwks-uri-card' fixture to be accepted, " +
                "because the fixture marks it valid.");
            Assert.AreEqual(
                CardUrl,
                keyUrlCard.ClientId,
                "Expected the client id read from the card.");
            Assert.AreEqual(
                "Example Bot",
                keyUrlCard.ClientName,
                "Expected the client name read from the card.");
            Assert.AreEqual(
                CardKeyUrl,
                keyUrlCard.JwksUri,
                "Expected the key URL read from the card.");
            Assert.IsNull(
                keyUrlCard.Jwks,
                "Expected no inline keys, because the card names a key URL.");
            Assert.AreEqual(
                "ExampleBot",
                keyUrlCard.ProductToken,
                "Expected the robots.txt product token read from the card.");
            Assert.AreEqual(
                "tdm",
                keyUrlCard.Purpose,
                "Expected the purpose read from the card.");
            Assert.AreEqual(
                "fetcher",
                keyUrlCard.Trigger,
                "Expected the trigger read from the card.");
            Assert.AreEqual(
                "Mozilla/5.0 ExampleBot",
                keyUrlCard.ExpectedUserAgent,
                "Expected the user agent read from the card.");
            Assert.AreEqual(
                1,
                keyUrlCard.Contacts.Count,
                "Expected the one contact the card lists.");
            Assert.AreEqual(
                "mailto:bot-support@example.com",
                keyUrlCard.Contacts[0],
                "Expected the contact read from the card.");

            var withInlineKeys = FixtureCard("jwks-card");
            Assert.IsTrue(
                AgentCard.TryParse(
                    withInlineKeys.CardJson,
                    withInlineKeys.Url,
                    out var inlineCard),
                "Expected the 'jwks-card' fixture to be accepted, because " +
                "the fixture marks it valid.");
            Assert.AreEqual(
                CardUrl,
                inlineCard.ClientId,
                "Expected the client id read from the card.");
            Assert.IsNull(
                inlineCard.JwksUri,
                "Expected no key URL, because the card carries its keys.");
            Assert.IsNotNull(
                inlineCard.Jwks,
                "Expected the inline keys read from the card.");
            Assert.AreEqual(
                1,
                inlineCard.Jwks.Keys.Count,
                "Expected the one key the card carries.");
            Assert.AreEqual(
                Fixtures.Ed25519Thumbprint,
                inlineCard.Jwks.Keys[0].Thumbprint,
                "Expected the card to carry the Ed25519 test key, which is " +
                "what lets a request signed with that key verify against it.");
        }

        /// <summary>
        /// The card reader refuses the two fixture cards marked invalid, one
        /// naming its keys twice and one whose client id is not the URL it
        /// was fetched from.
        /// </summary>
        [TestMethod]
        public void TryParseRejectsTheInvalidFixtureCards()
        {
            foreach (var name in new[]
                { "jwks-and-jwks-uri", "client-id-mismatch" })
            {
                var vector = FixtureCard(name);
                Assert.IsFalse(
                    AgentCard.TryParse(
                        vector.CardJson, vector.Url, out var card),
                    "Expected the '" + name + "' fixture to be refused, " +
                    "because the fixture marks it invalid.");
                Assert.IsNull(
                    card,
                    "Expected no card from the '" + name + "' fixture, " +
                    "because the fixture marks it invalid.");
            }
        }

        /// <summary>
        /// Both registry cases in the fixture file read to the card URLs the
        /// fixture says they list, so comments and blank lines are skipped.
        /// </summary>
        [TestMethod]
        public void RegistryTextReadsToTheCardUrlsListed()
        {
            foreach (var vector in Fixtures.Registries())
            {
                var actual = DirectoryFetcher.ParseRegistry(
                    vector.RegistryText);
                Assert.AreEqual(
                    vector.CardUrls.Count,
                    actual.Count,
                    "Expected the '" + vector.Name + "' registry to list " +
                    vector.CardUrls.Count + " card URLs, and it listed " +
                    actual.Count + " which were '" +
                    string.Join("', '", actual) + "'.");
                for (var i = 0; i < vector.CardUrls.Count; i++)
                {
                    Assert.AreEqual(
                        vector.CardUrls[i],
                        actual[i],
                        "Expected card URL " + i + " of the '" +
                        vector.Name + "' registry to be '" +
                        vector.CardUrls[i] + "', and it was '" +
                        actual[i] + "'.");
                }
            }
        }

        /// <summary>
        /// A registry line naming an address this element refuses to fetch,
        /// such as one inside the network or one carrying user information,
        /// is dropped when the registry is read. The registry's own address
        /// is configured by the operator, but the lines are whatever the
        /// registry served, so a registry that has been tampered with must
        /// not be able to point this element at an internal service.
        /// </summary>
        [TestMethod]
        public void RegistryLinesNamingUnsafeAddressesAreDropped()
        {
            var actual = DirectoryFetcher.ParseRegistry(
                "https://192.168.0.1/card.json\n" +
                "https://[::1]/card.json\n" +
                "https://169.254.169.254/card.json\n" +
                "https://user@example.com/card.json\n" +
                "http://example.com/card.json\n" +
                "https://example.com/card.json\n");
            Assert.AreEqual(
                1,
                actual.Count,
                "Expected only the public HTTPS address to survive, and " +
                "the list held '" + string.Join("', '", actual) + "'.");
            Assert.AreEqual(
                "https://example.com/card.json",
                actual[0],
                "Expected the public HTTPS address to be the one kept.");
        }

        /// <summary>
        /// A card found through a configured registry, whose key URL is the
        /// one the signature's agent resolves to, puts the name, the product
        /// token, the purpose and the card URL on a Verified result even
        /// though the agent named no card itself.
        /// </summary>
        [TestMethod]
        public void RegistryCardAddsItsDetailToAVerifiedSignature()
        {
            using (var harness = ElementHarness.Create(
                builder => builder
                    .SetRegistry(RegistryUrl)
                    // The registry is read in the background, so the request
                    // is given long enough to see the result of that read
                    // rather than racing it.
                    .SetWaitBudget(TimeSpan.FromSeconds(5))))
            {
                harness.Handler.Add(
                    RegistryUrl, CardUrl + "\n", "text/plain");
                harness.Handler.Add(
                    CardUrl,
                    FixtureCard("jwks-uri-card").CardJson,
                    Constants.JSON_MEDIA_TYPE);
                harness.Handler.AddDirectory(
                    CardKeyUrl,
                    RequestSigner.PublicPart(Fixtures.Ed25519Key()));

                var signed = RequestSigner.Sign(new SigningOptions
                {
                    SignatureAgent = CardOrigin,
                    SignatureAgentLabel = AgentLabel,
                });
                var result = harness.ProcessSigned(signed);

                AssertVerified(result);
                AssertValue(
                    "Example Bot", result.AgentSignatureName, "name", result);
                AssertValue(
                    "ExampleBot",
                    result.AgentSignatureProductToken,
                    "product token",
                    result);
                AssertValue(
                    "tdm", result.AgentSignaturePurpose, "purpose", result);
                AssertValue(
                    CardUrl, result.AgentSignatureCardUrl, "card URL", result);
            }
        }

        /// <summary>
        /// A card the registry lists but which cannot be fetched leaves the
        /// signature Verified, because a card says nothing about whether a
        /// signature is genuine, and leaves the three card properties without
        /// a value and saying that no card was available.
        /// </summary>
        [TestMethod]
        public void CardThatCannotBeFetchedLeavesTheSignatureVerified()
        {
            const string missingCardUrl = "https://example.com/missing-bot";
            using (var harness = ElementHarness.CreateWithTestKey(
                builder => builder
                    .SetRegistry(RegistryUrl)
                    .SetWaitBudget(TimeSpan.FromSeconds(5))))
            {
                harness.Handler.Add(
                    RegistryUrl, missingCardUrl + "\n", "text/plain");
                harness.Handler.AddStatus(
                    missingCardUrl, HttpStatusCode.NotFound);

                var signed = RequestSigner.Sign(new SigningOptions());
                var result = harness.ProcessSigned(signed);

                AssertVerified(result);
                AssertNoCard(result.AgentSignatureName, "name", result);
                AssertNoCard(
                    result.AgentSignatureProductToken,
                    "product token",
                    result);
                AssertNoCard(
                    result.AgentSignatureCardUrl, "card URL", result);
            }
        }

        /// <summary>
        /// Serve one of the fixture cards that the registry draft forbids and
        /// check that the signature reads Unverified because no key could be
        /// obtained from it.
        /// </summary>
        /// <param name="name">The name of the fixture case.</param>
        private static void AssertCardIsRefused(string name)
        {
            using (var harness = ElementHarness.Create())
            {
                harness.Handler.Add(
                    CardUrl,
                    FixtureCard(name).CardJson,
                    Constants.JSON_MEDIA_TYPE);

                var result = ProcessCimdRequest(harness);

                Assert.AreEqual(
                    Constants.STATUS_UNVERIFIED,
                    result.AgentSignature.Value,
                    "Expected Unverified for the '" + name + "' card, " +
                    "because the registry draft forbids it so no key can " +
                    "be obtained. " + Describe(result));
                Assert.AreEqual(
                    Constants.REASON_DIRECTORY_UNAVAILABLE,
                    result.AgentSignatureReason.Value,
                    "Expected the DirectoryUnavailable reason for the '" +
                    name + "' card, because the card was refused. " +
                    Describe(result));
                AssertNoCard(result.AgentSignatureName, "name", result);
                AssertNoCard(
                    result.AgentSignatureCardUrl, "card URL", result);
            }
        }

        /// <summary>
        /// Sign a request that names the fixture card with a 'cimd' member
        /// and run it through the element.
        /// </summary>
        /// <param name="harness">The harness to run it through.</param>
        /// <returns>What the element made of the request.</returns>
        private static IAgentSignatureData ProcessCimdRequest(
            ElementHarness harness)
        {
            var options = new SigningOptions
            {
                // The signer helper writes the plain form of the header, so
                // the member with a type is built here instead and added as
                // a covered component of its own.
                SignatureAgent = null,
            };
            options.ExtraComponents.Add(
                new KeyValuePair<string, string>(
                    "\"signature-agent\";key=\"" + AgentLabel + "\"",
                    CimdMemberValue()));
            var signed = RequestSigner.Sign(options);

            return harness.Process(new Dictionary<string, string>
            {
                { Constants.EVIDENCE_SIGNATURE_KEY, signed.Signature },
                {
                    Constants.EVIDENCE_SIGNATURE_INPUT_KEY,
                    signed.SignatureInput
                },
                {
                    Constants.EVIDENCE_SIGNATURE_AGENT_KEY,
                    AgentLabel + "=" + CimdMemberValue()
                },
                { Constants.EVIDENCE_HOST_KEY, "example.com" },
                { Core.Constants.EVIDENCE_PROTOCOL, "https" },
            });
        }

        /// <summary>
        /// The value of the 'Signature-Agent' member that names an agent
        /// card, which is the card URL as a quoted string with the type that
        /// says the URL leads to a card.
        /// </summary>
        /// <returns>The member value.</returns>
        private static string CimdMemberValue()
        {
            return "\"" + CardUrl + "\";type=" + Constants.AGENT_TYPE_CIMD;
        }

        /// <summary>
        /// Read one case from the agent card fixture file.
        /// </summary>
        /// <param name="name">The name of the case.</param>
        /// <returns>The case.</returns>
        private static AgentCardVector FixtureCard(string name)
        {
            var vector = Fixtures.AgentCards()
                .FirstOrDefault(c => c.Name == name);
            Assert.IsNotNull(
                vector,
                "Expected the agent card fixture file to carry a case " +
                "named '" + name + "'.");
            return vector;
        }

        /// <summary>
        /// Check that the signature verified.
        /// </summary>
        /// <param name="result">What the element made of the request.</param>
        private static void AssertVerified(IAgentSignatureData result)
        {
            Assert.AreEqual(
                Constants.STATUS_VERIFIED,
                result.AgentSignature.Value,
                "Expected Verified, because the request was signed with the " +
                "key the agent publishes. " + Describe(result));
            Assert.AreEqual(
                Constants.REASON_VERIFIED,
                result.AgentSignatureReason.Value,
                "Expected the Verified reason. " + Describe(result));
        }

        /// <summary>
        /// Check that a property carries the value expected.
        /// </summary>
        /// <param name="expected">The value expected.</param>
        /// <param name="actual">The property.</param>
        /// <param name="description">What the property holds.</param>
        /// <param name="result">What the element made of the request.</param>
        private static void AssertValue(
            string expected,
            IAspectPropertyValue<string> actual,
            string description,
            IAgentSignatureData result)
        {
            Assert.IsTrue(
                actual.HasValue,
                "Expected the " + description + " to be '" + expected +
                "', and it had no value because '" + actual.NoValueMessage +
                "'. " + Describe(result));
            Assert.AreEqual(
                expected,
                actual.Value,
                "Expected the " + description + " to be '" + expected +
                "', and it was '" + actual.Value + "'. " + Describe(result));
        }

        /// <summary>
        /// Check that a property has no value and says that no agent card was
        /// available.
        /// </summary>
        /// <param name="actual">The property.</param>
        /// <param name="description">What the property holds.</param>
        /// <param name="result">What the element made of the request.</param>
        private static void AssertNoCard(
            IAspectPropertyValue<string> actual,
            string description,
            IAgentSignatureData result)
        {
            Assert.IsFalse(
                actual.HasValue,
                "Expected no " + description + ", because no agent card was " +
                "available. " + Describe(result));
            Assert.AreEqual(
                Messages.NoValueNoCard,
                actual.NoValueMessage,
                "Expected the " + description + " to say '" +
                Messages.NoValueNoCard + "', and it said '" +
                actual.NoValueMessage + "'. " + Describe(result));
        }

        /// <summary>
        /// Describe what the element made of a request, so that every
        /// assertion message carries the status and the reason.
        /// </summary>
        /// <param name="result">What the element made of the request.</param>
        /// <returns>The description.</returns>
        private static string Describe(IAgentSignatureData result)
        {
            return "The status was '" + result.AgentSignature.Value +
                "' with the reason '" + result.AgentSignatureReason.Value +
                "'.";
        }
    }
}
