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

using FiftyOne.Pipeline.Core.Attributes;
using FiftyOne.Pipeline.Core.Configuration;
using FiftyOne.Pipeline.Core.Exceptions;
using FiftyOne.Pipeline.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace FiftyOne.Pipeline.Core.FlowElements
{
    /// <summary>
    /// Default pipeline builder.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/conceptual-overview.md#pipeline-builder">Specification</see>
    /// </summary>
    public class PipelineBuilder : PipelineBuilderBase<PipelineBuilder>,
        IPipelineBuilderFromConfiguration
    {
        /// <summary>
        /// A list of all the types that are element builders.
        /// I.e. they have build method (with or without parameters) 
        /// that returns an IFlowElement.
        /// </summary>
        private List<Type> _elementBuilders;

        /// <summary>
        /// Service collection which contains builder instances.
        /// </summary>
        private IServiceProvider _services = null;

        /// <summary>
        /// Create a new <see cref="PipelineBuilder"/> instance.
        /// </summary>
        /// <param name="loggerFactory">
        /// The <see cref="ILoggerFactory"/> to use when creating logger
        /// instances.
        /// </param>
        public PipelineBuilder(ILoggerFactory loggerFactory) 
            : base(loggerFactory)
        {
            GetAvailableElementBuilders();
        }

        /// <summary>
        /// Create a new <see cref="PipelineBuilder"/> instance.
        /// </summary>
        /// <param name="loggerFactory">
        /// The <see cref="ILoggerFactory"/> to use when creating logger
        /// instances.
        /// </param>
        /// <param name="services">
        /// Collection of services which contain builder instances for the
        /// required elements.
        /// </param>
        public PipelineBuilder(ILoggerFactory loggerFactory, 
            IServiceProvider services)
            : this(loggerFactory)
        {
            // If we're using a FiftyOneServiceProvider and it does not have an ILoggerFactory
            // instance then add one. This is to provide backward compatibility.
            if (services is FiftyOneServiceProvider fiftyOneProvider &&
                fiftyOneProvider.GetService(typeof(ILoggerFactory)) == null)
            {
                fiftyOneProvider.AddService(loggerFactory);
            }
            _services = services;
        }

        /// <summary>
        /// Build the pipeline using the specified configuration options.
        /// </summary>
        /// <param name="options">
        /// A <see cref="PipelineOptions"/> instance describing how to build
        /// the pipeline.
        /// </param>
        /// <returns>
        /// A new <see cref="IPipeline"/> instance containing the configured
        /// <see cref="IFlowElement"/> instances.
        /// </returns>
        /// <exception cref="PipelineConfigurationException"></exception>
        public virtual IPipeline BuildFromConfiguration(PipelineOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            // Clear the list of flow elements ready to be populated
            // from the configuration options.
            FlowElements.Clear();
            int counter = 0;

            try
            {
                foreach (var elementOptions in options.Elements)
                {
                    if (elementOptions.SubElements != null &&
                        elementOptions.SubElements.Count > 0)
                    {
                        // The configuration has sub elements so create
                        // a ParallelElements instance.
                        AddParallelElementsToList(FlowElements, elementOptions, counter);
                    }
                    else
                    {
                        // The configuration has no sub elements so create
                        // a flow element.
                        AddElementToList(FlowElements, elementOptions,
                            $"element {counter}");
                    }
                    counter++;
                }

                // Process any additional parameters for the pipeline
                // builder itself.
                ProcessBuildParameters(
                    options.BuildParameters,
                    GetType(),
                    this,
                    "pipeline");
            }
            catch (PipelineConfigurationException ex)
            {
                Logger.LogCritical(ex, Messages.MessagePipelineCreationFailed);
                throw;
            }
            // As the elements are all created within the builder, the user
            // will not be handling disposal so make sure the pipeline is
            // configured to do so.
            SetAutoDisposeElements(true);

            // Build and return the pipeline using the list of flow elements
            // that have been created from the configuration options.
            return Build();
        }

        /// <summary>
        /// Use reflection to get all element builders.
        /// These are defined as any type that has a Build method 
        /// where the return type is or implements IFlowElement.
        /// These will be used when building a pipeline from configuration.
        /// </summary>
        private void GetAvailableElementBuilders()
        {
            _elementBuilders = new List<Type>();
            // Get all loaded types where there is at least one..
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()
#if DEBUG
                // Exclude VisualStudio assemblies
                .Where(a => !a.FullName.StartsWith("Microsoft.VisualStudio", 
                    StringComparison.OrdinalIgnoreCase))
#endif
                )
            {
                // Exclude dynamic assemblies
                if (assembly.IsDynamic == false)
                {
                    try
                    {
                        _elementBuilders.AddRange(assembly.GetTypes()
                            .Where(t => t.GetMethods()
                                // ..method called 'Build'..
                                .Any(m => m.Name == "Build" &&
                                // ..where the return type is or implements IFlowElement
                                (m.ReturnType == typeof(IFlowElement) ||
                                m.ReturnType.GetInterfaces().Contains(typeof(IFlowElement)))))
                        .ToList());
                    }
                    // Catch type load exceptions when assembly can't be loaded 
                    // and log a warning. 
                    catch (ReflectionTypeLoadException ex)
                    {
                        if (Logger.IsEnabled(LogLevel.Debug))
                        {
                            Logger.LogDebug(ex, $"Failed to get Types for {assembly.FullName}", null);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Create a new <see cref="IFlowElement"/> using the specified
        /// <see cref="ElementOptions"/> and add it to the supplied list
        /// of elements.
        /// </summary>
        /// <param name="elements">
        /// The list to add the new <see cref="IFlowElement"/> to.
        /// </param>
        /// <param name="elementOptions">
        /// The <see cref="ElementOptions"/> instance to use when creating
        /// the <see cref="IFlowElement"/>.
        /// </param>
        /// <param name="elementLocation">
        /// The string description of the element's location within the 
        /// <see cref="PipelineOptions"/> instance.
        /// </param>
        private void AddElementToList(
            List<IFlowElement> elements,
            ElementOptions elementOptions,
            string elementLocation)
        {
            // Check that a builder name is set
            if (string.IsNullOrEmpty(elementOptions.BuilderName))
            {
                throw new PipelineConfigurationException(
                    $"A BuilderName must be specified for " +
                    $"{elementLocation}.");
            }

            // Try to get the builder to use
            var builderType = GetBuilderType(elementOptions.BuilderName);
            if (builderType == null)
            {
                throw new PipelineConfigurationException(
                    $"Could not find builder matching " +
                    $"'{elementOptions.BuilderName}' for " +
                    $"{elementLocation}. Available builders are: " +
                    $"{string.Join(",", _elementBuilders.Select(t => t.Name))}");
            }

            // Get the methods on the builder
            var buildMethods = builderType.GetRuntimeMethods()
                .Where(m => m.Name == "Build");
            // If there are no 'Build' methods or if there is no default 
            // constructor then throw an error.
            if (buildMethods.Any() == false)
            {
                throw new PipelineConfigurationException(
                    $"Builder '{builderType.FullName}' for " +
                    $"{elementLocation} has no 'Build' methods.");
            }

            object builderInstance = null;
            if (_services != null)
            {
                // Try to get a a builder instance from the service collection.
                builderInstance = _services.GetRequiredService(builderType);
            }
            else
            {
                // A service collection does not exist in the builder, so try
                // to construct a builder instance from the assemblies
                // currently loaded.
                builderInstance = GetBuilderFromAssemlies(builderType);
                if (builderInstance == null)
                {
                    throw new PipelineConfigurationException(
                        $"Builder '{builderType.FullName}' for " +
                        $"{elementLocation} does not have a default constructor. " +
                        $"i.e. One that takes no parameters. Or a constructor " +
                        $"which takes an ILoggerFactory parameter.");
                }
            }
            // Holds a list of the names of parameters to pass to the 
            // build method when we're ready.
            List<string> buildParameterList = new List<string>();

            if (elementOptions.BuildParameters != null)
            {
                // Call any non-build methods on the builder to set optional
                // parameters.
                buildParameterList = ProcessBuildParameters(
                    elementOptions.BuildParameters,
                    builderType,
                    builderInstance,
                    elementLocation);
            }

            // At this point, all the optional methods on the builder 
            // have been called and we're ready to call the Build method.
            // If there are no matching build methods or multiple possible 
            // build methods based on our parameters then throw an exception.
            var possibleBuildMethods = buildMethods.Where(m =>
                m.GetParameters().Length == buildParameterList.Count &&
                m.GetParameters().All(p => buildParameterList.Contains(p.Name.ToUpperInvariant())));

            StringBuilder buildSignatures = new StringBuilder();
            buildSignatures.AppendLine();
            foreach (var method in buildMethods)
            {
                buildSignatures.AppendLine(string.Join(",",
                    method.GetParameters().Select(p =>
                    $"{p.Name} ({p.ParameterType.Name})")));
            }

            if (possibleBuildMethods.Any() == false)
            {
                StringBuilder methodSignatures = new StringBuilder();
                methodSignatures.AppendLine();
                // Build a list of the 'set' methods on this builder
                // along with their parameter.
                foreach (var method in builderType.GetRuntimeMethods()
                    // Include any methods that are:
                    // 1. public
                    // 2. have a single parameter
                    .Where(m => m.IsPublic && m.GetParameters().Length == 1
                        && m.DeclaringType.Name.ToUpperInvariant().Contains("BUILDER")))
                {
                    methodSignatures.AppendLine($"{method.Name} ({method.GetParameters()[0].GetType().Name})");
                }
                throw new PipelineConfigurationException(
                    $"Builder '{builderType.FullName}' for " +
                    $"{elementLocation} has no " +
                    $"'Set' methods or 'Build' methods that match " +
                    $"(case-insensitively) the following parameters: " +
                    $"{string.Join(",", buildParameterList)}. " +
                    $"The available configuration methods on this builder are: " +
                    $"{methodSignatures.ToString()}" +
                    $"The available 'Build' methods have the following signatures: " +
                    $"{buildSignatures.ToString()}");
            }
            else if (possibleBuildMethods.Count() > 1)
            {
                throw new PipelineConfigurationException(
                    $"Builder '{builderType.FullName}' for " +
                    $"{elementLocation} has multiple " +
                    $"'Build' methods that match the following parameters: " +
                    $"{string.Join(",", buildParameterList)}. " +
                    $"Matching method signatures are: {buildSignatures.ToString()}");
            }

            // Get the build method parameters and add the configured
            // values to the parameter list.
            List<object> parameters = new List<object>();
            var buildMethod = possibleBuildMethods.Single();
            var caseInsensitiveParameters = elementOptions.BuildParameters
                .ToDictionary(d => d.Key.ToUpperInvariant(), d => d.Value);
            foreach (var parameterInfo in buildMethod.GetParameters())
            {
                var paramType = parameterInfo.ParameterType;
                object paramValue = caseInsensitiveParameters[parameterInfo.Name.ToUpperInvariant()];
                if (paramType != typeof(string))
                {
                    paramValue = ParseToType(paramType,
                        (string)paramValue,
                        $"Method 'Build' on builder " +
                        $"'{builderType.FullName}' for " +
                        $"{elementLocation} expects a parameter of type " +
                        $"'{paramType.Name}'");
                }
                parameters.Add(paramValue);
            }
            // Call the build method with the parameters we set up above.
            object result = buildMethod.Invoke(builderInstance, parameters.ToArray());
            if (result == null)
            {
                throw new PipelineConfigurationException(
                    $"Failed to build {elementLocation} using " +
                    $"'{builderType.FullName}', reason unknown.");
            }
            IFlowElement element = result as IFlowElement;
            if (element == null)
            {
                throw new PipelineConfigurationException(
                    $"Failed to cast '{result.GetType().FullName}' to " +
                    $"'IFlowElement' for {elementLocation}");
            }

            // Add the element to the list.
            elements.Add(element);
        }

        /// <summary>
        /// Create a <see cref="ParallelElements"/> from the specified 
        /// configuration and add it to the _flowElements list.
        /// </summary>
        /// <param name="elements">
        /// The list to add the new <see cref="ParallelElements"/> to.
        /// </param>
        /// <param name="elementOptions">
        /// The <see cref="ElementOptions"/> instance to use when creating
        /// the <see cref="ParallelElements"/>.
        /// </param>
        /// <param name="elementIndex">
        /// The index of the element within the <see cref="PipelineOptions"/>.
        /// </param>
        private void AddParallelElementsToList(
            List<IFlowElement> elements,
            ElementOptions elementOptions,
            int elementIndex)
        {
            // Element contains further sub elements, this is not allowed.
            if (string.IsNullOrEmpty(elementOptions.BuilderName) == false ||
                (elementOptions.BuildParameters != null &&
                elementOptions.BuildParameters.Count > 0))
            {
                throw new PipelineConfigurationException(
                    $"ElementOptions {elementIndex} contains both " +
                    $"SubElements and other settings values. " +
                    $"This is invalid");
            }
            List<IFlowElement> parallelElements = new List<IFlowElement>();

            // Iterate through the sub elements, creating them and
            // adding them to the list.
            int subCounter = 0;
            foreach (var subElement in elementOptions.SubElements)
            {
                if (subElement.SubElements != null &&
                    subElement.SubElements.Count > 0)
                {
                    throw new PipelineConfigurationException(
                        $"ElementOptions {elementIndex} contains nested " +
                        $"sub elements. This is not supported.");
                }
                else
                {
                    AddElementToList(parallelElements, subElement,
                        $"element {subCounter} in element {elementIndex}");
                }
                subCounter++;
            }
            // Now we've created all the elements, create the 
            // ParallelElements instance and add it to the pipeline's 
            // elements.
            var parallelInstance = new ParallelElements(
                LoggerFactory.CreateLogger<ParallelElements>(),
                parallelElements.ToArray());
            elements.Add(parallelInstance);
        }

        /// <summary>
        /// Instantiate a new builder instance from the assemblies which are
        /// currently loaded.
        /// </summary>
        /// <param name="builderType">The type of builder to get</param>
        /// <returns></returns>
        private object GetBuilderFromAssemlies(Type builderType)
        {
            // Get the valid constructors. This means either a default
            // constructor, or a constructor taking a logger factory as an
            // argument.
            var defaultConstructors = builderType.GetConstructors()
                .Where(c => c.GetParameters().Length == 0);
            var loggerConstructors = builderType.GetConstructors()
                .Where(c => c.GetParameters().Length == 1 &&
                c.GetParameters()[0].ParameterType == typeof(ILoggerFactory));
            
            if (defaultConstructors.Any() == false &&
                loggerConstructors.Any() == false)
            {
                return null;
            }

            // Create the builder instance using the constructor with a logger
            // factory, or the default constructor if one taking a logger
            // factory is not available.
            if (loggerConstructors.Any())
            {
                return Activator.CreateInstance(builderType, LoggerFactory);
            }
            else
            {
                return Activator.CreateInstance(builderType);
            }
        }

        /// <summary>
        /// Call the non-build methods on the builder that configuration
        /// options have been supplied for.
        /// </summary>
        /// <remarks>
        /// Each method must take only one parameter and the parameter type
        /// must either be a string or have a 'TryParse' method available.
        /// </remarks>
        /// <param name="buildParameters">
        /// A dictionary containing the names of the methods to call and 
        /// the value to pass as a parameters.
        /// </param>
        /// <param name="builderType">
        /// The <see cref="Type"/> of the builder that is being used to
        /// create the <see cref="IFlowElement"/>.
        /// </param>
        /// <param name="builderInstance">
        /// The instance of the builder that is being used to create the
        /// <see cref="IFlowElement"/>.
        /// </param>
        /// <param name="elementConfigLocation">
        /// A string containing a description of the location of the 
        /// configuration for this element.
        /// This will be added to error messages to help the user identify
        /// any problems.
        /// </param>
        /// <returns>
        /// A list of the names of the entries from buildParameters that 
        /// are to be used as mandatory parameters to the Build method 
        /// rather than optional builder methods.
        /// </returns>
        private static List<string> ProcessBuildParameters(
            IDictionary<string, object> buildParameters,
            Type builderType,
            object builderInstance,
            string elementConfigLocation)
        {
            // Holds a list of the names of parameters to pass to the 
            // build method when we're ready.
            List<string> buildParameterList = new List<string>();

            foreach (var parameter in buildParameters)
            {
                // Check if the build parameter corresponds to a method
                // on the builder.
                var methods = GetMethods(parameter.Key, builderType);
                if (methods == null)
                {
                    // If not then add the parameter to the list of parameters
                    // to pass to the Build method instead. 
                    buildParameterList.Add(parameter.Key.ToUpperInvariant());
                }
                else
                {
                    var methodCalled = false;
                    int counter = 0;
                    while (methodCalled == false && counter < methods.Count)
                    {
                        var method = methods[counter];
                        counter++;
                        
                        // The parameter corresponds to a method on the builder
                        // so get the parameters associated with that method.
                        var methodParams = method.GetParameters();
                        var extensionMethodOffset = method.IsDefined(typeof(ExtensionAttribute)) ? 1 : 0;
                        if (methodParams.Length != extensionMethodOffset + 1)
                        {
                            throw new PipelineConfigurationException(
                                $"Method '{method.Name}' on builder " +
                                $"'{builderType.FullName}' for " +
                                $"{elementConfigLocation} takes " +
                                $"{(methodParams.Length == extensionMethodOffset ? "no parameters " : "more than one parameter. ")}" +
                                $"This is not supported.");
                        }
                        // Call any methods which relate to the build parameters
                        // supplied in the configuration.
                        try
                        {
                            TryParseAndCallMethod(
                                parameter.Value,
                                method,
                                builderType,
                                builderInstance,
                                elementConfigLocation);
                            methodCalled = true;
                        }
                        catch (PipelineConfigurationException)
                        {
                            if (counter == methods.Count)
                            {
                                throw;
                            }
                        }
                    }
                }
            }

            return buildParameterList;
        }

        /// <summary>
        /// Attempt to call a method on the builder using the parameter value
        /// provided. The value can be parsed to basic types (e.g. string or
        /// int) but complex types are not supported.
        /// </summary>
        /// <param name="paramValue">
        /// Value of the parameter to call the method with
        /// </param>
        /// <param name="method">Method to attempt to call</param>
        /// <param name="builderType">
        /// The <see cref="Type"/> of the builder that is being used to
        /// create the <see cref="IFlowElement"/>.
        /// </param>
        /// <param name="builderInstance">
        /// The instance of the builder that is being used to create the
        /// <see cref="IFlowElement"/>.
        /// </param>
        /// <param name="elementConfigLocation">
        /// A string containing a description of the location of the 
        /// configuration for this element.
        /// This will be added to error messages to help the user identify
        /// any problems.
        /// </param>
        private static void TryParseAndCallMethod(
            object paramValue,
            MethodInfo method,
            Type builderType,
            object builderInstance,
            string elementConfigLocation)
        {
            var isExtensionMethod = method.IsDefined(typeof(ExtensionAttribute));
            var paramType = method.GetParameters()[isExtensionMethod ? 1 : 0].ParameterType;
            string expectedTypeMessage =
                    $"Method '{method.Name}' on builder " +
                    $"'{builderType.FullName}' for " +
                    $"{elementConfigLocation} expects a parameter of type " +
                    $"'{paramType.Name}'";

            // If the method takes a string then we can just pass it
            // in. If not, we'll have to parse it to the required 
            // type.
            if (paramType != typeof(string))
            {
                var suppliedObjectIsString = paramValue.GetType() == typeof(string);
                var suppliedObjectIsArray = paramValue.GetType().IsArray;
                var suppliedObjectIsList = typeof(IList<string>).IsAssignableFrom(paramValue.GetType());
                var expectedObjectIsArray = paramType.IsArray;
                var expectedObjectIsList = typeof(IList<string>).IsAssignableFrom(paramType);

                if (suppliedObjectIsString == false &&
                    (suppliedObjectIsArray == true ||
                    suppliedObjectIsList == true))
                {
                    // Parameter value is a list or array type.
                    // Only allow this if the method also accepts 
                    // a list or array type.
                    if (expectedObjectIsArray == false &&
                        expectedObjectIsList == false)
                    {
                        throw new PipelineConfigurationException(
                            $"{expectedTypeMessage} but supplied object is " +
                            $"of type '{paramValue.GetType().Name}'.");
                    }
                    // If necessary, convert the supplied object to 
                    // the expected type.
                    if(suppliedObjectIsArray == true && 
                        expectedObjectIsArray == false)
                    {
                        paramValue = new List<string>((IEnumerable<string>)paramValue);
                    }
                    else if(suppliedObjectIsList == true && 
                        expectedObjectIsList == false)
                    {
                        paramValue = ((IList<string>)paramValue).ToArray();
                    }
                }
                else
                {
                    // Parameter value is not a list or array type so
                    // attempt to parse it to the required type.
                    paramValue = ParseToType(paramType,
                        paramValue.ToString(),
                        expectedTypeMessage);
                }
            }

            // Invoke the method on the builder, passing the parameter
            // value that was defined in the configuration.
            if (isExtensionMethod)
            {
                method.Invoke(null, new object[] { builderInstance, paramValue });
            }
            else
            {
                method.Invoke(builderInstance, new object[] { paramValue });
            }
        }

        /// <summary>
        /// Get the method associated with the given name.
        /// </summary>
        /// <param name="methodName">
        /// The name of the method to get.
        /// This is case insensitive and can be:
        /// 1. The exact method name
        /// 2. The method name with the text 'set' removed from the start.
        /// 3. An alternate name, as defined by an 
        /// <see cref="AlternateNameAttribute"/>
        /// </param>
        /// <param name="builderType">
        /// The builder type to try and find a matches for.
        /// </param>
        /// <returns>
        /// The <see cref="MethodInfo"/> of the matching method or null if no
        /// match could be found.
        /// (Note: This might be an extension method)
        /// </returns>
        private static List<MethodInfo> GetMethods(string methodName, Type builderType)
            => FindMethodSources(builderType).Select(sources
                => FindPotentialMethods(methodName.ToUpperInvariant(), sources)
                    .Where(potentialMethods => potentialMethods != null && potentialMethods.Any())
                    .Select(Enumerable.ToList)
                    .FirstOrDefault())
                .FirstOrDefault(methods => methods != null);

        private static IEnumerable<IEnumerable<MethodInfo>> FindMethodSources(Type builderType)
        {
            yield return builderType.GetMethods();
            yield return GetExtensionMethods(builderType);
        }

        private static IEnumerable<IEnumerable<MethodInfo>> FindPotentialMethods(string upperMethodName,
            IEnumerable<MethodInfo> methods)
        {
            // First try and find a method that matches the
            // supplied name exactly.

            yield return methods.Where(m => m.Name.ToUpperInvariant() == upperMethodName);

            // Next, try and find a method that matches the
            // supplied name with 'set' added to the start.

            string tempName = "SET" + upperMethodName;
            yield return methods.Where(m => m.Name.ToUpperInvariant() == tempName);

            // Finally, see if there is a method that has an
            // AlternateNameAttribute with a matching name.

            yield return methods.Where(method =>
            {
                try
                {
                    var attributes = method.GetCustomAttributes<AlternateNameAttribute>();
                    if (attributes.Any(a => a.Name.ToUpperInvariant() == upperMethodName))
                    {
                        return true;
                    }
                    if (attributes.Any(
                        a => a.Name.ToUpperInvariant() ==
                        "SET" + upperMethodName))
                    {
                        return true;
                    }
                }
                catch (NotSupportedException) { }
                catch (TypeLoadException) { }
                return false;
            });
        }

        private static IEnumerable<Assembly> GetRelevantAssemblies(Type extendedType)
        {
            for (Type nextType = extendedType; nextType != null; nextType = nextType.BaseType)
            {
                yield return nextType.Assembly;
            }
            foreach (var iface in extendedType.GetInterfaces())
            {
                yield return iface.Assembly;
            }
        }

        private static IEnumerable<MethodInfo> GetExtensionMethods(Type extendedType)
        {
            const BindingFlags bindingFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            var allExtensionMethods = GetRelevantAssemblies(extendedType)
                .Distinct()
                .SelectMany(assembly => assembly.GetTypes())
                .Where(type => type.IsSealed && !type.IsGenericType && !type.IsNested)
                .SelectMany(type => type.GetMethods(bindingFlags))
                .Where(method => method.IsDefined(typeof(ExtensionAttribute)));
            foreach (var method in allExtensionMethods)
            {
                var callParams = method.GetParameters();
                if (callParams[0].ParameterType.IsAssignableFrom(extendedType))
                {
                    yield return method;
                    continue;
                }
                if (method.IsGenericMethod && method.GetGenericArguments().Contains(callParams[0].ParameterType))
                {
                    if (method.GetGenericArguments().Length == 1)
                    {
                        MethodInfo specializedMethod = null;
                        try
                        {
                            specializedMethod = method.MakeGenericMethod(extendedType);
                        }
                        catch (ArgumentException)
                        {
                            // nop -- type extended by method is incompatible with builder type.
                        }
                        if (specializedMethod != null)
                        {
                            yield return specializedMethod;
                        }
                    }
                    continue;
                }
            }
        }

        /// <summary>
        /// Get the element builder associated with the given name.
        /// </summary>
        /// <param name="builderName">
        /// The name of the builder to get.
        /// This is case insensitive and can be:
        /// 1. The builder type name
        /// 2. The builder type name with the text 'builder' removed 
        /// from the end.
        /// 3. An alternate name, as defined by an 
        /// <see cref="AlternateNameAttribute"/>
        /// </param>
        /// <returns>
        /// The <see cref="Type"/> of the element builder or null if no
        /// match could be found.
        /// </returns>
        private Type GetBuilderType(string builderName)
        {
            int tries = 0;
            Type builderType = null;
            string upperBuilderName = builderName.ToUpperInvariant();

            while (tries < 3 && builderType == null)
            {
                IEnumerable<Type> potentialBuilders = null;

                switch (tries)
                {
                    case 0:
                        // First try and find a builder that matches the
                        // supplied name exactly.
                        potentialBuilders = _elementBuilders
                            .Where(t => t.Name.ToUpperInvariant() == upperBuilderName);
                        break;
                    case 1:
                        string tempName = upperBuilderName + "BUILDER";
                        // Next, try and find a builder that matches the
                        // supplied name with 'builder' added to the end.
                        potentialBuilders = _elementBuilders
                            .Where(t => t.Name.ToUpperInvariant() == tempName);
                        break;
                    case 2:
                        // Finally, see if there is a builder that has an
                        // AlternateNameAttribute with a matching name.
                        List<Type> builders = new List<Type>();
                        foreach (var builder in _elementBuilders)
                        {
                            try
                            {
                                var attributes = builder.GetCustomAttributes<AlternateNameAttribute>();
                                if (attributes.Any(a => a.Name.ToUpperInvariant() == upperBuilderName))
                                {
                                    builders.Add(builder);
                                }
                            }
                            catch (NotSupportedException) { }
                            catch (TypeLoadException) { }
                        }
                        potentialBuilders = builders;
                        break;
                    default:
                        break;
                }

                if (potentialBuilders != null)
                {
                    // If the name matches multiple builders at any stage 
                    // then throw an error.
                    if (potentialBuilders.Count() > 1)
                    {
                        throw new PipelineConfigurationException(
                            $"The flow element builder name '{builderName}'" +
                            $"matches multiple builders: " +
                            $"[{string.Join(",", potentialBuilders.Select(t => t.FullName))}].");
                    }

                    try
                    {
                        builderType = potentialBuilders.SingleOrDefault();
                    }
                    catch (InvalidOperationException)
                    {
                        builderType = null;
                    }
                }

                tries++;
            }

            return builderType;
        }
        
        private static object ParseToType(Type targetType, string value, string errorTextPrefix)
        {
            object result = null;

            if (typeof(IList<string>).IsAssignableFrom(targetType) ||
                targetType.IsArray)
            {
                try
                {
                    // We are populating a list or array type so parse the 
                    // string as a comma-separated list of values.
                    List<string> list = new List<string>(ParseCsv(value));
                    if (targetType.IsArray)
                    {
                        result = list.ToArray();
                    }
                    else
                    {
                        result = list;
                    }
                }
                catch (Exception ex)
                {
                    throw new PipelineConfigurationException(
                        $"Failed to parse list value '{value}'. " +
                        $"This is expected to contain a comma-delimited list " +
                        $"of string values. Double quotes (\") may be used " +
                        $"to encapsulate values. Double quotes can be escaped " +
                        $"within a quoted value by using two in a row (For " +
                        $"example a list of text values containing 6 inches " +
                        $"and 12 inches would look like this: " +
                        $"\"6\"\"\",\"12\"\"\")", ex);
                }
            }
            else
            {
                // Check for a TryParse method on the type
                MethodInfo tryParse = null;
                try
                {
                    tryParse = targetType.GetMethods()
                        .SingleOrDefault(m => m.Name == "TryParse" &&
                            m.GetParameters()[0].ParameterType == typeof(string) &&
                            m.GetParameters().Length == 2);
                }
                catch (InvalidOperationException)
                {
                    tryParse = null;
                }

                if (tryParse == null)
                {
                    throw new PipelineConfigurationException(
                        $"{errorTextPrefix} but this type does not have " +
                        $"the expected 'TryParse' method " +
                        $"(or has multiples that cannot be resolved).");
                }
                // Call the try parse method.
                object[] args = new object[] { value, null };
                if ((bool)tryParse.Invoke(null, args) == false)
                {
                    throw new PipelineConfigurationException(
                        $"{errorTextPrefix}. Failed to parse value " +
                        $"'{value}' using the static " +
                        $"'TryParse' method.");
                }
                else
                {
                    result = args[1];
                }
            }

            return result;
        }
        
        private static IEnumerable<string> ParseCsv(string csv)
        {
            int pos = 0;
            bool inquotes = false;

            // Process the string one character at a time.
            while (pos < csv.Length)
            {
                if (csv[pos] == '"')
                {
                    // Remove these quotes
                    csv = csv.Remove(pos, 1);
                    if (inquotes)
                    {
                        // We're already in a quoted region so
                        // check if this is a double double quote.
                        // If so, we retain the second quotes and simply 
                        // continue to advance within the quoted region.
                        if (csv.Length > pos &&
                            csv[pos] == '"')
                        {
                        }
                        // If it's not a double double quote then
                        // change the state to indicate we are out of 
                        // the quoted region.
                        else
                        {
                            inquotes = false;
                            pos--;
                        }
                    }
                    else
                    {
                        // Change the state so we know we're inside
                        // a quoted region.
                        inquotes = true;
                        pos--;
                    }
                }
                else if (inquotes == false && csv[pos] == ',')
                {
                    // We're not within a quoted region and we've hit
                    // a comma delimiter so return the string item
                    // and trim the remaining string to remove it
                    // and the comma.
                    yield return csv.Remove(pos);
                    if (csv.Length > pos + 1)
                    {
                        csv = csv.Substring(pos + 1);
                        pos = -1;
                    }
                    else
                    {
                        csv = "";
                    }
                }
                // Advance to the next character position.
                pos++;
            }

            // Return the last item.
            if (csv.Length > 0)
            {
                yield return csv;
            }
        }
    }

}
