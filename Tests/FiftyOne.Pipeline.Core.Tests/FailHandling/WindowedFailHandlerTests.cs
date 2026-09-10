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

using FiftyOne.Pipeline.Core.FailHandling.Facade;
using FiftyOne.Pipeline.Core.FailHandling.Recovery;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading;

namespace FiftyOne.Pipeline.Core.Tests
{
    /// <summary>
    /// Tests the trip semantics of <see cref="WindowedFailHandler"/>: the
    /// breaker enters recovery only when the configured number of failures
    /// occurs within the configured window (a failure rate), which is what
    /// keeps stray failures on a healthy cloud from tripping it.
    /// </summary>
    [TestClass]
    public class WindowedFailHandlerTests
    {
        // Long enough that a recorded failure keeps the strategy suppressing
        // for the whole test once the threshold trips it.
        private static SimpleRecoveryStrategy AlwaysSuppressAfterFailure
            => new SimpleRecoveryStrategy(recoverySeconds: 100);

        private static WindowedFailHandler MakeHandler(
            int threshold, TimeSpan window, IRecoveryStrategy strategy)
            => new WindowedFailHandler(
                strategy, threshold, window, NullLogger.Instance, "test");

        private static void RecordFailure(WindowedFailHandler handler)
        {
            using var scope = handler.MakeAttemptScope();
            scope.RecordFailure(new Exception("simulated cloud failure"));
        }

        /// <summary>
        /// True if the handler is currently suppressing requests (the
        /// breaker is open). CheckIfRecovered throws the exception the
        /// factory returns while suppressing, and returns true otherwise.
        /// </summary>
        private static bool IsSuppressing(WindowedFailHandler handler)
        {
            try
            {
                handler.CheckIfRecovered(
                    (msg, ex) => new InvalidOperationException(msg, ex));
                return false;
            }
            catch (InvalidOperationException)
            {
                return true;
            }
        }

        [TestMethod]
        public void DoesNotTripBelowThreshold_TripsAtThreshold()
        {
            const int threshold = 5;
            var handler = MakeHandler(
                threshold,
                TimeSpan.FromSeconds(100),
                AlwaysSuppressAfterFailure);

            for (int i = 1; i < threshold; i++)
            {
                RecordFailure(handler);
                Assert.IsFalse(
                    IsSuppressing(handler),
                    $"Breaker tripped after only {i} of {threshold} failures.");
            }

            RecordFailure(handler);
            Assert.IsTrue(
                IsSuppressing(handler),
                "Breaker did not trip once the failure threshold was reached.");
        }

        [TestMethod]
        public void FailuresSpreadBeyondWindowDoNotAccumulate()
        {
            const int threshold = 3;
            var window = TimeSpan.FromSeconds(1);
            var handler = MakeHandler(
                threshold, window, AlwaysSuppressAfterFailure);

            // Two failures inside the window: below threshold, no trip.
            RecordFailure(handler);
            RecordFailure(handler);
            Assert.IsFalse(IsSuppressing(handler));

            // Let those age out of the window.
            Thread.Sleep(TimeSpan.FromSeconds(1.3));

            // Two fresh failures: the aged-out ones must not count, so the
            // in-window total is two, still below the threshold.
            RecordFailure(handler);
            RecordFailure(handler);
            Assert.IsFalse(
                IsSuppressing(handler),
                "Failures spread beyond the window accumulated to the " +
                "threshold; the window is not being applied.");

            // A third fresh failure reaches the threshold within the window.
            RecordFailure(handler);
            Assert.IsTrue(
                IsSuppressing(handler),
                "Breaker did not trip once the threshold was reached within " +
                "the window.");
        }
    }
}
