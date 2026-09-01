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

using System;

namespace FiftyOne.Pipeline.AgentSignature.Keys
{
    /// <summary>
    /// The outcome of one attempt to obtain an agent's keys, whether the
    /// attempt worked or not.
    /// </summary>
    /// <remarks>
    /// A failed fetch is carried in an entry rather than thrown out of the
    /// fetch task, so that the task a request waits on never faults and a
    /// failure can be remembered for the negative cache lifetime.
    /// </remarks>
    internal sealed class DirectoryEntry
    {
        /// <summary>
        /// True when the keys were obtained.
        /// </summary>
        public bool Success { get; }

        /// <summary>
        /// The keys, or null when the attempt failed.
        /// </summary>
        public KeyDirectory Directory { get; }

        /// <summary>
        /// The agent card, when the keys came from one, and null otherwise.
        /// </summary>
        public AgentCard Card { get; }

        /// <summary>
        /// When the attempt finished.
        /// </summary>
        public DateTimeOffset FetchedAt { get; }

        /// <summary>
        /// A short description of what went wrong, or null when the attempt
        /// worked. The fetcher writes the same description to the log as it
        /// builds the entry, so this carries it for the tests to read. It
        /// is deliberately never put into a property, because it is built
        /// partly from what a remote server sent.
        /// </summary>
        public string FailureReason { get; }

        /// <summary>
        /// The 'max-age' the response asked for, or null when it asked for
        /// none. A response asking for longer than the configured lifetime
        /// does not get it, because the cache takes whichever of the two is
        /// shorter. A set of keys already held may still answer for one
        /// further lifetime beyond that whilst refreshing it keeps failing.
        /// </summary>
        public TimeSpan? MaxAge { get; }

        private DirectoryEntry(
            bool success,
            KeyDirectory directory,
            AgentCard card,
            DateTimeOffset fetchedAt,
            string failureReason,
            TimeSpan? maxAge)
        {
            Success = success;
            Directory = directory;
            Card = card;
            FetchedAt = fetchedAt;
            FailureReason = failureReason;
            MaxAge = maxAge;
        }

        /// <summary>
        /// Create an entry for keys that were obtained.
        /// </summary>
        /// <param name="directory">The keys.</param>
        /// <param name="card">
        /// The agent card the keys came from, or null.
        /// </param>
        /// <param name="fetchedAt">When the attempt finished.</param>
        /// <param name="maxAge">
        /// The 'max-age' the response asked for, or null.
        /// </param>
        /// <returns>The entry.</returns>
        public static DirectoryEntry Succeeded(
            KeyDirectory directory,
            AgentCard card,
            DateTimeOffset fetchedAt,
            TimeSpan? maxAge)
        {
            return new DirectoryEntry(
                true, directory, card, fetchedAt, null, maxAge);
        }

        /// <summary>
        /// Create an entry for an attempt that failed.
        /// </summary>
        /// <param name="fetchedAt">When the attempt finished.</param>
        /// <param name="failureReason">
        /// A short description of what went wrong.
        /// </param>
        /// <returns>The entry.</returns>
        public static DirectoryEntry Failed(
            DateTimeOffset fetchedAt,
            string failureReason)
        {
            return new DirectoryEntry(
                false, null, null, fetchedAt, failureReason, null);
        }
    }
}
