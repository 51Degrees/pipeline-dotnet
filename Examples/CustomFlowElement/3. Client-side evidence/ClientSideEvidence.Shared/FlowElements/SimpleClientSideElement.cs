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
using SimpleClientSideElement.Data;
using FiftyOne.Pipeline.Core.FlowElements;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using FiftyOne.Pipeline.Core.Data.Types;

namespace Examples.ClientSideEvidence.Shared
{
    //! [class]
    //! [constructor]
    public class SimpleClientSideElement : FlowElementBase<IStarSignData, IElementPropertyMetaData>
    {
        public SimpleClientSideElement(
            ILogger<FlowElementBase<IStarSignData, IElementPropertyMetaData>> logger,
            Func<IPipeline, FlowElementBase<IStarSignData, IElementPropertyMetaData>, 
                IStarSignData> elementDataFactory)
            : base(logger, elementDataFactory)
        {
            Init();
        }
        //! [constructor]

        private IList<StarSign> _starSigns;

        private static string[][] _starSignData = {
            new string[3] {"Aries","21/03","19/04"},
            new string[3] {"Taurus","20/04","20/05"},
            new string[3] {"Gemini","21/05","20/06"},
            new string[3] {"Cancer","21/06","22/07"},
            new string[3] {"Leo","23/07","22/08"},
            new string[3] {"Virgo","23/08","22/09"},
            new string[3] {"Libra","23/09","22/10"},
            new string[3] {"Scorpio","23/10","21/11"},
            new string[3] {"Sagittarius","22/11","21/12"},
            new string[3] {"Capricorn","22/12","19/01"},
            new string[3] {"Aquarius","20/01","18/02"},
            new string[3] {"Pisces","19/02","20/03"}
        };

        //! [init]
        private void Init()
        {
            var starSigns = new List<StarSign>();
            foreach (var starSign in _starSignData)
            {
                starSigns.Add(new StarSign(
                    starSign[0],
                    DateTime.ParseExact(starSign[1], "dd/MM", null),
                    DateTime.ParseExact(starSign[2], "dd/MM", null)));
            }
            _starSigns = starSigns;
        }
        //! [init]

        // The IAgeData will be stored with the key "starsign" in the FlowData.
        public override string ElementDataKey => "starsign";

        // The only item of evidence needed is "date-of-birth".
        public override IEvidenceKeyFilter EvidenceKeyFilter =>
            new EvidenceKeyFilterWhitelist(new List<string>() { "cookie.date-of-birth" });

        public override IList<IElementPropertyMetaData> Properties =>
            new List<IElementPropertyMetaData>()
            {
                // The properties which will be returned are "starsign" and the
                // JavaScript to get the date of birth.
                new ElementPropertyMetaData(this, "starsign", typeof(string), true),
                new ElementPropertyMetaData(this, "dobjavascript", typeof(JavaScript), true)
            };

        protected override void ProcessInternal(IFlowData data)
        {
            var monthAndDay = new DateTime(1, 1, 1);

            // Create a new IStarSignData, and cast to StarSignData so the 'setter' is available.
            StarSignData starSignData = (StarSignData)data.GetOrAdd(ElementDataKey, CreateElementData);

            bool validDateOfBirth = false;

            if (data.TryGetEvidence("cookie.date-of-birth", out string dateString))
            {
                // "date-of-birth" is there, so parse it.
                string[] dateSections = dateString.Split('/');
                try
                {
                    monthAndDay = new DateTime(
                        1,
                        int.Parse(dateSections[1]),
                        int.Parse(dateSections[0]));
                    validDateOfBirth = true;
                }
                catch (Exception)
                {

                }
            }
            if (validDateOfBirth)
            {
                // "date-of-birth" is valid, so set the star sign.
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
                // No need to run the client side code again.
                starSignData.DobJavaScript = new JavaScript("");
            }
            else
            {
                // "date-of-birth" is not there, so set the star sign to unknown.
                starSignData.StarSign = "Unknown";
                // Set the client side JavaScript to get the date of birth.
                starSignData.DobJavaScript = new JavaScript(
                "var dob = window.prompt('Enter your date of birth.','dd/mm/yyyy');" +
                "if (dob != null) {" +
                "document.cookie='date-of-birth='+dob;" +
                "location.reload();" +
                "}");
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
    //! [class]
}