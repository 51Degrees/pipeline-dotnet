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

using FiftyOne.Pipeline.AgentSignature.Tests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace FiftyOne.Pipeline.AgentSignature.Tests
{
    /// <summary>
    /// The shortest path through the element, so that a failure elsewhere
    /// can be told apart from the pipeline plumbing being wrong.
    /// </summary>
    [TestClass]
    public class SmokeTests
    {
        /// <summary>
        /// A request with no signature headers reads Absent.
        /// </summary>
        [TestMethod]
        public void PlainRequestIsAbsent()
        {
            using (var harness = ElementHarness.Create())
            {
                var result = harness.ProcessPlainRequest();
                Assert.AreEqual(
                    Constants.STATUS_ABSENT, result.AgentSignature.Value);
                Assert.AreEqual(
                    Constants.REASON_NO_SIGNATURE,
                    result.AgentSignatureReason.Value);
            }
        }

        /// <summary>
        /// A request this test signs itself, with the key served by a fake
        /// directory, reads Verified.
        /// </summary>
        [TestMethod]
        public void FreshlySignedRequestIsVerified()
        {
            using (var harness = ElementHarness.CreateWithTestKey())
            {
                var signed = RequestSigner.Sign(new SigningOptions());
                var result = harness.ProcessSigned(signed);
                Assert.AreEqual(
                    Constants.STATUS_VERIFIED,
                    result.AgentSignature.Value,
                    "Reason was " + result.AgentSignatureReason.Value);
                Assert.AreEqual(
                    Constants.REASON_VERIFIED,
                    result.AgentSignatureReason.Value);
            }
        }

        /// <summary>
        /// The signed request vector from the standard that covers the
        /// signature agent by label reads Verified.
        /// </summary>
        [TestMethod]
        public void StandardVectorIsVerified()
        {
            var vector = Fixtures.ArchitectureV2()
                .First(v => v.SignatureAgent != null &&
                    v.Algorithm == "ed25519");
            using (var harness = ElementHarness.Create())
            {
                harness.Handler.AddDirectory(
                    Fixtures.SignatureAgentDirectoryUrl,
                    RequestSigner.PublicPart(vector.KeyJson));
                var result = harness.ProcessVector(vector);
                Assert.AreEqual(
                    Constants.STATUS_VERIFIED,
                    result.AgentSignature.Value,
                    "Reason was " + result.AgentSignatureReason.Value);
            }
        }
    }
}
