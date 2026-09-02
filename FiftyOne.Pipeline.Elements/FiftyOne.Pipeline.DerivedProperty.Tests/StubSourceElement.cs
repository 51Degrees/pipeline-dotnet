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
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace FiftyOne.Pipeline.DerivedProperty.Tests;

/// <summary>
/// Element that publishes a fixed set of property values under an element
/// data key chosen by the test. The derived property element reads
/// properties supplied by other elements rather than evidence, so a test
/// needs a source element it can point at. Several instances with
/// different keys, such as "device" and "ip", can sit in one pipeline.
/// </summary>
public class StubSourceElement
    : FlowElementBase<StubSourceData, ElementPropertyMetaData>
{
    private readonly string _elementDataKey;

    private readonly IReadOnlyDictionary<string, object> _values;

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
    /// The property values to publish, keyed by property name. One entry
    /// of property meta data is declared for each key.
    /// </param>
    public StubSourceElement(
        ILogger<FlowElementBase<StubSourceData, ElementPropertyMetaData>>
            logger,
        string elementDataKey,
        IReadOnlyDictionary<string, object> values)
        : this(logger, elementDataKey, values, null)
    {
    }

    /// <summary>
    /// Constructor that also declares property meta data for names which
    /// carry no value. A name passed in
    /// <paramref name="propertyNamesDeclaredWithoutValues"/> appears in
    /// <see cref="Properties"/>, so anything inspecting the pipeline sees
    /// the element offering that property, but no value for the name is
    /// written to the element data when a request is processed. A test can
    /// use the difference to tell apart two cases, being the element saying
    /// it supplies a property and the value actually being present on a
    /// given request.
    /// </summary>
    /// <param name="logger">
    /// The logger for the new instance to use.
    /// </param>
    /// <param name="elementDataKey">
    /// The element data key the values are published under.
    /// </param>
    /// <param name="values">
    /// The property values to publish, keyed by property name.
    /// </param>
    /// <param name="propertyNamesDeclaredWithoutValues">
    /// Property names to declare in <see cref="Properties"/> without
    /// publishing a value for them. Pass null or an empty collection where
    /// no such names are wanted.
    /// </param>
    public StubSourceElement(
        ILogger<FlowElementBase<StubSourceData, ElementPropertyMetaData>>
            logger,
        string elementDataKey,
        IReadOnlyDictionary<string, object> values,
        IReadOnlyCollection<string> propertyNamesDeclaredWithoutValues)
        : this(
            logger,
            elementDataKey,
            values,
            propertyNamesDeclaredWithoutValues,
            null)
    {
    }

    /// <summary>
    /// Constructor that also says what type each property is declared as.
    /// A real element declares the type callers read a property back as,
    /// and anything checking one element's property against another's has
    /// to read that type, so a test needs to be able to set it. A name
    /// with no entry in <paramref name="declaredTypes"/> is declared as
    /// object, which is what the other constructors give every name.
    /// </summary>
    /// <param name="logger">
    /// The logger for the new instance to use.
    /// </param>
    /// <param name="elementDataKey">
    /// The element data key the values are published under.
    /// </param>
    /// <param name="values">
    /// The property values to publish, keyed by property name.
    /// </param>
    /// <param name="propertyNamesDeclaredWithoutValues">
    /// Property names to declare in <see cref="Properties"/> without
    /// publishing a value for them. Pass null or an empty collection where
    /// no such names are wanted.
    /// </param>
    /// <param name="declaredTypes">
    /// The type to declare each property as, keyed by property name. Pass
    /// null where every property is to be declared as object.
    /// </param>
    public StubSourceElement(
        ILogger<FlowElementBase<StubSourceData, ElementPropertyMetaData>>
            logger,
        string elementDataKey,
        IReadOnlyDictionary<string, object> values,
        IReadOnlyCollection<string> propertyNamesDeclaredWithoutValues,
        IReadOnlyDictionary<string, Type> declaredTypes)
        : base(logger)
    {
        _elementDataKey = elementDataKey ??
            throw new ArgumentNullException(nameof(elementDataKey));
        _values = values ??
            throw new ArgumentNullException(nameof(values));

        // The list is built once here rather than on each access, as the
        // pipeline reads the properties of every element many times.
        var properties = new List<ElementPropertyMetaData>();
        foreach (var name in _values.Keys)
        {
            properties.Add(new ElementPropertyMetaData(
                this, name, TypeOf(declaredTypes, name), true));
        }
        if (propertyNamesDeclaredWithoutValues != null)
        {
            foreach (var name in propertyNamesDeclaredWithoutValues)
            {
                properties.Add(new ElementPropertyMetaData(
                    this, name, TypeOf(declaredTypes, name), true));
            }
        }
        _properties = properties;
    }

    private static Type TypeOf(
        IReadOnlyDictionary<string, Type> declaredTypes,
        string name)
    {
        if (declaredTypes != null &&
            declaredTypes.TryGetValue(name, out var type))
        {
            return type;
        }
        return typeof(object);
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
    /// One entry per property name the element declares. See the second
    /// constructor for names declared without a value.
    /// </summary>
    public override IList<ElementPropertyMetaData> Properties => _properties;

    /// <summary>
    /// Nothing managed to clean up.
    /// </summary>
    protected override void ManagedResourcesCleanup()
    {

    }

    /// <summary>
    /// Copies the values given to the constructor into the element data.
    /// </summary>
    /// <param name="data">
    /// The flow data to write the values to.
    /// </param>
    protected override void ProcessInternal(IFlowData data)
    {
        var elementData = data.GetOrAdd(
            ElementDataKey, p => new StubSourceData(null, p));
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
