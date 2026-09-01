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

using FiftyOne.Caching;
using FiftyOne.Pipeline.AgentSignature.Parsing;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FiftyOne.Pipeline.AgentSignature.Keys
{
    /// <summary>
    /// What a lookup in the key directory cache produced.
    /// </summary>
    internal enum DirectoryLookupOutcome
    {
        /// <summary>
        /// An entry was available, whether it holds keys or a failure.
        /// </summary>
        Resolved,

        /// <summary>
        /// The fetch had not finished when the wait budget ran out. The
        /// fetch keeps running, so a later request finds the result.
        /// </summary>
        Pending,
    }

    /// <summary>
    /// Holds the key directories that have been fetched, bounded so that an
    /// agent cannot fill memory by sending a different 'Signature-Agent'
    /// value on every request.
    /// </summary>
    /// <remarks>
    /// A request waits for a fetch only for the wait budget. When the budget
    /// runs out the request reports the Timeout status whilst the fetch
    /// keeps running, so that the next request from the same agent finds the
    /// result. This follows the cloud ConfigurationElement, which does the
    /// same thing with a LoadingDictionary from the FiftyOne.Caching
    /// package. That class has no bound on its size, which is why this
    /// element wraps a least recently used cache around the same
    /// single-start trick rather than using it directly.
    /// </remarks>
    internal sealed class DirectoryCache : IDisposable
    {
        private readonly IPutCache<string, DirectorySlot> _cache;
        private readonly DirectoryFetcher _fetcher;
        private readonly Func<DateTimeOffset> _clock;
        private readonly TimeSpan _lifetime;
        private readonly TimeSpan _negativeLifetime;
        private readonly TimeSpan _waitBudget;
        private readonly TimeSpan _fetchTimeout;
        private bool _disposed;

        /// <summary>
        /// The number of fetches that have been started. The tests read this
        /// to check that one agent causes one fetch.
        /// </summary>
        public int FetchCount => _fetchCount;

        private int _fetchCount;

        /// <summary>
        /// Construct a cache.
        /// </summary>
        /// <param name="fetcher">The fetcher to obtain keys with.</param>
        /// <param name="clock">
        /// The source of the current time, which the tests replace.
        /// </param>
        /// <param name="size">The number of directories to hold.</param>
        /// <param name="lifetime">
        /// How long a fetched directory is reused for.
        /// </param>
        /// <param name="negativeLifetime">
        /// How long a failed fetch is remembered for.
        /// </param>
        /// <param name="waitBudget">
        /// How long a request waits for a fetch.
        /// </param>
        /// <param name="fetchTimeout">
        /// The time limit on a single fetch.
        /// </param>
        /// <param name="concurrency">
        /// The number of threads the cache is built to serve at once.
        /// </param>
        public DirectoryCache(
            DirectoryFetcher fetcher,
            Func<DateTimeOffset> clock,
            int size,
            TimeSpan lifetime,
            TimeSpan negativeLifetime,
            TimeSpan waitBudget,
            TimeSpan fetchTimeout,
            int concurrency)
        {
            _fetcher = fetcher;
            _clock = clock;
            _lifetime = lifetime;
            _negativeLifetime = negativeLifetime;
            _waitBudget = waitBudget;
            _fetchTimeout = fetchTimeout;
            // The lifetime is not handed to the cache builder, because an
            // entry the cache drops cannot be served whilst a fresh copy is
            // fetched. This class ages entries itself so that a stale
            // directory keeps working until its replacement arrives.
            //
            // The concurrency is held at or below the size because the cache
            // splits its room evenly across that many lists and never lets a
            // list hold nothing. A concurrency above the size would therefore
            // give one slot per list and hold more directories than the size
            // asks for, which would make SetCacheSize say something untrue.
            _cache = new LruPutCacheBuilder()
                .SetUpdateExisting(false)
                .SetConcurrency(Math.Max(1, Math.Min(concurrency, size)))
                .Build<string, DirectorySlot>(Math.Max(1, size));
        }

        /// <summary>
        /// Obtain the keys the given 'Signature-Agent' member leads to.
        /// </summary>
        /// <param name="agent">The 'Signature-Agent' member.</param>
        /// <param name="stopToken">
        /// The token that cancels when the request that is waiting has been
        /// abandoned.
        /// </param>
        /// <param name="entry">
        /// The entry found, which is null when the outcome is Pending.
        /// </param>
        /// <returns>Whether an entry was available.</returns>
        public DirectoryLookupOutcome Lookup(
            SignatureAgentEntry agent,
            CancellationToken stopToken,
            out DirectoryEntry entry)
        {
            if (agent.InlineDirectory != null)
            {
                // The agent carried the directory in the header itself, so
                // there is nothing to fetch and nothing to cache.
                entry = _fetcher.ReadInline(
                    agent.Value, agent.InlineDirectory);
                return DirectoryLookupOutcome.Resolved;
            }

            var slot = GetSlot(agent.KeyUrl, agent.Type);
            entry = slot.Resolve(
                _clock,
                _lifetime,
                _negativeLifetime,
                _waitBudget,
                stopToken);
            return entry == null
                ? DirectoryLookupOutcome.Pending
                : DirectoryLookupOutcome.Resolved;
        }

        private DirectorySlot GetSlot(string url, string type)
        {
            var slot = _cache[url];
            if (slot == null)
            {
                var created = new DirectorySlot(() => Load(url, type));
                // The cache is built not to replace an entry that is
                // already there, so every thread that races here reads back
                // the same slot and only that slot's fetch is started.
                _cache.Put(url, created);
                slot = _cache[url] ?? created;
            }
            return slot;
        }

        private async Task<DirectoryEntry> Load(string url, string type)
        {
            Interlocked.Increment(ref _fetchCount);
            try
            {
                using (var source = new CancellationTokenSource(_fetchTimeout))
                {
                    return await _fetcher
                        .FetchAsync(url, type, source.Token)
                        .ConfigureAwait(false);
                }
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception exception)
#pragma warning restore CA1031
            {
                // The catch is deliberately broad. A request reads this task
                // with Result, so a task that faulted would throw into the
                // pipeline and break the promise that the element never
                // throws whatever an agent sends. Any failure at all becomes
                // an entry saying the directory could not be obtained, which
                // reads as Unverified rather than as evidence against the
                // agent.
                return DirectoryEntry.Failed(_clock(), exception.Message);
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_disposed == false)
            {
                _disposed = true;
                _cache.Dispose();
            }
        }

        /// <summary>
        /// One agent's key directory, together with whatever fetch of it is
        /// running or has finished.
        /// </summary>
        private sealed class DirectorySlot
        {
            private readonly Func<Task<DirectoryEntry>> _loader;
            private Lazy<Task<DirectoryEntry>> _current;
            private DirectoryEntry _lastSuccess;

            public DirectorySlot(Func<Task<DirectoryEntry>> loader)
            {
                _loader = loader;
                _current = new Lazy<Task<DirectoryEntry>>(loader);
            }

            /// <summary>
            /// Produce the entry to answer a request with, starting or
            /// refreshing the fetch when one is needed.
            /// </summary>
            /// <returns>
            /// The entry, or null when the fetch had not finished within the
            /// wait budget.
            /// </returns>
            public DirectoryEntry Resolve(
                Func<DateTimeOffset> clock,
                TimeSpan lifetime,
                TimeSpan negativeLifetime,
                TimeSpan waitBudget,
                CancellationToken stopToken)
            {
                var current = Volatile.Read(ref _current);

                if (current.IsValueCreated == false &&
                    stopToken.IsCancellationRequested)
                {
                    // The request has already been abandoned, so starting a
                    // fetch for it would be work nobody is waiting for.
                    return null;
                }

                var task = current.Value;
                if (task.IsCompleted == false)
                {
                    return Wait(current, waitBudget, stopToken);
                }

                var entry = task.Result;
                Remember(entry);
                var limit = entry.Success
                    ? Shorter(lifetime, entry.MaxAge)
                    : negativeLifetime;
                if (clock() - entry.FetchedAt < limit)
                {
                    return Answer(entry);
                }

                StartRefresh(current);
                if (entry.Success)
                {
                    // Answer from what is already known whilst the fresh
                    // copy is fetched. The protocol draft is explicit that a
                    // directory that fails to resolve must not throw away a
                    // key that is already held.
                    return entry;
                }
                var known = Volatile.Read(ref _lastSuccess);
                if (known != null)
                {
                    return known;
                }
                return Wait(
                    Volatile.Read(ref _current), waitBudget, stopToken);
            }

            private DirectoryEntry Wait(
                Lazy<Task<DirectoryEntry>> lazy,
                TimeSpan waitBudget,
                CancellationToken stopToken)
            {
                var task = lazy.Value;
                bool completed;
                try
                {
                    completed = task.Wait(
                        (int)waitBudget.TotalMilliseconds, stopToken);
                }
                catch (OperationCanceledException)
                {
                    completed = false;
                }
                if (completed == false)
                {
                    // The fetch is left running so that the next request
                    // from this agent finds the result.
                    return null;
                }
                var entry = task.Result;
                Remember(entry);
                return Answer(entry);
            }

            /// <summary>
            /// Keep the last set of keys that was obtained, so that a later
            /// fetch which fails does not throw them away.
            /// </summary>
            private void Remember(DirectoryEntry entry)
            {
                if (entry.Success)
                {
                    Volatile.Write(ref _lastSuccess, entry);
                }
            }

            /// <summary>
            /// Answer a request with the entry given, falling back to the
            /// last set of keys that was obtained when the entry is a
            /// failure.
            /// </summary>
            private DirectoryEntry Answer(DirectoryEntry entry)
            {
                if (entry.Success)
                {
                    return entry;
                }
                return Volatile.Read(ref _lastSuccess) ?? entry;
            }

            private void StartRefresh(Lazy<Task<DirectoryEntry>> observed)
            {
                var replacement = new Lazy<Task<DirectoryEntry>>(_loader);
                Interlocked.CompareExchange(
                    ref _current, replacement, observed);
                // Reading the value starts the fetch. Whichever thread won
                // the exchange, the current lazy is the one to start, and
                // starting one that is already running does nothing.
                _ = Volatile.Read(ref _current).Value;
            }

            private static TimeSpan Shorter(
                TimeSpan lifetime,
                TimeSpan? maxAge)
            {
                if (maxAge.HasValue && maxAge.Value < lifetime)
                {
                    return maxAge.Value;
                }
                return lifetime;
            }
        }
    }
}
