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

using FiftyOne.Pipeline.Core.Attributes;
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.DerivedProperty.Data;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;

namespace FiftyOne.Pipeline.DerivedProperty.FlowElements
{
    /// <summary>
    /// Builds a <see cref="DerivedPropertyElement"/>.
    ///
    /// Scripts reach the builder three ways, being a script built into this
    /// package, a file in your own environment, or a string of script text
    /// from your own code. The three may be mixed in one element. There is
    /// no way to name a URL, because a script has to be readable at build
    /// and cannot change under a running pipeline.
    ///
    /// Once <see cref="Build"/> returns, the element holds only the
    /// compiled scripts. No path, no text and no reference to where a
    /// script came from is kept.
    /// </summary>
    public class DerivedPropertyElementBuilder
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<DerivedPropertyData> _dataLogger;
        private readonly List<DerivedScript> _scripts =
            new List<DerivedScript>();
        private readonly List<DerivedScriptFault> _faults =
            new List<DerivedScriptFault>();

        private static readonly Assembly _assembly =
            typeof(DerivedPropertyElementBuilder).GetTypeInfo().Assembly;

        /// <summary>
        /// Create a new instance.
        /// </summary>
        /// <param name="loggerFactory">
        /// How the element and its data make their loggers.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown where the logger factory is null.
        /// </exception>
        public DerivedPropertyElementBuilder(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory
                ?? throw new ArgumentNullException(nameof(loggerFactory));
            _dataLogger = _loggerFactory
                .CreateLogger<DerivedPropertyData>();
        }

        /// <summary>
        /// Add a script that ships inside this package.
        /// </summary>
        /// <param name="script">Which script to add.</param>
        /// <returns>The builder, so calls can be chained.</returns>
        [CodeConfigOnly]
        public DerivedPropertyElementBuilder AddScript(BuiltInScript script)
        {
            var index = (int)script;
            if (index < 0 || index >= BuiltInScripts.ResourceNames.Length)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(script),
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "'{0}' is not a script in this package. The scripts " +
                        "in this package are {1}.",
                        script,
                        BuiltInScripts.ResourceNames.Length == 0
                            ? "(none)"
                            : string.Join(
                                ", ",
                                Enum.GetNames(typeof(BuiltInScript)))));
            }
            return AddBuiltIn(script.ToString());
        }

        /// <summary>
        /// Add the scripts that ship inside this package, by name. This is
        /// what the configuration key Scripts binds to.
        /// </summary>
        /// <param name="names">The script names.</param>
        /// <returns>The builder, so calls can be chained.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown where the list is null.
        /// </exception>
        public DerivedPropertyElementBuilder SetScripts(List<string> names)
        {
            if (names == null)
            {
                throw new ArgumentNullException(nameof(names));
            }
            foreach (var name in names)
            {
                AddBuiltIn(name == null ? null : name.Trim());
            }
            return this;
        }

        /// <summary>
        /// Add a script file. A path may hold a wildcard, in which case
        /// every file matching it is added, in the same way the Translation
        /// engine builder takes its sources.
        /// </summary>
        /// <param name="path">The path to the file.</param>
        /// <returns>The builder, so calls can be chained.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown where the path is null.
        /// </exception>
        public DerivedPropertyElementBuilder AddScriptFile(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }
            if (path.IndexOf('*') >= 0 || path.IndexOf('?') >= 0)
            {
                var directory = Path.GetDirectoryName(path);
                if (string.IsNullOrWhiteSpace(directory))
                {
                    directory = Directory.GetCurrentDirectory();
                }
                var pattern = Path.GetFileName(path);
                if (Directory.Exists(directory) == false)
                {
                    _faults.Add(new DerivedScriptFault(
                        null,
                        path,
                        string.Empty,
                        0,
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "the folder '{0}' does not exist",
                            directory)));
                    return this;
                }
                // Ordered so that a wildcard gives the same element on
                // every machine, whatever order the file system lists in.
                foreach (var file in Directory
                    .GetFiles(directory, pattern)
                    .OrderBy(f => f, StringComparer.Ordinal))
                {
                    AddOneFile(file);
                }
                return this;
            }
            AddOneFile(path);
            return this;
        }

        /// <summary>
        /// Add script files by path. Wildcards are allowed. This is what
        /// the configuration key ScriptFiles binds to.
        /// </summary>
        /// <param name="paths">The paths.</param>
        /// <returns>The builder, so calls can be chained.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown where the list is null.
        /// </exception>
        public DerivedPropertyElementBuilder SetScriptFiles(
            List<string> paths)
        {
            if (paths == null)
            {
                throw new ArgumentNullException(nameof(paths));
            }
            foreach (var path in paths)
            {
                if (path != null)
                {
                    AddScriptFile(path.Trim());
                }
            }
            return this;
        }

        /// <summary>
        /// Add a script held as a string in your own code. YAML and JSON
        /// are both accepted and give the same element.
        /// </summary>
        /// <param name="name">
        /// The name to know the script by, which the script's own Name must
        /// equal.
        /// </param>
        /// <param name="content">The script text.</param>
        /// <returns>The builder, so calls can be chained.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown where the name or the content is null.
        /// </exception>
        [CodeConfigOnly]
        public DerivedPropertyElementBuilder AddScript(
            string name,
            string content)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }
            if (content == null)
            {
                throw new ArgumentNullException(nameof(content));
            }
            Read(content, name, "code");
            return this;
        }

        /// <summary>
        /// Validate and compile every script added, then build the element.
        /// </summary>
        /// <returns>The element.</returns>
        /// <exception cref="DerivedScriptValidationException">
        /// Thrown where any script does not validate. The message lists
        /// every fault in every script, one per line, so one build failure
        /// says everything that has to be fixed.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown where no script was added.
        /// </exception>
        public DerivedPropertyElement Build()
        {
            if (_faults.Count > 0)
            {
                throw new DerivedScriptValidationException(_faults);
            }
            if (_scripts.Count == 0)
            {
                throw new ArgumentNullException(
                    "scripts",
                    "At least one script must be configured. Add one with " +
                    "AddScript, AddScriptFile, or the Scripts and " +
                    "ScriptFiles configuration keys.");
            }
            return new DerivedPropertyElement(
                _scripts,
                _loggerFactory.CreateLogger<DerivedPropertyElement>(),
                CreateData);
        }

        // ---------------------------------------------------------------

        private IDerivedPropertyData CreateData(
            IPipeline pipeline,
            FlowElementBase<
                IDerivedPropertyData,
                IElementPropertyMetaData> flowElement)
        {
            return new DerivedPropertyData(_dataLogger, pipeline);
        }

        private DerivedPropertyElementBuilder AddBuiltIn(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                _faults.Add(new DerivedScriptFault(
                    null,
                    "built in",
                    string.Empty,
                    0,
                    "a built in script name is empty"));
                return this;
            }

            var resource = BuiltInScripts.ResourceNames.FirstOrDefault(
                r => string.Equals(
                    ScriptNameOf(r), name, StringComparison.OrdinalIgnoreCase));
            if (resource == null)
            {
                _faults.Add(new DerivedScriptFault(
                    name,
                    "built in",
                    string.Empty,
                    0,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "there is no script called '{0}' in this package. " +
                        "The scripts in this package are {1}. A script from " +
                        "your own environment is added with ScriptFiles " +
                        "rather than Scripts.",
                        name,
                        BuiltInScripts.ResourceNames.Length == 0
                            ? "(none)"
                            : string.Join(
                                ", ",
                                BuiltInScripts.ResourceNames
                                    .Select(ScriptNameOf)))));
                return this;
            }

            string text;
            using (var stream = _assembly.GetManifestResourceStream(resource))
            {
                if (stream == null)
                {
                    _faults.Add(new DerivedScriptFault(
                        name,
                        "built in",
                        string.Empty,
                        0,
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "the script '{0}' is named in this package but " +
                            "the resource '{1}' is not in the assembly",
                            name,
                            resource)));
                    return this;
                }
                using (var reader = new StreamReader(
                    stream, System.Text.Encoding.UTF8))
                {
                    text = reader.ReadToEnd();
                }
            }

            Read(text, ScriptNameOf(resource), "built in " + name);
            return this;
        }

        private void AddOneFile(string path)
        {
            FileInfo file;
            try
            {
                file = new FileInfo(path);
            }
            catch (Exception exception)
                when (exception is ArgumentException ||
                    exception is PathTooLongException ||
                    exception is NotSupportedException)
            {
                _faults.Add(new DerivedScriptFault(
                    null, path, string.Empty, 0,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "the path cannot be read: {0}", exception.Message)));
                return;
            }
            if (file.Exists == false)
            {
                _faults.Add(new DerivedScriptFault(
                    null, path, string.Empty, 0, "the file does not exist"));
                return;
            }

            string text;
            try
            {
                text = File.ReadAllText(file.FullName);
            }
            catch (IOException exception)
            {
                _faults.Add(new DerivedScriptFault(
                    null, path, string.Empty, 0,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "the file cannot be read: {0}", exception.Message)));
                return;
            }
            catch (UnauthorizedAccessException exception)
            {
                _faults.Add(new DerivedScriptFault(
                    null, path, string.Empty, 0,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "the file cannot be read: {0}", exception.Message)));
                return;
            }

            Read(
                text,
                Path.GetFileNameWithoutExtension(file.Name),
                file.FullName);
        }

        private void Read(string text, string name, string source)
        {
            var result = DerivedScriptValidator.Validate(text, name, source);
            if (result.IsValid == false)
            {
                _faults.AddRange(result.Faults);
                return;
            }
            _scripts.Add(result.Script);
        }

        /// <summary>
        /// The script name inside an embedded resource name, being the part
        /// between the prefix every script resource carries and the file
        /// extension.
        /// </summary>
        private static string ScriptNameOf(string resourceName)
        {
            const string prefix =
                "FiftyOne.Pipeline.DerivedProperty.Scripts.";
            var start = resourceName.StartsWith(
                prefix, StringComparison.Ordinal)
                ? prefix.Length
                : 0;
            var end = resourceName.LastIndexOf('.');
            return end > start
                ? resourceName.Substring(start, end - start)
                : resourceName.Substring(start);
        }
    }
}
