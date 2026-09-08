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

using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Engines.Data;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace FiftyOne.Pipeline.DerivedProperty.Tests;

/// <summary>
/// Element data that answers the same property in two ways, being the
/// typed value through the indexer every caller uses and the string the
/// element stored through <see cref="IProvidesValuesAsString"/>.
///
/// Device detection does exactly this. It stores "Unknown" for the
/// properties the 51Degrees JavaScript fills in, meaning the JavaScript
/// has not run on the request, and the boolean accessor turns that into
/// False. A test of the string reading therefore needs source data whose
/// two answers can differ in the same way.
/// </summary>
public class StubStringSourceData : ElementDataBase, IProvidesValuesAsString
{
    private readonly
        IReadOnlyDictionary<string, IAspectPropertyValue<string>> _stored;

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="logger">
    /// The logger for the new instance to use.
    /// </param>
    /// <param name="pipeline">
    /// The pipeline the new instance belongs to.
    /// </param>
    /// <param name="stored">
    /// What <see cref="GetValueAsString(string)"/> answers, keyed by
    /// property name.
    /// </param>
    public StubStringSourceData(
        ILogger<ElementDataBase> logger,
        IPipeline pipeline,
        IReadOnlyDictionary<string, IAspectPropertyValue<string>> stored)
        : base(logger, pipeline)
    {
        _stored = stored ??
            throw new ArgumentNullException(nameof(stored));
    }

    /// <summary>
    /// The string the constructor was given for the named property.
    /// </summary>
    /// <param name="propertyName">
    /// The property to read.
    /// </param>
    /// <returns>
    /// The stored string, or a value with no value where the constructor
    /// was given nothing for the name, which is what a real element
    /// answers for a property it cannot read on this request.
    /// </returns>
    public IAspectPropertyValue<string> GetValueAsString(string propertyName)
    {
        if (_stored.TryGetValue(propertyName, out var value))
        {
            return value;
        }
        return new AspectPropertyValue<string>
        {
            NoValueMessage = "property not present on this request"
        };
    }
}

/// <summary>
/// Element that publishes a <see cref="StubStringSourceData"/> under an
/// element data key chosen by the test, holding the typed values one
/// dictionary gives and the stored strings another gives.
///
/// <see cref="StubSourceElement"/> is the one to use where a test does not
/// care about stored strings, as the element data it publishes does not
/// offer them.
/// </summary>
public class StubStringSourceElement
    : FlowElementBase<StubStringSourceData, ElementPropertyMetaData>
{
    private readonly string _elementDataKey;

    private readonly IReadOnlyDictionary<string, object> _values;

    private readonly
        IReadOnlyDictionary<string, IAspectPropertyValue<string>> _stored;

    private readonly IList<ElementPropertyMetaData> _properties;

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="logger">
    /// The logger for the new instance to use.
    /// </param>
    /// <param name="elementDataKey">
    /// The element data key the values are published under.
    /// </param>
    /// <param name="values">
    /// The property values the indexer answers, keyed by property name.
    /// </param>
    /// <param name="stored">
    /// The strings <see cref="StubStringSourceData.GetValueAsString"/>
    /// answers, keyed by property name. One entry of property meta data is
    /// declared for each name in either collection.
    /// </param>
    public StubStringSourceElement(
        ILogger<FlowElementBase<StubStringSourceData, ElementPropertyMetaData>>
            logger,
        string elementDataKey,
        IReadOnlyDictionary<string, object> values,
        IReadOnlyDictionary<string, IAspectPropertyValue<string>> stored)
        : base(logger)
    {
        _elementDataKey = elementDataKey ??
            throw new ArgumentNullException(nameof(elementDataKey));
        _values = values ??
            throw new ArgumentNullException(nameof(values));
        _stored = stored ??
            throw new ArgumentNullException(nameof(stored));

        // The list is built once here rather than on each access, as the
        // pipeline reads the properties of every element many times.
        var properties = new List<ElementPropertyMetaData>();
        var named = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in _values.Keys)
        {
            if (named.Add(name))
            {
                properties.Add(new ElementPropertyMetaData(
                    this, name, typeof(object), true));
            }
        }
        foreach (var name in _stored.Keys)
        {
            if (named.Add(name))
            {
                properties.Add(new ElementPropertyMetaData(
                    this, name, typeof(object), true));
            }
        }
        _properties = properties;
    }

    /// <summary>
    /// The element data key given to the constructor.
    /// </summary>
    public override string ElementDataKey => _elementDataKey;

    /// <summary>
    /// This element takes no evidence, so the filter is empty.
    /// </summary>
    public override IEvidenceKeyFilter EvidenceKeyFilter =>
        new EvidenceKeyFilterWhitelist(new List<string>());

    /// <summary>
    /// One entry per property name either collection given to the
    /// constructor holds.
    /// </summary>
    public override IList<ElementPropertyMetaData> Properties => _properties;

    /// <summary>
    /// Nothing managed to clean up.
    /// </summary>
    protected override void ManagedResourcesCleanup()
    {

    }

    /// <summary>
    /// Copies the values given to the constructor into the element data,
    /// which answers the stored strings of its own accord.
    /// </summary>
    /// <param name="data">
    /// The flow data to write the values to.
    /// </param>
    protected override void ProcessInternal(IFlowData data)
    {
        var elementData = data.GetOrAdd(
            ElementDataKey, p => new StubStringSourceData(null, p, _stored));
        foreach (var value in _values)
        {
            elementData[value.Key] = value.Value;
        }
    }

    /// <summary>
    /// Nothing unmanaged to clean up.
    /// </summary>
    protected override void UnmanagedResourcesCleanup()
    {

    }
}
