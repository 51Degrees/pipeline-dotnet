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

using FiftyOne.Pipeline.Engines.Exceptions;
using System;

namespace FiftyOne.Pipeline.Engines.Data
{
    /// <summary>
    /// This class can be used where engines have a property that 
    /// may be populated and may not.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/features/properties.md#null-values">Specification</see>
    /// </summary>
    /// <typeparam name="T">
    /// The type of data stored within the instance.
    /// </typeparam>
    public class AspectPropertyValue<T> : IAspectPropertyValue<T>
    {
        /// <summary>
        /// The value stored within this instance
        /// </summary>
        private T _value;

        /// <summary>
        /// The message in the Exception if the Value property is accessed
        /// and the instance does not have a set value.
        /// </summary>
        public string NoValueMessage { get; set; } =
            "This instance does not have a set value";

        /// <summary>
        /// True if this instance contains a value, false otherwise.
        /// </summary>
        public bool HasValue { get; private set; }

        /// <summary>
        /// Get/set the underlying value.
        /// </summary>
        /// <remarks>
        /// The underlying value can be set to null. In this case,
        /// the <see cref="HasValue"/> property will return true and
        /// <see cref="Value"/> will return null.
        /// </remarks>
        /// <exception cref="NoValueException">
        /// This exception will be thrown if the instance does not 
        /// contain a value.
        /// </exception>
        public T Value
        {
            get
            {
                if(HasValue == false)
                {
                    throw new NoValueException(NoValueMessage);
                }
                return _value;
            }
            set
            {
                HasValue = true;
                _value = value;
            }
        }

        /// <summary>
        /// Get/set the underlying value as an object.
        /// </summary>
        /// <exception cref="NoValueException">
        /// This exception will be thrown if the instance does not 
        /// contain a value.
        /// </exception>
        /// <exception cref="InvalidCastException">
        /// Will be thrown if the supplied value is not of the expected type.
        /// </exception>
        object IAspectPropertyValue.Value {
            get => Value;
            set
            {
                Value = (T)value;
            }
        }

        /// <summary>
        /// Default constructor. Used to create an instance 
        /// with no initial underlying value.
        /// </summary>
        public AspectPropertyValue()
        {
        }

        /// <summary>
        /// Constructor. Used to create an instance with an 
        /// underlying value.
        /// </summary>
        /// <param name="value">
        /// The underlying value to assign to this instance.
        /// </param>
        public AspectPropertyValue(T value)
        {
            Value = value;
        }

        /// <summary>
        /// Get the string representation of this instance.
        /// </summary>
        /// <returns>
        /// The string representation of the value of this instance or
        /// 'NULL' if it has explicitly been set to null.
        /// </returns>
        /// <exception cref="NoValueException">
        /// This exception will be thrown if the instance does not 
        /// contain a value.
        /// </exception>
        public override string ToString()
        {
            return HasValue ? Value.ToString() : "NULL";
        }

        /// <summary>
        /// Check if this instance is equal to the specified object.
        /// <see cref="AspectPropertyValue{T}"/> instances are considered 
        /// equal they have the same generic type parameter and:
        /// 1. They both have hasValue == false.
        /// 2. They both have Value explicitly set to null.
        /// 3. The Value properties are considered equal.
        /// </summary>
        /// <param name="obj">
        /// The object to check for equality
        /// </param>
        /// <returns>
        /// True if this instance is equal to the specified object.
        /// False if it is not.
        /// </returns>
        public override bool Equals(object obj)
        {
            bool result = false;
            var other = obj as AspectPropertyValue<T>;
            if (other != null)
            {
                result = (this.HasValue == false && other.HasValue == false) ||
                    (this.Value?.Equals(other.Value) ?? other.Value == null);
            }
            return result;
        }

        /// <summary>
        /// Get the hash code for this instance.
        /// If value has been set to null then this will return 0.
        /// If HasValue = false then this will return -1.
        /// Otherwise, this will return the result of GetHashCode for the
        /// stored value.
        /// </summary>
        /// <remarks> 
        /// In order to avoid the most problematic hash collisions, 
        /// where GetHashCode for the stored value returns 0 or -1 it 
        /// will be changed to 1 or -2 respectively.
        /// </remarks>
        /// <returns>
        /// The hash code for this instance.
        /// </returns>
        public override int GetHashCode()
        {
            var result = -1;
            if (HasValue)
            {
                result = 0;
                if(Value != null)
                {
                    result = Value.GetHashCode();
                    // Avoid hash collisions with results that would 
                    // indicate no value or a null value.
                    if (result == -1) { result = -2; }
                    if (result == 0) { result = 1; }
                }
            }
            return result;
        }
    }
}
