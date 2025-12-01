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
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.FlowElements;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FiftyOne.Pipeline.Examples.Shared
{
    /// <summary>
    /// This engine will check if a number is prime or not.
    /// It is used as an example of a simple engine.
    /// </summary>
    public class PrimeCheckerEngine : AspectEngineBase<IPrimeCheckerData, AspectPropertyMetaData>
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="logger">
        /// The logger to use
        /// </param>
        /// <param name="aspectDataFactory">
        /// The factory function to use when the engine creates an
        /// <see cref="IPrimeCheckerData"/> instance.
        /// </param>
        public PrimeCheckerEngine(
            ILogger<AspectEngineBase<IPrimeCheckerData, AspectPropertyMetaData>> logger, 
            Func<IPipeline, FlowElementBase<IPrimeCheckerData, AspectPropertyMetaData>, IPrimeCheckerData> aspectDataFactory) : 
            base(logger, aspectDataFactory)
        {
        }

        /// <summary>
        /// The string name of the key used to access the data populated 
        /// by this element in the <see cref="IFlowData"/>.
        /// </summary>
        public override string ElementDataKey => "primechecker";

        /// <summary>
        /// A list of all the evidence keys that this Flow Element can
        /// make use of.
        /// </summary>
        public override IEvidenceKeyFilter EvidenceKeyFilter => new EvidenceKeyFilterWhitelist(
            new List<string>()
        {
            Constants.PRIMECHECKER_EVIDENCE_KEY
        });

        /// <summary>
        /// The tier to which the current data source belongs.
        /// This is not applicable to this engine as it has no tiers.
        /// </summary>
        public override string DataSourceTier => "n/a";

        /// <summary>
        /// Details of the properties that this engine can populate. 
        /// </summary>
        public override IList<AspectPropertyMetaData> Properties => new List<AspectPropertyMetaData>()
        {
            new AspectPropertyMetaData(this, "IsPrime", typeof(bool?), "n/a", new string[] { "n/a" }, true)
        };

        protected override void ManagedResourcesCleanup()
        {
        }

        protected override void UnmanagedResourcesCleanup()
        {
        }

        /// <summary>
        /// Find out if the specified number is prime or not
        /// </summary>
        /// <param name="number">
        /// The number that may or may not be prime
        /// </param>
        /// <returns>
        /// true is the number is prime. false if not.
        /// </returns>
        private bool IsPrime(int number)
        {
            if (number == 1) return false;
            if (number == 2) return true;

            var limit = Math.Ceiling(Math.Sqrt(number));

            for (int i = 2; i <= limit; ++i)
            {
                if (number % i == 0) return false;
            }

            return true;
        }

        /// <summary>
        /// Called for this engine to perform its processing.
        /// </summary>
        /// <param name="data">
        /// The <see cref="IFlowData"/> that contains the input and output data.
        /// </param>
        /// <param name="aspectData">
        /// The data instance to use to output results.
        /// </param>
        protected override void ProcessEngine(IFlowData data, IPrimeCheckerData aspectData)
        {
            int value;
            // Try to get the evidence this engine requires.
            if (data.TryGetEvidence(Constants.PRIMECHECKER_EVIDENCE_KEY, out value))
            {
                // Determine if the number is prime and set the output
                // data accordingly.
                aspectData.IsPrime = IsPrime(value);
                // Wait for a short time.
                // This is done to help illustrate the impact of 
                // features such as caching and lazy loading.
                Task.Delay(Constants.PRIMECHECKER_ENGINE_DELAY).Wait();
            }
            else
            {
                // If the evidence is not present then throw an exception.
                throw new Exception($"No evidence for key '{Constants.PRIMECHECKER_EVIDENCE_KEY}'");
            }
        }
    }
}
