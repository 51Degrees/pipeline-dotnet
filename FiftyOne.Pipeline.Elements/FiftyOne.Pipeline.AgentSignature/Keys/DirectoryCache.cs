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
    /// result. The cloud ConfigurationElement resolves resource key
    /// entitlements with the same shape, a dictionary of lazily started
    /// tasks from the FiftyOne.Caching package, and describes the same
    /// intent, although the LoadingDictionary the cloud uses discards a
    /// load when its own time limit fires where this cache keeps the fetch
    /// running. LoadingDictionary also has no bound on its size, so this
    /// element wraps a least recently used cache around the same way of
    /// starting one fetch rather than using LoadingDictionary itself.
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
        public int FetchCount => Volatile.Read(ref _fetchCount);

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
            // The cache key carries the member type as well as the URL,
            // because what a fetch does with the URL depends on the type,
            // and the sender writes both. Keyed on the URL alone, a
            // request naming another agent's key URL under the wrong type
            // would cache a failure under that URL and hold the real
            // agent's requests to the failure for the negative lifetime.
            var key = type + " " + url;
            // The cache is built not to replace an entry that is already
            // there, so every thread that races here reads back the same
            // slot and only that slot's fetch is started. The entry put in
            // can also be evicted before the read back when more origins
            // are hot than the cache holds, which is why the read back is
            // checked and tried again rather than assumed. Without the
            // retry, every requester in that window would quietly run its
            // own fetch whose result nothing else could share.
            for (var attempt = 0; attempt < 3; attempt++)
            {
                var slot = _cache[key];
                if (slot != null)
                {
                    return slot;
                }
                _cache.Put(key, new DirectorySlot(() => Load(url, type)));
                slot = _cache[key];
                if (slot != null)
                {
                    return slot;
                }
            }
            // Under this much pressure the entry cannot be kept in the
            // cache at all, so this request gets a slot of its own. The
            // fetch it starts serves only this request, which is the cost
            // of answering rather than a fault.
            return new DirectorySlot(() => Load(url, type));
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
                    return Wait(
                        current, clock, lifetime, waitBudget, stopToken);
                }

                var entry = ReadResult(task);
                Remember(entry);
                var limit = entry.Success
                    ? Shorter(lifetime, entry.MaxAge)
                    : negativeLifetime;
                if (clock() - entry.FetchedAt < limit)
                {
                    return Answer(entry, clock, lifetime);
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
                if (StillUsable(known, clock, lifetime))
                {
                    return known;
                }
                return Wait(
                    Volatile.Read(ref _current),
                    clock,
                    lifetime,
                    waitBudget,
                    stopToken);
            }

            private DirectoryEntry Wait(
                Lazy<Task<DirectoryEntry>> lazy,
                Func<DateTimeOffset> clock,
                TimeSpan lifetime,
                TimeSpan waitBudget,
                CancellationToken stopToken)
            {
                // A set of keys already held answers at once, without
                // waiting at all. This is what serves every request that
                // arrives whilst a refresh of a stale directory is still
                // running. Only the request that started the refresh would
                // otherwise see the copy held, and everyone else would sit
                // out the budget and report Timeout with a usable
                // directory to hand.
                var known = Volatile.Read(ref _lastSuccess);
                if (StillUsable(known, clock, lifetime))
                {
                    return known;
                }
                var task = lazy.Value;
                bool completed;
                try
                {
                    completed = task.Wait(
                        (int)Math.Min(
                            waitBudget.TotalMilliseconds,
                            int.MaxValue),
                        stopToken);
                }
                catch (OperationCanceledException)
                {
                    completed = false;
                }
                catch (AggregateException)
                {
                    // Wait throws what a faulted task holds, before the
                    // result is ever read. The task is finished, so fall
                    // through and let ReadResult turn the fault into a
                    // failure entry rather than letting an exception out
                    // of a header decide a request.
                    completed = true;
                }
                if (completed == false)
                {
                    // The fetch is left running so that the next request
                    // from this agent finds the result.
                    return null;
                }
                var entry = ReadResult(task);
                Remember(entry);
                return Answer(entry, clock, lifetime);
            }

            /// <summary>
            /// Read the result of a fetch that has finished.
            /// </summary>
            /// <remarks>
            /// The loader turns every failure it can see into an entry, so
            /// a task that faulted means something threw where nothing was
            /// expected to, such as the clock or the logger inside the
            /// loader's own catch. Reading such a task through Result
            /// would throw an AggregateException into the pipeline, and
            /// the pipeline passes that on to the caller unless the host
            /// has turned that off, so the request would fail because of a
            /// header. Answering with a failure entry keeps the promise
            /// that whatever an agent sends, the request still completes.
            /// </remarks>
            /// <param name="task">The finished fetch.</param>
            /// <returns>The entry it produced, or a failure entry.</returns>
            private static DirectoryEntry ReadResult(
                Task<DirectoryEntry> task)
            {
                try
                {
                    return task.Result;
                }
#pragma warning disable CA1031 // Do not catch general exception types
                catch (Exception)
#pragma warning restore CA1031
                {
                    // The time is deliberately the earliest one there is,
                    // so the entry counts as old at once and the next
                    // request for this agent starts a fresh fetch rather
                    // than being held to the negative cache lifetime.
                    return DirectoryEntry.Failed(
                        DateTimeOffset.MinValue,
                        "the fetch did not complete");
                }
            }

            /// <summary>
            /// Whether a set of keys obtained earlier may still answer a
            /// request. Keys held on to whilst a refresh keeps failing are
            /// given one further lifetime and no more. Taking a directory
            /// offline is how an agent withdraws a key that has been
            /// stolen, so keys that answer for ever after the directory
            /// stops responding would make that impossible.
            /// </summary>
            private static bool StillUsable(
                DirectoryEntry known,
                Func<DateTimeOffset> clock,
                TimeSpan lifetime)
            {
                return known != null &&
                    clock() - known.FetchedAt < lifetime + lifetime;
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
            private DirectoryEntry Answer(
                DirectoryEntry entry,
                Func<DateTimeOffset> clock,
                TimeSpan lifetime)
            {
                if (entry.Success)
                {
                    return entry;
                }
                var known = Volatile.Read(ref _lastSuccess);
                return StillUsable(known, clock, lifetime) ? known : entry;
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
