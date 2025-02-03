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
using Examples.CloudEngine.Data;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using FiftyOne.Pipeline.CloudRequestEngine.FlowElements;
using FiftyOne.Pipeline.Core.Exceptions;
using Newtonsoft.Json;

namespace Examples.CloudEngine.FlowElements
{
    //! [class]
    //! [constructor]
    public class SimpleCloudEngine : CloudAspectEngineBase<IStarSignData>
    {
        public SimpleCloudEngine(
            ILogger<SimpleCloudEngine> logger,
            Func<IPipeline, FlowElementBase<IStarSignData, IAspectPropertyMetaData>, IStarSignData> deviceDataFactory,
            CloudRequestEngine engine)
            : base(logger, deviceDataFactory)
        {
            if (engine == null)
            {
                // There is no cloud request engine in the pipeline. This means
                // there is no result available.
                throw new PipelineConfigurationException(
                    $"The '{GetType().Name}' requires a 'CloudRequestEngine' " +
                    $"before it in the Pipeline. This engine will be unable " +
                    $"to produce results until this is corrected.");
            }
        }
        //! [constructor]
        
        public override string ElementDataKey => "starsign";

        public override IEvidenceKeyFilter EvidenceKeyFilter =>
            // This engine needs no evidence. 
            // It works from the cloud request data.
            new EvidenceKeyFilterWhitelist(new List<string>());

        protected override void ProcessCloudEngine(IFlowData data, IStarSignData aspectData, string json)
        {
            // Cast aspectData to StarSignData so the 'setter' is available.
            StarSignData starSignData = (StarSignData)aspectData;
            
            // Extract data from json to the aspectData instance.
            var dictionary = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
            // Get the results for the star sign component.
            var starSign = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            JsonConvert.PopulateObject(dictionary["starsign"].ToString(), starSign);
            // Now get the values from the star sign results.
            starSignData.StarSign = starSign["starsign"].ToString();
        }
    }
    //! [class]
}