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
using Examples.OnPremiseEngine.Data;
using FiftyOne.Pipeline.Engines.FlowElements;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;

namespace Examples.OnPremiseEngine.FlowElements
{
    //! [class]
    //! [constructor]
    public class SimpleOnPremiseEngine : OnPremiseAspectEngineBase<IStarSignData, IAspectPropertyMetaData>
    {
        public SimpleOnPremiseEngine(
            string dataFilePath,
            ILogger<AspectEngineBase<IStarSignData, IAspectPropertyMetaData>> logger,
            Func<IPipeline, FlowElementBase<IStarSignData, IAspectPropertyMetaData>, IStarSignData> aspectDataFactory,
            string tempDataFilePath)
            : base(logger, aspectDataFactory, tempDataFilePath)
        {
            _dataFile = dataFilePath;
            Init();
        }
        //! [constructor]

        private string _dataFile;

        private IList<StarSign> _starSigns;

        //! [init]
        private void Init()
        {
            // Create a new list to store the data.
            var starSigns = new List<StarSign>();
            // Open the data file to read the data.
            using (TextReader reader = File.OpenText(_dataFile))
            {
                // Read each line and parse it to construct a new instance of
                // the StarSign class.
                string line = reader.ReadLine();
                while (line != null)
                {
                    var columns = line.Split(',');
                    starSigns.Add(new StarSign(
                        columns[0],
                        DateTime.ParseExact(columns[1], @"yy/MM/dd", CultureInfo.InvariantCulture),
                        DateTime.ParseExact(columns[2], @"yy/MM/dd", CultureInfo.InvariantCulture)));

                    line = reader.ReadLine();
                }
            }
            // Set the data.
            _starSigns = starSigns;
        }
        //! [init]

        // The IStarSignData will be stored with the key "starsign" in the FlowData.
        public override string ElementDataKey => "starsign";

        // The only item of evidence needed is "date-of-birth".
        public override IEvidenceKeyFilter EvidenceKeyFilter =>
            new EvidenceKeyFilterWhitelist(new List<string>() { "date-of-birth" });

        public override IList<IAspectPropertyMetaData> Properties => new List<IAspectPropertyMetaData>() {
            // The only property which will be returned is "starsign" which will be
            // a string.
            new AspectPropertyMetaData(this, "starsign", typeof(string), "starsign", new List<string>(){"free"}, true),
        };

        // The data file is free.
        public override string DataSourceTier => "free";
        
        public override void RefreshData(string dataFileIdentifier)
        {
            // Reload star signs from the data file.
            Init();
        }

        public override void RefreshData(string dataFileIdentifier, Stream data)
        {
            // We won't implement this logic in this example.
            throw new NotImplementedException();
        }

        protected override void ProcessEngine(IFlowData data, IStarSignData aspectData)
        {
            // Cast aspectData to AgeData so the 'setter' is available.
            StarSignData starSignData = (StarSignData)aspectData;

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

        protected override void UnmanagedResourcesCleanup()
        {
            // Nothing to clean up here.
        }
    }
    //! [class]
}
