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
using FiftyOne.Pipeline.Core.TypedMap;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;

namespace FiftyOne.Pipeline.Math
{
    public class MathElement : FlowElementBase<IMathData, IElementPropertyMetaData>
    {
        private EvidenceKeyFilterWhitelist _evidenceKeyFilter;

        private IList<IElementPropertyMetaData> _properties =
            new List<IElementPropertyMetaData>();

        /// <summary>
        /// Typed data key used to return an IMathData from a FlowData object.
        /// </summary>
        public static TypedKey<IMathData> math =
            new TypedKey<IMathData>("math");

        /// <summary>
        /// Data key used when storing data in a FlowData object.
        /// </summary>
        public override string ElementDataKey => "math";

        /// <summary>
        /// Required evidence for process method.
        /// </summary>
        public List<string> EvidenceKeys = new List<string>()
        {
            Constants.EVIDENCE_OPERATION_KEY
        };
        
        public override IEvidenceKeyFilter EvidenceKeyFilter => _evidenceKeyFilter;

        private void initProperties()
        {
            _properties.Add(new ElementPropertyMetaData(this, "operation", typeof(string), true));
            _properties.Add(new ElementPropertyMetaData(this, "result", typeof(double), true));
        }

        /// <summary>
        /// Construct a new instance of MathElement using the logger and data
        /// factory provided.
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <param name="elementDataFactory">
        /// Data factory to construct new instances of MathData
        /// </param>
        internal MathElement(
            ILogger<MathElement> logger,
            Func<IPipeline, FlowElementBase<IMathData, IElementPropertyMetaData>, 
                IMathData> elementDataFactory)
            : base(logger, elementDataFactory)
        {
            // Set the evidence key filter for the flow data to use.
            _evidenceKeyFilter = new EvidenceKeyFilterWhitelist(EvidenceKeys);
            initProperties();
        }

        /// <summary>
        /// Process the math operation by parsing the text and computing the
        /// result.
        /// </summary>
        /// <param name="data">FlowData to add the result to</param>
        protected override void ProcessInternal(IFlowData data)
        {
            MathData elementData = (MathData)data.GetOrAdd(
                ElementDataKeyTyped,
                (f) => base.CreateElementData(f));
            string operation = ((string)data.GetEvidence()[EvidenceKeys[0]]);
            if (operation != null)
            {
                // Parse the text representation of the mathematical operation.
                elementData.Operation = operation
                        .Replace("plus", "+")
                        .Replace("minus", "-")
                        .Replace("divide", "/")
                        .Replace("times", "*");
                // Compute the value of the operation.
                elementData.Result = Convert.ToDouble(
                    new DataTable().Compute(elementData.Operation, ""));
            }
            else
            {
                // Nothing provided, so just set zeros.
                elementData.Operation = "0";
                elementData.Result = 0;
            }
        }

        public override IList<IElementPropertyMetaData> Properties =>
            _properties;

        protected override void ManagedResourcesCleanup()
        {
            
        }

        protected override void UnmanagedResourcesCleanup()
        {
            
        }
    }
}
