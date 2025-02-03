/* *********************************************************************
 * This Original Work is copyright of 51 Degrees Mobile Experts Limited.
 * Copyright 2025 51 Degrees Mobile Experts Limited, Davidson House,
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
    }
}
