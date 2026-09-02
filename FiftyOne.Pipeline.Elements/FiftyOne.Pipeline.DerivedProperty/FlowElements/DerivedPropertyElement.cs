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
using FiftyOne.Pipeline.Core.Exceptions;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.DerivedProperty.Data;
using FiftyOne.Pipeline.Engines.Data;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace FiftyOne.Pipeline.DerivedProperty.FlowElements
{
    /// <summary>
    /// Computes new properties from properties other elements have already
    /// put into the flow data, following one or more scripts.
    ///
    /// The element holds no data file, makes no request and uses no resource
    /// key, so it is a plain flow element rather than an aspect engine.
    /// </summary>
    public class DerivedPropertyElement :
        FlowElementBase<IDerivedPropertyData, IElementPropertyMetaData>,
        IDerivedPropertyElement
    {
        /// <summary>
        /// The element data key every derived property element in a
        /// pipeline shares, in the same way every Translation engine shares
        /// the key translation.
        /// </summary>
        public const string DerivedElementDataKey = "derived";

        private readonly CompiledScript[] _compiled;
        private readonly DerivedScript[] _scripts;
        private readonly IList<IElementPropertyMetaData> _properties;
        private readonly IEvidenceKeyFilter _evidenceKeyFilter =
            new EvidenceKeyFilterWhitelist(new List<string>());

        /// <summary>
        /// Create a new instance.
        /// </summary>
        /// <param name="scripts">
        /// The scripts to run, already validated.
        /// </param>
        /// <param name="logger">The logger for the element.</param>
        /// <param name="elementDataFactory">
        /// How the element makes its element data.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown where no scripts were given.
        /// </exception>
        /// <exception cref="DerivedScriptValidationException">
        /// Thrown where two scripts write the same output property.
        /// </exception>
        public DerivedPropertyElement(
            IReadOnlyCollection<DerivedScript> scripts,
            ILogger<FlowElementBase<IDerivedPropertyData,
                IElementPropertyMetaData>> logger,
            Func<IPipeline,
                FlowElementBase<IDerivedPropertyData,
                    IElementPropertyMetaData>,
                IDerivedPropertyData> elementDataFactory)
            : base(logger, elementDataFactory)
        {
            if (scripts == null || scripts.Count == 0)
            {
                throw new ArgumentNullException(
                    nameof(scripts),
                    "At least one script must be configured.");
            }

            _scripts = scripts.ToArray();
            CheckOutputNamesAreUnique(_scripts);
            _compiled = _scripts.Select(s => new CompiledScript(s)).ToArray();

            // Only a script that creates a property contributes one here.
            // A script that replaces the value of a property another
            // element owns defines nothing, because that element already
            // published the definition, so advertising it here would be a
            // second definition of the same property.
            _properties = _scripts
                .Where(s => s.Output.IsOverride == false)
                .Select(s => (IElementPropertyMetaData)
                    new ElementPropertyMetaData(
                        this,
                        s.Output.Name,
                        s.Output.GetClrType(),
                        true,
                        s.Output.Category ?? string.Empty))
                .ToList();

            LogScripts();
        }

        /// <inheritdoc/>
        public override string ElementDataKey => DerivedElementDataKey;

        /// <summary>
        /// The element takes no evidence at all, because every input is a
        /// property another element has already produced, so the filter is
        /// empty.
        /// </summary>
        public override IEvidenceKeyFilter EvidenceKeyFilter =>
            _evidenceKeyFilter;

        /// <inheritdoc/>
        public override IList<IElementPropertyMetaData> Properties =>
            _properties;

        /// <inheritdoc/>
        public IReadOnlyList<DerivedScript> Scripts => _scripts;

        /// <summary>
        /// Checks the rest of the pipeline for the source properties every
        /// script needs, which cannot be done when the element is built
        /// because the element cannot see the pipeline then.
        /// </summary>
        /// <param name="pipeline">The pipeline being built.</param>
        /// <exception cref="PipelineConfigurationException">
        /// Thrown where a source property has no supplier earlier in the
        /// pipeline, where another element already writes one of the
        /// derived property names this element writes, or where a script
        /// replaces a property no earlier element produces or produces a
        /// value of a type that property is not declared as.
        /// </exception>
        public override void AddPipeline(IPipeline pipeline)
        {
            base.AddPipeline(pipeline);
            if (pipeline == null)
            {
                return;
            }
            CheckPipeline(pipeline);
        }

        /// <inheritdoc/>
        protected override void ProcessInternal(IFlowData data)
        {
            var derived = data.GetOrAdd(
                ElementDataKeyTyped,
                CreateElementData);
            for (var i = 0; i < _compiled.Length; i++)
            {
                _compiled[i].Process(data, derived);
            }
        }

        /// <inheritdoc/>
        protected override void ManagedResourcesCleanup()
        {
        }

        /// <inheritdoc/>
        protected override void UnmanagedResourcesCleanup()
        {
        }

        // ---------------------------------------------------------------
        // Build time checks and logging.
        // ---------------------------------------------------------------

        private static void CheckOutputNamesAreUnique(DerivedScript[] scripts)
        {
            var seen = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);
            var faults = new List<DerivedScriptFault>();
            foreach (var script in scripts)
            {
                // Compared on the qualified name, so a script creating
                // derived.IsCrawler and one replacing device.IsCrawler are
                // two different properties rather than a collision.
                if (seen.TryGetValue(
                    script.Output.QualifiedName, out var first))
                {
                    faults.Add(new DerivedScriptFault(
                        script.Name,
                        script.Source,
                        "Output.Name",
                        0,
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "two scripts in the same element write the " +
                            "property '{0}', being '{1}' and '{2}'. One " +
                            "element writes each property once",
                            // Named the way the script named it and the way
                            // the canonical form prints it, so a reader can
                            // find the line to change.
                            script.Output.IsOverride
                                ? script.Output.QualifiedName
                                : script.Output.Name,
                            first,
                            script.Name)));
                    continue;
                }
                seen.Add(script.Output.QualifiedName, script.Name);
            }
            if (faults.Count > 0)
            {
                throw new DerivedScriptValidationException(faults);
            }
        }

        private void LogScripts()
        {
            foreach (var script in _scripts)
            {
                Logger.LogInformation(
                    "Derived property script '{Name}' version {Version} " +
                    "(format {Format}) from {Source} produces '{Output}' " +
                    "as {Type}.",
                    script.Name,
                    script.Version,
                    script.Format,
                    script.Source,
                    script.Output.Name,
                    DerivedValueConverter.NameOf(script.Output.ValueType));

                if (script.Deprecated)
                {
                    Logger.LogWarning(
                        "Derived property script '{Name}' is deprecated. " +
                        "{Note}",
                        script.Name,
                        script.DeprecationNote);
                }

                if (Logger.IsEnabled(LogLevel.Debug))
                {
                    Logger.LogDebug(
                        "Derived property script '{Name}' compiled to " +
                        "{Model}",
                        script.Name,
                        DerivedScriptWriter.ToCanonicalJson(script));
                }
            }
        }

        /// <summary>
        /// The check from the design, run when the pipeline adds the
        /// element. The element can see every element in the pipeline at
        /// that point, including ones placed after itself, which is what
        /// lets a source property supplied too late be named rather than
        /// simply reported missing on every request.
        /// </summary>
        private void CheckPipeline(IPipeline pipeline)
        {
            IReadOnlyList<IFlowElement> elements;
            try
            {
                elements = pipeline.FlowElements;
            }
            catch (Exception exception)
                when (exception is PropertiesNotYetLoadedException ||
                    exception is PipelineTemporarilyUnavailableException)
            {
                // The pipeline cannot answer yet, so the check is skipped
                // rather than failing a build for a reason that has nothing
                // to do with the scripts.
                return;
            }
            if (elements == null)
            {
                return;
            }

            var position = IndexOfSelf(elements);
            var faults = new List<string>();

            CheckForCollisions(elements, faults);
            CheckSourceProperties(elements, position, faults);
            CheckOverrideTargets(elements, position, faults);

            if (faults.Count > 0)
            {
                var message = new StringBuilder();
                message.Append(
                    "The derived property element cannot be used in this " +
                    "pipeline.");
                foreach (var fault in faults)
                {
                    message.AppendLine();
                    message.Append(fault);
                }
                throw new PipelineConfigurationException(message.ToString());
            }
        }

        private int IndexOfSelf(IReadOnlyList<IFlowElement> elements)
        {
            for (var i = 0; i < elements.Count; i++)
            {
                if (ReferenceEquals(elements[i], this))
                {
                    return i;
                }
            }
            // Not finding the element means the pipeline is built in a way
            // this check does not understand, so everything is treated as
            // being earlier and no false failure is raised.
            return elements.Count;
        }

        /// <summary>
        /// Two elements writing the same derived property collide wherever
        /// they sit, because both write the one element data held under the
        /// derived key, so where the other element sits does not matter and
        /// no position is taken.
        /// </summary>
        private void CheckForCollisions(
            IReadOnlyList<IFlowElement> elements,
            List<string> faults)
        {
            foreach (var script in _scripts)
            {
                // A script that replaces a property another element owns
                // writes nothing under the derived key, so it cannot
                // collide with what another derived property element
                // writes there.
                if (script.Output.IsOverride)
                {
                    continue;
                }
                for (var i = 0; i < elements.Count; i++)
                {
                    var element = elements[i];
                    if (ReferenceEquals(element, this) ||
                        string.Equals(
                            element.ElementDataKey,
                            DerivedElementDataKey,
                            StringComparison.OrdinalIgnoreCase) == false)
                    {
                        continue;
                    }
                    if (Supplies(element, script.Output.Name))
                    {
                        faults.Add(string.Format(
                            CultureInfo.InvariantCulture,
                            "The script '{0}' writes the derived property " +
                            "'{1}', and another element in the pipeline " +
                            "already writes a property of that name. One " +
                            "pipeline writes each derived property once.",
                            script.Name,
                            script.Output.Name));
                    }
                }
            }
        }

        private void CheckSourceProperties(
            IReadOnlyList<IFlowElement> elements,
            int position,
            List<string> faults)
        {
            foreach (var script in _scripts)
            {
                foreach (var property in script.Properties)
                {
                    var earlier = false;
                    var later = new List<string>();

                    for (var i = 0; i < elements.Count; i++)
                    {
                        var element = elements[i];
                        if (ReferenceEquals(element, this))
                        {
                            continue;
                        }
                        if (string.Equals(
                            element.ElementDataKey,
                            property.ElementDataKey,
                            StringComparison.OrdinalIgnoreCase) == false)
                        {
                            continue;
                        }
                        if (Supplies(element, property.PropertyName) == false)
                        {
                            continue;
                        }
                        if (i < position)
                        {
                            earlier = true;
                            break;
                        }
                        later.Add(element.GetType().Name);
                    }

                    if (earlier)
                    {
                        continue;
                    }

                    // Every property a script names is needed, so a pipeline
                    // that cannot supply one would produce no value on every
                    // request. Failing the build says so at the point the
                    // mistake was made rather than on the first request.
                    faults.Add(later.Count > 0
                        ? string.Format(
                            CultureInfo.InvariantCulture,
                            "The script '{0}' needs the property '{1}', " +
                            "which is supplied by {2}, placed after the " +
                            "derived property element rather than before " +
                            "it. Move the derived property element after " +
                            "{2}.",
                            script.Name,
                            property.Name,
                            string.Join(", ", later))
                        : string.Format(
                            CultureInfo.InvariantCulture,
                            "The script '{0}' needs the property '{1}', " +
                            "and no element in the pipeline supplies it. " +
                            "Either add the element that supplies '{1}', " +
                            "or change the script so that it does not name " +
                            "'{1}'.",
                            script.Name,
                            property.Name));
                }
            }
        }

        /// <summary>
        /// A script that replaces the value of a property another element
        /// owns needs that element to be in the pipeline, to be placed
        /// before this one, and to declare the property as the type the
        /// script produces. None of the three can be known before the
        /// pipeline is built.
        ///
        /// An override that finds nothing to replace writes nothing on
        /// every request, and would do so silently, because leaving the
        /// owning element's value alone is exactly what an override does
        /// when it has nothing to say. Failing the build is therefore the
        /// only point at which the mistake can be seen.
        /// </summary>
        private void CheckOverrideTargets(
            IReadOnlyList<IFlowElement> elements,
            int position,
            List<string> faults)
        {
            foreach (var script in _scripts)
            {
                if (script.Output.IsOverride == false)
                {
                    continue;
                }

                IElementPropertyMetaData target = null;
                var later = new List<string>();

                for (var i = 0; i < elements.Count; i++)
                {
                    var element = elements[i];
                    if (ReferenceEquals(element, this))
                    {
                        continue;
                    }
                    if (string.Equals(
                        element.ElementDataKey,
                        script.Output.ElementDataKey,
                        StringComparison.OrdinalIgnoreCase) == false)
                    {
                        continue;
                    }
                    var supplied = Supplied(element, script.Output.Name);
                    if (supplied == null)
                    {
                        continue;
                    }
                    if (i < position)
                    {
                        target = supplied;
                        break;
                    }
                    later.Add(element.GetType().Name);
                }

                if (target == null)
                {
                    faults.Add(later.Count > 0
                        ? string.Format(
                            CultureInfo.InvariantCulture,
                            "The script '{0}' replaces the value of the " +
                            "property '{1}' in the element data '{2}', " +
                            "which is produced by {3}, placed after the " +
                            "derived property element rather than before " +
                            "it. Move the derived property element after " +
                            "{3}.",
                            script.Name,
                            script.Output.Name,
                            script.Output.ElementDataKey,
                            string.Join(", ", later))
                        : string.Format(
                            CultureInfo.InvariantCulture,
                            "The script '{0}' replaces the value of the " +
                            "property '{1}' in the element data '{2}', and " +
                            "no element in the pipeline produces '{3}'. " +
                            "Either add the element that produces it, or " +
                            "change Output.Name to '{1}' so that the " +
                            "script creates a property of its own.",
                            script.Name,
                            script.Output.Name,
                            script.Output.ElementDataKey,
                            script.Output.QualifiedName));
                    continue;
                }

                CheckOverrideType(script, target, faults);
            }
        }

        /// <summary>
        /// The type the owning element declares is the type every caller
        /// reads the property as, so a script that produces anything else
        /// would break those callers on the requests where it happened to
        /// have an answer. An element that declares no type has said
        /// nothing that can be checked, and nothing is checked.
        /// </summary>
        private static void CheckOverrideType(
            DerivedScript script,
            IElementPropertyMetaData target,
            List<string> faults)
        {
            var declared = UnwrapAspectPropertyValue(target.Type);
            if (declared == null)
            {
                return;
            }
            var produced = script.Output.GetClrType();
            if (declared == produced)
            {
                return;
            }
            faults.Add(string.Format(
                CultureInfo.InvariantCulture,
                "The script '{0}' replaces the value of the property " +
                "'{1}', which {2} declares as {3}, and the script produces " +
                "{4}. A script can only replace a property with a value of " +
                "the type the element that owns the property declares.",
                script.Name,
                script.Output.QualifiedName,
                target.Element == null
                    ? "the element that owns it"
                    : target.Element.GetType().Name,
                declared.Name,
                produced.Name));
        }

        /// <summary>
        /// The type a caller reads a property as. An element that hands
        /// back values able to say they have no value declares the wrapper,
        /// and the type a script has to match is the one inside it.
        /// </summary>
        private static Type UnwrapAspectPropertyValue(Type type)
        {
            if (type != null &&
                type.IsGenericType &&
                type.GetGenericTypeDefinition() ==
                    typeof(IAspectPropertyValue<>))
            {
                return type.GetGenericArguments()[0];
            }
            return type;
        }

        /// <summary>
        /// Whether an element says it produces a property, read from the
        /// element's own metadata.
        /// </summary>
        private static bool Supplies(IFlowElement element, string propertyName)
        {
            return Supplied(element, propertyName) != null;
        }

        /// <summary>
        /// The metadata an element publishes for a property, or null where
        /// the element does not produce one of that name. An element that
        /// cannot answer yet is read as producing nothing, which is all
        /// that can be made of an element which has not finished starting.
        /// </summary>
        private static IElementPropertyMetaData Supplied(
            IFlowElement element,
            string propertyName)
        {
            IList<IElementPropertyMetaData> properties;
            try
            {
                properties = element.Properties;
            }
            catch (Exception exception)
                when (exception is PropertiesNotYetLoadedException ||
                    exception is PipelineTemporarilyUnavailableException)
            {
                return null;
            }
            if (properties == null)
            {
                return null;
            }
            for (var i = 0; i < properties.Count; i++)
            {
                if (string.Equals(
                    properties[i].Name,
                    propertyName,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return properties[i];
                }
            }
            return null;
        }
    }
}
