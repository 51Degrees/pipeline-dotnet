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

using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.Exceptions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using NSubstitute.ReceivedExtensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Configuration;

namespace FiftyOne.Pipeline.Web.Framework.Tests
{
    [TestClass]
    public class WebPipelineEvidenceFillerTests
    {
        private class FakeException : Exception
        {
            public FakeException(): base() { }
            public FakeException(string msg) : base(msg) { }
            public FakeException(string msg, Exception ex) : base(msg, ex) { }
        }


        [TestMethod]
        public void TestConstructor_Ok()
        {
            var filter = Substitute.For<IEvidenceKeyFilter>();
            var fakeData = Substitute.For<IFlowData>();
            fakeData.EvidenceKeyFilter.Returns(filter);
            
            var filler = new WebPipeline.EvidenceFiller(fakeData);

            _ = fakeData.Received(1).EvidenceKeyFilter;
            Assert.IsNull(filler.Errors);
        }

        [TestMethod]
        [ExpectedException(typeof(NullReferenceException))] // pass through (not explicitly handled)
        public void TestConstructor_Error_OnNullData()
        {
            var filler = new WebPipeline.EvidenceFiller(null);
        }

        [TestMethod]
        [ExpectedException(typeof(PipelineException))]
        public void TestConstructor_Error_OnNullFilter()
        {
            IEvidenceKeyFilter noFilter = null;
            var fakeData = Substitute.For<IFlowData>();
            fakeData.EvidenceKeyFilter.Returns(noFilter);

            var filler = new WebPipeline.EvidenceFiller(fakeData);
        }

        [TestMethod]
        [ExpectedException(typeof(FakeException))]
        public void TestConstructor_Error_OnGetFilter()
        {
            var fakeData = Substitute.For<IFlowData>();
            fakeData.EvidenceKeyFilter.Returns(x => { throw new FakeException("no filter"); });

            var filler = new WebPipeline.EvidenceFiller(fakeData);
        }

        [TestMethod]
        public void TestCheckToAdd_Ok()
        {
            var fakeData = Substitute.For<IFlowData>();

            var filter = Substitute.For<IEvidenceKeyFilter>();
            filter.Include(Arg.Any<string>()).Returns(x => ((string)x[0]).Contains("s"));
            fakeData.EvidenceKeyFilter.Returns(filter);

            var testData = new Dictionary<string, string>()
            {
                { "crabby", "crabs" },
                { "sleepy", "beetle" },
                { "zummy", "fish" },
                { "gloomy", "day" },
                { "fishy", "cat" },
                { "raw", "beef" },
            };


            var filler = new WebPipeline.EvidenceFiller(fakeData);
            foreach ( var data in testData )
            {
                filler.CheckAndAdd(data.Key, data.Value);
            }

            _ = fakeData.Received(1).EvidenceKeyFilter;
            filter.Received(testData.Count).Include(Arg.Any<string>());
            Assert.IsNull(filler.Errors);
            fakeData.Received(2).AddEvidence(Arg.Any<string>(), Arg.Any<string>());
            fakeData.Received(1).AddEvidence("sleepy", "beetle");
            fakeData.Received(1).AddEvidence("fishy", "cat");
        }

        [TestMethod]
        public void TestCheckToAdd_Error_OnAddEvidence()
        {
            var fakeData = Substitute.For<IFlowData>();

            var filter = Substitute.For<IEvidenceKeyFilter>();
            filter.Include(Arg.Any<string>()).Returns(x => ((string)x[0])?.Contains("s") != false);
            fakeData.EvidenceKeyFilter.Returns(filter);
            fakeData
                .When(x => x.AddEvidence(Arg.Is((string)null), Arg.Any<object>()))
                .Do(x => throw new ArgumentNullException());

            var testData = new string[][]
            {
                new string[2]{ "crabby", "crabs" },
                new string[2]{ null, "void" },
                new string[2]{ "sleepy", "beetle" },
                new string[2]{ "zummy", "fish" },
                new string[2]{ null, "hell" },
                new string[2]{ "gloomy", "day" },
                new string[2]{ "fishy", "cat" },
                new string[2]{ null, "the end" },
                new string[2]{ "raw", "beef" },
            };


            var filler = new WebPipeline.EvidenceFiller(fakeData);
            foreach (var data in testData)
            {
                filler.CheckAndAdd(data[0], data[1]);
            }

            _ = fakeData.Received(1).EvidenceKeyFilter;
            filter.Received(testData.Length).Include(Arg.Any<string>());
            Assert.IsNotNull(filler.Errors);
            Assert.AreEqual(3, filler.Errors.Count);
            foreach (var error in filler.Errors)
            {
                Assert.IsInstanceOfType<ArgumentNullException>(error);
            }
            fakeData.Received(5).AddEvidence(Arg.Any<string>(), Arg.Any<string>());
            fakeData.Received(1).AddEvidence(null, "void");
            fakeData.Received(1).AddEvidence("sleepy", "beetle");
            fakeData.Received(1).AddEvidence(null, "hell");
            fakeData.Received(1).AddEvidence("fishy", "cat");
            fakeData.Received(1).AddEvidence(null, "the end");
        }

        [TestMethod]
        public void TestCheckToAdd_Error_OnInclude()
        {
            var fakeData = Substitute.For<IFlowData>();

            var filter = Substitute.For<IEvidenceKeyFilter>();
            filter.Include(Arg.Any<string>()).Returns(x =>
            {
                if (x[0] is null)
                {
                    throw new ArgumentNullException();  // <--- thrown here
                }
                return ((string)x[0]).Contains("s");
            });
            fakeData.EvidenceKeyFilter.Returns(filter);

            var testData = new string[][]
            {
                new string[2]{ "crabby", "crabs" },
                new string[2]{ null, "void" },
                new string[2]{ "sleepy", "beetle" },
                new string[2]{ "zummy", "fish" },
                new string[2]{ null, "hell" },
                new string[2]{ "gloomy", "day" },
                new string[2]{ "fishy", "cat" },
                new string[2]{ null, "the end" },
                new string[2]{ "raw", "beef" },
            };


            var filler = new WebPipeline.EvidenceFiller(fakeData);
            foreach (var data in testData)
            {
                filler.CheckAndAdd(data[0], data[1]);
            }

            _ = fakeData.Received(1).EvidenceKeyFilter;
            filter.Received(testData.Length).Include(Arg.Any<string>());
            Assert.IsNotNull(filler.Errors);
            Assert.AreEqual(3, filler.Errors.Count);
            foreach (var error in filler.Errors)
            {
                Assert.IsInstanceOfType<ArgumentNullException>(error);
            }
            fakeData.Received(2).AddEvidence(Arg.Any<string>(), Arg.Any<string>());
            fakeData.Received(1).AddEvidence("sleepy", "beetle");
            fakeData.Received(1).AddEvidence("fishy", "cat");
        }

        /// <summary>
        /// The method, the path and the whole query string are added where
        /// an element asks for them, and the path and the query keep their
        /// percent escapes. A 'Uri' would decode them, and these values
        /// rebuild the text an HTTP message signature was made over, where
        /// one changed byte makes a valid signature read as invalid.
        /// </summary>
        [TestMethod]
        public void RequestLine_KeepsEscapes()
        {
            var filter = Substitute.For<IEvidenceKeyFilter>();
            filter.Include(Arg.Any<string>()).Returns(true);
            var fakeData = Substitute.For<IFlowData>();
            fakeData.EvidenceKeyFilter.Returns(filter);
            var request = new System.Web.HttpRequest(
                string.Empty,
                "http://example.com/a%2Fb/c%20d",
                "q=%2Fx%20y&r=1");

            var filler = new WebPipeline.EvidenceFiller(fakeData);
            filler.AddRequestLineToEvidence(request);

            fakeData.Received(1).AddEvidence(
                Core.Constants.EVIDENCE_REQUEST_METHOD_KEY, "GET");
            fakeData.Received(1).AddEvidence(
                Core.Constants.EVIDENCE_REQUEST_PATH_KEY, "/a%2Fb/c%20d");
            fakeData.Received(1).AddEvidence(
                Core.Constants.EVIDENCE_REQUEST_QUERY_KEY,
                "q=%2Fx%20y&r=1");
        }

        /// <summary>
        /// Where no element asks for the request line, none of it is added
        /// and none of it is worked out. This is what keeps the values out
        /// of every pipeline that holds no element needing them.
        /// </summary>
        [TestMethod]
        public void RequestLine_NotAddedWhenNotRequired()
        {
            var filter = Substitute.For<IEvidenceKeyFilter>();
            filter.Include(Arg.Any<string>()).Returns(false);
            var fakeData = Substitute.For<IFlowData>();
            fakeData.EvidenceKeyFilter.Returns(filter);
            var request = new System.Web.HttpRequest(
                string.Empty, "http://example.com/a", "q=1");

            var filler = new WebPipeline.EvidenceFiller(fakeData);
            filler.AddRequestLineToEvidence(request);

            fakeData.DidNotReceive().AddEvidence(
                Arg.Any<string>(), Arg.Any<object>());
        }
    }
}
