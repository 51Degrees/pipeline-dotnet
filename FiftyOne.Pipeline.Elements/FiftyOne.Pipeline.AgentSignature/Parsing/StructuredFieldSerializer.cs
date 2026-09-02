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
    /// Writes the structures the parser produces back out in the one form
    /// RFC 8941 section 4.1 allows, which the standard calls strict
    /// serialisation. RFC 9421 has a verifier rebuild the signature base
    /// from this form rather than from the text as the signer happened to
    /// write it, so that a signer whose spelling differs in some legal
    /// detail, such as a space after a parameter semicolon, still
    /// verifies.
    /// </summary>
    /// <remarks>
    /// The inputs come from <see cref="StructuredFieldParser"/>, which
    /// only produces values RFC 8941 can serialise, so the methods here
    /// return text rather than reporting failure. A value outside what
    /// the standard can carry, which only a caller other than the parser
    /// could supply, throws <see cref="ArgumentException"/>.
    /// </remarks>
    internal static class StructuredFieldSerializer
    {
        /// <summary>
        /// Serialise a whole dictionary, as RFC 8941 section 4.1.2
        /// describes. A member whose value is the boolean true is written
        /// as its key alone, followed by any parameters.
        /// </summary>
        /// <param name="dictionary">The dictionary to serialise.</param>
        /// <returns>The field value.</returns>
        public static string Serialize(SfDictionary dictionary)
        {
            if (dictionary == null)
            {
                throw new ArgumentNullException(nameof(dictionary));
            }
            var builder = new StringBuilder();
            var first = true;
            foreach (var entry in dictionary.Members)
            {
                if (first == false)
                {
                    builder.Append(", ");
                }
                first = false;
                builder.Append(entry.Key);
                var member = entry.Value;
                if (member.IsInnerList == false &&
                    member.Item.Value is bool flag &&
                    flag)
                {
                    // RFC 8941 section 4.1.2 step 2.2 writes a member
                    // whose value is true as the bare key, so 'a=?1'
                    // becomes 'a'.
                    AppendParameters(builder, member.Parameters);
                }
                else
                {
                    builder.Append('=');
                    Append(builder, member);
                }
            }
            return builder.ToString();
        }

        /// <summary>
        /// Serialise the value of one dictionary member with its
        /// parameters, leaving the key out. RFC 9421 section 2.1.2 puts
        /// this text on the signature base line of a component covered
        /// with the 'key' parameter, so a member written without a value
        /// serialises as '?1' here even though the dictionary form writes
        /// it as the bare key.
        /// </summary>
        /// <param name="member">The member to serialise.</param>
        /// <returns>The member value as text.</returns>
        public static string Serialize(SfMember member)
        {
            if (member == null)
            {
                throw new ArgumentNullException(nameof(member));
            }
            var builder = new StringBuilder();
            Append(builder, member);
            return builder.ToString();
        }

        /// <summary>
        /// Serialise one item with its parameters, as RFC 8941 section
        /// 4.1.3 describes. RFC 9421 section 2.5 writes each covered
        /// component identifier in this form.
        /// </summary>
        /// <param name="item">The item to serialise.</param>
        /// <returns>The item as text.</returns>
        public static string Serialize(SfItem item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }
            var builder = new StringBuilder();
            Append(builder, item);
            return builder.ToString();
        }

        private static void Append(StringBuilder builder, SfMember member)
        {
            if (member.IsInnerList)
            {
                AppendInnerList(
                    builder, member.InnerList, member.Parameters);
            }
            else
            {
                Append(builder, member.Item);
            }
        }

        private static void Append(StringBuilder builder, SfItem item)
        {
            AppendBareValue(builder, item.Value);
            AppendParameters(builder, item.Parameters);
        }

        /// <summary>
        /// Serialise an inner list, as RFC 8941 section 4.1.1.1 describes,
        /// being the items separated by single spaces inside parentheses,
        /// followed by the parameters of the list itself.
        /// </summary>
        private static void AppendInnerList(
            StringBuilder builder,
            IList<SfItem> items,
            IList<SfParameter> parameters)
        {
            builder.Append('(');
            for (var i = 0; i < items.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(' ');
                }
                Append(builder, items[i]);
            }
            builder.Append(')');
            AppendParameters(builder, parameters);
        }

        /// <summary>
        /// Serialise parameters, as RFC 8941 section 4.1.1.2 describes.
        /// There are no spaces, and a parameter whose value is the boolean
        /// true is written as its key alone.
        /// </summary>
        private static void AppendParameters(
            StringBuilder builder,
            IList<SfParameter> parameters)
        {
            if (parameters == null)
            {
                return;
            }
            foreach (var parameter in parameters)
            {
                builder.Append(';');
                builder.Append(parameter.Name);
                if (parameter.Value is bool flag && flag)
                {
                    continue;
                }
                builder.Append('=');
                AppendBareValue(builder, parameter.Value);
            }
        }

        private static void AppendBareValue(
            StringBuilder builder,
            object value)
        {
            if (value is string text)
            {
                AppendString(builder, text);
            }
            else if (value is SfToken token)
            {
                // The parser only builds a token from characters the token
                // grammar allows, so the characters go out as they came in,
                // which is what RFC 8941 section 4.1.7 asks for.
                builder.Append(token.Value);
            }
            else if (value is long number)
            {
                AppendInteger(builder, number);
            }
            else if (value is decimal fraction)
            {
                AppendDecimal(builder, fraction);
            }
            else if (value is bool flag)
            {
                // RFC 8941 section 4.1.9.
                builder.Append(flag ? "?1" : "?0");
            }
            else if (value is byte[] bytes)
            {
                // RFC 8941 section 4.1.8 requires standard base64 with the
                // '=' padding kept, which Convert.ToBase64String writes.
                builder.Append(':');
                builder.Append(Convert.ToBase64String(bytes));
                builder.Append(':');
            }
            else
            {
                throw new ArgumentException(
                    "A structured field cannot carry a value of type '" +
                    (value == null ? "null" : value.GetType().Name) + "'.",
                    nameof(value));
            }
        }

        /// <summary>
        /// Serialise an integer, as RFC 8941 section 4.1.4 describes. The
        /// range check matters because a long can hold four digits more
        /// than a structured field can, although the parser never reads a
        /// number that large.
        /// </summary>
        private static void AppendInteger(StringBuilder builder, long value)
        {
            if (value < -999999999999999 || value > 999999999999999)
            {
                throw new ArgumentException(
                    "A structured field integer holds at most fifteen " +
                    "digits.",
                    nameof(value));
            }
            builder.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Serialise a decimal, as RFC 8941 section 4.1.5 describes. The
        /// value is rounded to three places, taking the even digit when
        /// the value sits exactly half way, and the fraction is written
        /// without trailing zeros but never empty, so 1.200 becomes '1.2'
        /// and 1.000 becomes '1.0'.
        /// </summary>
        private static void AppendDecimal(
            StringBuilder builder,
            decimal value)
        {
            var rounded = Math.Round(value, 3, MidpointRounding.ToEven);
            var integerPart = Math.Abs(decimal.Truncate(rounded));
            if (integerPart > 999999999999m)
            {
                // The rounding is done first on purpose. RFC 8941 puts the
                // digit check after the rounding, because rounding can
                // carry into a thirteenth digit.
                throw new ArgumentException(
                    "A structured field decimal holds at most twelve " +
                    "digits before the decimal point.",
                    nameof(value));
            }
            builder.Append(rounded.ToString(
                "0.0##", CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Serialise a string, as RFC 8941 section 4.1.6 describes. Only
        /// the backslash and the double quote are escaped, and a character
        /// outside the printable ASCII range cannot be carried at all,
        /// although the parser never reads one.
        /// </summary>
        private static void AppendString(StringBuilder builder, string value)
        {
            builder.Append('"');
            foreach (var character in value)
            {
                if (character < ' ' || character >= (char)0x7f)
                {
                    throw new ArgumentException(
                        "A structured field string carries printable " +
                        "ASCII only.",
                        nameof(value));
                }
                if (character == '"' || character == '\\')
                {
                    builder.Append('\\');
                }
                builder.Append(character);
            }
            builder.Append('"');
        }
    }
}
