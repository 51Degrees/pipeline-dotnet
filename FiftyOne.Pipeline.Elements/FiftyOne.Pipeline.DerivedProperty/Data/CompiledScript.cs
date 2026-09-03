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

using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.TypedMap;
using FiftyOne.Pipeline.Engines;
using FiftyOne.Pipeline.Engines.Data;
using Microsoft.Extensions.Logging;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace FiftyOne.Pipeline.DerivedProperty.Data
{
    /// <summary>
    /// One script, turned at build into the smallest thing that can answer
    /// a request. Everything expensive happens once here rather than on
    /// every request, being the element data keys to read, the converter for
    /// each source property, the condition tree and the rule array.
    ///
    /// Every field of an instance is read only once the constructor
    /// returns, so one instance serves every request and every thread with
    /// no locking. Each request gets its own
    /// <see cref="DerivedEvaluationContext"/> holding the values read for
    /// that request, and nothing in a request is written back.
    ///
    /// One thing is shared and does change, being the static cache of
    /// readers for weighted value types further down this file. The cache
    /// is a <see cref="ConcurrentDictionary{TKey, TValue}"/> filled through
    /// GetOrAdd, so a type may be worked out twice under a race and the
    /// answer is the same either way.
    ///
    /// A test processes the same evidence on 32 threads and asserts every
    /// answer equals the single threaded answer, which is what would fail
    /// if any of the above stopped being true.
    /// </summary>
    public sealed class CompiledScript
    {
        /// <summary>
        /// The sentence appended to every message saying a derived property
        /// has no value, naming the things that usually cause a source
        /// property to be missing. The wording matches the JavaScript
        /// reference evaluator and every other language.
        /// </summary>
        public const string UsualCauses =
            "Usual causes are the element that supplies the property not " +
            "being in the pipeline, or being added after this element " +
            "rather than before it, the property being excluded in the " +
            "engine configuration, the property not being included in the " +
            "resource key, or JavaScript that populates the property not " +
            "having run yet.";

        private readonly DerivedScript _script;

        // The distinct element data keys the script reads, and for each
        // source property the index of its key, so each element data is
        // fetched once per request however many properties come from it.
        private readonly string[] _elementDataKeys;
        private readonly ITypedKey<IElementData>[] _typedKeys;
        private readonly int[] _slotElementIndex;
        private readonly string[] _slotPropertyName;
        private readonly DerivedValueType[] _slotValueType;

        // The element data a script replaces a property in, or null where
        // the script creates a property of its own.
        private readonly ITypedKey<IElementData> _targetKey;

        private readonly Func<object, IAspectPropertyValue> _valueFactory;
        private readonly Func<string, IAspectPropertyValue> _noValueFactory;

        private readonly ILogger _logger;

        // Zero until an element data the pipeline said would be there has
        // been reported missing, then one. Read and set through Interlocked
        // because requests run on many threads at once, so without it the
        // first few requests would each report the same thing.
        private int _reported;

        /// <summary>
        /// Compile a validated script.
        /// </summary>
        /// <param name="script">The script.</param>
        /// <param name="logger">
        /// Used to report an element data that the pipeline said would be
        /// there and that no request carries, which is the one mistake the
        /// pipeline build cannot catch. Optional, and where it is null
        /// nothing is reported.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown where the script is null.
        /// </exception>
        public CompiledScript(DerivedScript script, ILogger logger = null)
        {
            _script = script ?? throw new ArgumentNullException(nameof(script));
            _logger = logger;

            var keys = new List<string>();
            var count = script.Properties.Count;
            _slotElementIndex = new int[count];
            _slotPropertyName = new string[count];
            _slotValueType = new DerivedValueType[count];

            for (var i = 0; i < count; i++)
            {
                var property = script.Properties[i];
                var index = keys.FindIndex(
                    k => string.Equals(
                        k,
                        property.ElementDataKey,
                        StringComparison.OrdinalIgnoreCase));
                if (index < 0)
                {
                    index = keys.Count;
                    keys.Add(property.ElementDataKey);
                }
                _slotElementIndex[i] = index;
                _slotPropertyName[i] = property.PropertyName;
                _slotValueType[i] = property.ValueType;
            }

            _elementDataKeys = keys.ToArray();
            _typedKeys = keys
                .Select(k => (ITypedKey<IElementData>)
                    new TypedKey<IElementData>(k))
                .ToArray();

            // Only a script that replaces a property in another element
            // needs a key for that element. A script that creates one is
            // handed the element data to write into.
            _targetKey = script.Output.IsOverride
                ? new TypedKey<IElementData>(script.Output.ElementDataKey)
                : null;

            _valueFactory = MakeValueFactory(script.Output.ValueType);
            _noValueFactory = MakeNoValueFactory(script.Output.ValueType);
        }

        /// <summary>The script this was compiled from.</summary>
        public DerivedScript Script => _script;

        /// <summary>
        /// The name of the property this script writes, which is the key it
        /// writes under in the derived element data.
        /// </summary>
        public string OutputName => _script.Output.Name;

        /// <summary>
        /// Run the script for one request and write one value, or one value
        /// that has no value with the reason, into the element data.
        /// </summary>
        /// <param name="data">The flow data for the request.</param>
        /// <param name="output">The derived element data to write into.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown where either argument is null.
        /// </exception>
        public void Process(IFlowData data, IDerivedPropertyData output)
        {
            if (data == null) { throw new ArgumentNullException(nameof(data)); }
            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            if (_targetKey == null)
            {
                output[_script.Output.Name] = Evaluate(data, null);
                return;
            }

            // A script that replaces a property in another element writes
            // nothing at all where it cannot read every source property it
            // names, so the value that element produced survives untouched.
            // Nothing is missing from the caller's point of view, so no
            // message is produced either.
            if (data.TryGetValue(_targetKey, out var target) == false ||
                target == null)
            {
                return;
            }
            var value = Evaluate(data, null);
            if (value.HasValue == false)
            {
                return;
            }
            target[_script.Output.Name] = value;
        }

        /// <summary>
        /// Run the script for one request and give back the value written,
        /// optionally filling in a trace of what happened. The trace is for
        /// tests and for tooling, and nothing on the request path asks for
        /// one.
        /// </summary>
        /// <param name="data">The flow data for the request.</param>
        /// <param name="trace">
        /// A trace to fill in, or null for no trace.
        /// </param>
        /// <returns>The value, which may be a value that has no value.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown where the flow data is null.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown where the rules do not end in an Else, which the validator
        /// does not allow and so can only happen where a script was built by
        /// hand rather than read from a file.
        /// </exception>
        public IAspectPropertyValue Evaluate(
            IFlowData data,
            DerivedTrace trace)
        {
            if (data == null) { throw new ArgumentNullException(nameof(data)); }

            var elements = new IElementData[_elementDataKeys.Length];
            for (var i = 0; i < _typedKeys.Length; i++)
            {
                data.TryGetValue(_typedKeys[i], out var elementData);
                elements[i] = elementData;
            }

            var context = new DerivedEvaluationContext(
                _slotPropertyName.Length, _script.Checks.Count);
            for (var i = 0; i < _slotPropertyName.Length; i++)
            {
                ReadSlot(i, elements, context);
            }
            if (trace != null)
            {
                trace.Fill(_script, context);
            }

            // A property is either there or it is not. Where any one of the
            // properties the script names is missing, the script says so
            // rather than answering on part of the evidence.
            var missing = Missing(context);
            if (missing != null)
            {
                var value = _noValueFactory(missing);
                trace?.SetNoValue(value.NoValueMessage);
                return value;
            }

            for (var i = 0; i < _script.Checks.Count; i++)
            {
                context.Checks[i] =
                    _script.Checks[i].Condition.Evaluate(context);
            }
            trace?.FillChecks(_script, context);

            for (var i = 0; i < _script.Rules.Count; i++)
            {
                var rule = _script.Rules[i];
                if (rule.IsElse || rule.Condition.Evaluate(context))
                {
                    trace?.SetMatch(i, rule.IsElse);
                    return _valueFactory(rule.Value);
                }
            }

            // Every script ends in an Else, which the validator enforces, so
            // the loop above always returns. Reaching here means a script was
            // built by hand rather than read and validated.
            throw new InvalidOperationException(string.Format(
                CultureInfo.InvariantCulture,
                "the rules of '{0}' do not end in an Else, which format 1 " +
                "does not allow",
                _script.Output.Name));
        }

        // ---------------------------------------------------------------
        // Reading one source property.
        // ---------------------------------------------------------------

        /// <summary>
        /// Reports, once only, that a request carried no element data at all
        /// under a key the pipeline said some element writes.
        ///
        /// The pipeline build refuses a source property that no element in
        /// the pipeline supplies, so reaching here means an element does
        /// supply it and did not run before this one. That is the mistake
        /// the build deliberately does not try to catch, because a pipeline
        /// reports elements that run in parallel in an order that does not
        /// say what ran before what, and judging that order failed
        /// pipelines that were correct.
        ///
        /// It is reported once rather than on every request, because a
        /// pipeline built the wrong way round is built the wrong way round
        /// for the life of the process and would otherwise write the same
        /// line for every request it serves.
        ///
        /// A property that is simply not there on a request, such as one
        /// the 51Degrees JavaScript has not populated yet, does not come
        /// through here at all. That is an ordinary thing to happen and is
        /// reported in the value's own message rather than in the log.
        /// </summary>
        private void ReportMissingElementData(
            string elementKey, string propertyName)
        {
            if (_logger == null ||
                System.Threading.Interlocked.CompareExchange(
                    ref _reported, 1, 0) != 0)
            {
                return;
            }
            _logger.LogError(
                "The script '{Script}' reads '{Property}', and the request " +
                "carried no element data '{Element}' at all. An element in " +
                "the pipeline does supply it, because the pipeline build " +
                "refuses a source property nothing supplies, so that " +
                "element is running after this one rather than before it. " +
                "Add the derived property element after it. Until then " +
                "every request leaves the script with no value. This is " +
                "reported once.",
                _script.Name,
                elementKey + "." + propertyName,
                elementKey);
        }

        private void ReadSlot(
            int slot,
            IElementData[] elements,
            DerivedEvaluationContext context)
        {
            var elementData = elements[_slotElementIndex[slot]];
            var elementKey = _elementDataKeys[_slotElementIndex[slot]];
            var propertyName = _slotPropertyName[slot];

            if (elementData == null)
            {
                context.Available[slot] = false;
                context.Reasons[slot] = NotAvailable(
                    elementKey,
                    propertyName,
                    "property not present on this request");
                ReportMissingElementData(elementKey, propertyName);
                return;
            }

            // Read through the indexer rather than TryGet, because TryGet
            // reads the dictionary as it stands and deliberately does not
            // wait for lazy loading, so a source engine configured with
            // SetLazyLoading would still be filling its data and every
            // request would lose its derived value in a race. The indexer
            // waits, which is what the translation engine does with its own
            // source values. It also gives a real reason for a property the
            // engine does not have, rather than the fixed wording below.
            object raw;
            try
            {
                raw = elementData[propertyName];
            }
            catch (PropertyMissingException missing)
            {
                context.Available[slot] = false;
                context.Reasons[slot] = NotAvailable(
                    elementKey,
                    propertyName,
                    string.IsNullOrEmpty(missing.Message)
                        ? "property not present on this request"
                        : missing.Message);
                return;
            }
            catch (Exception exception)
                when (exception is KeyNotFoundException ||
                    exception is InvalidCastException)
            {
                raw = null;
            }
            catch (Exception exception)
                when (exception is TimeoutException ||
                    exception is OperationCanceledException ||
                    exception is AggregateException)
            {
                // Lazy loading did not finish. The property is not readable
                // on this request, which is a state the format already has,
                // so it is reported as one rather than thrown out of an
                // element that is only reading.
                context.Available[slot] = false;
                context.Reasons[slot] = NotAvailable(
                    elementKey,
                    propertyName,
                    exception.Message);
                return;
            }

            if (raw == null)
            {
                context.Available[slot] = false;
                context.Reasons[slot] = NotAvailable(
                    elementKey,
                    propertyName,
                    "property not present on this request");
                return;
            }

            // A source value that carries its own no value state hands over
            // its message, so the reason a derived property is missing
            // reaches back to the element that actually knows.
            if (raw is IAspectPropertyValue aspectValue)
            {
                if (aspectValue.HasValue == false)
                {
                    context.Available[slot] = false;
                    context.Reasons[slot] = NotAvailable(
                        elementKey,
                        propertyName,
                        string.IsNullOrEmpty(aspectValue.NoValueMessage)
                            ? "property not present on this request"
                            : aspectValue.NoValueMessage);
                    return;
                }
                raw = aspectValue.Value;
                if (raw == null)
                {
                    context.Available[slot] = false;
                    context.Reasons[slot] = NotAvailable(
                        elementKey,
                        propertyName,
                        "property not present on this request");
                    return;
                }
            }

            if (raw is string == false && raw is IEnumerable list)
            {
                if (TryTakeHeaviest(list, out raw) == false)
                {
                    context.Available[slot] = false;
                    context.Reasons[slot] =
                        "held a list where a single value is needed";
                    return;
                }
                if (raw == null)
                {
                    context.Available[slot] = false;
                    context.Reasons[slot] = NotAvailable(
                        elementKey,
                        propertyName,
                        "property not present on this request");
                    return;
                }
                if (raw is IAspectPropertyValue nestedAspect)
                {
                    if (nestedAspect.HasValue == false)
                    {
                        context.Available[slot] = false;
                        context.Reasons[slot] = NotAvailable(
                            elementKey,
                            propertyName,
                            string.IsNullOrEmpty(nestedAspect.NoValueMessage)
                                ? "property not present on this request"
                                : nestedAspect.NoValueMessage);
                        return;
                    }
                    raw = nestedAspect.Value;
                }
            }

            var valueType = _slotValueType[slot];
            if (DerivedValueConverter.TryConvert(
                raw, valueType, out var converted) == false)
            {
                context.Available[slot] = false;
                context.Reasons[slot] = string.Format(
                    CultureInfo.InvariantCulture,
                    "held '{0}' which cannot be read as {1}",
                    DerivedValueConverter.Display(raw),
                    DerivedValueConverter.NameOf(valueType));
                return;
            }

            context.Available[slot] = true;
            context.Values[slot] = converted;
        }

        private static string NotAvailable(
            string elementKey,
            string propertyName,
            string detail)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "element '{0}' has no value for '{1}': {2}",
                elementKey,
                propertyName,
                detail);
        }

        /// <summary>
        /// The message saying which source properties were not available and
        /// what the element that supplies each one said, or null where every
        /// property the script names was read.
        /// </summary>
        private string Missing(DerivedEvaluationContext context)
        {
            var count = 0;
            for (var i = 0; i < context.Available.Length; i++)
            {
                if (context.Available[i] == false)
                {
                    count++;
                }
            }
            if (count == 0)
            {
                // Nothing is built where nothing is missing, which is the
                // usual case, so the message costs nothing on a request
                // that has everything it needs.
                return null;
            }

            var builder = new StringBuilder();
            builder.AppendFormat(
                CultureInfo.InvariantCulture,
                "Derived property '{0}' has no value because ",
                _script.Output.Name);
            builder.Append(count == 1
                ? "1 source property was not available."
                : string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} source properties were not available.",
                    count));
            for (var i = 0; i < context.Available.Length; i++)
            {
                if (context.Available[i])
                {
                    continue;
                }
                builder.AppendFormat(
                    CultureInfo.InvariantCulture,
                    " '{0}' ({1}).",
                    _script.Properties[i].Name,
                    context.Reasons[i]);
            }
            builder.Append(' ');
            builder.Append(UsualCauses);
            return builder.ToString();
        }

        // ---------------------------------------------------------------
        // Weighted values.
        // ---------------------------------------------------------------

        private static readonly ConcurrentDictionary<Type, WeightedReader>
            _weightedReaders =
                new ConcurrentDictionary<Type, WeightedReader>();

        private sealed class WeightedReader
        {
            public Func<object, int> Weight { get; set; }
            public Func<object, object> Value { get; set; }
        }

        /// <summary>
        /// Takes the value with the highest weight out of a list of weighted
        /// values. Any other list is refused, because a script names one
        /// property and compares one value.
        /// </summary>
        private static bool TryTakeHeaviest(IEnumerable list, out object taken)
        {
            taken = null;
            object best = null;
            var bestWeight = -1;
            var any = false;

            foreach (var item in list)
            {
                any = true;
                if (item == null)
                {
                    return false;
                }
                var reader = GetWeightedReader(item.GetType());
                if (reader == null)
                {
                    return false;
                }
                var weight = reader.Weight(item);
                if (weight > bestWeight)
                {
                    bestWeight = weight;
                    best = reader.Value(item);
                }
            }
            if (any == false)
            {
                return false;
            }
            taken = best;
            return true;
        }

        /// <summary>
        /// Builds and caches a reader for one weighted value type. The
        /// reflection happens once for each type ever seen and the request
        /// path then calls a compiled delegate.
        /// </summary>
        private static WeightedReader GetWeightedReader(Type type)
        {
            return _weightedReaders.GetOrAdd(type, BuildWeightedReader);
        }

        private static WeightedReader BuildWeightedReader(Type type)
        {
            var weighted = type.GetInterfaces().FirstOrDefault(
                i => i.IsGenericType &&
                    i.GetGenericTypeDefinition() == typeof(IWeightedValue<>));
            if (weighted == null)
            {
                return null;
            }
            var weightProperty = weighted.GetProperty("RawWeighting");
            var valueProperty = weighted.GetProperty("Value");
            if (weightProperty == null || valueProperty == null)
            {
                return null;
            }

            var argument = Expression.Parameter(typeof(object), "item");
            var cast = Expression.Convert(argument, weighted);
            var weight = Expression.Lambda<Func<object, int>>(
                Expression.Convert(
                    Expression.Property(cast, weightProperty), typeof(int)),
                argument).Compile();
            var value = Expression.Lambda<Func<object, object>>(
                Expression.Convert(
                    Expression.Property(cast, valueProperty), typeof(object)),
                argument).Compile();
            return new WeightedReader { Weight = weight, Value = value };
        }

        // ---------------------------------------------------------------
        // Building the value the element writes.
        // ---------------------------------------------------------------

        private static Func<object, IAspectPropertyValue> MakeValueFactory(
            DerivedValueType type)
        {
            switch (type)
            {
                case DerivedValueType.Bool:
                    return v => new AspectPropertyValue<bool>((bool)v);
                case DerivedValueType.Int:
                    return v => new AspectPropertyValue<int>((int)v);
                case DerivedValueType.Double:
                    return v => new AspectPropertyValue<double>(
                        Convert.ToDouble(v, CultureInfo.InvariantCulture));
                default:
                    return v => new AspectPropertyValue<string>(
                        Convert.ToString(v, CultureInfo.InvariantCulture));
            }
        }

        private static Func<string, IAspectPropertyValue> MakeNoValueFactory(
            DerivedValueType type)
        {
            switch (type)
            {
                case DerivedValueType.Bool:
                    return m => new AspectPropertyValue<bool>
                    {
                        NoValueMessage = m
                    };
                case DerivedValueType.Int:
                    return m => new AspectPropertyValue<int>
                    {
                        NoValueMessage = m
                    };
                case DerivedValueType.Double:
                    return m => new AspectPropertyValue<double>
                    {
                        NoValueMessage = m
                    };
                default:
                    return m => new AspectPropertyValue<string>
                    {
                        NoValueMessage = m
                    };
            }
        }
    }
}
