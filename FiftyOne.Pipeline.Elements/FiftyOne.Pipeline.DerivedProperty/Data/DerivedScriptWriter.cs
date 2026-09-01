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

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace FiftyOne.Pipeline.DerivedProperty.Data
{
    /// <summary>
    /// Prints a validated script as canonical JSON. The element writes the
    /// result to its debug log at build, so that anyone holding the log can
    /// reconstruct exactly what was evaluated without the script file.
    ///
    /// The text produced here must match the JavaScript reference at
    /// tools/canonical.mjs of the derived-properties repository character
    /// for character, because both are quoted in documentation and because
    /// matching text is how the two implementations are shown to build one
    /// model. A YAML script and the JSON script that mirrors it therefore
    /// print the same text.
    ///
    /// The form is PascalCase keys in the order of the format reference,
    /// two space indent, no trailing newline, and literal types kept as they
    /// were written, so false prints as false rather than as "false".
    /// </summary>
    public static class DerivedScriptWriter
    {
        /// <summary>
        /// Print a script as canonical JSON.
        /// </summary>
        /// <param name="script">The script, after validation.</param>
        /// <returns>The canonical JSON, without a trailing newline.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown where no script is given.
        /// </exception>
        public static string ToCanonicalJson(DerivedScript script)
        {
            if (script == null)
            {
                throw new ArgumentNullException(nameof(script));
            }
            var text = new StringBuilder();
            Print(Build(script), text, 0);
            return text.ToString();
        }

        // ---------------------------------------------------------------
        // The canonical form as a tree, before it is printed.
        // ---------------------------------------------------------------

        private static JsonObject Build(DerivedScript script)
        {
            var result = new JsonObject();
            result.AddValue("Format", script.Format);
            result.AddValue("Name", script.Name);
            result.AddValue("Version", script.Version);
            if (script.Deprecated)
            {
                result.AddValue("Deprecated", true);
                result.AddValue("DeprecationNote", script.DeprecationNote);
            }
            result.Add("Output", Output(script.Output));

            var optional = new JsonArray();
            foreach (var property in script.Properties)
            {
                if (property.Required == false)
                {
                    optional.Add(new JsonLiteral(property.Name));
                }
            }
            if (optional.Items.Count > 0)
            {
                result.Add("Optional", optional);
            }

            // The inferred type of every source property, which the script
            // file does not carry because the type is worked out from the
            // literals the property is compared against.
            var properties = new JsonObject();
            foreach (var property in script.Properties)
            {
                var entry = new JsonObject();
                entry.AddValue(
                    "Type",
                    property.ValueType.HasValue
                        ? DerivedValueConverter.NameOf(property.ValueType.Value)
                        : null);
                entry.AddValue("Required", property.Required);
                properties.Add(property.Name, entry);
            }
            result.Add("Properties", properties);

            if (script.Checks.Count > 0)
            {
                var checks = new JsonObject();
                foreach (var check in script.Checks)
                {
                    checks.Add(check.Name, Condition(check.Condition, script));
                }
                result.Add("Checks", checks);
            }

            var rules = new JsonArray();
            foreach (var rule in script.Rules)
            {
                var entry = new JsonObject();
                if (rule.IsElse)
                {
                    entry.Add("Else", Value(rule.Value, script));
                }
                else
                {
                    entry.Add("When", Condition(rule.Condition, script));
                    entry.Add("Then", Value(rule.Value, script));
                }
                rules.Add(entry);
            }
            result.Add("Rules", rules);

            return result;
        }

        /// <summary>
        /// The output block, in the order of the format reference, leaving
        /// out every field the script did not give and printing Values last.
        /// </summary>
        private static JsonObject Output(DerivedPropertyMetaData output)
        {
            var result = new JsonObject();
            AddText(result, "Name", output.Name);
            AddText(result, "Description", output.Description);
            result.AddValue(
                "ValueType", DerivedValueConverter.NameOf(output.ValueType));
            AddText(result, "StoredValueType", output.StoredValueType);
            AddText(result, "DefaultValue", output.DefaultValue);
            result.AddValue("IsList", output.IsList);
            AddFlag(result, "IsMandatory", output.IsMandatory);
            AddFlag(result, "IsObsolete", output.IsObsolete);
            AddText(result, "Category", output.Category);
            AddFlag(result, "IsPopular", output.IsPopular);
            AddFlag(result, "ExportValues", output.ExportValues);
            AddText(result, "Url", output.Url);
            AddNumber(result, "DisplayOrder", output.DisplayOrder);
            AddNumber(result, "PropertyId", output.PropertyId);
            AddList(result, "VendorIds", output.VendorIds);
            AddList(result, "Dependencies", output.Dependencies);
            if (output.Values != null)
            {
                var values = new JsonArray();
                foreach (var value in output.Values)
                {
                    var entry = new JsonObject();
                    entry.AddValue("Name", value.Name);
                    if (value.Description != null)
                    {
                        entry.AddValue("Description", value.Description);
                    }
                    values.Add(entry);
                }
                result.Add("Values", values);
            }
            return result;
        }

        private static void AddText(
            JsonObject target,
            string name,
            string value)
        {
            if (value != null)
            {
                target.AddValue(name, value);
            }
        }

        private static void AddFlag(
            JsonObject target,
            string name,
            bool? value)
        {
            if (value.HasValue)
            {
                target.AddValue(name, value.Value);
            }
        }

        private static void AddNumber(
            JsonObject target,
            string name,
            int? value)
        {
            if (value.HasValue)
            {
                target.AddValue(name, value.Value);
            }
        }

        private static void AddList(
            JsonObject target,
            string name,
            IReadOnlyList<string> values)
        {
            if (values == null)
            {
                return;
            }
            var list = new JsonArray();
            foreach (var value in values)
            {
                list.Add(new JsonLiteral(value));
            }
            target.Add(name, list);
        }

        // ---------------------------------------------------------------
        // Conditions and rule values, printed back in the shape the script
        // wrote them.
        // ---------------------------------------------------------------

        private static JsonNode Condition(
            DerivedCondition condition,
            DerivedScript script)
        {
            if (condition is DerivedComparison comparison)
            {
                return Comparison(comparison, script);
            }
            if (condition is DerivedPresence presence)
            {
                var result = new JsonObject();
                result.AddValue(
                    "Property", script.Properties[presence.Slot].Name);
                result.AddValue("Present", presence.Expected);
                return result;
            }
            if (condition is DerivedCheckReference reference)
            {
                var result = new JsonObject();
                result.AddValue(
                    "Check", script.Checks[reference.Index].Name);
                return result;
            }
            if (condition is DerivedAggregateComparison aggregate)
            {
                return Aggregate(aggregate, script);
            }
            if (condition is DerivedAll all)
            {
                var result = new JsonObject();
                result.Add("All", Items(all.Items, script));
                return result;
            }
            if (condition is DerivedAny any)
            {
                var result = new JsonObject();
                result.Add("Any", Items(any.Items, script));
                return result;
            }
            if (condition is DerivedNot not)
            {
                var result = new JsonObject();
                result.Add("Not", Condition(not.Item, script));
                return result;
            }
            throw new NotSupportedException(string.Format(
                CultureInfo.InvariantCulture,
                "a condition of type '{0}' has no canonical form",
                condition == null ? "nothing" : condition.GetType().Name));
        }

        private static JsonArray Items(
            IReadOnlyList<DerivedCondition> items,
            DerivedScript script)
        {
            var result = new JsonArray();
            foreach (var item in items)
            {
                result.Add(Condition(item, script));
            }
            return result;
        }

        private static JsonObject Comparison(
            DerivedComparison comparison,
            DerivedScript script)
        {
            var result = new JsonObject();
            result.AddValue(
                "Property", script.Properties[comparison.Slot].Name);
            var op = NameOf(comparison.Operator);
            var members = comparison.OperandList;
            if (members == null)
            {
                result.AddValue(op, comparison.Operand);
                return result;
            }
            var list = new JsonArray();
            foreach (var member in members)
            {
                list.Add(new JsonLiteral(member));
            }
            result.Add(op, list);
            return result;
        }

        private static JsonObject Aggregate(
            DerivedAggregateComparison aggregate,
            DerivedScript script)
        {
            var left = aggregate.Left;
            var right = aggregate.Right;
            var op = NameOf(aggregate.Operator);

            var result = new JsonObject();
            result.Add(NameOf(left.Aggregate), Group(left.Group, script));
            if (right == null)
            {
                result.AddValue(op, aggregate.Operand);
                return result;
            }
            var nested = new JsonObject();
            nested.Add(NameOf(right.Aggregate), Group(right.Group, script));
            result.Add(op, nested);
            return result;
        }

        /// <summary>
        /// A group is the word Checks where it covers every check, and the
        /// list of check names otherwise.
        /// </summary>
        private static JsonNode Group(
            IReadOnlyList<int> group,
            DerivedScript script)
        {
            if (group == null)
            {
                return new JsonLiteral("Checks");
            }
            var result = new JsonArray();
            foreach (var index in group)
            {
                result.Add(new JsonLiteral(script.Checks[index].Name));
            }
            return result;
        }

        private static JsonNode Value(
            DerivedRuleValue value,
            DerivedScript script)
        {
            if (value.IsAggregate)
            {
                var result = new JsonObject();
                result.Add(
                    NameOf(value.Aggregate.Value),
                    Group(value.Group, script));
                return result;
            }
            return new JsonLiteral(value.Literal);
        }

        private static string NameOf(DerivedOperator op)
        {
            switch (op)
            {
                case DerivedOperator.Eq: return "Eq";
                case DerivedOperator.Ne: return "Ne";
                case DerivedOperator.Gt: return "Gt";
                case DerivedOperator.Ge: return "Ge";
                case DerivedOperator.Lt: return "Lt";
                case DerivedOperator.Le: return "Le";
                case DerivedOperator.In: return "In";
                case DerivedOperator.NotIn: return "NotIn";
                case DerivedOperator.StartsWith: return "StartsWith";
                case DerivedOperator.EndsWith: return "EndsWith";
                case DerivedOperator.Contains: return "Contains";
                default: return "Present";
            }
        }

        private static string NameOf(DerivedAggregate aggregate)
        {
            switch (aggregate)
            {
                case DerivedAggregate.Passed: return "Passed";
                case DerivedAggregate.Failed: return "Failed";
                default: return "Evaluated";
            }
        }

        // ---------------------------------------------------------------
        // The tree.
        // ---------------------------------------------------------------

        private abstract class JsonNode
        {
        }

        private sealed class JsonLiteral : JsonNode
        {
            public JsonLiteral(object value)
            {
                Value = value;
            }

            public object Value { get; }
        }

        private sealed class JsonObject : JsonNode
        {
            private readonly List<KeyValuePair<string, JsonNode>> _members =
                new List<KeyValuePair<string, JsonNode>>();

            public IReadOnlyList<KeyValuePair<string, JsonNode>> Members =>
                _members;

            public void Add(string name, JsonNode value)
            {
                _members.Add(
                    new KeyValuePair<string, JsonNode>(name, value));
            }

            public void AddValue(string name, object value)
            {
                Add(name, new JsonLiteral(value));
            }
        }

        private sealed class JsonArray : JsonNode
        {
            private readonly List<JsonNode> _items = new List<JsonNode>();

            public IReadOnlyList<JsonNode> Items => _items;

            public void Add(JsonNode item)
            {
                _items.Add(item);
            }
        }

        // ---------------------------------------------------------------
        // Printing, following JSON.stringify(value, null, 2).
        // ---------------------------------------------------------------

        private static void Print(JsonNode node, StringBuilder text, int depth)
        {
            if (node is JsonObject mapping)
            {
                PrintObject(mapping, text, depth);
                return;
            }
            if (node is JsonArray list)
            {
                PrintArray(list, text, depth);
                return;
            }
            PrintLiteral(((JsonLiteral)node).Value, text);
        }

        private static void PrintObject(
            JsonObject value,
            StringBuilder text,
            int depth)
        {
            if (value.Members.Count == 0)
            {
                text.Append("{}");
                return;
            }
            text.Append('{');
            for (var i = 0; i < value.Members.Count; i++)
            {
                if (i > 0)
                {
                    text.Append(',');
                }
                text.Append('\n');
                Indent(text, depth + 1);
                PrintText(value.Members[i].Key, text);
                text.Append(": ");
                Print(value.Members[i].Value, text, depth + 1);
            }
            text.Append('\n');
            Indent(text, depth);
            text.Append('}');
        }

        private static void PrintArray(
            JsonArray value,
            StringBuilder text,
            int depth)
        {
            if (value.Items.Count == 0)
            {
                text.Append("[]");
                return;
            }
            text.Append('[');
            for (var i = 0; i < value.Items.Count; i++)
            {
                if (i > 0)
                {
                    text.Append(',');
                }
                text.Append('\n');
                Indent(text, depth + 1);
                Print(value.Items[i], text, depth + 1);
            }
            text.Append('\n');
            Indent(text, depth);
            text.Append(']');
        }

        private static void Indent(StringBuilder text, int depth)
        {
            for (var i = 0; i < depth; i++)
            {
                text.Append("  ");
            }
        }

        private static void PrintLiteral(object value, StringBuilder text)
        {
            if (value == null)
            {
                text.Append("null");
                return;
            }
            if (value is bool flag)
            {
                text.Append(flag ? "true" : "false");
                return;
            }
            if (value is string words)
            {
                PrintText(words, text);
                return;
            }
            if (value is int whole)
            {
                text.Append(whole.ToString(CultureInfo.InvariantCulture));
                return;
            }
            if (value is double number)
            {
                text.Append(PrintNumber(number));
                return;
            }
            // Nothing else reaches the writer from a validated script, so a
            // value of any other type is printed as text rather than
            // silently dropped.
            PrintText(
                Convert.ToString(value, CultureInfo.InvariantCulture), text);
        }

        /// <summary>
        /// Writes a string the way JSON requires. Non ASCII characters are
        /// not escaped, because JSON.stringify does not escape them either,
        /// and a surrogate without its pair is written as an escape, which
        /// is what JSON.stringify does to keep its output well formed.
        /// </summary>
        private static void PrintText(string value, StringBuilder text)
        {
            text.Append('"');
            for (var i = 0; i < value.Length; i++)
            {
                var character = value[i];
                switch (character)
                {
                    case '"': text.Append("\\\""); continue;
                    case '\\': text.Append("\\\\"); continue;
                    case '\b': text.Append("\\b"); continue;
                    case '\f': text.Append("\\f"); continue;
                    case '\n': text.Append("\\n"); continue;
                    case '\r': text.Append("\\r"); continue;
                    case '\t': text.Append("\\t"); continue;
                    default: break;
                }
                if (character < ' ')
                {
                    PrintEscape(character, text);
                    continue;
                }
                if (char.IsHighSurrogate(character))
                {
                    var paired = i + 1 < value.Length &&
                        char.IsLowSurrogate(value[i + 1]);
                    if (paired)
                    {
                        text.Append(character);
                        text.Append(value[i + 1]);
                        i++;
                        continue;
                    }
                    PrintEscape(character, text);
                    continue;
                }
                if (char.IsLowSurrogate(character))
                {
                    PrintEscape(character, text);
                    continue;
                }
                text.Append(character);
            }
            text.Append('"');
        }

        private static void PrintEscape(char character, StringBuilder text)
        {
            text.Append("\\u");
            text.Append(((int)character)
                .ToString("x4", CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Writes a number the way JavaScript writes one, so a whole valued
        /// double prints with no decimal point and no trailing zero and 2.0
        /// prints as 2. The rule is the one in the JavaScript language
        /// standard, where a number is written in plain form while its
        /// decimal exponent sits between -6 and 21 and in exponent form
        /// outside that range.
        /// </summary>
        private static string PrintNumber(double value)
        {
            // JSON has no way to write these, and JSON.stringify writes null
            // in their place.
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                return "null";
            }
            if (value == 0)
            {
                // JavaScript writes minus zero as zero.
                return "0";
            }

            var negative = value < 0;
            Digits(
                Math.Abs(value).ToString("R", CultureInfo.InvariantCulture),
                out var digits,
                out var exponent);

            var text = new StringBuilder();
            if (negative)
            {
                text.Append('-');
            }
            if (exponent >= digits.Length && exponent <= 21)
            {
                text.Append(digits);
                text.Append('0', exponent - digits.Length);
            }
            else if (exponent > 0 && exponent <= 21)
            {
                text.Append(digits, 0, exponent);
                text.Append('.');
                text.Append(digits, exponent, digits.Length - exponent);
            }
            else if (exponent > -6 && exponent <= 0)
            {
                text.Append("0.");
                text.Append('0', -exponent);
                text.Append(digits);
            }
            else
            {
                text.Append(digits[0]);
                if (digits.Length > 1)
                {
                    text.Append('.');
                    text.Append(digits, 1, digits.Length - 1);
                }
                text.Append(exponent - 1 >= 0 ? "e+" : "e-");
                text.Append(Math.Abs(exponent - 1)
                    .ToString(CultureInfo.InvariantCulture));
            }
            return text.ToString();
        }

        /// <summary>
        /// Splits the shortest round trip form of a number into its
        /// significant digits and the decimal exponent, so that the value is
        /// 0.digits multiplied by ten to the power of the exponent. The input
        /// has no sign and is either plain, such as 12.5, or in the exponent
        /// form .NET writes, such as 1.25E+05.
        /// </summary>
        private static void Digits(
            string value,
            out string digits,
            out int exponent)
        {
            var mantissa = value;
            var shift = 0;
            var marker = value.IndexOf('E');
            if (marker < 0)
            {
                marker = value.IndexOf('e');
            }
            if (marker >= 0)
            {
                mantissa = value.Substring(0, marker);
                shift = int.Parse(
                    value.Substring(marker + 1),
                    NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture);
            }

            var point = mantissa.IndexOf('.');
            var whole = point < 0 ? mantissa : mantissa.Substring(0, point);
            var fraction = point < 0
                ? string.Empty
                : mantissa.Substring(point + 1);

            var all = whole + fraction;
            exponent = whole.Length + shift;

            var first = 0;
            while (first < all.Length && all[first] == '0')
            {
                first++;
                exponent--;
            }
            var last = all.Length;
            while (last > first && all[last - 1] == '0')
            {
                last--;
            }
            digits = first == last
                ? "0"
                : all.Substring(first, last - first);
        }
    }
}
