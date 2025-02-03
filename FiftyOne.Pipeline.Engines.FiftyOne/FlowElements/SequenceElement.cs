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
using System;
using System.Collections.Generic;

namespace FiftyOne.Pipeline.Engines.FiftyOne.FlowElements
{
    /// <summary>
    /// Sequence element establishes session and sequence evidence in the 
    /// pipeline.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/pipeline-elements/sequence-element.md">Specification</see>
    /// </summary>
    public class SequenceElement : FlowElementBase<IElementData, IElementPropertyMetaData>
    {
        private IEvidenceKeyFilter _evidenceKeyFilter = new EvidenceKeyFilterWhitelist(
            new List<string>() {
                Constants.EVIDENCE_SESSIONID,
                Constants.EVIDENCE_SEQUENCE
            });

        private IList<IElementPropertyMetaData> _properties = new List<IElementPropertyMetaData>();

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="logger"></param>
        public SequenceElement(ILogger<FlowElementBase<IElementData, IElementPropertyMetaData>> logger) : base(logger)
        {
        }

        /// <summary>
        /// The default element data key that will be used for this element. 
        /// </summary>
#pragma warning disable CA1707 // Identifiers should not contain underscores
        public const string DEFAULT_ELEMENT_DATA_KEY = "sequence";
#pragma warning restore CA1707 // Identifiers should not contain underscores

        /// <summary>
        /// The element data key.
        /// </summary>
        public override string ElementDataKey => DEFAULT_ELEMENT_DATA_KEY;

        /// <summary>
        /// The evidence key filter.
        /// </summary>
        public override IEvidenceKeyFilter EvidenceKeyFilter
        {
            get { return _evidenceKeyFilter; }
        }

        /// <summary>
        /// The properties populated by this element.
        /// </summary>
        public override IList<IElementPropertyMetaData> Properties => _properties;

        /// <summary>
        /// Called when the element is disposed.
        /// </summary>
        protected override void ManagedResourcesCleanup()
        {
        }

        /// <summary>
        /// Process method checks for presence of a session id and sequence 
        /// number. If they do not exist then they are initialized in evidence.
        /// If they do exist in evidence then the sequence number is incremented
        /// and added back to the evidence.
        /// </summary>
        /// <param name="data">
        /// The <see cref="IFlowData"/> instance to process.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if the supplied data instance is null
        /// </exception>
        protected override void ProcessInternal(IFlowData data)
        {
            if(data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            var evidence = data.GetEvidence().AsDictionary();

            // If the evidence does not contain a session id then create a new one.
            if (evidence.ContainsKey(Constants.EVIDENCE_SESSIONID) == false)
            {
                data.AddEvidence(Constants.EVIDENCE_SESSIONID, GetNewSessionId());
            }

            // If the evidence does not have a sequence then add one. Otherwise
            // increment it.
            if (evidence.ContainsKey(Constants.EVIDENCE_SEQUENCE) == false)
            {
                data.AddEvidence(Constants.EVIDENCE_SEQUENCE, 1);
            }
            else if (evidence.TryGetValue(Constants.EVIDENCE_SEQUENCE, out object sequence))
            {
                if (sequence is int result || (sequence is string seq && int.TryParse(seq, out result)))
                {
                    data.AddEvidence(Constants.EVIDENCE_SEQUENCE, result + 1);
                }
                else
                {
                    data.AddError(new Exception(Messages.MessageFailSequenceNumberParse), this);
                    Logger.LogError(Messages.MessageFailSequenceNumberIncrement);
                }
            }
            else
            {
                Logger.LogError(Messages.MessageFailSequenceNumberRetreive);
            }
        }

        /// <summary>
        /// Called as part of object disposal.
        /// This element has no unmanaged resources so this method is empty.
        /// </summary>
        protected override void UnmanagedResourcesCleanup()
        {
        }

        private string GetNewSessionId()
        {
            Guid g = Guid.NewGuid();
            return g.ToString();
        }
    }
}
