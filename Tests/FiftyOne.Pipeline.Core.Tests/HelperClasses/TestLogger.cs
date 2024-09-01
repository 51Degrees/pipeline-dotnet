/* *********************************************************************
 * This Original Work is copyright of 51 Degrees Mobile Experts Limited.
 * Copyright 2023 51 Degrees Mobile Experts Limited, Davidson House,
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

using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;

namespace FiftyOne.Pipeline.Core.Tests.HelperClasses
{
    class TestLogger<TLog> : TestLogger, ILogger<TLog>
    {
    }

    /// <summary>
    /// Implementation of <see cref="ILogger"/> that will track errors and 
    /// warnings that are logged in order to later assert that no
    /// more than a certain number of errors/warnings have been logged.
    /// </summary>
    class TestLogger : ILogger
    {
        /// <summary>
        /// A list of the text of warnings that have been logged.
        /// </summary>
        public List<string> WarningsLogged { get; set; }

        /// <summary>
        /// A list of the text of errors and critical warnings that have been 
        /// logged.
        /// </summary>
        public List<string> ErrorsLogged { get; set; }

        public TestLogger()
        {
            WarningsLogged = new List<string>();
            ErrorsLogged = new List<string>();
        }

        /// <summary>
        /// Throw an AssertFailedException if more than the specified number
        /// of warnings have been logged.
        /// </summary>
        /// <param name="count">
        /// The maximum number of logged warnings to allow.
        /// </param>
        public void AssertMaxWarnings(int count)
        {
            if (WarningsLogged.Count > count)
            {
                var message = $"{WarningsLogged.Count} warnings occurred " +
                    "during test " +
                    $" {(count > 0 ? $"(expected no more than {count})" : "")}:";
                foreach (var warning in WarningsLogged)
                {
                    message += Environment.NewLine;
                    message += Environment.NewLine;
                    message += warning;
                }
                Assert.Fail(message);
            }
        }

        /// <summary>
        /// Throw an AssertFailedException if more than the specified number
        /// of errors have been logged.
        /// </summary>
        /// <param name="count">
        /// The maximum number of logged errors to allow.
        /// </param>
        public void AssertMaxErrors(int count)
        {
            if (ErrorsLogged.Count > count)
            {
                var message = $"{ErrorsLogged.Count} errors occurred during test" +
                    $"{(count > 0 ? $" (expected no more than {count})" : "")}:";
                foreach (var error in ErrorsLogged)
                {
                    message += Environment.NewLine;
                    message += Environment.NewLine;
                    message += error;
                }
                Assert.Fail(message);
            }
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            throw new NotImplementedException();
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            switch (logLevel)
            {
                case LogLevel.Trace:
                case LogLevel.Debug:
                case LogLevel.Information:
                    break;
                case LogLevel.Warning:
                    WarningsLogged.Add(formatter(state, exception));
                    break;
                case LogLevel.Error:
                case LogLevel.Critical:
                    ErrorsLogged.Add(formatter(state, exception));
                    break;
                case LogLevel.None:
                    break;
                default:
                    break;
            }
        }
    }
}
