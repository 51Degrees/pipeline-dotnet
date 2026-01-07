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

namespace FiftyOne.Pipeline.Core.Data
{
    /// <summary>
    /// <para>
    /// Well-known text representation of geometry.
    /// </para><para>
    /// See
    /// <see href="https://en.wikipedia.org/wiki/Well-known_text_representation_of_geometry"/>
    /// </para>
    /// </summary>
    public struct WktString : IEquatable<WktString>, IEquatable<string>
    {
        /// <summary>
        /// <para>
        /// Value that adheres to the OGC 06-103r4 standard.
        /// </para><para>
        /// See
        /// <see href="https://www.ogc.org/publications/standard/sfa/"/>
        /// </para>
        /// </summary>
        /// <example>
        /// <para><c>POINT(2 4)</c></para>
        /// <para><c>POLYGON((10 10,10 20,20 20,20 15,10 10))</c></para>
        /// </example>
        public string Value { get; }

        /// <summary>
        /// Designated constructor.
        /// </summary>
        /// <param name="value">Internal text value.</param>
        public WktString(string value)
        {
            Value = value;
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
        public bool Equals(WktString other)
        {
            return Value == other.Value; 
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
            return Value == other;
        }

        /// <summary>
        /// Check if the specified value is equal to this instance.
        /// </summary>
        /// <param name="obj">
        /// The value to check for equality
        /// </param>
        /// <returns>
        /// True if the values are equal, false otherwise
        /// </returns>
        public override bool Equals(object obj)
        {
            return obj is WktString otherWkt && Equals(otherWkt)
                   || obj is string otherString && Equals(otherString);
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
            return Value.GetHashCode();
        }

        /// <summary>
        /// Get the string representation of this instance.
        /// </summary>
        /// <returns>
        /// The string representation of this instance.
        /// </returns>
        public override string ToString()
        {
            return Value;
        }
        
        /// <summary>
        /// Operator overload
        /// </summary>
        /// <param name="left">Left-hand side of comparison</param>
        /// <param name="right">Right-hand side of comparison</param>
        /// <returns>true/false</returns>
        public static bool operator==(WktString left, WktString right)
            => left.Equals(right);
        
        /// <summary>
        /// Operator overload
        /// </summary>
        /// <param name="left">Left-hand side of comparison</param>
        /// <param name="right">Right-hand side of comparison</param>
        /// <returns>true/false</returns>
        public static bool operator!=(WktString left, WktString right)
            => !left.Equals(right);
        
        /// <summary>
        /// Implicit conversion
        /// from <see cref="WktString"/>
        /// to <see cref="string"/>.
        /// </summary>
        /// <param name="wktString">WKT string</param>
        /// <returns>The string</returns>
        public static implicit operator string(WktString wktString)
            => wktString.Value;
        
        /// <summary>
        /// Implicit conversion
        /// from <see cref="string"/>
        /// to <see cref="WktString"/>.
        /// </summary>
        /// <param name="wktString">"normal" string</param>
        /// <returns>The WKT String</returns>
        public static implicit operator WktString(string wktString)
            => new WktString(wktString);
    }
}