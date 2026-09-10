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
using System.IO;
using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace FiftyOne.Pipeline.DerivedProperty.Data
{
    /// <summary>
    /// A place in a parsed script, being a mapping, a sequence or a single
    /// value, with the line the place started on so a fault can point at it.
    /// </summary>
    public abstract class DerivedNode
    {
        /// <summary>
        /// Create a new instance.
        /// </summary>
        /// <param name="line">
        /// The one based line the place started on, or zero where the line
        /// is not known.
        /// </param>
        protected DerivedNode(int line)
        {
            Line = line;
        }

        /// <summary>
        /// The one based line the place started on, or zero where the line
        /// is not known.
        /// </summary>
        public int Line { get; }
    }

    /// <summary>
    /// A mapping of names to places. Names are matched without regard to
    /// case, as configuration files and common-metadata do.
    /// </summary>
    public sealed class DerivedMapping : DerivedNode
    {
        private readonly Dictionary<string, DerivedNode> _byLowerName;
        private readonly List<string> _names;

        /// <summary>
        /// Create a new instance.
        /// </summary>
        /// <param name="line">The line the mapping started on.</param>
        public DerivedMapping(int line) : base(line)
        {
            _byLowerName = new Dictionary<string, DerivedNode>(
                StringComparer.OrdinalIgnoreCase);
            _names = new List<string>();
        }

        /// <summary>
        /// The names, in the order the script wrote them and with the
        /// letter case the script used.
        /// </summary>
        public IReadOnlyList<string> Names => _names;

        /// <summary>
        /// Add a name and the place it leads to.
        /// </summary>
        /// <param name="name">The name as the script wrote it.</param>
        /// <param name="value">The place.</param>
        /// <exception cref="ArgumentException">
        /// Thrown where the name is already present, ignoring case.
        /// </exception>
        public void Add(string name, DerivedNode value)
        {
            if (_byLowerName.ContainsKey(name))
            {
                throw new ArgumentException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "the key '{0}' is written more than once in the " +
                        "same mapping",
                        name),
                    nameof(name));
            }
            _byLowerName.Add(name, value);
            _names.Add(name);
        }

        /// <summary>
        /// Find a name, without regard to case.
        /// </summary>
        /// <param name="name">The name to find.</param>
        /// <returns>The place, or null where the name is not present.</returns>
        public DerivedNode Get(string name)
        {
            return _byLowerName.TryGetValue(name, out var value)
                ? value
                : null;
        }

        /// <summary>
        /// Whether a name is present, without regard to case.
        /// </summary>
        /// <param name="name">The name to look for.</param>
        /// <returns>True where the name is present.</returns>
        public bool Has(string name)
        {
            return _byLowerName.ContainsKey(name);
        }
    }

    /// <summary>
    /// An ordered list of places.
    /// </summary>
    public sealed class DerivedSequence : DerivedNode
    {
        private readonly List<DerivedNode> _items;

        /// <summary>
        /// Create a new instance.
        /// </summary>
        /// <param name="line">The line the list started on.</param>
        /// <param name="items">The places in the list.</param>
        public DerivedSequence(int line, List<DerivedNode> items) : base(line)
        {
            _items = items;
        }

        /// <summary>The places in the list.</summary>
        public IReadOnlyList<DerivedNode> Items => _items;
    }

    /// <summary>
    /// A single value written in the script.
    /// </summary>
    public sealed class DerivedScalar : DerivedNode
    {
        /// <summary>
        /// Create a new instance.
        /// </summary>
        /// <param name="line">The line the value was written on.</param>
        /// <param name="value">
        /// The value as its own type, being a boolean, a whole number, a
        /// number, text, or null where the script wrote nothing.
        /// </param>
        /// <param name="text">The value exactly as written.</param>
        public DerivedScalar(int line, object value, string text) : base(line)
        {
            Value = value;
            Text = text;
        }

        /// <summary>
        /// The value as its own type, being a boolean, an int, a double,
        /// a string, or null where the script wrote nothing.
        /// </summary>
        public object Value { get; }

        /// <summary>The value exactly as written.</summary>
        public string Text { get; }
    }

    /// <summary>
    /// Raised where the text of a script is not YAML or JSON at all, or
    /// where the script is not a mapping at the top level. Every other
    /// problem is a validation fault rather than a parse failure, so that
    /// an author sees every fault at once.
    /// </summary>
    public class DerivedScriptParseException : Exception
    {
        /// <summary>
        /// Create a new instance.
        /// </summary>
        public DerivedScriptParseException() : base()
        {
        }

        /// <summary>
        /// Create a new instance.
        /// </summary>
        /// <param name="message">What went wrong.</param>
        public DerivedScriptParseException(string message) : base(message)
        {
        }

        /// <summary>
        /// Create a new instance.
        /// </summary>
        /// <param name="message">What went wrong.</param>
        /// <param name="innerException">The failure underneath.</param>
        public DerivedScriptParseException(
            string message,
            Exception innerException)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// The one based line the failure was found on, or zero where the
        /// line is not known.
        /// </summary>
        public int Line { get; set; }
    }

    /// <summary>
    /// Reads the text of a script into a tree of
    /// <see cref="DerivedNode"/>, keeping the line each place started on.
    ///
    /// YAML and JSON both go through the same reader, because JSON is a
    /// subset of YAML 1.2, so the two formats give one tree and everything
    /// after parsing is unaware of which one was written.
    /// </summary>
    public static class DerivedScriptParser
    {
        private static readonly Regex _wholeNumber = new Regex(
            @"^[+-]?(?:0|[1-9][0-9]*)$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly Regex _hexNumber = new Regex(
            @"^[+-]?0x[0-9a-fA-F]+$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly Regex _realNumber = new Regex(
            @"^[+-]?(?:[0-9]+\.[0-9]*|\.[0-9]+|[0-9]+)(?:[eE][+-]?[0-9]+)?$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        /// <summary>
        /// Whether the text is JSON, worked out from the first character
        /// that is not white space being an opening brace. Only used to
        /// name the format in a message, since one reader handles both.
        /// </summary>
        /// <param name="text">The script text.</param>
        /// <returns>True where the text looks like JSON.</returns>
        public static bool LooksLikeJson(string text)
        {
            if (text == null)
            {
                return false;
            }
            for (var i = 0; i < text.Length; i++)
            {
                if (char.IsWhiteSpace(text[i]) == false)
                {
                    return text[i] == '{';
                }
            }
            return false;
        }

        /// <summary>
        /// Read the text of a script.
        /// </summary>
        /// <param name="text">The script as YAML or as JSON.</param>
        /// <returns>The top level mapping.</returns>
        /// <exception cref="DerivedScriptParseException">
        /// Thrown where the text is not YAML or JSON, or where the script is
        /// not a mapping at the top level.
        /// </exception>
        public static DerivedMapping Parse(string text)
        {
            if (text == null)
            {
                throw new DerivedScriptParseException(
                    "the script text is missing");
            }

            var stream = new YamlStream();
            try
            {
                using (var reader = new StringReader(text))
                {
                    stream.Load(reader);
                }
            }
            catch (YamlException exception)
            {
                throw new DerivedScriptParseException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "the text is not valid {0}: {1}",
                        LooksLikeJson(text) ? "JSON" : "YAML",
                        exception.Message),
                    exception)
                {
                    Line = (int)exception.Start.Line
                };
            }

            if (stream.Documents.Count == 0)
            {
                throw new DerivedScriptParseException("the script is empty");
            }
            if (stream.Documents.Count > 1)
            {
                throw new DerivedScriptParseException(
                    "the text holds more than one document, and a script " +
                    "is one document")
                {
                    Line = (int)stream.Documents[1].RootNode.Start.Line
                };
            }

            var root = stream.Documents[0].RootNode;
            if (!(root is YamlMappingNode))
            {
                throw new DerivedScriptParseException(
                    "the script must be a mapping of keys to values at the " +
                    "top level")
                {
                    Line = (int)root.Start.Line
                };
            }
            return (DerivedMapping)Convert(root);
        }

        private static DerivedNode Convert(YamlNode node)
        {
            var line = (int)node.Start.Line;
            switch (node)
            {
                case YamlMappingNode mapping:
                {
                    var result = new DerivedMapping(line);
                    foreach (var entry in mapping.Children)
                    {
                        var key = entry.Key as YamlScalarNode;
                        if (key == null)
                        {
                            throw new DerivedScriptParseException(
                                "a key in a mapping is not a plain name")
                            {
                                Line = (int)entry.Key.Start.Line
                            };
                        }
                        try
                        {
                            result.Add(key.Value, Convert(entry.Value));
                        }
                        catch (ArgumentException exception)
                        {
                            throw new DerivedScriptParseException(
                                exception.Message, exception)
                            {
                                Line = (int)key.Start.Line
                            };
                        }
                    }
                    return result;
                }

                case YamlSequenceNode sequence:
                {
                    var items = new List<DerivedNode>(sequence.Children.Count);
                    foreach (var child in sequence.Children)
                    {
                        items.Add(Convert(child));
                    }
                    return new DerivedSequence(line, items);
                }

                case YamlScalarNode scalar:
                    return new DerivedScalar(
                        line, ReadScalar(scalar), scalar.Value);

                default:
                    throw new DerivedScriptParseException(
                        "the script holds something that is not a mapping, " +
                        "a list or a value")
                    {
                        Line = line
                    };
            }
        }

        /// <summary>
        /// Works out what a single value written in a script means, using
        /// the YAML 1.2 core rules, which JSON also follows. A value in
        /// quotes is always text, so an author can force a number or the
        /// word true to stay as text by quoting it.
        /// </summary>
        private static object ReadScalar(YamlScalarNode scalar)
        {
            if (scalar.Style == ScalarStyle.SingleQuoted ||
                scalar.Style == ScalarStyle.DoubleQuoted ||
                scalar.Style == ScalarStyle.Literal ||
                scalar.Style == ScalarStyle.Folded)
            {
                return scalar.Value;
            }

            var text = scalar.Value;
            if (text == null || text.Length == 0)
            {
                return null;
            }
            if (string.Equals(text, "null", StringComparison.Ordinal) ||
                string.Equals(text, "Null", StringComparison.Ordinal) ||
                string.Equals(text, "NULL", StringComparison.Ordinal) ||
                string.Equals(text, "~", StringComparison.Ordinal))
            {
                return null;
            }
            // Only true and false are booleans. YAML 1.1 also read yes, no,
            // on and off as booleans, which caught authors out, so the
            // format reference tells authors to write true and false and
            // nothing here reads the older spellings.
            if (string.Equals(text, "true", StringComparison.Ordinal) ||
                string.Equals(text, "True", StringComparison.Ordinal) ||
                string.Equals(text, "TRUE", StringComparison.Ordinal))
            {
                return true;
            }
            if (string.Equals(text, "false", StringComparison.Ordinal) ||
                string.Equals(text, "False", StringComparison.Ordinal) ||
                string.Equals(text, "FALSE", StringComparison.Ordinal))
            {
                return false;
            }
            if (_wholeNumber.IsMatch(text))
            {
                if (int.TryParse(
                    text,
                    NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture,
                    out var whole))
                {
                    return whole;
                }
                // A whole number too large for an int is still a number, so
                // it is read as one and the validator refuses it where an
                // int was needed.
                if (double.TryParse(
                    text,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var large))
                {
                    return large;
                }
                return text;
            }
            if (_hexNumber.IsMatch(text))
            {
                var negative = text[0] == '-';
                var digits = text.Substring(
                    text[0] == '+' || text[0] == '-' ? 3 : 2);
                if (int.TryParse(
                    digits,
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture,
                    out var hex))
                {
                    return negative ? -hex : hex;
                }
                return text;
            }
            if (_realNumber.IsMatch(text))
            {
                if (double.TryParse(
                    text,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var real))
                {
                    // A number with nothing after the decimal point stands
                    // for a whole number, so 8.0 and 8 are read the same
                    // way and infer int. The rule is written this way
                    // rather than reading the text because it is the only
                    // rule every language can implement identically, since
                    // several YAML libraries do not hand back the text a
                    // value was written as. The format reference says so
                    // plainly and the conformance cases pin it.
                    if (double.IsNaN(real) == false &&
                        double.IsInfinity(real) == false &&
                        Math.Floor(real) == real &&
                        real >= int.MinValue && real <= int.MaxValue)
                    {
                        return (int)real;
                    }
                    return real;
                }
                return text;
            }
            return text;
        }
    }
}
