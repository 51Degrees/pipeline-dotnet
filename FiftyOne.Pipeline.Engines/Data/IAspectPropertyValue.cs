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
using FiftyOne.Pipeline.Engines.FlowElements;
using FiftyOne.Pipeline.Engines.Services;

namespace FiftyOne.Pipeline.Engines.Data
{
    /// <summary>
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/features/properties.md#null-values">Specification</see>
    /// </summary>
    public interface IAspectPropertyValue
    {
        /// <summary>
        /// True if this instance contains a value, false otherwise.
        /// </summary>
        bool HasValue { get; }

        /// <summary>
        /// Get/set the underlying value.
        /// </summary>
        /// <exception cref="NoValueException">
        /// This exception will be thrown if the instance does not 
        /// contain a value.
        /// </exception>
        object Value { get; set; }

        /// <summary>
        /// The message that will appear in the exception thrown 
        /// if this instance has no value.
        /// </summary>
        string NoValueMessage { get; }
    }

    /// <summary>
    /// Represents a property of an <see cref="IAspectEngine"/> for which 
    /// we may not know the value.
    /// </summary>
    /// <remarks>
    /// Some reasons for a value not being set could be:
    /// - Some required evidence was not supplied.
    /// - No match could be found using the supplied evidence.
    /// Note that this is distinct from the related concept of a given
    /// property not being accessible. (For example, due to the property 
    /// only being available in a larger data file) In that case, 
    /// the situation is handled by an <see cref="IMissingPropertyService"/>
    /// instance.
    /// </remarks>
    /// <typeparam name="T">
    /// The type of data stored within the instance.
    /// </typeparam>
    public interface IAspectPropertyValue<T> : IAspectPropertyValue
    {
        /// <summary>
        /// Get/set the underlying value.
        /// </summary>
        /// <exception cref="NoValueException">
        /// This exception will be thrown if the instance does not 
        /// contain a value.
        /// </exception>
        new T Value { get; set; }
    }
}
