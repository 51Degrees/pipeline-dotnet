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

namespace FiftyOne.Pipeline.DerivedProperty.Data
{
    /// <summary>
    /// What one request supplies to a condition, being the converted value
    /// of every source property and the answer of every check evaluated so
    /// far. One instance belongs to one request on one thread, and nothing
    /// in it is shared.
    /// </summary>
    public sealed class DerivedEvaluationContext
    {
        /// <summary>
        /// Create a new instance.
        /// </summary>
        /// <param name="slotCount">How many source properties there are.</param>
        /// <param name="checkCount">How many checks there are.</param>
        public DerivedEvaluationContext(int slotCount, int checkCount)
        {
            Available = new bool[slotCount];
            Values = new object[slotCount];
            Reasons = new string[slotCount];
            Checks = new bool[checkCount];
        }

        /// <summary>
        /// Whether each source property was available and valid on this
        /// request, indexed the same way as the script's property list.
        /// </summary>
        public bool[] Available { get; }

        /// <summary>
        /// The converted value of each source property, where available.
        /// </summary>
        public object[] Values { get; }

        /// <summary>
        /// Why each source property was not available, where it was not.
        /// </summary>
        public string[] Reasons { get; }

        /// <summary>
        /// The answer of each check, filled in before the rules are read.
        /// </summary>
        public bool[] Checks { get; }
    }

    /// <summary>
    /// A condition in a script, compiled into a small object that answers
    /// itself. Every instance is immutable once built, so one compiled
    /// script serves every request and every thread with no locking.
    ///
    /// Every condition is true or false, because the rules only run once
    /// every source property the script names has been read.
    /// </summary>
    public abstract class DerivedCondition
    {
        /// <summary>
        /// Answer the condition for one request.
        /// </summary>
        /// <param name="context">The values for this request.</param>
        /// <returns>True where the condition holds.</returns>
        public abstract bool Evaluate(DerivedEvaluationContext context);
    }

    /// <summary>
    /// Compares one source property against a literal.
    /// </summary>
    public sealed class DerivedComparison : DerivedCondition
    {
        private readonly int _slot;
        private readonly DerivedOperator _operator;
        private readonly object _operand;
        private readonly IReadOnlyList<object> _operandList;
        private readonly DerivedValueType _valueType;

        /// <summary>
        /// Create a new instance.
        /// </summary>
        /// <param name="slot">
        /// Which source property to read, by index into the script's
        /// property list.
        /// </param>
        /// <param name="op">How to compare.</param>
        /// <param name="operand">
        /// The literal to compare against, for every operator except In and
        /// NotIn.
        /// </param>
        /// <param name="operandList">
        /// The literals to compare against, for In and NotIn.
        /// </param>
        /// <param name="valueType">The type the comparison reads.</param>
        public DerivedComparison(
            int slot,
            DerivedOperator op,
            object operand,
            IReadOnlyList<object> operandList,
            DerivedValueType valueType)
        {
            _slot = slot;
            _operator = op;
            _operand = operand;
            _operandList = operandList;
            _valueType = valueType;
        }

        /// <summary>
        /// Which source property is read, by index.
        /// </summary>
        public int Slot => _slot;

        /// <summary>How the value is compared.</summary>
        public DerivedOperator Operator => _operator;

        /// <summary>
        /// The literal compared against, for every operator except In and
        /// NotIn, where the value is null and <see cref="OperandList"/>
        /// holds the literals instead.
        /// </summary>
        public object Operand => _operand;

        /// <summary>
        /// The literals compared against for In and NotIn, or null for
        /// every other operator.
        /// </summary>
        public IReadOnlyList<object> OperandList => _operandList;

        /// <summary>The type the comparison reads the value as.</summary>
        public DerivedValueType ValueType => _valueType;

        /// <inheritdoc/>
        public override bool Evaluate(DerivedEvaluationContext context)
        {
            return Compare(context.Values[_slot]);
        }

        private bool Compare(object value)
        {
            switch (_operator)
            {
                case DerivedOperator.Eq:
                    return AreEqual(value, _operand);
                case DerivedOperator.Ne:
                    return AreEqual(value, _operand) == false;
                case DerivedOperator.Gt:
                    return CompareNumbers(value, _operand) > 0;
                case DerivedOperator.Ge:
                    return CompareNumbers(value, _operand) >= 0;
                case DerivedOperator.Lt:
                    return CompareNumbers(value, _operand) < 0;
                case DerivedOperator.Le:
                    return CompareNumbers(value, _operand) <= 0;
                case DerivedOperator.In:
                    return IsInList(value);
                case DerivedOperator.NotIn:
                    return IsInList(value) == false;
                case DerivedOperator.StartsWith:
                    return ((string)value).StartsWith(
                        (string)_operand, StringComparison.Ordinal);
                case DerivedOperator.EndsWith:
                    return ((string)value).EndsWith(
                        (string)_operand, StringComparison.Ordinal);
                case DerivedOperator.Contains:
                    return ((string)value).IndexOf(
                        (string)_operand, StringComparison.Ordinal) >= 0;
                default:
                    throw new NotSupportedException(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "The operator '{0}' cannot be evaluated as a " +
                            "comparison.",
                            _operator));
            }
        }

        private bool IsInList(object value)
        {
            for (var i = 0; i < _operandList.Count; i++)
            {
                if (AreEqual(value, _operandList[i]))
                {
                    return true;
                }
            }
            return false;
        }

        private bool AreEqual(object left, object right)
        {
            switch (_valueType)
            {
                case DerivedValueType.Bool:
                    return (bool)left == (bool)right;
                case DerivedValueType.Int:
                    return (int)left == (int)right;
                case DerivedValueType.Double:
                    return ToDouble(left).Equals(ToDouble(right));
                default:
                    // Strings compare ordinally and with regard to case, so
                    // one script gives the same answer in every language.
                    return string.Equals(
                        (string)left, (string)right, StringComparison.Ordinal);
            }
        }

        private int CompareNumbers(object left, object right)
        {
            if (_valueType == DerivedValueType.Int)
            {
                return ((int)left).CompareTo((int)right);
            }
            return ToDouble(left).CompareTo(ToDouble(right));
        }

        private static double ToDouble(object value)
        {
            if (value is double asDouble)
            {
                return asDouble;
            }
            return Convert.ToDouble(value, CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// Reuses the answer of a named check.
    /// </summary>
    public sealed class DerivedCheckReference : DerivedCondition
    {
        private readonly int _index;

        /// <summary>
        /// Create a new instance.
        /// </summary>
        /// <param name="index">
        /// Which check to reuse, by index into the script's check list.
        /// </param>
        public DerivedCheckReference(int index)
        {
            _index = index;
        }

        /// <summary>Which check is reused, by index.</summary>
        public int Index => _index;

        /// <inheritdoc/>
        public override bool Evaluate(DerivedEvaluationContext context)
        {
            return context.Checks[_index];
        }
    }

    /// <summary>
    /// Counts how many checks in a group passed or failed.
    /// </summary>
    public sealed class DerivedAggregateValue
    {
        private readonly DerivedAggregate _aggregate;
        private readonly int[] _group;

        /// <summary>
        /// Create a new instance.
        /// </summary>
        /// <param name="aggregate">Which count to take.</param>
        /// <param name="group">
        /// The checks to count, by index, or null for every check.
        /// </param>
        public DerivedAggregateValue(
            DerivedAggregate aggregate,
            int[] group)
        {
            _aggregate = aggregate;
            _group = group;
        }

        /// <summary>Which count is taken.</summary>
        public DerivedAggregate Aggregate => _aggregate;

        /// <summary>
        /// The checks counted, by index, or null for every check.
        /// </summary>
        public IReadOnlyList<int> Group => _group;

        /// <summary>
        /// Take the count for one request.
        /// </summary>
        /// <param name="context">The values for this request.</param>
        /// <returns>The count.</returns>
        public int Count(DerivedEvaluationContext context)
        {
            var count = 0;
            if (_group == null)
            {
                for (var i = 0; i < context.Checks.Length; i++)
                {
                    if (Counts(context.Checks[i]))
                    {
                        count++;
                    }
                }
                return count;
            }
            for (var i = 0; i < _group.Length; i++)
            {
                if (Counts(context.Checks[_group[i]]))
                {
                    count++;
                }
            }
            return count;
        }

        private bool Counts(bool state)
        {
            return _aggregate == DerivedAggregate.Passed ? state : !state;
        }
    }

    /// <summary>
    /// Compares a count of checks against a whole number or against another
    /// count.
    /// </summary>
    public sealed class DerivedAggregateComparison : DerivedCondition
    {
        private readonly DerivedAggregateValue _left;
        private readonly DerivedOperator _operator;
        private readonly int _operand;
        private readonly DerivedAggregateValue _right;

        /// <summary>
        /// Create a new instance.
        /// </summary>
        /// <param name="left">The count on the left of the comparison.</param>
        /// <param name="op">How to compare.</param>
        /// <param name="operand">
        /// The whole number to compare against, where the right side is a
        /// number.
        /// </param>
        /// <param name="right">
        /// The count to compare against, or null where the right side is a
        /// number.
        /// </param>
        public DerivedAggregateComparison(
            DerivedAggregateValue left,
            DerivedOperator op,
            int operand,
            DerivedAggregateValue right)
        {
            _left = left;
            _operator = op;
            _operand = operand;
            _right = right;
        }

        /// <summary>The count on the left of the comparison.</summary>
        public DerivedAggregateValue Left => _left;

        /// <summary>How the two sides are compared.</summary>
        public DerivedOperator Operator => _operator;

        /// <summary>
        /// The whole number on the right of the comparison, where
        /// <see cref="Right"/> is null.
        /// </summary>
        public int Operand => _operand;

        /// <summary>
        /// The count on the right of the comparison, or null where the
        /// right side is the whole number in <see cref="Operand"/>.
        /// </summary>
        public DerivedAggregateValue Right => _right;

        /// <inheritdoc/>
        public override bool Evaluate(DerivedEvaluationContext context)
        {
            var left = _left.Count(context);
            var right = _right == null
                ? _operand
                : _right.Count(context);
            return Holds(left, right);
        }

        private bool Holds(int left, int right)
        {
            switch (_operator)
            {
                case DerivedOperator.Eq: return left == right;
                case DerivedOperator.Ne: return left != right;
                case DerivedOperator.Gt: return left > right;
                case DerivedOperator.Ge: return left >= right;
                case DerivedOperator.Lt: return left < right;
                case DerivedOperator.Le: return left <= right;
                default:
                    throw new NotSupportedException(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "The operator '{0}' cannot be used on a count.",
                            _operator));
            }
        }
    }

    /// <summary>
    /// Holds where every listed condition holds, and is false as soon as one
    /// is false.
    /// </summary>
    public sealed class DerivedAll : DerivedCondition
    {
        private readonly DerivedCondition[] _items;

        /// <summary>
        /// Create a new instance.
        /// </summary>
        /// <param name="items">The conditions that must all hold.</param>
        public DerivedAll(DerivedCondition[] items)
        {
            _items = items;
        }

        /// <summary>The conditions that must all hold.</summary>
        public IReadOnlyList<DerivedCondition> Items => _items;

        /// <inheritdoc/>
        public override bool Evaluate(DerivedEvaluationContext context)
        {
            for (var i = 0; i < _items.Length; i++)
            {
                if (_items[i].Evaluate(context) == false)
                {
                    return false;
                }
            }
            return true;
        }
    }

    /// <summary>
    /// Holds where at least one listed condition holds, and is true as soon
    /// as one is true.
    /// </summary>
    public sealed class DerivedAny : DerivedCondition
    {
        private readonly DerivedCondition[] _items;

        /// <summary>
        /// Create a new instance.
        /// </summary>
        /// <param name="items">The conditions, of which one must hold.</param>
        public DerivedAny(DerivedCondition[] items)
        {
            _items = items;
        }

        /// <summary>The conditions, of which one must hold.</summary>
        public IReadOnlyList<DerivedCondition> Items => _items;

        /// <inheritdoc/>
        public override bool Evaluate(DerivedEvaluationContext context)
        {
            for (var i = 0; i < _items.Length; i++)
            {
                if (_items[i].Evaluate(context))
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>
    /// Turns true into false and false into true.
    /// </summary>
    public sealed class DerivedNot : DerivedCondition
    {
        private readonly DerivedCondition _item;

        /// <summary>
        /// Create a new instance.
        /// </summary>
        /// <param name="item">The condition to negate.</param>
        public DerivedNot(DerivedCondition item)
        {
            _item = item;
        }

        /// <summary>The condition negated.</summary>
        public DerivedCondition Item => _item;

        /// <inheritdoc/>
        public override bool Evaluate(DerivedEvaluationContext context)
        {
            return _item.Evaluate(context) == false;
        }
    }
}
