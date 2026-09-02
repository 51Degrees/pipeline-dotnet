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

namespace FiftyOne.Pipeline.AgentSignature.Parsing
{
    /// <summary>
    /// A token as RFC 8941 section 3.3.4 defines it. A token is held in its
    /// own type so that it is never confused with a quoted string, which
    /// matters because the two serialise differently.
    /// </summary>
    internal sealed class SfToken
    {
        /// <summary>
        /// The characters of the token, without any quoting.
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// Construct a token from its characters.
        /// </summary>
        /// <param name="value">The characters of the token.</param>
        public SfToken(string value)
        {
            Value = value;
        }

        /// <inheritdoc/>
        public override string ToString() => Value;
    }

    /// <summary>
    /// One parameter on a structured field item, inner list or dictionary
    /// member, as RFC 8941 section 3.1.2 defines it.
    /// </summary>
    internal sealed class SfParameter
    {
        /// <summary>
        /// The parameter name, which RFC 8941 requires to be lower case.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// The parameter value. A parameter written without a value has the
        /// boolean value true.
        /// </summary>
        public object Value { get; }

        /// <summary>
        /// Construct a parameter.
        /// </summary>
        /// <param name="name">The parameter name.</param>
        /// <param name="value">The parameter value.</param>
        public SfParameter(string name, object value)
        {
            Name = name;
            Value = value;
        }
    }

    /// <summary>
    /// An item as RFC 8941 section 3.3 defines it, being a bare value with
    /// its parameters.
    /// </summary>
    internal sealed class SfItem
    {
        /// <summary>
        /// The bare value. One of <see cref="string"/>,
        /// <see cref="SfToken"/>, <see cref="long"/>,
        /// <see cref="decimal"/>, a byte array or <see cref="bool"/>.
        /// </summary>
        public object Value { get; }

        /// <summary>
        /// The parameters on this item, in the order they were written.
        /// </summary>
        public IList<SfParameter> Parameters { get; }

        /// <summary>
        /// The text of this item, including its parameters, exactly as it
        /// appeared in the header. The signature base is built from the
        /// strict serialisation instead, as RFC 9421 section 2.5 requires,
        /// so this text is kept only as a record of what was read.
        /// </summary>
        public string Raw { get; }

        /// <summary>
        /// Construct an item.
        /// </summary>
        /// <param name="value">The bare value.</param>
        /// <param name="parameters">The parameters on the item.</param>
        /// <param name="raw">
        /// The text of the item as it appeared in the header.
        /// </param>
        public SfItem(
            object value,
            IList<SfParameter> parameters,
            string raw)
        {
            Value = value;
            Parameters = parameters;
            Raw = raw;
        }

        /// <summary>
        /// Get the value of the named parameter.
        /// </summary>
        /// <param name="name">The parameter name.</param>
        /// <param name="value">The value found.</param>
        /// <returns>True if the parameter was present.</returns>
        public bool TryGetParameter(string name, out object value) =>
            StructuredFieldParser.TryGetParameter(Parameters, name, out value);

        /// <summary>
        /// Get the value of the named parameter as a string, whether it was
        /// written as a quoted string or as a token.
        /// </summary>
        /// <param name="name">The parameter name.</param>
        /// <returns>
        /// The parameter value, or null when the parameter was absent or
        /// held a value that is neither a string nor a token.
        /// </returns>
        public string GetStringParameter(string name) =>
            StructuredFieldParser.GetStringParameter(Parameters, name);
    }

    /// <summary>
    /// The value of one dictionary member, which RFC 8941 allows to be
    /// either a single item or an inner list of items.
    /// </summary>
    internal sealed class SfMember
    {
        /// <summary>
        /// True when the member holds an inner list rather than a single
        /// item.
        /// </summary>
        public bool IsInnerList { get; }

        /// <summary>
        /// The single item, when <see cref="IsInnerList"/> is false.
        /// </summary>
        public SfItem Item { get; }

        /// <summary>
        /// The items of the inner list, when <see cref="IsInnerList"/> is
        /// true.
        /// </summary>
        public IList<SfItem> InnerList { get; }

        /// <summary>
        /// The parameters on the member, in the order they were written.
        /// For an inner list these are the parameters written after the
        /// closing bracket. For a single item they are the item parameters.
        /// </summary>
        public IList<SfParameter> Parameters { get; }

        /// <summary>
        /// The text of the whole member value, including its parameters,
        /// exactly as it appeared in the header. The '@signature-params'
        /// line of the signature base carries the strict serialisation
        /// instead, as RFC 9421 section 2.3 requires, so this text is kept
        /// only as a record of what was read.
        /// </summary>
        public string Raw { get; }

        /// <summary>
        /// Construct a member holding a single item.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="raw">
        /// The text of the member value as it appeared in the header.
        /// </param>
        public SfMember(SfItem item, string raw)
        {
            IsInnerList = false;
            Item = item;
            Parameters = item.Parameters;
            Raw = raw;
        }

        /// <summary>
        /// Construct a member holding an inner list.
        /// </summary>
        /// <param name="innerList">The items of the inner list.</param>
        /// <param name="parameters">The parameters on the inner list.</param>
        /// <param name="raw">
        /// The text of the member value as it appeared in the header.
        /// </param>
        public SfMember(
            IList<SfItem> innerList,
            IList<SfParameter> parameters,
            string raw)
        {
            IsInnerList = true;
            InnerList = innerList;
            Parameters = parameters;
            Raw = raw;
        }

        /// <summary>
        /// Get the value of the named parameter.
        /// </summary>
        /// <param name="name">The parameter name.</param>
        /// <param name="value">The value found.</param>
        /// <returns>True if the parameter was present.</returns>
        public bool TryGetParameter(string name, out object value) =>
            StructuredFieldParser.TryGetParameter(Parameters, name, out value);

        /// <summary>
        /// Get the value of the named parameter as a string, whether it was
        /// written as a quoted string or as a token.
        /// </summary>
        /// <param name="name">The parameter name.</param>
        /// <returns>
        /// The parameter value, or null when the parameter was absent or
        /// held a value that is neither a string nor a token.
        /// </returns>
        public string GetStringParameter(string name) =>
            StructuredFieldParser.GetStringParameter(Parameters, name);

        /// <summary>
        /// Get the value of the named parameter as a whole number.
        /// </summary>
        /// <param name="name">The parameter name.</param>
        /// <param name="value">The value found.</param>
        /// <returns>
        /// True if the parameter was present and held a whole number.
        /// </returns>
        public bool TryGetLongParameter(string name, out long value)
        {
            value = 0;
            if (TryGetParameter(name, out var raw) && raw is long number)
            {
                value = number;
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// A dictionary as RFC 8941 section 3.2 defines it. The order of the
    /// members is kept, because the signature base depends on it.
    /// </summary>
    internal sealed class SfDictionary
    {
        private readonly List<KeyValuePair<string, SfMember>> _members =
            new List<KeyValuePair<string, SfMember>>();

        /// <summary>
        /// The members in the order they were written.
        /// </summary>
        public IReadOnlyList<KeyValuePair<string, SfMember>> Members =>
            _members;

        /// <summary>
        /// The number of members.
        /// </summary>
        public int Count => _members.Count;

        /// <summary>
        /// Add a member. RFC 8941 says a repeated key keeps the last value,
        /// so an existing member with the same key is replaced.
        /// </summary>
        /// <param name="key">The member key.</param>
        /// <param name="member">The member value.</param>
        public void Add(string key, SfMember member)
        {
            for (var i = 0; i < _members.Count; i++)
            {
                if (string.Equals(
                    _members[i].Key, key, StringComparison.Ordinal))
                {
                    _members[i] =
                        new KeyValuePair<string, SfMember>(key, member);
                    return;
                }
            }
            _members.Add(new KeyValuePair<string, SfMember>(key, member));
        }

        /// <summary>
        /// Find the member with the given key.
        /// </summary>
        /// <param name="key">The member key.</param>
        /// <param name="member">The member found.</param>
        /// <returns>True if a member with that key was present.</returns>
        public bool TryGetValue(string key, out SfMember member)
        {
            foreach (var entry in _members)
            {
                if (string.Equals(entry.Key, key, StringComparison.Ordinal))
                {
                    member = entry.Value;
                    return true;
                }
            }
            member = null;
            return false;
        }
    }
}
