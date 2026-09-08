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
/// Element data that answers the way a device detection engine does,
/// holding the value the data file carries as text and converting it on
/// each read. A read as a Boolean gives True only for the literal "True"
/// and False for anything else, including a placeholder such as
/// "Unknown", and reports that it has a value either way. A read as a
/// string hands the stored text over untouched.
/// </summary>
public class CoercingSourceData : ElementDataBase
{
    private readonly IReadOnlyDictionary<string, string> _stored;

    private readonly IReadOnlyDictionary<string, Type> _declaredTypes;

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
    /// The text the data holds for each property name.
    /// </param>
    /// <param name="declaredTypes">
    /// The type each property is declared as, which is the type an
    /// untyped read gives back. A name with no entry is a Boolean.
    /// </param>
    public CoercingSourceData(
        ILogger<ElementDataBase> logger,
        IPipeline pipeline,
        IReadOnlyDictionary<string, string> stored,
        IReadOnlyDictionary<string, Type> declaredTypes)
        : base(logger, pipeline)
    {
        _stored = stored ?? throw new ArgumentNullException(nameof(stored));
        _declaredTypes = declaredTypes;
    }

    /// <summary>
    /// Reads a stored value as the type asked for, in the way the device
    /// detection engine does. A name the constructor was not given is read
    /// from the base dictionary as usual.
    /// </summary>
    /// <typeparam name="T">
    /// The type to read the value as.
    /// </typeparam>
    /// <param name="key">
    /// The name of the property.
    /// </param>
    /// <returns>
    /// The value.
    /// </returns>
    protected override T GetAs<T>(string key)
    {
        if (key == null || _stored.TryGetValue(key, out var text) == false)
        {
            return base.GetAs<T>(key);
        }
        if (typeof(T) == typeof(AspectPropertyValue<string>))
        {
            return (T)(object)new AspectPropertyValue<string>(text);
        }
        if (typeof(T) == typeof(AspectPropertyValue<bool>))
        {
            return (T)(object)Coerced(text);
        }
        // An untyped read gives back the type the element declares the
        // property as, which is how an engine answers the indexer.
        if (typeof(T) == typeof(object))
        {
            if (_declaredTypes != null &&
                _declaredTypes.TryGetValue(key, out var declared) &&
                declared == typeof(string))
            {
                return (T)(object)new AspectPropertyValue<string>(text);
            }
            return (T)(object)Coerced(text);
        }
        return base.GetAs<T>(key);
    }

    /// <summary>
    /// The stored text read as a Boolean the way the native conversion
    /// reads it, giving True only for the literal "True" and reporting a
    /// value either way.
    /// </summary>
    /// <param name="text">
    /// The stored text.
    /// </param>
    /// <returns>
    /// The converted value.
    /// </returns>
    private static AspectPropertyValue<bool> Coerced(string text)
    {
        return new AspectPropertyValue<bool>(
            string.Equals(text, "True", StringComparison.Ordinal));
    }
}

/// <summary>
/// Element publishing <see cref="CoercingSourceData"/> under a chosen
/// element data key, so a test can read a source property that behaves the
/// way a Boolean device detection property does.
/// </summary>
public class CoercingSourceElement
    : FlowElementBase<CoercingSourceData, ElementPropertyMetaData>
{
    private readonly string _elementDataKey;

    private readonly IReadOnlyDictionary<string, string> _stored;

    private readonly IReadOnlyDictionary<string, Type> _declaredTypes;

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
    /// <param name="stored">
    /// The text the data holds for each property name.
    /// </param>
    /// <param name="declaredTypes">
    /// The type to declare each property as. A name with no entry is
    /// declared as Boolean, which is what the properties this element
    /// exists to imitate are declared as.
    /// </param>
    public CoercingSourceElement(
        ILogger<FlowElementBase<CoercingSourceData, ElementPropertyMetaData>>
            logger,
        string elementDataKey,
        IReadOnlyDictionary<string, string> stored,
        IReadOnlyDictionary<string, Type> declaredTypes = null)
        : base(logger)
    {
        _elementDataKey = elementDataKey ??
            throw new ArgumentNullException(nameof(elementDataKey));
        _stored = stored ??
            throw new ArgumentNullException(nameof(stored));
        _declaredTypes = declaredTypes;
        var properties = new List<ElementPropertyMetaData>();
        foreach (var name in _stored.Keys)
        {
            var type = typeof(bool);
            if (declaredTypes != null &&
                declaredTypes.TryGetValue(name, out var declared))
            {
                type = declared;
            }
            properties.Add(
                new ElementPropertyMetaData(this, name, type, true));
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
    /// One entry per property name the element declares.
    /// </summary>
    public override IList<ElementPropertyMetaData> Properties => _properties;

    /// <summary>
    /// Nothing managed to clean up.
    /// </summary>
    protected override void ManagedResourcesCleanup()
    {
    }

    /// <summary>
    /// Adds the element data. The values are held by the data itself and
    /// converted as they are read, so nothing is written here.
    /// </summary>
    /// <param name="data">
    /// The flow data to add the element data to.
    /// </param>
    protected override void ProcessInternal(IFlowData data)
    {
        data.GetOrAdd(
            ElementDataKey,
            p => new CoercingSourceData(
                null, p, _stored, _declaredTypes));
    }

    /// <summary>
    /// Nothing unmanaged to clean up.
    /// </summary>
    protected override void UnmanagedResourcesCleanup()
    {
    }
}
