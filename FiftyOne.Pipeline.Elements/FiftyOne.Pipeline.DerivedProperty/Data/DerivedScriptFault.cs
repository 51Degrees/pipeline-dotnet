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

using FiftyOne.Pipeline.Core.Exceptions;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace FiftyOne.Pipeline.DerivedProperty.Data
{
    /// <summary>
    /// One thing wrong with a script. Validation collects every fault rather
    /// than stopping at the first, so an author sees everything wrong with a
    /// file in one go.
    /// </summary>
    public sealed class DerivedScriptFault
    {
        /// <summary>
        /// Create a new instance.
        /// </summary>
        /// <param name="script">
        /// The name of the script, or null where the script has no readable
        /// name.
        /// </param>
        /// <param name="source">
        /// Where the script came from, being a built in name, a file path,
        /// or the word code.
        /// </param>
        /// <param name="path">
        /// The place in the document, such as Rules[3].When.All[1], or an
        /// empty string for the document as a whole.
        /// </param>
        /// <param name="line">
        /// The one based line, or zero where the line is not known.
        /// </param>
        /// <param name="message">What is wrong, in plain words.</param>
        public DerivedScriptFault(
            string script,
            string source,
            string path,
            int line,
            string message)
        {
            Script = script;
            Source = source;
            Path = path;
            Line = line;
            Message = message;
        }

        /// <summary>
        /// The name of the script, or null where the script has no readable
        /// name.
        /// </summary>
        public string Script { get; }

        /// <summary>
        /// Where the script came from, being a built in name, a file path,
        /// or the word code.
        /// </summary>
        public string Source { get; }

        /// <summary>
        /// The place in the document, such as Rules[3].When.All[1], or an
        /// empty string for the document as a whole.
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// The one based line, or zero where the line is not known.
        /// </summary>
        public int Line { get; }

        /// <summary>What is wrong, in plain words.</summary>
        public string Message { get; }

        /// <summary>
        /// The fault as one line, naming the script, the source, the line
        /// and the place.
        /// </summary>
        /// <returns>The line.</returns>
        public override string ToString()
        {
            var where = string.IsNullOrEmpty(Path) ? "(document)" : Path;
            var line = Line > 0
                ? string.Format(
                    CultureInfo.InvariantCulture, " line {0}", Line)
                : string.Empty;
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} ({1}){2} at {3}: {4}",
                Script ?? "script",
                Source,
                line,
                where,
                Message);
        }
    }

    /// <summary>
    /// Raised where one or more scripts do not validate. The message lists
    /// every fault, one per line, so a build failure says everything that
    /// has to be fixed rather than only the first thing found.
    /// </summary>
    public class DerivedScriptValidationException : PipelineConfigurationException
    {
        private static readonly IReadOnlyList<DerivedScriptFault> _none =
            new List<DerivedScriptFault>();

        /// <summary>
        /// Create a new instance with no faults.
        /// </summary>
        public DerivedScriptValidationException() : base()
        {
            Faults = _none;
        }

        /// <summary>
        /// Create a new instance.
        /// </summary>
        /// <param name="message">What went wrong.</param>
        public DerivedScriptValidationException(string message)
            : base(message)
        {
            Faults = _none;
        }

        /// <summary>
        /// Create a new instance.
        /// </summary>
        /// <param name="message">What went wrong.</param>
        /// <param name="innerException">The failure underneath.</param>
        public DerivedScriptValidationException(
            string message,
            Exception innerException)
            : base(message, innerException)
        {
            Faults = _none;
        }

        /// <summary>
        /// Create a new instance listing every fault found.
        /// </summary>
        /// <param name="faults">The faults.</param>
        public DerivedScriptValidationException(
            IReadOnlyList<DerivedScriptFault> faults)
            : base(BuildMessage(faults))
        {
            Faults = faults ?? _none;
        }

        /// <summary>
        /// Every fault found, in the order they were found.
        /// </summary>
        public IReadOnlyList<DerivedScriptFault> Faults { get; }

        private static string BuildMessage(
            IReadOnlyList<DerivedScriptFault> faults)
        {
            if (faults == null || faults.Count == 0)
            {
                return "A derived property script did not validate.";
            }
            var builder = new StringBuilder();
            builder.AppendFormat(
                CultureInfo.InvariantCulture,
                "{0} {1} found while reading derived property scripts.",
                faults.Count,
                faults.Count == 1 ? "fault was" : "faults were");
            foreach (var fault in faults)
            {
                builder.AppendLine();
                builder.Append(fault.ToString());
            }
            return builder.ToString();
        }

        /// <summary>
        /// The faults as one line each, without the leading count.
        /// </summary>
        /// <param name="faults">The faults.</param>
        /// <returns>The lines.</returns>
        public static string Describe(
            IReadOnlyList<DerivedScriptFault> faults)
        {
            if (faults == null)
            {
                return string.Empty;
            }
            return string.Join(
                Environment.NewLine,
                faults.Select(f => f.ToString()));
        }
    }
}
