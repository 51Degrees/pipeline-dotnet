/* *********************************************************************
 * This Original Work is copyright of 51 Degrees Mobile Experts Limited.
 * Copyright 2025 51 Degrees Mobile Experts Limited, Davidson House,
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

namespace FiftyOne.Pipeline.Core.Data.Types
{
    /// <summary>
    /// JavaScript type which can be returned as a value by an ElementData.
    /// A value being of type JavaScript indicates that it is intended to be
    /// run on a client browser.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/features/properties.md#the-javascript-type">Specification</see>
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", 
        "CA1036:Override methods on comparable types", 
        Justification = "Mathematical-style operators are not appropriate for this type")]
    public class JavaScript : IComparable<string>, IEquatable<string>
    {
        #region Private Properties

        /// <summary>
        /// String value of the JavaScript.
        /// </summary>
        private string _value;

        #endregion

        #region Constructor

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="value">String value containing the JavaScript</param>
        public JavaScript(string value)
        {
            _value = value;
        }

        #endregion

        #region Public Interface Methods
        /// <summary>
        /// Compare the specified value to this instance.
        /// </summary>
        /// <param name="other">
        /// The value to compare with
        /// </param>
        /// <returns>
        /// 0 if the instances have the same value
        /// </returns>
        public int CompareTo(string other)
        {
            return string.CompareOrdinal(_value, other);
        }

        /// <summary>
        /// Check if the specified value is equal to this instance.
        /// </summary>
        /// <param name="other">
        /// The value to check for equality
        /// </param>
        /// <returns>
        /// True if the values are equal, false otherwise
        /// </returns>
        public bool Equals(string other)
        {
            return _value.Equals(other, StringComparison.Ordinal);
        }

        #endregion

        #region Public Overrides
        /// <summary>
        /// Check if the current instance is equal to the specified
        /// object.
        /// </summary>
        /// <param name="obj">
        /// The object to check for equality
        /// </param>
        /// <returns>
        /// True if the values are equal, false otherwise
        /// </returns>
        public override bool Equals(object obj)
        {
            if (obj == null) return false;
            if (obj.GetType().Equals(typeof(JavaScript)))
            {
                return Equals(((JavaScript)obj)._value);
            }
            return base.Equals(obj);
        }

        /// <summary>
        /// Get a hash code for this instance
        /// </summary>
        /// <remarks>
        /// The hash code is taken directly from the string representation
        /// of this instance.
        /// </remarks>
        /// <returns>
        /// The hash code for this instance
        /// </returns>
        public override int GetHashCode()
        {
            return _value.GetHashCode();
        }

        /// <summary>
        /// Get the string representation of this instance.
        /// </summary>
        /// <returns>
        /// The string representation of this instance.
        /// </returns>
        public override string ToString()
        {
            return _value;
        }

        #endregion
    }
}
