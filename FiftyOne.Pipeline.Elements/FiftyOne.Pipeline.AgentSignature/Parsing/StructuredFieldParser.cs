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

namespace FiftyOne.Pipeline.AgentSignature.Parsing
{
    /// <summary>
    /// A parser for the part of RFC 8941, Structured Field Values for HTTP,
    /// that the three Web Bot Auth headers use. That is a dictionary whose
    /// members are either a single item or an inner list of items, with
    /// parameters on both.
    /// </summary>
    /// <remarks>
    /// The parser never throws for any input. A field it cannot read makes
    /// the parse method answer false, which the element reports as the
    /// Invalid status with the Malformed reason.
    /// </remarks>
    internal static class StructuredFieldParser
    {
        /// <summary>
        /// Parse a field value as an RFC 8941 dictionary.
        /// </summary>
        /// <param name="input">The field value.</param>
        /// <param name="result">The dictionary parsed.</param>
        /// <returns>True if the whole field value was read.</returns>
        public static bool TryParseDictionary(
            string input,
            out SfDictionary result)
        {
            result = null;
            if (input == null)
            {
                return false;
            }

            var dictionary = new SfDictionary();
            var position = 0;
            SkipSpaces(input, ref position);
            if (position >= input.Length)
            {
                // An empty field value is an empty dictionary.
                result = dictionary;
                return true;
            }

            while (position < input.Length)
            {
                if (TryParseKey(input, ref position, out var key) == false)
                {
                    return false;
                }

                SfMember member;
                if (position < input.Length && input[position] == '=')
                {
                    position++;
                    if (TryParseMemberValue(input, ref position, out member)
                        == false)
                    {
                        return false;
                    }
                }
                else
                {
                    // A member written without a value is the boolean true
                    // with whatever parameters follow it.
                    var start = position;
                    if (TryParseParameters(
                        input, ref position, out var parameters) == false)
                    {
                        return false;
                    }
                    var raw = input.Substring(start, position - start);
                    member = new SfMember(
                        new SfItem(true, parameters, raw), raw);
                }

                dictionary.Add(key, member);

                SkipOptionalWhitespace(input, ref position);
                if (position >= input.Length)
                {
                    break;
                }
                if (input[position] != ',')
                {
                    return false;
                }
                position++;
                SkipOptionalWhitespace(input, ref position);
                // RFC 8941 forbids a trailing comma.
                if (position >= input.Length)
                {
                    return false;
                }
            }

            result = dictionary;
            return true;
        }

        /// <summary>
        /// Parse a field value as a single RFC 8941 item. The bare quoted
        /// string form of the 'Signature-Agent' header needs this.
        /// </summary>
        /// <param name="input">The field value.</param>
        /// <param name="result">The item parsed.</param>
        /// <returns>True if the whole field value was read.</returns>
        public static bool TryParseItem(string input, out SfItem result)
        {
            result = null;
            if (input == null)
            {
                return false;
            }
            var position = 0;
            SkipSpaces(input, ref position);
            if (TryParseItemAt(input, ref position, out var item) == false)
            {
                return false;
            }
            SkipSpaces(input, ref position);
            if (position != input.Length)
            {
                return false;
            }
            result = item;
            return true;
        }

        /// <summary>
        /// Find a parameter by name in a list of parameters.
        /// </summary>
        /// <param name="parameters">The parameters to search.</param>
        /// <param name="name">The parameter name.</param>
        /// <param name="value">The value found.</param>
        /// <returns>True if the parameter was present.</returns>
        public static bool TryGetParameter(
            IList<SfParameter> parameters,
            string name,
            out object value)
        {
            if (parameters != null)
            {
                foreach (var parameter in parameters)
                {
                    if (string.Equals(
                        parameter.Name, name, StringComparison.Ordinal))
                    {
                        value = parameter.Value;
                        return true;
                    }
                }
            }
            value = null;
            return false;
        }

        /// <summary>
        /// Find a parameter by name and read it as text, whether it was
        /// written as a quoted string or as a token.
        /// </summary>
        /// <param name="parameters">The parameters to search.</param>
        /// <param name="name">The parameter name.</param>
        /// <returns>
        /// The parameter value, or null when the parameter was absent or
        /// held a value that is neither a string nor a token.
        /// </returns>
        public static string GetStringParameter(
            IList<SfParameter> parameters,
            string name)
        {
            if (TryGetParameter(parameters, name, out var value))
            {
                if (value is string text)
                {
                    return text;
                }
                if (value is SfToken token)
                {
                    return token.Value;
                }
            }
            return null;
        }

        private static bool TryParseMemberValue(
            string input,
            ref int position,
            out SfMember member)
        {
            member = null;
            var start = position;
            if (position < input.Length && input[position] == '(')
            {
                if (TryParseInnerList(input, ref position, out var items)
                    == false)
                {
                    return false;
                }
                if (TryParseParameters(
                    input, ref position, out var parameters) == false)
                {
                    return false;
                }
                member = new SfMember(
                    items,
                    parameters,
                    input.Substring(start, position - start));
                return true;
            }

            if (TryParseItemAt(input, ref position, out var item) == false)
            {
                return false;
            }
            member = new SfMember(
                item, input.Substring(start, position - start));
            return true;
        }

        private static bool TryParseInnerList(
            string input,
            ref int position,
            out IList<SfItem> items)
        {
            items = null;
            if (position >= input.Length || input[position] != '(')
            {
                return false;
            }
            position++;
            var list = new List<SfItem>();
            while (true)
            {
                SkipSpaces(input, ref position);
                if (position >= input.Length)
                {
                    // The closing bracket is missing.
                    return false;
                }
                if (input[position] == ')')
                {
                    position++;
                    items = list;
                    return true;
                }
                if (TryParseItemAt(input, ref position, out var item)
                    == false)
                {
                    return false;
                }
                list.Add(item);
                if (position < input.Length &&
                    input[position] != ' ' &&
                    input[position] != ')')
                {
                    return false;
                }
            }
        }

        private static bool TryParseItemAt(
            string input,
            ref int position,
            out SfItem item)
        {
            item = null;
            var start = position;
            if (TryParseBareItem(input, ref position, out var value)
                == false)
            {
                return false;
            }
            if (TryParseParameters(input, ref position, out var parameters)
                == false)
            {
                return false;
            }
            item = new SfItem(
                value,
                parameters,
                input.Substring(start, position - start));
            return true;
        }

        private static bool TryParseParameters(
            string input,
            ref int position,
            out IList<SfParameter> parameters)
        {
            var list = new List<SfParameter>();
            parameters = list;
            while (position < input.Length && input[position] == ';')
            {
                position++;
                SkipSpaces(input, ref position);
                if (TryParseKey(input, ref position, out var name) == false)
                {
                    return false;
                }
                object value = true;
                if (position < input.Length && input[position] == '=')
                {
                    position++;
                    if (TryParseBareItem(input, ref position, out value)
                        == false)
                    {
                        return false;
                    }
                }
                list.Add(new SfParameter(name, value));
            }
            return true;
        }

        private static bool TryParseKey(
            string input,
            ref int position,
            out string key)
        {
            key = null;
            if (position >= input.Length)
            {
                return false;
            }
            var first = input[position];
            if (IsLowerAlpha(first) == false && first != '*')
            {
                return false;
            }
            var start = position;
            position++;
            while (position < input.Length)
            {
                var current = input[position];
                if (IsLowerAlpha(current) ||
                    IsDigit(current) ||
                    current == '_' ||
                    current == '-' ||
                    current == '.' ||
                    current == '*')
                {
                    position++;
                }
                else
                {
                    break;
                }
            }
            key = input.Substring(start, position - start);
            return true;
        }

        private static bool TryParseBareItem(
            string input,
            ref int position,
            out object value)
        {
            value = null;
            if (position >= input.Length)
            {
                return false;
            }
            var current = input[position];
            if (current == '"')
            {
                return TryParseString(input, ref position, out value);
            }
            if (current == ':')
            {
                return TryParseByteSequence(input, ref position, out value);
            }
            if (current == '?')
            {
                return TryParseBoolean(input, ref position, out value);
            }
            if (current == '-' || IsDigit(current))
            {
                return TryParseNumber(input, ref position, out value);
            }
            if (IsAlpha(current) || current == '*')
            {
                return TryParseToken(input, ref position, out value);
            }
            return false;
        }

        private static bool TryParseString(
            string input,
            ref int position,
            out object value)
        {
            value = null;
            // Skip the opening quote.
            position++;
            var builder = new StringBuilder();
            while (position < input.Length)
            {
                var current = input[position];
                position++;
                if (current == '\\')
                {
                    if (position >= input.Length)
                    {
                        return false;
                    }
                    var escaped = input[position];
                    position++;
                    if (escaped != '"' && escaped != '\\')
                    {
                        return false;
                    }
                    builder.Append(escaped);
                }
                else if (current == '"')
                {
                    value = builder.ToString();
                    return true;
                }
                else if (current < ' ' || current >= (char)0x7f)
                {
                    return false;
                }
                else
                {
                    builder.Append(current);
                }
            }
            // The closing quote is missing.
            return false;
        }

        private static bool TryParseByteSequence(
            string input,
            ref int position,
            out object value)
        {
            value = null;
            // Skip the opening colon.
            position++;
            var start = position;
            while (position < input.Length && input[position] != ':')
            {
                position++;
            }
            if (position >= input.Length)
            {
                // The closing colon is missing.
                return false;
            }
            var encoded = input.Substring(start, position - start);
            // Skip the closing colon.
            position++;
            try
            {
                value = Convert.FromBase64String(encoded);
            }
            catch (FormatException)
            {
                return false;
            }
            return true;
        }

        private static bool TryParseBoolean(
            string input,
            ref int position,
            out object value)
        {
            value = null;
            // Skip the question mark.
            position++;
            if (position >= input.Length)
            {
                return false;
            }
            var current = input[position];
            if (current == '0')
            {
                value = false;
            }
            else if (current == '1')
            {
                value = true;
            }
            else
            {
                return false;
            }
            position++;
            return true;
        }

        private static bool TryParseNumber(
            string input,
            ref int position,
            out object value)
        {
            value = null;
            var start = position;
            if (input[position] == '-')
            {
                position++;
            }
            var digits = 0;
            var isDecimal = false;
            while (position < input.Length)
            {
                var current = input[position];
                if (IsDigit(current))
                {
                    digits++;
                    position++;
                }
                else if (current == '.' && isDecimal == false && digits > 0)
                {
                    isDecimal = true;
                    position++;
                }
                else
                {
                    break;
                }
            }
            if (digits == 0)
            {
                return false;
            }
            var text = input.Substring(start, position - start);
            if (isDecimal)
            {
                if (decimal.TryParse(
                    text,
                    NumberStyles.AllowLeadingSign |
                        NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture,
                    out var number) == false)
                {
                    return false;
                }
                value = number;
            }
            else
            {
                if (long.TryParse(
                    text,
                    NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture,
                    out var number) == false)
                {
                    return false;
                }
                value = number;
            }
            return true;
        }

        private static bool TryParseToken(
            string input,
            ref int position,
            out object value)
        {
            var start = position;
            position++;
            while (position < input.Length && IsTokenCharacter(input[position]))
            {
                position++;
            }
            value = new SfToken(input.Substring(start, position - start));
            return true;
        }

        private static bool IsTokenCharacter(char value)
        {
            if (IsAlpha(value) || IsDigit(value))
            {
                return true;
            }
            switch (value)
            {
                case '!':
                case '#':
                case '$':
                case '%':
                case '&':
                case '\'':
                case '*':
                case '+':
                case '-':
                case '.':
                case '^':
                case '_':
                case '`':
                case '|':
                case '~':
                case ':':
                case '/':
                    return true;
                default:
                    return false;
            }
        }

        private static void SkipSpaces(string input, ref int position)
        {
            while (position < input.Length && input[position] == ' ')
            {
                position++;
            }
        }

        private static void SkipOptionalWhitespace(
            string input,
            ref int position)
        {
            while (position < input.Length &&
                (input[position] == ' ' || input[position] == '\t'))
            {
                position++;
            }
        }

        private static bool IsLowerAlpha(char value) =>
            value >= 'a' && value <= 'z';

        private static bool IsAlpha(char value) =>
            (value >= 'a' && value <= 'z') || (value >= 'A' && value <= 'Z');

        private static bool IsDigit(char value) =>
            value >= '0' && value <= '9';
    }
}
