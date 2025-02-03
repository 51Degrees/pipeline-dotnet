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
using Examples.CustomFlowElement.Data;
using FiftyOne.Pipeline.Core.FlowElements;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Examples.CustomFlowElement.FlowElements
{
    //! [class]
    //! [constructor]
    public class SimpleFlowElement : FlowElementBase<IStarSignData, IElementPropertyMetaData>
    {
        public SimpleFlowElement(
            ILogger<FlowElementBase<IStarSignData, IElementPropertyMetaData>> logger,
            Func<IPipeline, FlowElementBase<IStarSignData, IElementPropertyMetaData>, IStarSignData> elementDataFactory)
            : base(logger, elementDataFactory)
        {
            Init();
        }
        //! [constructor]

        private IList<StarSign> _starSigns;

        private static string[][] _starSignData = {
            new string[3] {"Aries", "01/03/21", "01/04/19"},
            new string[3] {"Taurus","01/04/20","01/05/20"},
            new string[3] {"Gemini","01/05/21","01/06/20"},
            new string[3] {"Cancer","01/06/21","01/07/22"},
            new string[3] {"Leo","01/07/23","01/08/22"},
            new string[3] {"Virgo","01/08/23","01/09/22"},
            new string[3] {"Libra","01/09/23","01/10/22"},
            new string[3] {"Scorpio","01/10/23","01/11/21"},
            new string[3] {"Sagittarius","01/11/22","01/12/21"},
            new string[3] {"Capricorn","01/12/22","01/01/19"},
            new string[3] {"Aquarius","01/01/20","01/02/18"},
            new string[3] {"Pisces","01/02/19","01/03/20"}
        };

        //! [init]
        private void Init()
        {
            var starSigns = new List<StarSign>();
            foreach (var starSign in _starSignData)
            {
                starSigns.Add(new StarSign(
                    starSign[0],
                    DateTime.ParseExact(starSign[1], @"yy/MM/dd", CultureInfo.InvariantCulture),
                    DateTime.ParseExact(starSign[2], @"yy/MM/dd", CultureInfo.InvariantCulture)));
            }
            _starSigns = starSigns;
        }
        //! [init]

        // The IStarSignData will be stored with the key "starsign" in the FlowData.
        public override string ElementDataKey => "starsign";

        // The only item of evidence needed is "date-of-birth".
        public override IEvidenceKeyFilter EvidenceKeyFilter =>
            new EvidenceKeyFilterWhitelist(new List<string>() { "date-of-birth" });

        public override IList<IElementPropertyMetaData> Properties =>
            new List<IElementPropertyMetaData>()
            {
                // The only property which will be returned is "starsign" which will be
                // a string.
                new ElementPropertyMetaData(this, "starsign", typeof(string), true)
            };

        protected override void ProcessInternal(IFlowData data)
        {
            // Create a new IStarSignData, and cast to StarSignData so the 'setter' is available.
            StarSignData starSignData = (StarSignData)data.GetOrAdd(ElementDataKey, CreateElementData);

            if (data.TryGetEvidence("date-of-birth", out DateTime dateOfBirth))
            {
                // "date-of-birth" is there, so set the star sign.
                var monthAndDay = new DateTime(1, dateOfBirth.Month, dateOfBirth.Day);
                foreach (var starSign in _starSigns)
                {
                    if (monthAndDay > starSign.Start &&
                        monthAndDay < starSign.End)
                    {
                        // The star sign has been found, so set it in the
                        // results.
                        starSignData.StarSign = starSign.Name;
                        break;
                    }
                }
            }
            else
            {
                // "date-of-birth" is not there, so set the star sign to unknown.
                starSignData.StarSign = "Unknown";
            }
        }

        protected override void ManagedResourcesCleanup()
        {
            // Nothing to clean up here.
        }

        protected override void UnmanagedResourcesCleanup()
        {
            // Nothing to clean up here.
        }
    }
}
//! [class]
