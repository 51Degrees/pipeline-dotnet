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

using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Core.TypedMap;
using System;
using System.Collections.Generic;

namespace FiftyOne.Pipeline.Core.Data
{
    /// <summary>
    /// The IFlowData represents the data that is used within a pipeline.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/conceptual-overview.md#flow-data">Specification</see>
    /// </summary>
    public interface IFlowData : IDisposable
    {
        /// <summary>
        /// A boolean flag that can be used to stop further elements
        /// from executing.
        /// </summary>
        [Obsolete("This property will be replaced with a more appropriate " +
            "mechanism such as a CancellationToken in a future version.")]
#pragma warning disable CA1716 // Identifiers should not match keywords
        // Marked as obsolete and will be removed in future.
        bool Stop { get; set; }
#pragma warning restore CA1716 // Identifiers should not match keywords

        /// <summary>
        /// The errors that have occurred during processing
        /// </summary>
        IList<IFlowError> Errors { get; }

        /// <summary>
        /// The pipeline used to create this flow data.
        /// </summary>
        IPipeline Pipeline { get; }

        /// <summary>
        /// Register an error that occurred while working with this 
        /// instance.
        /// </summary>
        /// <param name="ex">
        /// The exception that occurred.
        /// </param>
        /// <param name="flowElement">
        /// The flow element that the exception occurred in.
        /// </param>
        void AddError(Exception ex, IFlowElement flowElement);

        /// <summary>
        /// Register an error that occurred while working with this 
        /// instance.
        /// </summary>
        /// <param name="ex">
        /// The exception that occurred.
        /// </param>
        /// <param name="flowElement">
        /// The flow element that the exception occurred in.
        /// </param>
        /// <param name="shouldThrow">
        /// Set whether the pipeline should throw this exception.
        /// </param>
        /// <param name="shouldLog">
        /// Set whether the pipeline should log the exception as an error.
        /// </param>
        void AddError(Exception ex, IFlowElement flowElement, bool shouldThrow, bool shouldLog);

        /// <summary>
        /// Get the <see cref="IEvidence"/> object that contains the 
        /// input data for this instance.
        /// </summary>
        /// <returns></returns>
        IEvidence GetEvidence();

        /// <summary>
        /// Try to get the data value from evidence.
        /// </summary>
        /// <param name="key">The evidence key.</param>
        /// <param name="value">The value from evidence.</param>
        /// <returns>True if a value for a given key is found or False if the 
        /// key is not found or if the method cannot cast the value to the 
        /// requested type.</returns>
        bool TryGetEvidence<T>(string key, out T value);

        /// <summary>
        /// Get the string keys to the aspects that are contained within
        /// the output data.
        /// </summary>
        /// <returns></returns>
        IList<string> GetDataKeys();

        /// <summary>
        /// Get all element data values that match the specified predicate
        /// </summary>
        /// <param name="predicate">
        /// If a property passed to this function returns true then it will
        /// be included in the results
        /// </param>
        /// <returns>
        /// All the element data values that match the predicate
        /// </returns>
        IEnumerable<KeyValuePair<string, object>> GetWhere(
            Func<IElementPropertyMetaData, bool> predicate);

        /// <summary>
        /// Use the pipeline to process this FlowData instance and 
        /// populate the aspect data values.
        /// </summary>
        void Process();

        /// <summary>
        /// Add the specified evidence to the FlowData
        /// </summary>
        /// <param name="key">
        /// The evidence key
        /// </param>
        /// <param name="value">
        /// The evidence value
        /// </param>
        IFlowData AddEvidence(string key, object value);

        /// <summary>
        /// Add the specified evidence to the FlowData
        /// </summary>
        /// <param name="evidence">
        /// The evidence to add
        /// </param>
        IFlowData AddEvidence(IDictionary<string, object> evidence);

        /// <summary>
        /// Get the <see cref="IData"/> instance containing data
        /// populated by the specified element.
        /// </summary>
        /// <param name="elementDataKey">
        /// The name of the element to get data from.
        /// </param>
        /// <returns>
        /// An <see cref="IElementData"/> instance containing the data.
        /// </returns>
#pragma warning disable CA1716 // Identifiers should not match keywords
        // 'Get' is the name used in the Pipeline specification.
        // This warning is only highlighting potential confusion,
        // rather than functional problems.
        IElementData Get(string elementDataKey);
#pragma warning restore CA1716 // Identifiers should not match keywords

        /// <summary>
        /// Get the <see cref="IElementData"/> instance containing data
        /// populated by the specified element.
        /// </summary>
        /// <typeparam name="T">
        /// The expected type of the data to be returned.
        /// </typeparam>
        /// <param name="key">
        /// An <see cref="ITypedKey{T}"/> indicating the element 
        /// to get data from.
        /// </param>
        /// <returns>
        /// An instance of type T containing the data.
        /// </returns>
#pragma warning disable CA1716 // Identifiers should not match keywords
        // 'Get' is the name used in the Pipeline specification.
        // This warning is only highlighting potential confusion,
        // rather than functional problems.
        T Get<T>(ITypedKey<T> key)
            where T : IElementData;
#pragma warning restore CA1716 // Identifiers should not match keywords

        /// <summary>
        /// Check if the flow data contains an item with the specified
        /// key name and type. If it does exist, retrieve it.
        /// </summary>
        /// <param name="key">
        /// The key to check for.
        /// </param>
        /// <param name="value">
        /// The value associated with the key.
        /// </param>
        /// <returns>
        /// True if an entry matching the key exists, false otherwise.
        /// </returns>
        bool TryGetValue<T>(ITypedKey<T> key, out T value)
             where T : IElementData;

        /// <summary>
        /// Get the <see cref="IData"/> instance containing data
        /// of the specified type. If multiple instances of the type
        /// exist then an exception is thrown.
        /// </summary>
        /// <typeparam name="T">
        /// The type of the data to look for and return.
        /// </typeparam>
        /// <returns>
        /// The data instance.
        /// </returns>
#pragma warning disable CA1716 // Identifiers should not match keywords
        // 'Get' is the name used in the Pipeline specification.
        // This warning is only highlighting potential confusion,
        // rather than functional problems.
        T Get<T>() where T : IElementData;
#pragma warning restore CA1716 // Identifiers should not match keywords

        /// <summary>
        /// Get the <see cref="IElementData"/> instance containing data
        /// populated by the specified element.
        /// </summary>
        /// <typeparam name="T">
        /// The expected type of the data to be returned.
        /// </typeparam>
        /// <typeparam name="TMeta">
        /// The type of meta data that the flow element will supply 
        /// about the properties it populates.
        /// </typeparam>
        /// <param name="flowElement">
        /// The <see cref="IFlowElement{T, TMeta}"/> that populated the desired data. 
        /// </param>
        /// <returns>
        /// An instance of type T containing the data.
        /// </returns>
        T GetFromElement<T, TMeta>(IFlowElement<T, TMeta> flowElement)
            where T : IElementData
            where TMeta : IElementPropertyMetaData;

        /// <summary>
        /// Get the specified property as the specified type.
        /// </summary>
        /// <typeparam name="T">
        /// The type to return the property value as
        /// </typeparam>
        /// <param name="propertyName">
        /// The name of the property to get
        /// </param>
        /// <returns>
        /// The property value
        /// </returns>
        T GetAs<T>(string propertyName);

        /// <summary>
        /// Get the specified property as a boolean.
        /// </summary>
        /// <param name="propertyName">
        /// The name of the property to get
        /// </param>
        /// <returns>
        /// The property value
        /// </returns>
        bool GetAsBool(string propertyName);

        /// <summary>
        /// Get the specified property as a string.
        /// </summary>
        /// <param name="propertyName">
        /// The name of the property to get
        /// </param>
        /// <returns>
        /// The property value
        /// </returns>
        string GetAsString(string propertyName);

        /// <summary>
        /// Get the specified property as a int.
        /// </summary>
        /// <param name="propertyName">
        /// The name of the property to get
        /// </param>
        /// <returns>
        /// The property value
        /// </returns>
        int GetAsInt(string propertyName);

        /// <summary>
        /// Get the specified property as a long.
        /// </summary>
        /// <param name="propertyName">
        /// The name of the property to get
        /// </param>
        /// <returns>
        /// The property value
        /// </returns>
        long GetAsLong(string propertyName);

        /// <summary>
        /// Get the specified property as a float.
        /// </summary>
        /// <param name="propertyName">
        /// The name of the property to get
        /// </param>
        /// <returns>
        /// The property value
        /// </returns>
        float GetAsFloat(string propertyName);

        /// <summary>
        /// Get the specified property as a double.
        /// </summary>
        /// <param name="propertyName">
        /// The name of the property to get
        /// </param>
        /// <returns>
        /// The property value
        /// </returns>
        double GetAsDouble(string propertyName);

        /// <summary>
        /// Get or add the specified element data to the internal map.
        /// </summary>
        /// <typeparam name="T">
        /// The type of the data being stored.
        /// </typeparam>
        /// <param name="elementDataKey">
        /// The name of the element to store the data under.
        /// </param>
        /// <param name="createData">
        /// The method to use to create a new data to store if one does not
        /// already exist.
        /// </param>
        /// <returns>
        /// Existing data matching the key, or newly added data.
        /// </returns>
        T GetOrAdd<T>(string elementDataKey, Func<IPipeline, T> createData)
            where T : IElementData;

        /// <summary>
        /// Add the specified element data to the internal map.
        /// </summary>
        /// <typeparam name="T">
        /// The type of the data being stored.
        /// </typeparam>
        /// <param name="key">
        /// The key to use when storing the data.
        /// </param>
        /// <param name="createData">
        /// The method to use to create a new data to store if one does not
        /// already exist.
        /// </param>
        /// <returns>
        /// Existing data matching the key, or newly added data.
        /// </returns>
        T GetOrAdd<T>(ITypedKey<T> key, Func<IPipeline, T> createData)
             where T : IElementData;
        
        /// <summary>
        /// Get the element data for this instance as a dictionary.
        /// </summary>
        /// <returns>
        /// A dictionary containing the element data.
        /// </returns>
        IDictionary<string, object> ElementDataAsDictionary();

        /// <summary>
        /// Get the element data for this instance as an enumerable.
        /// </summary>
        /// <returns>
        /// An enumerable containing the element data.
        /// </returns>
        IEnumerable<IElementData> ElementDataAsEnumerable();

        /// <summary>
        /// Generate a <see cref="DataKey"/> that contains the evidence 
        /// data from this instance that matches the specified filter.
        /// </summary>
        /// <param name="filter">
        /// An <see cref="IEvidenceKeyFilter"/> instance that defines the 
        /// values to include in the generated key.
        /// </param>
        /// <returns>
        /// A new <see cref="DataKey"/> instance.
        /// </returns>
        DataKey GenerateKey(IEvidenceKeyFilter filter);

        /// <summary>
        /// Get a filter that will only include the evidence keys that can 
        /// be used by the elements within the pipeline that created this
        /// flow element.
        /// </summary>
        IEvidenceKeyFilter EvidenceKeyFilter { get; }
    }
}
