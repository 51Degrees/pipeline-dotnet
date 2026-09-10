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
using System;
using System.Linq;

namespace FiftyOne.Pipeline.AgentSignature.Tests
{
    /// <summary>
    /// A request may carry more than one signature with the Web Bot Auth
    /// tag, which the protocol draft's reverse proxy case does, the
    /// agent's own signature plus the proxy's. The draft has a verifier
    /// validate each independently, so the element checks each tagged
    /// signature until one verifies, reports the first tagged signature's
    /// outcome when none does, and bounds how many are checked because
    /// each can cost a directory wait and the header is written by the
    /// sender.
    /// </summary>
    [TestClass]
    public class MultipleSignatureTests
    {
        /// <summary>
        /// A request whose first tagged signature does not check out but
        /// whose second does reads Verified. Before signatures were
        /// checked independently, the first tagged signature decided the
        /// request and the good one was never looked at.
        /// </summary>
        [TestMethod]
        public void SecondTaggedSignatureVerifiesWhenTheFirstDoesNot()
        {
            using (var harness = ElementHarness.CreateWithTestKey())
            {
                var merged = Merge(
                    Corrupt(RequestSigner.Sign(new SigningOptions())),
                    RequestSigner.Sign(new SigningOptions
                    {
                        Label = "sig2",
                    }));
                var result = harness.ProcessSigned(merged);

                Assert.AreEqual(
                    Constants.STATUS_VERIFIED,
                    result.AgentSignature.Value,
                    "Expected the second tagged signature to answer the " +
                    "request after the first did not check out, and the " +
                    "status was '" + result.AgentSignature.Value +
                    "' with reason '" + result.AgentSignatureReason.Value +
                    "'.");
            }
        }

        /// <summary>
        /// Where no tagged signature verifies, the first tagged
        /// signature's outcome is what the request reports, whichever
        /// order the failures arrive in. The two signatures here fail for
        /// different reasons so the test can see whose outcome won.
        /// </summary>
        [TestMethod]
        public void FirstTaggedSignaturesOutcomeIsReportedWhenNoneVerifies()
        {
            using (var harness = ElementHarness.CreateWithTestKey())
            {
                var mismatch = Corrupt(
                    RequestSigner.Sign(new SigningOptions()));
                var unbound = RequestSigner.Sign(new SigningOptions
                {
                    Label = "sig2",
                    CoverAuthority = false,
                });

                var result = harness.ProcessSigned(Merge(mismatch, unbound));
                Assert.AreEqual(
                    Constants.REASON_SIGNATURE_MISMATCH,
                    result.AgentSignatureReason.Value,
                    "Expected the first tagged signature's reason when " +
                    "none verifies, and the reason was '" +
                    result.AgentSignatureReason.Value + "'.");
            }
            using (var harness = ElementHarness.CreateWithTestKey())
            {
                var unbound = RequestSigner.Sign(new SigningOptions
                {
                    CoverAuthority = false,
                });
                var mismatch = Corrupt(
                    RequestSigner.Sign(new SigningOptions
                    {
                        Label = "sig2",
                    }));

                var result = harness.ProcessSigned(Merge(unbound, mismatch));
                Assert.AreEqual(
                    Constants.REASON_UNBOUND_SIGNATURE,
                    result.AgentSignatureReason.Value,
                    "Expected the first tagged signature's reason when " +
                    "the order is reversed, and the reason was '" +
                    result.AgentSignatureReason.Value + "'.");
            }
        }

        /// <summary>
        /// A signature carrying another tag is skipped without using up
        /// the bounded number of checks, because the draft says a
        /// verifier discards signatures made for other purposes rather
        /// than counting them.
        /// </summary>
        [TestMethod]
        public void SignatureWithAnotherTagDoesNotStopTheTaggedOneVerifying()
        {
            using (var harness = ElementHarness.CreateWithTestKey())
            {
                var other = RequestSigner.Sign(new SigningOptions
                {
                    Tag = "some-other-purpose",
                });
                var good = RequestSigner.Sign(new SigningOptions
                {
                    Label = "sig2",
                });
                var result = harness.ProcessSigned(Merge(other, good));

                Assert.AreEqual(
                    Constants.STATUS_VERIFIED,
                    result.AgentSignature.Value,
                    "Expected the tagged signature to verify with an " +
                    "untagged one beside it, and the status was '" +
                    result.AgentSignature.Value + "' with reason '" +
                    result.AgentSignatureReason.Value + "'.");
            }
        }

        /// <summary>
        /// The third tagged signature is still checked and the fourth is
        /// not, which pins the bound. The bound exists because each
        /// tagged signature can cost a directory wait, so a sender
        /// writing one long header must not be able to hold a request
        /// thread for as long as the header lasts.
        /// </summary>
        [TestMethod]
        public void OnlyTheFirstThreeTaggedSignaturesAreChecked()
        {
            using (var harness = ElementHarness.CreateWithTestKey())
            {
                var third = Merge(
                    Corrupt(RequestSigner.Sign(new SigningOptions())),
                    Corrupt(RequestSigner.Sign(new SigningOptions
                    {
                        Label = "sig2",
                    })),
                    RequestSigner.Sign(new SigningOptions
                    {
                        Label = "sig3",
                    }));
                var result = harness.ProcessSigned(third);
                Assert.AreEqual(
                    Constants.STATUS_VERIFIED,
                    result.AgentSignature.Value,
                    "Expected a good third tagged signature to be " +
                    "checked, and the status was '" +
                    result.AgentSignature.Value + "'.");
            }
            using (var harness = ElementHarness.CreateWithTestKey())
            {
                var fourth = Merge(
                    Corrupt(RequestSigner.Sign(new SigningOptions())),
                    Corrupt(RequestSigner.Sign(new SigningOptions
                    {
                        Label = "sig2",
                    })),
                    Corrupt(RequestSigner.Sign(new SigningOptions
                    {
                        Label = "sig3",
                    })),
                    RequestSigner.Sign(new SigningOptions
                    {
                        Label = "sig4",
                    }));
                var result = harness.ProcessSigned(fourth);
                Assert.AreEqual(
                    Constants.STATUS_INVALID,
                    result.AgentSignature.Value,
                    "Expected a good fourth tagged signature to be " +
                    "beyond the bound, and the status was '" +
                    result.AgentSignature.Value + "'.");
                Assert.AreEqual(
                    Constants.REASON_SIGNATURE_MISMATCH,
                    result.AgentSignatureReason.Value,
                    "Expected the first tagged signature's reason once " +
                    "the bound is passed, and the reason was '" +
                    result.AgentSignatureReason.Value + "'.");
            }
        }

        /// <summary>
        /// Join several independently signed requests into one request
        /// carrying every signature. Each was signed under its own label,
        /// so the two headers join as ordinary dictionary members, and
        /// the 'Signature-Agent' header is the same for all so the first
        /// is kept.
        /// </summary>
        private static SignedRequest Merge(params SignedRequest[] signed)
        {
            return new SignedRequest
            {
                Signature = string.Join(
                    ", ", signed.Select(s => s.Signature)),
                SignatureInput = string.Join(
                    ", ", signed.Select(s => s.SignatureInput)),
                SignatureAgent = signed[0].SignatureAgent,
                SignatureBase = signed[0].SignatureBase,
            };
        }

        /// <summary>
        /// Flip one bit of a signature so the signature no longer checks
        /// out against the key.
        /// </summary>
        private static SignedRequest Corrupt(SignedRequest signed)
        {
            var first = signed.Signature.IndexOf(':');
            var last = signed.Signature.LastIndexOf(':');
            var bytes = Convert.FromBase64String(
                signed.Signature.Substring(first + 1, last - first - 1));
            bytes[0] ^= 0x01;
            return new SignedRequest
            {
                Signature = signed.Signature.Substring(0, first + 1) +
                    Convert.ToBase64String(bytes) + ":",
                SignatureInput = signed.SignatureInput,
                SignatureAgent = signed.SignatureAgent,
                SignatureBase = signed.SignatureBase,
            };
        }
    }
}
