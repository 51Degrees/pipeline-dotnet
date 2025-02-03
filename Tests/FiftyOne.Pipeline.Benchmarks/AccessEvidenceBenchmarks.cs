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

using BenchmarkDotNet.Attributes;
using FiftyOne.Pipeline.Engines.TestHelpers;
using Microsoft.Extensions.Primitives;

namespace FiftyOne.Pipeline.Benchmarks
{
    //[MemoryDiagnoser]
    public class AccessEvidenceBenchmarks
    {
        private TestFlowData _flowData;
        private TestFlowData _flowDataLargeDict;
        private TestFlowData _flowDataStringValues;

        public AccessEvidenceBenchmarks()
        {
            _flowData = new TestFlowData();
            _flowData.AddEvidence("test", "value");

            _flowDataLargeDict = new TestFlowData();
            for(int i = 0; i < 100; i++)
            {
                _flowDataLargeDict.AddEvidence($"test{i}", $"value{i}");
            }

            _flowDataStringValues = new TestFlowData();
            _flowDataStringValues.AddEvidence("test", new StringValues(new string[] { "value1", "value2", "value3" }));
            for (int i = 0; i < 100; i++)
            {
                _flowDataStringValues.AddEvidence($"test{i}", $"value{i}");
            }
        }

        [Benchmark]
        [BenchmarkCategory("Accessor-FlowData")]
        public void AccessEvidence_FlowData_TryGetEvidence()
        {
            _flowData.TryGetEvidence("test", out string value);
        }

        [Benchmark]
        [BenchmarkCategory("Accessor-AsDictionary")]
        public void AccessEvidence_AsDictionary_ToString()
        {
            var value = _flowData.GetEvidence().AsDictionary()["test"].ToString();
        }

        [Benchmark]
        [BenchmarkCategory("Accessor-FlowData")]
        public void AccessEvidence_FlowData_TryGetEvidence_NoEntry()
        {
            _flowData.TryGetEvidence("doesnotexist", out string value);
        }

        [Benchmark]
        [BenchmarkCategory("Accessor-FlowData")]
        public void AccessEvidenceLargeDict_FlowData_TryGetEvidence()
        {
            _flowDataLargeDict.TryGetEvidence("test50", out string value);
        }

        [Benchmark]
        [BenchmarkCategory("Accessor-AsDictionary")]
        public void AccessEvidenceLargeDict_AsDictionary_ToString()
        {
            var value = _flowDataLargeDict.GetEvidence().AsDictionary()["test50"].ToString();
        }

        [Benchmark]
        [BenchmarkCategory("Accessor-FlowData")]
        public void AccessEvidenceStringValues_FlowData_TryGetEvidence_WrongType()
        {
            _flowDataStringValues.TryGetEvidence("test", out string value);
        }

        [Benchmark]
        [BenchmarkCategory("Accessor-FlowData")]
        public void AccessEvidenceStringValues_FlowData_TryGetEvidence()
        {
            _flowDataStringValues.TryGetEvidence("test", out StringValues value);
        }

        [Benchmark]
        [BenchmarkCategory("Accessor-AsDictionary")]
        public void AccessEvidenceStringValues_AsDictionary_ToString()
        {
            var value = _flowDataStringValues.GetEvidence().AsDictionary()["test"].ToString();
        }
    }
}
