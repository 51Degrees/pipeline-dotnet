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
using System.Linq;
using System.Text.RegularExpressions;

namespace FiftyOne.Pipeline.DerivedProperty.Data
{
    /// <summary>
    /// What validating one script produced, being either a script or a list
    /// of faults, never both.
    /// </summary>
    public sealed class DerivedScriptValidationResult
    {
        /// <summary>
        /// Create a new instance.
        /// </summary>
        /// <param name="script">The script, where it validated.</param>
        /// <param name="faults">The faults, where it did not.</param>
        public DerivedScriptValidationResult(
            DerivedScript script,
            IReadOnlyList<DerivedScriptFault> faults)
        {
            Script = script;
            Faults = faults ?? new List<DerivedScriptFault>();
        }

        /// <summary>
        /// The script, or null where the script did not validate.
        /// </summary>
        public DerivedScript Script { get; }

        /// <summary>
        /// Every fault found, empty where the script validated.
        /// </summary>
        public IReadOnlyList<DerivedScriptFault> Faults { get; }

        /// <summary>True where the script validated.</summary>
        public bool IsValid => Script != null;
    }

    /// <summary>
    /// Checks a parsed script against format 1 and turns it into a
    /// <see cref="DerivedScript"/>. Every fault is collected rather than
    /// stopping at the first.
    ///
    /// The faults here have to match the JavaScript reference validator in
    /// the derived-properties repository, because the rejection cases under
    /// that repository's tests folder name the path and a fragment of the
    /// message, and every language runs the same cases.
    /// </summary>
    public static class DerivedScriptValidator
    {
        private static readonly Regex _identifier = new Regex(
            "^[A-Za-z][A-Za-z0-9]*$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly Regex _sourceProperty = new Regex(
            "^[A-Za-z][A-Za-z0-9]*\\.[A-Za-z][A-Za-z0-9]*$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly Regex _semanticVersion = new Regex(
            "^\\d+\\.\\d+\\.\\d+(?:-[0-9A-Za-z.-]+)?(?:\\+[0-9A-Za-z.-]+)?$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly string[] _topLevelKeys =
        {
            "Format", "Name", "Version", "Deprecated", "DeprecationNote",
            "Output", "Checks", "Rules"
        };

        private static readonly string[] _outputKeys =
        {
            "Name", "Description", "ValueType", "StoredValueType",
            "DefaultValue", "IsList", "IsMandatory", "IsObsolete",
            "Category", "IsPopular", "ExportValues", "Url", "DisplayOrder",
            "PropertyId", "VendorIds", "Dependencies", "Values"
        };

        private static readonly string[] _operatorNames = Enum
            .GetNames(typeof(DerivedOperator));

        private static readonly string[] _aggregateNames = Enum
            .GetNames(typeof(DerivedAggregate));

        /// <summary>
        /// Read and check the text of a script.
        /// </summary>
        /// <param name="text">The script as YAML or as JSON.</param>
        /// <param name="name">
        /// The file name without its extension, which the script's Name must
        /// equal. Null where the script did not come from a file.
        /// </param>
        /// <param name="source">
        /// Where the script came from, for the fault messages.
        /// </param>
        /// <returns>The script, or the faults.</returns>
        public static DerivedScriptValidationResult Validate(
            string text,
            string name,
            string source)
        {
            var actualSource = source ?? "code";
            DerivedMapping document;
            try
            {
                document = DerivedScriptParser.Parse(text);
            }
            catch (DerivedScriptParseException exception)
            {
                return new DerivedScriptValidationResult(
                    null,
                    new List<DerivedScriptFault>
                    {
                        new DerivedScriptFault(
                            name,
                            actualSource,
                            string.Empty,
                            exception.Line,
                            exception.Message)
                    });
            }

            var context = new Context(name, actualSource);
            var script = Build(context, document);
            return context.Faults.Count > 0
                ? new DerivedScriptValidationResult(null, context.Faults)
                : new DerivedScriptValidationResult(script, context.Faults);
        }

        // ---------------------------------------------------------------
        // The working state for one script.
        // ---------------------------------------------------------------

        private sealed class Context
        {
            public Context(string script, string source)
            {
                Script = script;
                Source = source;
                Faults = new List<DerivedScriptFault>();
                Properties = new Dictionary<string, PropertyUse>(
                    StringComparer.OrdinalIgnoreCase);
                Order = new List<PropertyUse>();
                CheckNames = new List<string>();
            }

            public string Script { get; }
            public string Source { get; }
            public List<DerivedScriptFault> Faults { get; }
            public Dictionary<string, PropertyUse> Properties { get; }
            public List<PropertyUse> Order { get; }
            public List<string> CheckNames { get; }

            public void Fault(string path, DerivedNode node, string message)
            {
                Faults.Add(new DerivedScriptFault(
                    Script,
                    Source,
                    path,
                    node == null ? 0 : node.Line,
                    message));
            }
        }

        private sealed class PropertyUse
        {
            public string Name { get; set; }
            public DerivedValueType ValueType { get; set; }
            public string TypePath { get; set; }
            public int Slot { get; set; }
        }

        // ---------------------------------------------------------------

        private static DerivedScript Build(
            Context context,
            DerivedMapping document)
        {
            foreach (var key in UnknownKeys(document, _topLevelKeys))
            {
                context.Fault(key, document, string.Format(
                    CultureInfo.InvariantCulture,
                    "unknown key '{0}' at the top level. Expected one of {1}",
                    key,
                    string.Join(", ", _topLevelKeys)));
            }

            var format = ReadFormat(context, document);
            var name = ReadName(context, document);
            var version = ReadVersion(context, document);
            ReadDeprecation(context, document,
                out var deprecated, out var deprecationNote);
            var output = ReadOutput(context, document);

            // Checks are read before rules so that a rule can name one.
            ReadCheckNames(context, document);
            var checks = ReadChecks(context, document);
            var rules = ReadRules(context, document, output);

            var properties = context.Order
                .Select(p => MakeSourceProperty(p))
                .ToList();

            if (output.Dependencies == null)
            {
                output = WithDependencies(
                    output, properties.Select(p => p.Name).ToList());
            }

            return new DerivedScript(
                format,
                name,
                version,
                deprecated,
                deprecationNote,
                context.Source,
                output,
                properties,
                checks,
                rules);
        }

        private static DerivedSourceProperty MakeSourceProperty(
            PropertyUse use)
        {
            var dot = use.Name.IndexOf('.');
            return new DerivedSourceProperty(
                use.Name,
                use.Name.Substring(0, dot),
                use.Name.Substring(dot + 1),
                use.ValueType);
        }

        private static DerivedPropertyMetaData WithDependencies(
            DerivedPropertyMetaData output,
            IReadOnlyList<string> dependencies)
        {
            return new DerivedPropertyMetaData(
                output.Name,
                output.Description,
                output.ValueType,
                output.IsList,
                output.DefaultValue,
                output.Values,
                output.Category,
                output.IsMandatory,
                output.IsObsolete,
                output.IsPopular,
                output.ExportValues,
                output.Url,
                output.DisplayOrder,
                output.PropertyId,
                output.StoredValueType,
                output.VendorIds,
                dependencies,
                output.ElementDataKey);
        }

        private static IEnumerable<string> UnknownKeys(
            DerivedMapping mapping,
            string[] allowed)
        {
            return mapping.Names.Where(
                n => allowed.Any(
                    a => string.Equals(
                        a, n, StringComparison.OrdinalIgnoreCase)) == false);
        }

        // ---------------------------------------------------------------
        // Top level.
        // ---------------------------------------------------------------

        private static int ReadFormat(Context context, DerivedMapping document)
        {
            var node = document.Get("Format");
            if (node == null)
            {
                context.Fault("Format", document,
                    "required key 'Format' is missing");
                return 0;
            }
            var scalar = node as DerivedScalar;
            if (scalar == null || !(scalar.Value is int value))
            {
                context.Fault("Format", node, string.Format(
                    CultureInfo.InvariantCulture,
                    "Format must be 1, found {0}", Describe(node)));
                return 0;
            }
            if (value != 1)
            {
                context.Fault("Format", node, string.Format(
                    CultureInfo.InvariantCulture,
                    "Format must be 1, found {0}",
                    value.ToString(CultureInfo.InvariantCulture)));
                return 0;
            }
            return 1;
        }

        private static string ReadName(Context context, DerivedMapping document)
        {
            var node = document.Get("Name");
            if (node == null)
            {
                context.Fault("Name", document,
                    "required key 'Name' is missing");
                return context.Script;
            }
            if (!(node is DerivedScalar scalar) ||
                !(scalar.Value is string value))
            {
                context.Fault("Name", node, string.Format(
                    CultureInfo.InvariantCulture,
                    "Name expected a string, found {0}", Describe(node)));
                return context.Script;
            }
            if (_identifier.IsMatch(value) == false)
            {
                context.Fault("Name", node, string.Format(
                    CultureInfo.InvariantCulture,
                    "script name '{0}' does not match the pattern {1}",
                    value, _identifier.ToString()));
                return value;
            }
            if (context.Script != null &&
                string.Equals(
                    context.Script, value, StringComparison.Ordinal) == false)
            {
                context.Fault("Name", node, string.Format(
                    CultureInfo.InvariantCulture,
                    "script name '{0}' must equal the file name '{1}'",
                    value, context.Script));
            }
            return value;
        }

        private static string ReadVersion(
            Context context,
            DerivedMapping document)
        {
            var node = document.Get("Version");
            if (node == null)
            {
                context.Fault("Version", document,
                    "required key 'Version' is missing");
                return null;
            }
            if (!(node is DerivedScalar scalar) ||
                !(scalar.Value is string value) ||
                _semanticVersion.IsMatch(value) == false)
            {
                context.Fault("Version", node, string.Format(
                    CultureInfo.InvariantCulture,
                    "Version expected a semantic version such as 1.0.0, " +
                    "found {0}",
                    Describe(node)));
                return null;
            }
            return value;
        }

        private static void ReadDeprecation(
            Context context,
            DerivedMapping document,
            out bool deprecated,
            out string deprecationNote)
        {
            deprecated = false;
            deprecationNote = null;

            var flag = document.Get("Deprecated");
            if (flag != null)
            {
                if (flag is DerivedScalar scalar && scalar.Value is bool value)
                {
                    deprecated = value;
                }
                else
                {
                    context.Fault("Deprecated", flag, string.Format(
                        CultureInfo.InvariantCulture,
                        "Deprecated expected a boolean, found {0}",
                        Describe(flag)));
                }
            }

            var note = document.Get("DeprecationNote");
            if (note != null)
            {
                if (note is DerivedScalar scalar && scalar.Value is string text)
                {
                    deprecationNote = text;
                }
                else
                {
                    context.Fault("DeprecationNote", note, string.Format(
                        CultureInfo.InvariantCulture,
                        "DeprecationNote expected a string, found {0}",
                        Describe(note)));
                }
            }

            if (deprecated && string.IsNullOrEmpty(deprecationNote))
            {
                context.Fault("DeprecationNote", document,
                    "a deprecated script must say what to use instead in " +
                    "DeprecationNote");
            }
            if (deprecated == false && deprecationNote != null)
            {
                context.Fault("DeprecationNote", note,
                    "DeprecationNote is only allowed when Deprecated is true");
            }
        }

        // ---------------------------------------------------------------
        // Output.
        // ---------------------------------------------------------------

        private static DerivedPropertyMetaData ReadOutput(
            Context context,
            DerivedMapping document)
        {
            var node = document.Get("Output");
            if (node == null)
            {
                context.Fault("Output", document,
                    "required key 'Output' is missing");
                return Fallback(context);
            }
            if (!(node is DerivedMapping mapping))
            {
                context.Fault("Output", node, string.Format(
                    CultureInfo.InvariantCulture,
                    "Output expected a mapping, found {0}", Describe(node)));
                return Fallback(context);
            }

            foreach (var key in UnknownKeys(mapping, _outputKeys))
            {
                context.Fault("Output." + key, mapping, string.Format(
                    CultureInfo.InvariantCulture,
                    "unknown key '{0}' under Output. A typo in a metadata " +
                    "field is a fault rather than a value that is quietly " +
                    "dropped",
                    key));
            }

            var name = ReadOutputName(context, mapping, out var elementDataKey);
            var description = ReadOutputDescription(context, mapping);
            var valueType = ReadValueType(context, mapping, out var readType);
            var isList = ReadIsList(context, mapping);
            var values = ReadValues(context, mapping, readType, valueType);
            var defaultValue = ReadDefaultValue(
                context, mapping, readType, valueType, values);

            return new DerivedPropertyMetaData(
                name,
                description,
                valueType,
                isList,
                defaultValue,
                values,
                ReadOptionalString(context, mapping, "Category"),
                ReadOptionalBool(context, mapping, "IsMandatory"),
                ReadOptionalBool(context, mapping, "IsObsolete"),
                ReadOptionalBool(context, mapping, "IsPopular"),
                ReadOptionalBool(context, mapping, "ExportValues"),
                ReadOptionalString(context, mapping, "Url"),
                ReadOptionalInt(context, mapping, "DisplayOrder"),
                ReadOptionalInt(context, mapping, "PropertyId"),
                ReadOptionalString(context, mapping, "StoredValueType"),
                ReadOptionalStringList(context, mapping, "VendorIds"),
                ReadOptionalStringList(context, mapping, "Dependencies"),
                elementDataKey);
        }

        private static DerivedPropertyMetaData Fallback(Context context)
        {
            return new DerivedPropertyMetaData(
                context.Script ?? "unknown",
                string.Empty,
                DerivedValueType.String,
                false);
        }

        /// <summary>
        /// Reads Output.Name, which may carry an element data key as a
        /// prefix. A bare name such as `HumanConfidence` creates a property
        /// in this element's own data. A prefixed name such as
        /// `device.IsCrawler` names a property another element already
        /// produces, and the script replaces its value.
        /// </summary>
        private static string ReadOutputName(
            Context context,
            DerivedMapping mapping,
            out string elementDataKey)
        {
            elementDataKey = null;
            var node = mapping.Get("Name");
            if (node == null)
            {
                context.Fault("Output.Name", mapping,
                    "required key 'Name' is missing");
                return context.Script ?? "unknown";
            }
            if (node is DerivedScalar scalar &&
                scalar.Value is string value)
            {
                if (_identifier.IsMatch(value))
                {
                    return value;
                }
                if (_sourceProperty.IsMatch(value))
                {
                    var stop = value.IndexOf('.');
                    elementDataKey = value.Substring(0, stop);
                    return value.Substring(stop + 1);
                }
            }
            context.Fault("Output.Name", node, string.Format(
                CultureInfo.InvariantCulture,
                "Output.Name {0} is neither a property name matching {1} " +
                "nor a property in another element written as " +
                "elementDataKey.PropertyName, matching {2}",
                Describe(node),
                _identifier.ToString(),
                _sourceProperty.ToString()));
            return context.Script ?? "unknown";
        }

        private static string ReadOutputDescription(
            Context context,
            DerivedMapping mapping)
        {
            var node = mapping.Get("Description");
            if (node == null)
            {
                context.Fault("Output.Description", mapping,
                    "required key 'Description' is missing. Say what the " +
                    "property asserts, not how far to trust it");
                return string.Empty;
            }
            if (node is DerivedScalar scalar &&
                scalar.Value is string value &&
                string.IsNullOrWhiteSpace(value) == false)
            {
                return value;
            }
            context.Fault("Output.Description", node, string.Format(
                CultureInfo.InvariantCulture,
                "Output.Description expected a non empty string, found {0}",
                Describe(node)));
            return string.Empty;
        }

        private static DerivedValueType ReadValueType(
            Context context,
            DerivedMapping mapping,
            out bool read)
        {
            read = false;
            var node = mapping.Get("ValueType");
            if (node == null)
            {
                context.Fault("Output.ValueType", mapping,
                    "required key 'ValueType' is missing");
                return DerivedValueType.String;
            }
            if (node is DerivedScalar scalar && scalar.Value is string value)
            {
                if (string.Equals(value, "string",
                    StringComparison.OrdinalIgnoreCase))
                {
                    read = true;
                    return DerivedValueType.String;
                }
                if (string.Equals(value, "bool",
                    StringComparison.OrdinalIgnoreCase))
                {
                    read = true;
                    return DerivedValueType.Bool;
                }
                if (string.Equals(value, "int",
                    StringComparison.OrdinalIgnoreCase))
                {
                    read = true;
                    return DerivedValueType.Int;
                }
                if (string.Equals(value, "double",
                    StringComparison.OrdinalIgnoreCase))
                {
                    read = true;
                    return DerivedValueType.Double;
                }
                context.Fault("Output.ValueType", node, string.Format(
                    CultureInfo.InvariantCulture,
                    "Output.ValueType '{0}' is not allowed in format 1. " +
                    "Expected one of string, bool, int, double",
                    value));
                return DerivedValueType.String;
            }
            context.Fault("Output.ValueType", node, string.Format(
                CultureInfo.InvariantCulture,
                "Output.ValueType expected a string, found {0}",
                Describe(node)));
            return DerivedValueType.String;
        }

        private static bool ReadIsList(Context context, DerivedMapping mapping)
        {
            var node = mapping.Get("IsList");
            if (node == null)
            {
                context.Fault("Output.IsList", mapping,
                    "required key 'IsList' is missing");
                return false;
            }
            if (!(node is DerivedScalar scalar) || !(scalar.Value is bool value))
            {
                context.Fault("Output.IsList", node, string.Format(
                    CultureInfo.InvariantCulture,
                    "Output.IsList expected a boolean, found {0}",
                    Describe(node)));
                return false;
            }
            if (value)
            {
                context.Fault("Output.IsList", node,
                    "Output.IsList must be false in format 1. List outputs " +
                    "are deferred");
            }
            return false;
        }

        private static IReadOnlyList<DerivedValueMetaData> ReadValues(
            Context context,
            DerivedMapping mapping,
            bool readType,
            DerivedValueType valueType)
        {
            var node = mapping.Get("Values");
            if (node == null)
            {
                return null;
            }
            if (!(node is DerivedSequence sequence))
            {
                context.Fault("Output.Values", node, string.Format(
                    CultureInfo.InvariantCulture,
                    "Output.Values expected a list, found {0}",
                    Describe(node)));
                return null;
            }
            if (readType &&
                valueType != DerivedValueType.String &&
                valueType != DerivedValueType.Int)
            {
                context.Fault("Output.Values", node, string.Format(
                    CultureInfo.InvariantCulture,
                    "Output.Values is only allowed where ValueType is " +
                    "string or int, not {0}",
                    DerivedValueConverter.NameOf(valueType)));
                return null;
            }

            var result = new List<DerivedValueMetaData>();
            for (var i = 0; i < sequence.Items.Count; i++)
            {
                var path = string.Format(
                    CultureInfo.InvariantCulture, "Output.Values[{0}]", i);
                var item = sequence.Items[i];
                if (!(item is DerivedMapping entry))
                {
                    context.Fault(path, item, string.Format(
                        CultureInfo.InvariantCulture,
                        "a value expected a mapping of Name and " +
                        "Description, found {0}",
                        Describe(item)));
                    continue;
                }
                foreach (var key in UnknownKeys(
                    entry, new[] { "Name", "Description" }))
                {
                    context.Fault(path + "." + key, entry, string.Format(
                        CultureInfo.InvariantCulture,
                        "unknown key '{0}' in a value. Expected Name and " +
                        "Description",
                        key));
                }
                var nameNode = entry.Get("Name");
                if (!(nameNode is DerivedScalar nameScalar) ||
                    nameScalar.Value == null ||
                    (!(nameScalar.Value is string) &&
                        !(nameScalar.Value is int)))
                {
                    context.Fault(path + ".Name", entry,
                        "a value must have a Name");
                    continue;
                }
                string description = null;
                var descriptionNode = entry.Get("Description");
                if (descriptionNode != null)
                {
                    if (descriptionNode is DerivedScalar descriptionScalar &&
                        descriptionScalar.Value is string text)
                    {
                        description = text;
                    }
                    else
                    {
                        context.Fault(
                            path + ".Description", descriptionNode,
                            string.Format(
                                CultureInfo.InvariantCulture,
                                "a value Description expected a string, " +
                                "found {0}",
                                Describe(descriptionNode)));
                    }
                }
                result.Add(new DerivedValueMetaData(
                    Convert.ToString(
                        nameScalar.Value, CultureInfo.InvariantCulture),
                    description));
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in result)
            {
                if (seen.Add(entry.Name) == false)
                {
                    context.Fault("Output.Values", node, string.Format(
                        CultureInfo.InvariantCulture,
                        "the value '{0}' is listed more than once",
                        entry.Name));
                }
            }
            return result;
        }

        private static string ReadDefaultValue(
            Context context,
            DerivedMapping mapping,
            bool readType,
            DerivedValueType valueType,
            IReadOnlyList<DerivedValueMetaData> values)
        {
            var node = mapping.Get("DefaultValue");
            if (node == null)
            {
                return null;
            }
            if (!(node is DerivedScalar scalar) ||
                !(scalar.Value is string value))
            {
                context.Fault("Output.DefaultValue", node, string.Format(
                    CultureInfo.InvariantCulture,
                    "Output.DefaultValue expected a string holding the " +
                    "string form of the value, found {0}",
                    Describe(node)));
                return null;
            }
            if (readType && DerivedValueConverter.TryConvertString(
                value, valueType, out _) == false)
            {
                context.Fault("Output.DefaultValue", node, string.Format(
                    CultureInfo.InvariantCulture,
                    "Output.DefaultValue '{0}' cannot be read as {1}",
                    value, DerivedValueConverter.NameOf(valueType)));
            }
            if (values != null && values.Any(
                v => string.Equals(
                    v.Name, value, StringComparison.Ordinal)) == false)
            {
                context.Fault("Output.DefaultValue", node, string.Format(
                    CultureInfo.InvariantCulture,
                    "Output.DefaultValue '{0}' is not one of the values " +
                    "listed under Output.Values",
                    value));
            }
            return value;
        }

        private static string ReadOptionalString(
            Context context,
            DerivedMapping mapping,
            string key)
        {
            var node = mapping.Get(key);
            if (node == null)
            {
                return null;
            }
            if (node is DerivedScalar scalar && scalar.Value is string value)
            {
                return value;
            }
            context.Fault("Output." + key, node, string.Format(
                CultureInfo.InvariantCulture,
                "Output.{0} expected a string, found {1}",
                key, Describe(node)));
            return null;
        }

        private static bool? ReadOptionalBool(
            Context context,
            DerivedMapping mapping,
            string key)
        {
            var node = mapping.Get(key);
            if (node == null)
            {
                return null;
            }
            if (node is DerivedScalar scalar && scalar.Value is bool value)
            {
                return value;
            }
            context.Fault("Output." + key, node, string.Format(
                CultureInfo.InvariantCulture,
                "Output.{0} expected a boolean, found {1}",
                key, Describe(node)));
            return null;
        }

        private static int? ReadOptionalInt(
            Context context,
            DerivedMapping mapping,
            string key)
        {
            var node = mapping.Get(key);
            if (node == null)
            {
                return null;
            }
            if (node is DerivedScalar scalar && scalar.Value is int value)
            {
                return value;
            }
            context.Fault("Output." + key, node, string.Format(
                CultureInfo.InvariantCulture,
                "Output.{0} expected an integer, found {1}",
                key, Describe(node)));
            return null;
        }

        private static IReadOnlyList<string> ReadOptionalStringList(
            Context context,
            DerivedMapping mapping,
            string key)
        {
            var node = mapping.Get(key);
            if (node == null)
            {
                return null;
            }
            if (!(node is DerivedSequence sequence))
            {
                context.Fault("Output." + key, node, string.Format(
                    CultureInfo.InvariantCulture,
                    "Output.{0} expected a list, found {1}",
                    key, Describe(node)));
                return null;
            }
            var result = new List<string>();
            foreach (var item in sequence.Items)
            {
                if (item is DerivedScalar scalar && scalar.Value != null)
                {
                    result.Add(Convert.ToString(
                        scalar.Value, CultureInfo.InvariantCulture));
                }
                else
                {
                    context.Fault("Output." + key, item, string.Format(
                        CultureInfo.InvariantCulture,
                        "Output.{0} expected a list of values, found {1} " +
                        "in the list",
                        key, Describe(item)));
                }
            }
            return result;
        }

        // ---------------------------------------------------------------
        // Checks and rules.
        // ---------------------------------------------------------------

        private static void ReadCheckNames(
            Context context,
            DerivedMapping document)
        {
            var node = document.Get("Checks");
            if (node == null)
            {
                return;
            }
            if (!(node is DerivedMapping mapping))
            {
                context.Fault("Checks", node, string.Format(
                    CultureInfo.InvariantCulture,
                    "Checks expected a mapping of names to conditions, " +
                    "found {0}",
                    Describe(node)));
                return;
            }
            foreach (var name in mapping.Names)
            {
                if (_identifier.IsMatch(name) == false)
                {
                    context.Fault("Checks." + name, mapping, string.Format(
                        CultureInfo.InvariantCulture,
                        "check name '{0}' does not match the pattern {1}",
                        name, _identifier.ToString()));
                    continue;
                }
                context.CheckNames.Add(name);
            }
        }

        private static IReadOnlyList<DerivedCheck> ReadChecks(
            Context context,
            DerivedMapping document)
        {
            var result = new List<DerivedCheck>();
            var node = document.Get("Checks") as DerivedMapping;
            if (node == null)
            {
                return result;
            }
            foreach (var name in context.CheckNames)
            {
                var condition = ReadCondition(
                    context, node.Get(name), "Checks." + name, node);
                result.Add(new DerivedCheck(name, condition));
            }
            return result;
        }

        private static IReadOnlyList<DerivedRule> ReadRules(
            Context context,
            DerivedMapping document,
            DerivedPropertyMetaData output)
        {
            var result = new List<DerivedRule>();
            var node = document.Get("Rules");
            if (node == null)
            {
                context.Fault("Rules", document,
                    "required key 'Rules' is missing");
                return result;
            }
            if (!(node is DerivedSequence sequence))
            {
                context.Fault("Rules", node, string.Format(
                    CultureInfo.InvariantCulture,
                    "Rules expected a list, found {0}", Describe(node)));
                return result;
            }
            if (sequence.Items.Count == 0)
            {
                context.Fault("Rules", node,
                    "Rules must hold at least one rule");
                return result;
            }

            // Every script ends in an Else, so a script always chooses a
            // value once its source properties have been read and there is
            // no runtime path for no rule having matched.
            var last = sequence.Items[sequence.Items.Count - 1];
            if (!(last is DerivedMapping lastEntry) ||
                lastEntry.Has("Else") == false)
            {
                context.Fault("Rules", node,
                    "the last rule must be an Else, which is what a script " +
                    "falls back to when no earlier rule matched");
            }

            for (var i = 0; i < sequence.Items.Count; i++)
            {
                var path = string.Format(
                    CultureInfo.InvariantCulture, "Rules[{0}]", i);
                var item = sequence.Items[i];
                if (!(item is DerivedMapping entry))
                {
                    context.Fault(path, item, string.Format(
                        CultureInfo.InvariantCulture,
                        "a rule expected a mapping, found {0}",
                        Describe(item)));
                    continue;
                }
                foreach (var key in UnknownKeys(
                    entry, new[] { "When", "Then", "Else" }))
                {
                    context.Fault(path + "." + key, entry, string.Format(
                        CultureInfo.InvariantCulture,
                        "unknown key '{0}' in a rule. Expected When and " +
                        "Then, or Else",
                        key));
                }

                var hasWhen = entry.Has("When");
                var hasThen = entry.Has("Then");
                var hasElse = entry.Has("Else");
                var isLast = i == sequence.Items.Count - 1;

                if (hasElse && hasWhen)
                {
                    context.Fault(path, entry,
                        "a rule has both When and Else. A rule is either " +
                        "When with Then, or Else on its own");
                    continue;
                }
                if (hasElse && isLast == false)
                {
                    context.Fault(path, entry,
                        "Else is only allowed on the last rule");
                    continue;
                }
                if (hasElse == false && hasWhen == false)
                {
                    context.Fault(path, entry,
                        "a rule needs a When, or an Else on the last rule");
                    continue;
                }
                if (hasWhen && hasThen == false)
                {
                    context.Fault(path, entry,
                        "a rule with When needs a Then");
                    continue;
                }

                var valuePath = path + (hasElse ? ".Else" : ".Then");
                var value = ReadRuleValue(
                    context,
                    hasElse ? entry.Get("Else") : entry.Get("Then"),
                    valuePath,
                    entry,
                    output);
                var condition = hasWhen
                    ? ReadCondition(
                        context, entry.Get("When"), path + ".When", entry)
                    : null;
                result.Add(new DerivedRule(condition, value));
            }
            return result;
        }

        /// <summary>
        /// Reads a Then or an Else, which is a literal of Output.ValueType
        /// and must be one of Output.Values where that list is given.
        /// </summary>
        private static object ReadRuleValue(
            Context context,
            DerivedNode node,
            string path,
            DerivedNode parent,
            DerivedPropertyMetaData output)
        {
            if (node is DerivedScalar scalar == false)
            {
                if (node == null)
                {
                    context.Fault(path, parent,
                        "a rule value is a null literal, which format 1 does " +
                        "not allow");
                    return null;
                }
                context.Fault(path, node, string.Format(
                    CultureInfo.InvariantCulture,
                    "a rule value is a literal of the output value type, " +
                    "found {0}",
                    Describe(node)));
                return null;
            }
            if (scalar.Value == null)
            {
                context.Fault(path, node,
                    "a rule value is a null literal, which format 1 does " +
                    "not allow");
                return null;
            }

            var literalType = InferType(scalar.Value);
            if (Matches(literalType, output.ValueType) == false)
            {
                context.Fault(path, node, string.Format(
                    CultureInfo.InvariantCulture,
                    "expected a {0} to match Output.ValueType, found {1}",
                    DerivedValueConverter.NameOf(output.ValueType),
                    Describe(node)));
                return null;
            }

            var text = Convert.ToString(
                scalar.Value, CultureInfo.InvariantCulture);
            if (output.Values != null && output.Values.Any(
                v => string.Equals(
                    v.Name, text, StringComparison.Ordinal)) == false)
            {
                context.Fault(path, node, string.Format(
                    CultureInfo.InvariantCulture,
                    "'{0}' is not one of the values listed under " +
                    "Output.Values ({1})",
                    text,
                    string.Join(", ", output.Values.Select(v => v.Name))));
                return null;
            }

            // A whole number written for a double output stands for the
            // same number with no fractional part.
            if (output.ValueType == DerivedValueType.Double &&
                scalar.Value is int wholeNumber)
            {
                return (double)wholeNumber;
            }
            return scalar.Value;
        }

        // ---------------------------------------------------------------
        // Conditions.
        // ---------------------------------------------------------------

        private static readonly DerivedCondition _unreadable =
            new DerivedAll(new DerivedCondition[0]);

        private static DerivedCondition ReadCondition(
            Context context,
            DerivedNode node,
            string path,
            DerivedNode parent)
        {
            if (!(node is DerivedMapping mapping))
            {
                context.Fault(path, node ?? parent, string.Format(
                    CultureInfo.InvariantCulture,
                    "a condition expected a mapping, found {0}",
                    Describe(node)));
                return _unreadable;
            }
            if (mapping.Names.Count == 0)
            {
                context.Fault(path, mapping, "a condition is empty");
                return _unreadable;
            }

            if (mapping.Has("Property"))
            {
                return ReadComparison(context, mapping, path);
            }
            if (mapping.Has("Check"))
            {
                return ReadCheckReference(context, mapping, path);
            }
            if (_aggregateNames.Any(a => mapping.Has(a)))
            {
                return ReadAggregateComparison(context, mapping, path);
            }
            if (mapping.Has("All") || mapping.Has("Any"))
            {
                var which = mapping.Has("All") ? "All" : "Any";
                if (mapping.Names.Count != 1)
                {
                    context.Fault(path, mapping, string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} must be the only key of its condition, found {1}",
                        which, string.Join(", ", mapping.Names)));
                }
                var items = mapping.Get(which);
                if (!(items is DerivedSequence sequence))
                {
                    context.Fault(path + "." + which, items ?? mapping,
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "{0} expected a list of conditions, found {1}",
                            which, Describe(items)));
                    return _unreadable;
                }
                if (sequence.Items.Count == 0)
                {
                    context.Fault(path + "." + which, sequence, string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} must list at least one condition", which));
                    return _unreadable;
                }
                var children = new DerivedCondition[sequence.Items.Count];
                for (var i = 0; i < sequence.Items.Count; i++)
                {
                    children[i] = ReadCondition(
                        context,
                        sequence.Items[i],
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "{0}.{1}[{2}]", path, which, i),
                        sequence);
                }
                return which == "All"
                    ? (DerivedCondition)new DerivedAll(children)
                    : new DerivedAny(children);
            }
            if (mapping.Has("Not"))
            {
                if (mapping.Names.Count != 1)
                {
                    context.Fault(path, mapping, string.Format(
                        CultureInfo.InvariantCulture,
                        "Not must be the only key of its condition, found {0}",
                        string.Join(", ", mapping.Names)));
                }
                return new DerivedNot(ReadCondition(
                    context, mapping.Get("Not"), path + ".Not", mapping));
            }

            context.Fault(path, mapping, string.Format(
                CultureInfo.InvariantCulture,
                "a condition must be a comparison, a Check reference, an " +
                "aggregate, All, Any or Not. Found the keys {0}",
                string.Join(", ", mapping.Names)));
            return _unreadable;
        }

        private static DerivedCondition ReadComparison(
            Context context,
            DerivedMapping mapping,
            string path)
        {
            var propertyKey = mapping.Names.First(
                n => string.Equals(
                    n, "Property", StringComparison.OrdinalIgnoreCase));
            var propertyNode = mapping.Get("Property");
            var operatorKeys = mapping.Names
                .Where(n => string.Equals(
                    n, propertyKey, StringComparison.Ordinal) == false)
                .ToList();

            if (!(propertyNode is DerivedScalar propertyScalar) ||
                !(propertyScalar.Value is string property) ||
                _sourceProperty.IsMatch(property) == false)
            {
                context.Fault(path + ".Property", propertyNode ?? mapping,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "'{0}' is not a source property. Write it as " +
                        "elementKey.PropertyName, for example " +
                        "device.IsCrawler",
                        propertyNode is DerivedScalar s
                            ? s.Text
                            : Describe(propertyNode)));
                return _unreadable;
            }

            if (operatorKeys.Count == 0)
            {
                context.Fault(path, mapping, string.Format(
                    CultureInfo.InvariantCulture,
                    "a comparison on '{0}' has no operator. Expected " +
                    "exactly one of {1}",
                    property, string.Join(", ", _operatorNames)));
                return _unreadable;
            }

            var known = operatorKeys
                .Where(k => _operatorNames.Any(
                    o => string.Equals(
                        o, k, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            foreach (var key in operatorKeys)
            {
                if (known.Contains(key) == false)
                {
                    context.Fault(path, mapping, string.Format(
                        CultureInfo.InvariantCulture,
                        "unknown operator '{0}', expected one of {1}",
                        key, string.Join(", ", _operatorNames)));
                }
            }
            if (known.Count == 0)
            {
                return _unreadable;
            }
            if (operatorKeys.Count > 1)
            {
                context.Fault(path, mapping, string.Format(
                    CultureInfo.InvariantCulture,
                    "a condition takes exactly one operator, found {0}",
                    string.Join(", ", operatorKeys)));
                return _unreadable;
            }

            var op = ParseOperator(known[0]);
            var operandNode = mapping.Get(known[0]);

            if (op == DerivedOperator.In || op == DerivedOperator.NotIn)
            {
                return ReadMembership(
                    context, mapping, path, property, op, operandNode);
            }

            if (!(operandNode is DerivedScalar operandScalar) ||
                operandScalar.Value == null)
            {
                context.Fault(
                    path + "." + op, operandNode ?? mapping,
                    "a null literal is not allowed. Give the value to " +
                    "compare against");
                return _unreadable;
            }

            var type = InferType(operandScalar.Value);
            if (type == null)
            {
                context.Fault(path + "." + op, operandNode, string.Format(
                    CultureInfo.InvariantCulture,
                    "the literal {0} has no type format 1 knows",
                    Describe(operandNode)));
                return _unreadable;
            }
            if (IsAllowed(op, type.Value) == false)
            {
                context.Fault(path, mapping, string.Format(
                    CultureInfo.InvariantCulture,
                    "operator '{0}' is not allowed on type {1}. It is " +
                    "allowed on {2}",
                    op,
                    DerivedValueConverter.NameOf(type.Value),
                    AllowedTypes(op)));
                return _unreadable;
            }

            var slot = UseProperty(
                context, property, type.Value, path, mapping);
            return new DerivedComparison(
                slot, op, operandScalar.Value, null, type.Value);
        }

        private static DerivedCondition ReadMembership(
            Context context,
            DerivedMapping mapping,
            string path,
            string property,
            DerivedOperator op,
            DerivedNode operandNode)
        {
            if (!(operandNode is DerivedSequence sequence))
            {
                context.Fault(path + "." + op, operandNode ?? mapping,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} expects a list of values, found {1}",
                        op, Describe(operandNode)));
                return _unreadable;
            }
            if (sequence.Items.Count == 0)
            {
                context.Fault(path + "." + op, sequence, string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} expects a non empty list", op));
                return _unreadable;
            }

            var members = new List<object>();
            var types = new List<DerivedValueType>();
            foreach (var item in sequence.Items)
            {
                if (!(item is DerivedScalar scalar) || scalar.Value == null)
                {
                    context.Fault(path + "." + op, item,
                        "a null literal is not allowed in a list");
                    return _unreadable;
                }
                var memberType = InferType(scalar.Value);
                if (memberType == null)
                {
                    context.Fault(path + "." + op, item, string.Format(
                        CultureInfo.InvariantCulture,
                        "the literal {0} has no type format 1 knows",
                        Describe(item)));
                    return _unreadable;
                }
                members.Add(scalar.Value);
                types.Add(memberType.Value);
            }

            // Whole numbers and decimals may sit together and the list then
            // reads as double, so an author can write In: [1, 2.5]. Any
            // other mixture is a fault.
            var numeric = types.All(
                t => t == DerivedValueType.Int || t == DerivedValueType.Double);
            DerivedValueType type;
            if (numeric)
            {
                type = types.Contains(DerivedValueType.Double)
                    ? DerivedValueType.Double
                    : DerivedValueType.Int;
            }
            else if (types.Distinct().Count() > 1)
            {
                context.Fault(path + "." + op, sequence, string.Format(
                    CultureInfo.InvariantCulture,
                    "every member of a list must be of the same type, " +
                    "found {0}",
                    string.Join(", ", types.Select(
                        DerivedValueConverter.NameOf))));
                return _unreadable;
            }
            else
            {
                type = types[0];
            }

            if (type == DerivedValueType.Double)
            {
                for (var i = 0; i < members.Count; i++)
                {
                    if (members[i] is int whole)
                    {
                        members[i] = (double)whole;
                    }
                }
            }

            var slot = UseProperty(context, property, type, path, mapping);
            return new DerivedComparison(slot, op, null, members, type);
        }

        private static DerivedCondition ReadCheckReference(
            Context context,
            DerivedMapping mapping,
            string path)
        {
            if (mapping.Names.Count != 1)
            {
                context.Fault(path, mapping, string.Format(
                    CultureInfo.InvariantCulture,
                    "Check must be the only key of its condition, found {0}",
                    string.Join(", ", mapping.Names)));
            }
            var node = mapping.Get("Check");
            if (!(node is DerivedScalar scalar) ||
                !(scalar.Value is string name))
            {
                context.Fault(path + ".Check", node ?? mapping, string.Format(
                    CultureInfo.InvariantCulture,
                    "Check expected the name of a check, found {0}",
                    Describe(node)));
                return _unreadable;
            }
            var index = context.CheckNames.IndexOf(name);
            if (index < 0)
            {
                context.Fault(path + ".Check", node, string.Format(
                    CultureInfo.InvariantCulture,
                    "check '{0}' is not defined. The checks are {1}",
                    name,
                    context.CheckNames.Count == 0
                        ? "(none)"
                        : string.Join(", ", context.CheckNames)));
                return _unreadable;
            }
            return new DerivedCheckReference(index);
        }

        private static DerivedCondition ReadAggregateComparison(
            Context context,
            DerivedMapping mapping,
            string path)
        {
            var aggregateKeys = mapping.Names
                .Where(n => _aggregateNames.Any(
                    a => string.Equals(
                        a, n, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            if (aggregateKeys.Count > 1)
            {
                context.Fault(path, mapping, string.Format(
                    CultureInfo.InvariantCulture,
                    "an aggregate condition takes one of {0}, found {1}",
                    string.Join(", ", _aggregateNames),
                    string.Join(", ", aggregateKeys)));
                return _unreadable;
            }
            var aggregate = ParseAggregate(aggregateKeys[0]);
            var group = ReadGroup(
                context,
                mapping.Get(aggregateKeys[0]),
                path + "." + aggregate,
                mapping);

            var operatorKeys = mapping.Names
                .Where(n => string.Equals(
                    n, aggregateKeys[0], StringComparison.Ordinal) == false)
                .ToList();
            if (operatorKeys.Count == 0)
            {
                context.Fault(path, mapping,
                    "an aggregate condition has no operator. Expected " +
                    "exactly one of Eq, Ne, Gt, Ge, Lt, Le");
                return _unreadable;
            }
            if (operatorKeys.Count > 1)
            {
                context.Fault(path, mapping, string.Format(
                    CultureInfo.InvariantCulture,
                    "a condition takes exactly one operator, found {0}",
                    string.Join(", ", operatorKeys)));
                return _unreadable;
            }
            if (_operatorNames.Any(
                o => string.Equals(
                    o, operatorKeys[0],
                    StringComparison.OrdinalIgnoreCase)) == false)
            {
                context.Fault(path, mapping, string.Format(
                    CultureInfo.InvariantCulture,
                    "unknown operator '{0}', expected one of Eq, Ne, Gt, " +
                    "Ge, Lt, Le",
                    operatorKeys[0]));
                return _unreadable;
            }
            var op = ParseOperator(operatorKeys[0]);
            if (IsAllowed(op, DerivedValueType.Int) == false ||
                op == DerivedOperator.In || op == DerivedOperator.NotIn)
            {
                context.Fault(path, mapping, string.Format(
                    CultureInfo.InvariantCulture,
                    "operator '{0}' is not allowed on a count, which is " +
                    "an int",
                    op));
                return _unreadable;
            }

            // A count is compared with a whole number and never with
            // another count. Comparing one count against another was
            // allowed until 1 September 2026 and was removed with the rest
            // of the format's second layer, because no script needs it.
            var operandNode = mapping.Get(operatorKeys[0]);
            if (!(operandNode is DerivedScalar operandScalar) ||
                !(operandScalar.Value is int operand))
            {
                context.Fault(
                    path + "." + op, operandNode ?? mapping, string.Format(
                        CultureInfo.InvariantCulture,
                        "an aggregate is compared with a whole number, " +
                        "found {0}",
                        Describe(operandNode)));
                return _unreadable;
            }

            return new DerivedAggregateComparison(
                new DerivedAggregateValue(aggregate, group),
                op,
                operand);
        }

        /// <summary>
        /// A group is the word Checks, meaning every check, or a list of
        /// check names. Null stands for every check.
        /// </summary>
        private static int[] ReadGroup(
            Context context,
            DerivedNode node,
            string path,
            DerivedNode parent)
        {
            if (node is DerivedScalar scalar && scalar.Value is string word)
            {
                if (string.Equals(
                    word, "Checks", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }
                context.Fault(path, node, string.Format(
                    CultureInfo.InvariantCulture,
                    "a group is the word Checks, meaning every check, or a " +
                    "list of check names. Found '{0}'",
                    word));
                return new int[0];
            }
            if (node is DerivedSequence sequence)
            {
                var indexes = new List<int>();
                foreach (var item in sequence.Items)
                {
                    if (!(item is DerivedScalar member) ||
                        !(member.Value is string name))
                    {
                        context.Fault(path, item, string.Format(
                            CultureInfo.InvariantCulture,
                            "a group lists check names, found {0}",
                            Describe(item)));
                        continue;
                    }
                    var index = context.CheckNames.IndexOf(name);
                    if (index < 0)
                    {
                        context.Fault(path, item, string.Format(
                            CultureInfo.InvariantCulture,
                            "check '{0}' is not defined. The checks are {1}",
                            name,
                            context.CheckNames.Count == 0
                                ? "(none)"
                                : string.Join(", ", context.CheckNames)));
                        continue;
                    }
                    indexes.Add(index);
                }
                return indexes.ToArray();
            }
            context.Fault(path, node ?? parent, string.Format(
                CultureInfo.InvariantCulture,
                "a group is the word Checks or a list of check names, " +
                "found {0}",
                Describe(node)));
            return new int[0];
        }

        // ---------------------------------------------------------------
        // Source properties and literals.
        // ---------------------------------------------------------------

        private static int UseProperty(
            Context context,
            string name,
            DerivedValueType type,
            string path,
            DerivedNode node)
        {
            if (context.Properties.TryGetValue(name, out var use) == false)
            {
                use = new PropertyUse
                {
                    Name = name,
                    ValueType = type,
                    TypePath = path,
                    Slot = context.Order.Count
                };
                context.Properties.Add(name, use);
                context.Order.Add(use);
                return use.Slot;
            }
            if (use.ValueType != type)
            {
                context.Fault(path, node, string.Format(
                    CultureInfo.InvariantCulture,
                    "'{0}' is inferred as {1} here but was already " +
                    "inferred as {2} at {3}. Every use of a property " +
                    "must infer the same type",
                    name,
                    DerivedValueConverter.NameOf(type),
                    DerivedValueConverter.NameOf(use.ValueType),
                    use.TypePath));
            }
            return use.Slot;
        }

        /// <summary>
        /// The type a literal written in a script stands for.
        /// </summary>
        private static DerivedValueType? InferType(object literal)
        {
            if (literal is bool) { return DerivedValueType.Bool; }
            if (literal is int) { return DerivedValueType.Int; }
            if (literal is double) { return DerivedValueType.Double; }
            if (literal is string) { return DerivedValueType.String; }
            return null;
        }

        /// <summary>
        /// Whether a literal of one type may stand for a value of another.
        /// </summary>
        private static bool Matches(
            DerivedValueType? literalType,
            DerivedValueType valueType)
        {
            if (literalType.HasValue == false)
            {
                return false;
            }
            if (literalType.Value == valueType)
            {
                return true;
            }
            // A whole number written without a decimal point reads as a
            // double, so an author can write 1 rather than 1.0.
            return literalType.Value == DerivedValueType.Int &&
                valueType == DerivedValueType.Double;
        }

        private static bool IsAllowed(
            DerivedOperator op,
            DerivedValueType type)
        {
            switch (op)
            {
                case DerivedOperator.Gt:
                case DerivedOperator.Ge:
                case DerivedOperator.Lt:
                case DerivedOperator.Le:
                    return type == DerivedValueType.Int ||
                        type == DerivedValueType.Double;
                case DerivedOperator.StartsWith:
                case DerivedOperator.EndsWith:
                case DerivedOperator.Contains:
                    return type == DerivedValueType.String;
                default:
                    return true;
            }
        }

        private static string AllowedTypes(DerivedOperator op)
        {
            switch (op)
            {
                case DerivedOperator.Gt:
                case DerivedOperator.Ge:
                case DerivedOperator.Lt:
                case DerivedOperator.Le:
                    return "int, double";
                case DerivedOperator.StartsWith:
                case DerivedOperator.EndsWith:
                case DerivedOperator.Contains:
                    return "string";
                default:
                    return "bool, int, double, string";
            }
        }

        private static DerivedOperator ParseOperator(string name)
        {
            return (DerivedOperator)Enum.Parse(
                typeof(DerivedOperator), name, true);
        }

        private static DerivedAggregate ParseAggregate(string name)
        {
            return (DerivedAggregate)Enum.Parse(
                typeof(DerivedAggregate), name, true);
        }

        /// <summary>
        /// How a place in the document is named in a fault message.
        /// </summary>
        private static string Describe(DerivedNode node)
        {
            switch (node)
            {
                case null:
                    return "nothing";
                case DerivedMapping _:
                    return "a mapping";
                case DerivedSequence _:
                    return "a list";
                case DerivedScalar scalar:
                    if (scalar.Value == null) { return "a null literal"; }
                    if (scalar.Value is bool) { return "a boolean"; }
                    if (scalar.Value is int)
                    {
                        return string.Format(
                            CultureInfo.InvariantCulture,
                            "an integer ({0})", scalar.Text);
                    }
                    if (scalar.Value is double)
                    {
                        return string.Format(
                            CultureInfo.InvariantCulture,
                            "a number ({0})", scalar.Text);
                    }
                    return string.Format(
                        CultureInfo.InvariantCulture,
                        "a string (\"{0}\")", scalar.Text);
                default:
                    return "something unreadable";
            }
        }
    }
}
