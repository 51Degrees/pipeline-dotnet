/* *********************************************************************
 * This Original Work is copyright of 51 Degrees Mobile Experts Limited.
 * Copyright 2026 51 Degrees Mobile Experts Limited, Davidson House,
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

using System.Collections.Generic;

namespace FiftyOne.Pipeline.DerivedProperty.Data
{
    /// <summary>
    /// What one source property did on one request.
    /// </summary>
    public sealed class DerivedTracedProperty
    {
        /// <summary>
        /// Create a new instance.
        /// </summary>
        /// <param name="name">The property in elementKey.PropertyName form.</param>
        /// <param name="available">
        /// True where the property was there and could be read.
        /// </param>
        /// <param name="value">The value read, where it was read.</param>
        /// <param name="reason">
        /// Why the property could not be read, where it could not.
        /// </param>
        public DerivedTracedProperty(
            string name,
            bool available,
            object value,
            string reason)
        {
            Name = name;
            Available = available;
            Value = value;
            Reason = reason;
        }

        /// <summary>The property in elementKey.PropertyName form.</summary>
        public string Name { get; }

        /// <summary>
        /// True where the property was there and could be read.
        /// </summary>
        public bool Available { get; }

        /// <summary>The value read, where it was read.</summary>
        public object Value { get; }

        /// <summary>
        /// Why the property could not be read, where it could not.
        /// </summary>
        public string Reason { get; }
    }

    /// <summary>
    /// What one check answered on one request.
    /// </summary>
    public sealed class DerivedTracedCheck
    {
        /// <summary>
        /// Create a new instance.
        /// </summary>
        /// <param name="name">The name the script gave the check.</param>
        /// <param name="state">What the check answered.</param>
        public DerivedTracedCheck(string name, bool state)
        {
            Name = name;
            State = state;
        }

        /// <summary>The name the script gave the check.</summary>
        public string Name { get; }

        /// <summary>What the check answered.</summary>
        public bool State { get; }
    }

    /// <summary>
    /// A record of one run of one script, being what every source property
    /// did, what every check answered, and which rule supplied the answer.
    ///
    /// A trace is for tests, for the tester page and for anyone working out
    /// why a script gave the answer it gave. Nothing on the request path
    /// asks for one, so running a pipeline costs nothing for the trace to
    /// exist.
    /// </summary>
    public sealed class DerivedTrace
    {
        private readonly List<DerivedTracedProperty> _properties =
            new List<DerivedTracedProperty>();
        private readonly List<DerivedTracedCheck> _checks =
            new List<DerivedTracedCheck>();

        /// <summary>
        /// Every source property the script names, in the order the script
        /// first named them.
        /// </summary>
        public IReadOnlyList<DerivedTracedProperty> Properties => _properties;

        /// <summary>
        /// Every check, in script order. Empty where a source property was
        /// missing, because the checks are then never reached.
        /// </summary>
        public IReadOnlyList<DerivedTracedCheck> Checks => _checks;

        /// <summary>
        /// Which rule supplied the answer, by index, or null where a source
        /// property was missing and no rule was reached.
        /// </summary>
        public int? MatchedRule { get; private set; }

        /// <summary>
        /// True where the rule that matched was the Else.
        /// </summary>
        public bool MatchedElse { get; private set; }

        /// <summary>
        /// The message saying why there is no value, where there is none.
        /// </summary>
        public string NoValueMessage { get; private set; }

        /// <summary>
        /// Record what every source property did. Called by the evaluator.
        /// </summary>
        /// <param name="script">The script being run.</param>
        /// <param name="context">The values for this request.</param>
        public void Fill(
            DerivedScript script,
            DerivedEvaluationContext context)
        {
            if (script == null || context == null)
            {
                return;
            }
            _properties.Clear();
            for (var i = 0; i < script.Properties.Count; i++)
            {
                _properties.Add(new DerivedTracedProperty(
                    script.Properties[i].Name,
                    context.Available[i],
                    context.Available[i] ? context.Values[i] : null,
                    context.Reasons[i]));
            }
        }

        /// <summary>
        /// Record what every check answered. Called by the evaluator.
        /// </summary>
        /// <param name="script">The script being run.</param>
        /// <param name="context">The values for this request.</param>
        public void FillChecks(
            DerivedScript script,
            DerivedEvaluationContext context)
        {
            if (script == null || context == null)
            {
                return;
            }
            _checks.Clear();
            for (var i = 0; i < script.Checks.Count; i++)
            {
                _checks.Add(new DerivedTracedCheck(
                    script.Checks[i].Name, context.Checks[i]));
            }
        }

        /// <summary>
        /// Record which rule matched. Called by the evaluator.
        /// </summary>
        /// <param name="index">The rule, by index.</param>
        /// <param name="isElse">True where the rule is the Else.</param>
        public void SetMatch(int index, bool isElse)
        {
            MatchedRule = index;
            MatchedElse = isElse;
        }

        /// <summary>
        /// Record that there is no value, and why. Called by the evaluator.
        /// </summary>
        /// <param name="message">The reason there is no value.</param>
        public void SetNoValue(string message)
        {
            NoValueMessage = message;
        }
    }
}
