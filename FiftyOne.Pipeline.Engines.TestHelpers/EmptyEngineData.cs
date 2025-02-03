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

using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.FlowElements;
using FiftyOne.Pipeline.Engines.Services;
using Microsoft.Extensions.Logging;

namespace FiftyOne.Pipeline.Engines.TestHelpers
{
    public class EmptyEngineData : AspectDataBase
    {
        public const string VALUE_ONE_KEY = "valueone";
        public const string VALUE_TWO_KEY = "valuetwo";
        public const string VALUE_THREE_KEY = "valuethree";

        public EmptyEngineData(
            ILogger<EmptyEngineData> logger,
            IPipeline pipeline,
            IAspectEngine engine,
            IMissingPropertyService missingPropertyService) :
            base(logger, pipeline, engine, missingPropertyService)
        {
        }

        public int ValueOne
        {
            get
            {
                return (int)base[VALUE_ONE_KEY];
            }
            set
            {
                base[VALUE_ONE_KEY] = value;
            }
        }

        public int ValueTwo
        {
            get
            {
                return (int)base[VALUE_TWO_KEY];
            }
            set
            {
                base[VALUE_TWO_KEY] = value;
            }
        }

        public int ValueThree
        {
            get
            {
                return (int)base[VALUE_THREE_KEY];
            }
            set
            {
                base[VALUE_THREE_KEY] = value;
            }
        }
    }
}
