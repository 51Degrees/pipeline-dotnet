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
using System.Globalization;

namespace FiftyOne.Pipeline.DerivedProperty.Data
{
    /// <summary>
    /// Reads a source value as the type a script inferred for the property.
    ///
    /// Values are never coerced loosely, so the strings N/A, Unknown and an
    /// empty string never become false or zero. A value that cannot be read
    /// as the inferred type makes the property absent, and every condition
    /// on the property is then unanswered rather than false.
    ///
    /// The behaviour here has to match the JavaScript reference evaluator in
    /// the derived-properties repository exactly, and the conformance cases
    /// under that repository's tests folder are what prove the two agree.
    /// </summary>
    public static class DerivedValueConverter
    {
        /// <summary>
        /// Read a value that arrived as its native type or as a string.
        /// </summary>
        /// <param name="raw">The value as the source element gave it.</param>
        /// <param name="type">The type the script inferred.</param>
        /// <param name="converted">The value read, where reading worked.</param>
        /// <returns>True where the value could be read.</returns>
        public static bool TryConvert(
            object raw,
            DerivedValueType type,
            out object converted)
        {
            converted = null;
            if (raw == null)
            {
                return false;
            }
            if (raw is string text)
            {
                return TryConvertString(text, type, out converted);
            }
            switch (type)
            {
                case DerivedValueType.Bool:
                    if (raw is bool boolValue)
                    {
                        converted = boolValue;
                        return true;
                    }
                    return false;

                case DerivedValueType.Int:
                    return TryConvertNativeInt(raw, out converted);

                case DerivedValueType.Double:
                    return TryConvertNativeDouble(raw, out converted);

                default:
                    return TryConvertNativeString(raw, out converted);
            }
        }

        /// <summary>
        /// Read the string form of a value, which is how a value from a data
        /// file or from a cloud response usually arrives.
        /// </summary>
        /// <param name="raw">The string form.</param>
        /// <param name="type">The type the script inferred.</param>
        /// <param name="converted">The value read, where reading worked.</param>
        /// <returns>True where the value could be read.</returns>
        public static bool TryConvertString(
            string raw,
            DerivedValueType type,
            out object converted)
        {
            converted = null;
            if (raw == null)
            {
                return false;
            }
            switch (type)
            {
                case DerivedValueType.Bool:
                {
                    var trimmed = raw.Trim();
                    if (string.Equals(trimmed, "true",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        converted = true;
                        return true;
                    }
                    if (string.Equals(trimmed, "false",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        converted = false;
                        return true;
                    }
                    return false;
                }

                case DerivedValueType.Int:
                {
                    var trimmed = raw.Trim();
                    // Only an optional sign followed by digits. A value
                    // written as 1.0 is not a whole number and is refused,
                    // so that a script cannot silently round.
                    if (IsWholeNumber(trimmed) == false)
                    {
                        return false;
                    }
                    if (int.TryParse(
                        trimmed,
                        NumberStyles.AllowLeadingSign,
                        CultureInfo.InvariantCulture,
                        out var intValue))
                    {
                        converted = intValue;
                        return true;
                    }
                    return false;
                }

                case DerivedValueType.Double:
                {
                    var trimmed = raw.Trim();
                    if (double.TryParse(
                        trimmed,
                        NumberStyles.AllowLeadingSign |
                            NumberStyles.AllowDecimalPoint |
                            NumberStyles.AllowExponent,
                        CultureInfo.InvariantCulture,
                        out var doubleValue) &&
                        double.IsNaN(doubleValue) == false &&
                        double.IsInfinity(doubleValue) == false)
                    {
                        converted = doubleValue;
                        return true;
                    }
                    return false;
                }

                default:
                    converted = raw;
                    return true;
            }
        }

        /// <summary>
        /// The text shown for a value in a message saying the value could
        /// not be read.
        /// </summary>
        /// <param name="raw">The value.</param>
        /// <returns>The text.</returns>
        public static string Display(object raw)
        {
            if (raw == null)
            {
                return string.Empty;
            }
            if (raw is string text)
            {
                return text;
            }
            if (raw is bool boolValue)
            {
                return boolValue ? "True" : "False";
            }
            return Convert.ToString(raw, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// The name of a type as the messages write it.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>The name.</returns>
        public static string NameOf(DerivedValueType type)
        {
            switch (type)
            {
                case DerivedValueType.Bool: return "bool";
                case DerivedValueType.Int: return "int";
                case DerivedValueType.Double: return "double";
                default: return "string";
            }
        }

        private static bool IsWholeNumber(string trimmed)
        {
            if (trimmed.Length == 0)
            {
                return false;
            }
            var start = 0;
            if (trimmed[0] == '+' || trimmed[0] == '-')
            {
                start = 1;
                if (trimmed.Length == 1)
                {
                    return false;
                }
            }
            for (var i = start; i < trimmed.Length; i++)
            {
                if (trimmed[i] < '0' || trimmed[i] > '9')
                {
                    return false;
                }
            }
            return true;
        }

        private static bool TryConvertNativeInt(
            object raw,
            out object converted)
        {
            converted = null;
            switch (raw)
            {
                case int value:
                    converted = value;
                    return true;
                case sbyte value:
                    converted = (int)value;
                    return true;
                case byte value:
                    converted = (int)value;
                    return true;
                case short value:
                    converted = (int)value;
                    return true;
                case ushort value:
                    converted = (int)value;
                    return true;
                case uint value:
                    if (value > int.MaxValue) { return false; }
                    converted = (int)value;
                    return true;
                case long value:
                    if (value < int.MinValue || value > int.MaxValue)
                    {
                        return false;
                    }
                    converted = (int)value;
                    return true;
                case ulong value:
                    if (value > int.MaxValue) { return false; }
                    converted = (int)value;
                    return true;
                case float value:
                    return TryWholeFromReal(value, out converted);
                case double value:
                    return TryWholeFromReal(value, out converted);
                case decimal value:
                    if (decimal.Truncate(value) != value ||
                        value < int.MinValue || value > int.MaxValue)
                    {
                        return false;
                    }
                    converted = (int)value;
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryWholeFromReal(double value, out object converted)
        {
            converted = null;
            // A real number stands for a whole number only where nothing is
            // lost, so 1.0 is read as 1 and 1.5 is refused.
            if (double.IsNaN(value) || double.IsInfinity(value) ||
                Math.Floor(value) != value ||
                value < int.MinValue || value > int.MaxValue)
            {
                return false;
            }
            converted = (int)value;
            return true;
        }

        private static bool TryConvertNativeDouble(
            object raw,
            out object converted)
        {
            converted = null;
            switch (raw)
            {
                case double value:
                    if (double.IsNaN(value) || double.IsInfinity(value))
                    {
                        return false;
                    }
                    converted = value;
                    return true;
                case float value:
                    if (float.IsNaN(value) || float.IsInfinity(value))
                    {
                        return false;
                    }
                    converted = (double)value;
                    return true;
                case decimal value:
                    converted = (double)value;
                    return true;
                case sbyte value: converted = (double)value; return true;
                case byte value: converted = (double)value; return true;
                case short value: converted = (double)value; return true;
                case ushort value: converted = (double)value; return true;
                case int value: converted = (double)value; return true;
                case uint value: converted = (double)value; return true;
                case long value: converted = (double)value; return true;
                case ulong value: converted = (double)value; return true;
                default: return false;
            }
        }

        private static bool TryConvertNativeString(
            object raw,
            out object converted)
        {
            converted = null;
            if (raw is bool boolValue)
            {
                // The canonical string form of a boolean, matching the way
                // 51Degrees data files write True and False.
                converted = boolValue ? "True" : "False";
                return true;
            }
            if (IsNumber(raw))
            {
                converted = Convert.ToString(raw, CultureInfo.InvariantCulture);
                return true;
            }
            return false;
        }

        private static bool IsNumber(object raw)
        {
            return raw is sbyte || raw is byte || raw is short ||
                raw is ushort || raw is int || raw is uint ||
                raw is long || raw is ulong || raw is float ||
                raw is double || raw is decimal;
        }
    }
}
