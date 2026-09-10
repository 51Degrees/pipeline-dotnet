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

namespace FiftyOne.Pipeline.DerivedProperty.Data
{
    /// <summary>
    /// The types a source property can be read as. The type is worked out
    /// from the literal a condition compares the property against, so a
    /// script never declares one.
    /// </summary>
    public enum DerivedValueType
    {
        /// <summary>
        /// True or false.
        /// </summary>
        Bool,

        /// <summary>
        /// A whole number.
        /// </summary>
        Int,

        /// <summary>
        /// A number that may carry a fractional part.
        /// </summary>
        Double,

        /// <summary>
        /// Text.
        /// </summary>
        String
    }

    /// <summary>
    /// The ways one value can be compared with another.
    /// </summary>
    public enum DerivedOperator
    {
        /// <summary>Equal.</summary>
        Eq,
        /// <summary>Not equal.</summary>
        Ne,
        /// <summary>Greater than.</summary>
        Gt,
        /// <summary>Greater than or equal to.</summary>
        Ge,
        /// <summary>Less than.</summary>
        Lt,
        /// <summary>Less than or equal to.</summary>
        Le,
        /// <summary>A member of the list given.</summary>
        In,
        /// <summary>Not a member of the list given.</summary>
        NotIn,
        /// <summary>Text starting with the text given.</summary>
        StartsWith,
        /// <summary>Text ending with the text given.</summary>
        EndsWith,
        /// <summary>Text holding the text given.</summary>
        Contains
    }

    /// <summary>
    /// The counts a rule can take over a group of checks. Every check is
    /// true or false, so the two always add up to the size of the group.
    /// </summary>
    public enum DerivedAggregate
    {
        /// <summary>How many checks in the group were true.</summary>
        Passed,
        /// <summary>How many checks in the group were false.</summary>
        Failed
    }

    /// <summary>
    /// One value a property can return, with what the value means.
    /// </summary>
    public sealed class DerivedValueMetaData
    {
        /// <summary>
        /// Create a new instance.
        /// </summary>
        /// <param name="name">The value as the property returns it.</param>
        /// <param name="description">What the value means, or null.</param>
        public DerivedValueMetaData(string name, string description)
        {
            Name = name;
            Description = description;
        }

        /// <summary>
        /// The value as the property returns it.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// What the value means, or null where the script gives no
        /// description.
        /// </summary>
        public string Description { get; }
    }

    /// <summary>
    /// The property definition a script carries, holding every field of the
    /// 51Degrees common-metadata property schema so that a script is a
    /// complete property definition rather than only a name and a type.
    /// </summary>
    public sealed class DerivedPropertyMetaData
    {
        /// <summary>
        /// Create a new instance. Only the first four arguments are
        /// required, matching the format reference.
        /// </summary>
        /// <param name="name">The property name.</param>
        /// <param name="description">What the property asserts.</param>
        /// <param name="valueType">The type of the value returned.</param>
        /// <param name="isList">
        /// Always false in format 1. List outputs are deferred.
        /// </param>
        /// <param name="defaultValue">
        /// The string form of the value common-metadata records as the
        /// default, or null where the script gives none. It is carried
        /// through to the metadata and nothing reads it while a request is
        /// being processed.
        /// </param>
        /// <param name="values">
        /// The values the property can return, or null where the script
        /// lists none.
        /// </param>
        /// <param name="category">The category, or null.</param>
        /// <param name="isMandatory">Carried through unchanged.</param>
        /// <param name="isObsolete">Carried through unchanged.</param>
        /// <param name="isPopular">Carried through unchanged.</param>
        /// <param name="exportValues">Carried through unchanged.</param>
        /// <param name="url">Carried through unchanged.</param>
        /// <param name="displayOrder">Carried through unchanged.</param>
        /// <param name="propertyId">Carried through unchanged.</param>
        /// <param name="storedValueType">Carried through unchanged.</param>
        /// <param name="vendorIds">Carried through unchanged.</param>
        /// <param name="dependencies">
        /// Every source property the checks and rules name, computed by the
        /// validator where the script does not give the list.
        /// </param>
        /// <param name="elementDataKey">
        /// The element data the property is written into. Null or the
        /// default means this element's own key, so the script creates a
        /// property. Any other key names a property another element already
        /// produces, and the script replaces its value instead. See
        /// <see cref="IsOverride"/>.
        /// </param>
        public DerivedPropertyMetaData(
            string name,
            string description,
            DerivedValueType valueType,
            bool isList,
            string defaultValue = null,
            IReadOnlyList<DerivedValueMetaData> values = null,
            string category = null,
            bool? isMandatory = null,
            bool? isObsolete = null,
            bool? isPopular = null,
            bool? exportValues = null,
            string url = null,
            int? displayOrder = null,
            int? propertyId = null,
            string storedValueType = null,
            IReadOnlyList<string> vendorIds = null,
            IReadOnlyList<string> dependencies = null,
            string elementDataKey = null)
        {
            ElementDataKey = string.IsNullOrEmpty(elementDataKey)
                ? DefaultElementDataKey
                : elementDataKey;
            Name = name;
            Description = description;
            ValueType = valueType;
            IsList = isList;
            DefaultValue = defaultValue;
            Values = values;
            Category = category;
            IsMandatory = isMandatory;
            IsObsolete = isObsolete;
            IsPopular = isPopular;
            ExportValues = exportValues;
            Url = url;
            DisplayOrder = displayOrder;
            PropertyId = propertyId;
            StoredValueType = storedValueType;
            VendorIds = vendorIds;
            Dependencies = dependencies;
        }

        /// <summary>
        /// The element data key a script writes into when its
        /// <c>Output.Name</c> carries no prefix, which is this element's
        /// own key.
        /// </summary>
        public const string DefaultElementDataKey = "derived";

        /// <summary>The property name, without any element data key.</summary>
        public string Name { get; }

        /// <summary>
        /// The element data the property is written into, taken from the
        /// prefix of <c>Output.Name</c>. Where the script writes
        /// <c>HumanConfidence</c> this is <c>derived</c>, and where it
        /// writes <c>device.IsCrawler</c> this is <c>device</c>.
        /// </summary>
        public string ElementDataKey { get; }

        /// <summary>
        /// True where the script replaces the value of a property another
        /// element already produces, rather than creating one of its own.
        /// An override that cannot read every source property it names
        /// leaves the existing value untouched rather than writing a value
        /// that has no value.
        /// </summary>
        public bool IsOverride => string.Equals(
            ElementDataKey,
            DefaultElementDataKey,
            StringComparison.OrdinalIgnoreCase) == false;

        /// <summary>
        /// The property written as <c>elementDataKey.PropertyName</c>,
        /// which is how a script names it and how messages name it.
        /// </summary>
        public string QualifiedName => ElementDataKey + "." + Name;

        /// <summary>What the property asserts.</summary>
        public string Description { get; }

        /// <summary>The type of the value returned.</summary>
        public DerivedValueType ValueType { get; }

        /// <summary>Always false in format 1.</summary>
        public bool IsList { get; }

        /// <summary>
        /// The string form of the value common-metadata records as the
        /// default, or null where the script gives none. Every script ends
        /// in an Else and so always chooses a value, which is why nothing
        /// reads this while a request is being processed.
        /// </summary>
        public string DefaultValue { get; }

        /// <summary>
        /// The values the property can return, or null where the script
        /// lists none.
        /// </summary>
        public IReadOnlyList<DerivedValueMetaData> Values { get; }

        /// <summary>The category, or null.</summary>
        public string Category { get; }

        /// <summary>True where the property must be populated.</summary>
        public bool? IsMandatory { get; }

        /// <summary>True where the property should no longer be used.</summary>
        public bool? IsObsolete { get; }

        /// <summary>True where the property is highlighted to users.</summary>
        public bool? IsPopular { get; }

        /// <summary>True where the values are published externally.</summary>
        public bool? ExportValues { get; }

        /// <summary>A page giving more about the property, or null.</summary>
        public string Url { get; }

        /// <summary>Where the property sits in a display order.</summary>
        public int? DisplayOrder { get; }

        /// <summary>The identifier the property carries elsewhere.</summary>
        public int? PropertyId { get; }

        /// <summary>
        /// The type the value is held as in a data file, carried through
        /// from the script unchanged.
        /// </summary>
        public string StoredValueType { get; }

        /// <summary>The vendors the property belongs to.</summary>
        public IReadOnlyList<string> VendorIds { get; }

        /// <summary>
        /// Every source property the checks and rules name, in
        /// elementKey.PropertyName form.
        /// </summary>
        public IReadOnlyList<string> Dependencies { get; }

        /// <summary>
        /// The CLR type a value of <see cref="ValueType"/> is returned as.
        /// </summary>
        /// <returns>The type.</returns>
        public Type GetClrType()
        {
            switch (ValueType)
            {
                case DerivedValueType.Bool: return typeof(bool);
                case DerivedValueType.Int: return typeof(int);
                case DerivedValueType.Double: return typeof(double);
                default: return typeof(string);
            }
        }
    }

    /// <summary>
    /// One source property a script reads, with the type worked out from the
    /// literals it is compared against.
    /// </summary>
    public sealed class DerivedSourceProperty
    {
        /// <summary>
        /// Create a new instance.
        /// </summary>
        /// <param name="name">
        /// The property in elementKey.PropertyName form, as the script wrote
        /// it.
        /// </param>
        /// <param name="elementDataKey">
        /// The element the property comes from.
        /// </param>
        /// <param name="propertyName">The property on that element.</param>
        /// <param name="valueType">
        /// The type the value is read as, worked out from the literals the
        /// property is compared against.
        /// </param>
        public DerivedSourceProperty(
            string name,
            string elementDataKey,
            string propertyName,
            DerivedValueType valueType)
        {
            Name = name;
            ElementDataKey = elementDataKey;
            PropertyName = propertyName;
            ValueType = valueType;
        }

        /// <summary>
        /// The property in elementKey.PropertyName form.
        /// </summary>
        public string Name { get; }

        /// <summary>The element the property comes from.</summary>
        public string ElementDataKey { get; }

        /// <summary>The property on that element.</summary>
        public string PropertyName { get; }

        /// <summary>
        /// The type the value is read as, worked out from the literals the
        /// property is compared against.
        /// </summary>
        public DerivedValueType ValueType { get; }
    }

    /// <summary>
    /// A named condition, so a rule can reuse one and an aggregate can count
    /// how many of them held.
    /// </summary>
    public sealed class DerivedCheck
    {
        /// <summary>
        /// Create a new instance.
        /// </summary>
        /// <param name="name">The name the script gave the check.</param>
        /// <param name="condition">The condition itself.</param>
        public DerivedCheck(string name, DerivedCondition condition)
        {
            Name = name;
            Condition = condition;
        }

        /// <summary>The name the script gave the check.</summary>
        public string Name { get; }

        /// <summary>The condition itself.</summary>
        public DerivedCondition Condition { get; }
    }

    /// <summary>
    /// One rule. Rules are read in order and the first rule whose condition
    /// is true supplies the output. The last rule of every script is an
    /// Else, so a script always chooses a value once its source properties
    /// have been read.
    /// </summary>
    public sealed class DerivedRule
    {
        /// <summary>
        /// Create a new instance.
        /// </summary>
        /// <param name="condition">
        /// The condition, or null for an Else, which always matches and is
        /// only allowed on the last rule.
        /// </param>
        /// <param name="value">
        /// The literal the rule supplies, of the output's value type.
        /// </param>
        public DerivedRule(DerivedCondition condition, object value)
        {
            Condition = condition;
            Value = value;
        }

        /// <summary>
        /// The condition, or null for an Else.
        /// </summary>
        public DerivedCondition Condition { get; }

        /// <summary>
        /// The literal the rule supplies, of the output's value type.
        /// </summary>
        public object Value { get; }

        /// <summary>
        /// True where the rule is an Else, which always matches.
        /// </summary>
        public bool IsElse => Condition == null;
    }

    /// <summary>
    /// One script, after parsing and validation. Every field is read only,
    /// so one instance is shared by every request and by every thread.
    /// </summary>
    public sealed class DerivedScript
    {
        /// <summary>
        /// Create a new instance.
        /// </summary>
        /// <param name="format">The script language version, always 1.</param>
        /// <param name="name">
        /// What configuration selects the script by.
        /// </param>
        /// <param name="version">The author's semantic version.</param>
        /// <param name="deprecated">True where the script is deprecated.</param>
        /// <param name="deprecationNote">
        /// What to use instead, where the script is deprecated.
        /// </param>
        /// <param name="source">
        /// Where the script came from, being a built in name, a file path,
        /// or the word code.
        /// </param>
        /// <param name="output">The property definition.</param>
        /// <param name="properties">
        /// The source properties, in the order the script first named them.
        /// </param>
        /// <param name="checks">The named conditions, in script order.</param>
        /// <param name="rules">The rules, in script order.</param>
        public DerivedScript(
            int format,
            string name,
            string version,
            bool deprecated,
            string deprecationNote,
            string source,
            DerivedPropertyMetaData output,
            IReadOnlyList<DerivedSourceProperty> properties,
            IReadOnlyList<DerivedCheck> checks,
            IReadOnlyList<DerivedRule> rules)
        {
            Format = format;
            Name = name;
            Version = version;
            Deprecated = deprecated;
            DeprecationNote = deprecationNote;
            Source = source;
            Output = output;
            Properties = properties;
            Checks = checks;
            Rules = rules;
        }

        /// <summary>The script language version, always 1.</summary>
        public int Format { get; }

        /// <summary>What configuration selects the script by.</summary>
        public string Name { get; }

        /// <summary>
        /// The author's semantic version. Printed in the build log and
        /// playing no part in selection.
        /// </summary>
        public string Version { get; }

        /// <summary>
        /// True where the script still works but should no longer be used.
        /// </summary>
        public bool Deprecated { get; }

        /// <summary>
        /// What to use instead, where the script is deprecated.
        /// </summary>
        public string DeprecationNote { get; }

        /// <summary>
        /// Where the script came from, being a built in name, a file path,
        /// or the word code.
        /// </summary>
        public string Source { get; }

        /// <summary>The property definition.</summary>
        public DerivedPropertyMetaData Output { get; }

        /// <summary>
        /// The source properties, in the order the script first named them.
        /// </summary>
        public IReadOnlyList<DerivedSourceProperty> Properties { get; }

        /// <summary>The named conditions, in script order.</summary>
        public IReadOnlyList<DerivedCheck> Checks { get; }

        /// <summary>The rules, in script order.</summary>
        public IReadOnlyList<DerivedRule> Rules { get; }
    }
}
