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
        Contains,
        /// <summary>Whether the property is available and valid.</summary>
        Present
    }

    /// <summary>
    /// The counts a rule can take over a group of checks.
    /// </summary>
    public enum DerivedAggregate
    {
        /// <summary>How many checks in the group were true.</summary>
        Passed,
        /// <summary>How many checks in the group were false.</summary>
        Failed,
        /// <summary>
        /// How many checks in the group were either true or false, so how
        /// many could be answered at all.
        /// </summary>
        Evaluated
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
        /// The string form of the value returned where no rule matches, or
        /// null where the script gives none.
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
            IReadOnlyList<string> dependencies = null)
        {
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

        /// <summary>The property name.</summary>
        public string Name { get; }

        /// <summary>What the property asserts.</summary>
        public string Description { get; }

        /// <summary>The type of the value returned.</summary>
        public DerivedValueType ValueType { get; }

        /// <summary>Always false in format 1.</summary>
        public bool IsList { get; }

        /// <summary>
        /// The string form of the value returned where no rule matches and
        /// the script has no Else, or null where the script gives none.
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
        /// The type the value is read as, or null where the script only ever
        /// asks whether the property is present.
        /// </param>
        /// <param name="required">
        /// False where the script lists the property under Optional.
        /// </param>
        public DerivedSourceProperty(
            string name,
            string elementDataKey,
            string propertyName,
            DerivedValueType? valueType,
            bool required)
        {
            Name = name;
            ElementDataKey = elementDataKey;
            PropertyName = propertyName;
            ValueType = valueType;
            Required = required;
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
        /// The type the value is read as, or null where the script only ever
        /// asks whether the property is present.
        /// </summary>
        public DerivedValueType? ValueType { get; }

        /// <summary>
        /// False where the script lists the property under Optional, meaning
        /// the script can produce a value without it.
        /// </summary>
        public bool Required { get; }
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
    /// is true supplies the output.
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
        /// <param name="value">The value the rule supplies.</param>
        public DerivedRule(DerivedCondition condition, DerivedRuleValue value)
        {
            Condition = condition;
            Value = value;
        }

        /// <summary>
        /// The condition, or null for an Else.
        /// </summary>
        public DerivedCondition Condition { get; }

        /// <summary>The value the rule supplies.</summary>
        public DerivedRuleValue Value { get; }

        /// <summary>
        /// True where the rule is an Else, which always matches.
        /// </summary>
        public bool IsElse => Condition == null;
    }

    /// <summary>
    /// What a rule supplies, being either a literal or a count of checks.
    /// </summary>
    public sealed class DerivedRuleValue
    {
        private DerivedRuleValue(
            object literal,
            DerivedAggregate? aggregate,
            IReadOnlyList<int> group)
        {
            Literal = literal;
            Aggregate = aggregate;
            Group = group;
        }

        /// <summary>
        /// A rule value that is a literal of the output's value type.
        /// </summary>
        /// <param name="literal">The value.</param>
        /// <returns>The rule value.</returns>
        public static DerivedRuleValue FromLiteral(object literal)
        {
            return new DerivedRuleValue(literal, null, null);
        }

        /// <summary>
        /// A rule value that is a count of checks, which an int output uses
        /// to expose how much evidence it had.
        /// </summary>
        /// <param name="aggregate">Which count to take.</param>
        /// <param name="group">
        /// The checks to count, by index, or null for every check.
        /// </param>
        /// <returns>The rule value.</returns>
        public static DerivedRuleValue FromAggregate(
            DerivedAggregate aggregate,
            IReadOnlyList<int> group)
        {
            return new DerivedRuleValue(null, aggregate, group);
        }

        /// <summary>
        /// The literal, where the rule value is a literal.
        /// </summary>
        public object Literal { get; }

        /// <summary>
        /// Which count to take, where the rule value is a count.
        /// </summary>
        public DerivedAggregate? Aggregate { get; }

        /// <summary>
        /// The checks to count, by index, or null for every check.
        /// </summary>
        public IReadOnlyList<int> Group { get; }

        /// <summary>
        /// True where the rule value is a count of checks rather than a
        /// literal.
        /// </summary>
        public bool IsAggregate => Aggregate.HasValue;
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
