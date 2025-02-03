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
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FiftyOne.Pipeline.Core.Tests.HelperClasses
{
    /// <summary>
    /// Simple flow element used for testing.
    /// This element will split a given string based on a delimiter specified
    /// at construction time.
    /// </summary>
    public class ListSplitterElement : 
        FlowElementBase<ListSplitterElementData, IElementPropertyMetaData>
    {
        public override string ElementDataKey => "listSplitter";

        public List<string> EvidenceKeys = new List<string>()
        {
            "list-to-split"
        };
        private EvidenceKeyFilterWhitelist _evidenceKeyFilter;

        public override IEvidenceKeyFilter EvidenceKeyFilter => _evidenceKeyFilter;

        public override IList<IElementPropertyMetaData> Properties => new List<IElementPropertyMetaData>();

        /// <summary>
        /// The delimiter(s) to split the string on
        /// </summary>
        private string[] _delimiters;
        /// <summary>
        /// The maximum length of a resulting string.
        /// If the string is larger than this, it will be split into chunks
        /// of _maxLength.
        /// </summary>
        private int _maxLength;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="delimiters">
        /// The delimiter(s) to use when splitting strings
        /// </param>
        /// <param name="maxLength">
        /// The maximum length of a resulting string.
        /// If the string is larger than this, it will be split into chunks
        /// of _maxLength.
        /// </param>
        public ListSplitterElement(List<string> delimiters, int maxLength) :
            base(new Mock<ILogger<ListSplitterElement>>().Object)
        {
            _delimiters = delimiters.ToArray();
            _maxLength = maxLength;
            _evidenceKeyFilter = new EvidenceKeyFilterWhitelist(EvidenceKeys);
        }

        /// <summary>
        /// Process the flow data
        /// </summary>
        /// <param name="data">
        /// The <see cref="IFlowData"/>
        /// </param>
        protected override void ProcessInternal(IFlowData data)
        {
            ListSplitterElementData elementData = data.GetOrAdd(
                ElementDataKeyTyped,
                (p) => new ListSplitterElementData(p));
            // Get the source string
            string source = (string)data.GetEvidence()[EvidenceKeys[0]];
            // Split the source string using the configured delimiter.
            var results = source
                .Split(_delimiters, StringSplitOptions.RemoveEmptyEntries)
                .ToList();

            elementData.Result = new List<string>();
            // Iterate through the resulting strings, checking if they are 
            // over the max length.
            // If any string is too long then it is split into chunks of
            // max length.
            for (int i = 0; i < results.Count; i++)
            {
                var result = results[i];
                while (result.Length > _maxLength)
                {
                    // Take the first _maxLength characters and add them
                    // to the element data result.
                    elementData.Result.Add(result.Remove(_maxLength));
                    // Remove the first _maxLength characters from the 
                    // string and repeat.
                    result = result.Substring(_maxLength);
                }
                // Add the string to the element data result.
                if (result.Length > 0)
                {
                    elementData.Result.Add(result);
                }
            }
        }

        protected override void ManagedResourcesCleanup()
        {
        }

        protected override void UnmanagedResourcesCleanup()
        {
        }

    }
}
