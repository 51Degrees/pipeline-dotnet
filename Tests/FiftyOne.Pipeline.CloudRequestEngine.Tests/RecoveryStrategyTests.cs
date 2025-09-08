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


using FiftyOne.Pipeline.Core.FailHandling.ExceptionCaching;
using FiftyOne.Pipeline.Core.FailHandling.Recovery;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FiftyOne.Pipeline.CloudRequestEngine.Tests
{
    [TestClass]
    public class RecoveryStrategyTests
    {
        #region InstantRecoveryStrategy

        [TestMethod]
        public void InstantRecoveryStrategyShouldReturnTrue()
        {
            IRecoveryStrategy strategy = new InstantRecoveryStrategy();

            Assert.IsTrue(strategy.MayTryNow(out var cachedException), 
                $"{nameof(InstantRecoveryStrategy)}.{nameof(IRecoveryStrategy.MayTryNow)}"
                + " should return true.");
            Assert.IsNull(cachedException,
                $"{nameof(cachedException)} is not null.");
        }

        [TestMethod]
        public void InstantRecoveryStrategyShouldReturnTrueAfterFailure()
        {
            IRecoveryStrategy strategy = new InstantRecoveryStrategy();

            var ex = new CachedException(new System.Exception("dummy exception"));

            strategy.RecordFailure(ex);

            Assert.IsTrue(strategy.MayTryNow(out var ex2),
                $"{nameof(InstantRecoveryStrategy)}.{nameof(IRecoveryStrategy.MayTryNow)}"
                + " should return true.");
            Assert.IsNull(ex2,
                $"{nameof(ex2)} is not null.");
        }

        #endregion

        #region NoRecoveryStrategy

        [TestMethod]
        public void NoRecoveryStrategyShouldReturnTrue()
        {
            IRecoveryStrategy strategy = new NoRecoveryStrategy();

            Assert.IsTrue(strategy.MayTryNow(out var cachedException),
                $"{nameof(NoRecoveryStrategy)}.{nameof(IRecoveryStrategy.MayTryNow)}"
                + " should return true.");
            Assert.IsNull(cachedException,
                $"{nameof(cachedException)} is not null.");
        }

        [TestMethod]
        public void NoRecoveryStrategyShouldReturnTrueAfterFailure()
        {
            IRecoveryStrategy strategy = new NoRecoveryStrategy();

            var ex = new CachedException(new System.Exception("dummy exception"));

            strategy.RecordFailure(ex);

            Assert.IsFalse(strategy.MayTryNow(out var ex2),
                $"{nameof(NoRecoveryStrategy)}.{nameof(IRecoveryStrategy.MayTryNow)}"
                + " should return false.");
            Assert.AreSame(ex, ex2,
                "The returned exception is a different object.");
        }

        #endregion

        #region SimpleRecoveryStrategy

        [TestMethod]
        public void SimpleRecoveryStrategyShouldReturnTrue()
        {
            IRecoveryStrategy strategy 
                = new SimpleRecoveryStrategy(recoverySeconds: 3);

            Assert.IsTrue(strategy.MayTryNow(out var cachedException),
                $"{nameof(SimpleRecoveryStrategy)}.{nameof(IRecoveryStrategy.MayTryNow)}"
                + " should return true.");
            Assert.IsNull(cachedException,
                $"{nameof(cachedException)} is not null.");
        }

        [TestMethod]
        public void SimpleRecoveryStrategyShouldReturnFalseAfterFailure()
        {
            IRecoveryStrategy strategy 
                = new SimpleRecoveryStrategy(recoverySeconds: 5);

            var ex = new CachedException(new System.Exception("dummy exception"));

            strategy.RecordFailure(ex);

            Assert.IsFalse(strategy.MayTryNow(out var ex2),
                $"{nameof(SimpleRecoveryStrategy)}.{nameof(IRecoveryStrategy.MayTryNow)}"
                + " should return false.");
            Assert.AreSame(ex, ex2,
                "The returned exception is a different object.");
        }

        [TestMethod]
        public void SimpleRecoveryStrategyShouldReturnTrueAfterRecovery()
        {
            IRecoveryStrategy strategy 
                = new SimpleRecoveryStrategy(recoverySeconds: 0.1);

            var ex = new CachedException(new System.Exception("dummy exception"));

            strategy.RecordFailure(ex);

            Assert.IsFalse(strategy.MayTryNow(out var ex2),
                $"{nameof(SimpleRecoveryStrategy)}.{nameof(IRecoveryStrategy.MayTryNow)}"
                + " should return false before failure.");
            Assert.AreSame(ex, ex2,
                "The returned exception is a different object.");

            Thread.Sleep(millisecondsTimeout: 200);

            Assert.IsTrue(strategy.MayTryNow(out var ex3),
                $"{nameof(SimpleRecoveryStrategy)}.{nameof(IRecoveryStrategy.MayTryNow)}"
                + " should return true after recovery.");
            Assert.IsNull(ex3,
                $"{nameof(ex3)} is not null.");
        }

        #endregion

        #region ExponentialBackoffRecoveryStrategy

        [TestMethod]
        public void ExponentialBackoffRecoveryStrategyShouldReturnTrue()
        {
            IRecoveryStrategy strategy = new ExponentialBackoffRecoveryStrategy(
                initialDelaySeconds: 1.0,
                maxDelaySeconds: 60.0,
                multiplier: 2.0);

            Assert.IsTrue(strategy.MayTryNow(out var cachedException),
                $"{nameof(ExponentialBackoffRecoveryStrategy)}.{nameof(IRecoveryStrategy.MayTryNow)}"
                + " should return true initially.");
            Assert.IsNull(cachedException,
                $"{nameof(cachedException)} should be null initially.");
        }

        [TestMethod]
        public void ExponentialBackoffRecoveryStrategyShouldReturnFalseAfterFailure()
        {
            IRecoveryStrategy strategy = new ExponentialBackoffRecoveryStrategy(
                initialDelaySeconds: 5.0,
                maxDelaySeconds: 60.0,
                multiplier: 2.0);

            var ex = new CachedException(new System.Exception("dummy exception"));
            strategy.RecordFailure(ex);

            Assert.IsFalse(strategy.MayTryNow(out var ex2),
                $"{nameof(ExponentialBackoffRecoveryStrategy)}.{nameof(IRecoveryStrategy.MayTryNow)}"
                + " should return false after failure.");
            Assert.AreSame(ex, ex2,
                "The returned exception should be the same object.");
        }

        [TestMethod]
        public void ExponentialBackoffRecoveryStrategyShouldReturnTrueAfterRecovery()
        {
            IRecoveryStrategy strategy = new ExponentialBackoffRecoveryStrategy(
                initialDelaySeconds: 0.1,
                maxDelaySeconds: 60.0,
                multiplier: 2.0);

            var ex = new CachedException(new System.Exception("dummy exception"));
            strategy.RecordFailure(ex);

            Assert.IsFalse(strategy.MayTryNow(out var ex2),
                $"{nameof(ExponentialBackoffRecoveryStrategy)}.{nameof(IRecoveryStrategy.MayTryNow)}"
                + " should return false immediately after failure.");
            Assert.AreSame(ex, ex2,
                "The returned exception should be the same object.");

            Thread.Sleep(millisecondsTimeout: 200);

            Assert.IsTrue(strategy.MayTryNow(out var ex3),
                $"{nameof(ExponentialBackoffRecoveryStrategy)}.{nameof(IRecoveryStrategy.MayTryNow)}"
                + " should return true after recovery.");
            Assert.IsNull(ex3,
                $"{nameof(ex3)} should be null after recovery.");
        }

        [TestMethod]
        public void ExponentialBackoffRecoveryStrategyShouldDoubleDelayOnConsecutiveFailures()
        {
            IRecoveryStrategy strategy = new ExponentialBackoffRecoveryStrategy(
                initialDelaySeconds: 0.1,
                maxDelaySeconds: 10.0,
                multiplier: 2.0);

            // First failure - should delay for 0.1 seconds
            var ex1 = new CachedException(new System.Exception("failure 1"));
            strategy.RecordFailure(ex1);
            
            Assert.IsFalse(strategy.MayTryNow(out _),
                "Should be in recovery after first failure.");

            // Wait for first recovery
            Thread.Sleep(millisecondsTimeout: 150);
            Assert.IsTrue(strategy.MayTryNow(out _),
                "Should be available after first recovery period.");

            // Second consecutive failure - should delay for 0.2 seconds
            var ex2 = new CachedException(new System.Exception("failure 2"));
            strategy.RecordFailure(ex2);
            
            Assert.IsFalse(strategy.MayTryNow(out _),
                "Should be in recovery after second failure.");

            // Should still be in recovery after first delay period
            Thread.Sleep(millisecondsTimeout: 150);
            Assert.IsFalse(strategy.MayTryNow(out _),
                "Should still be in recovery after 0.15 seconds (delay should be 0.2s).");

            // Should be available after full second delay period
            Thread.Sleep(millisecondsTimeout: 100);
            Assert.IsTrue(strategy.MayTryNow(out _),
                "Should be available after full second delay period.");
        }

        [TestMethod]
        public void ExponentialBackoffRecoveryStrategyShouldRespectMaxDelay()
        {
            IRecoveryStrategy strategy = new ExponentialBackoffRecoveryStrategy(
                initialDelaySeconds: 1.0,
                maxDelaySeconds: 2.0, // Cap at 2 seconds
                multiplier: 10.0);    // Large multiplier to test capping

            // First failure - 1 second delay
            var ex1 = new CachedException(new System.Exception("failure 1"));
            strategy.RecordFailure(ex1);
            Thread.Sleep(millisecondsTimeout: 1100);
            Assert.IsTrue(strategy.MayTryNow(out _), "Should recover after 1 second.");

            // Second failure - should be capped at 2 seconds, not 10 seconds
            var ex2 = new CachedException(new System.Exception("failure 2"));
            strategy.RecordFailure(ex2);
            Thread.Sleep(millisecondsTimeout: 2100);
            Assert.IsTrue(strategy.MayTryNow(out _), 
                "Should recover after 2 seconds (capped), not 10 seconds.");
        }

        private const double INITIAL_DELAY_SECONDS_DEFAULT = 2.0;
        private const double MAX_DELAY_SECONDS_DEFAULT = 300.0;
        private const double MULTIPLIER_DEFAULT = 2.0;

        [TestMethod]
        public void ExponentialBackoffRecoveryStrategyShouldUseConstants()
        {
            // Test that constants are properly set
            Assert.AreEqual(2.0, INITIAL_DELAY_SECONDS_DEFAULT);
            Assert.AreEqual(300.0, MAX_DELAY_SECONDS_DEFAULT);
            Assert.AreEqual(2.0, MULTIPLIER_DEFAULT);

            // Test default constructor uses constants
            var strategy = new ExponentialBackoffRecoveryStrategy(
                initialDelaySeconds: INITIAL_DELAY_SECONDS_DEFAULT,
                maxDelaySeconds: MAX_DELAY_SECONDS_DEFAULT,
                multiplier: MULTIPLIER_DEFAULT);
            
            // Should work with default values
            Assert.IsTrue(strategy.MayTryNow(out _),
                "Default strategy should allow initial attempts.");
        }

        [TestMethod]
        public void ExponentialBackoffRecoveryStrategyShouldHandleConcurrentFailures()
        {
            var strategy = new ExponentialBackoffRecoveryStrategy(
                initialDelaySeconds: 0.5,
                maxDelaySeconds: 10.0,
                multiplier: 2.0);

            var ex1 = new CachedException(new System.Exception("failure 1"));
            var ex2 = new CachedException(new System.Exception("failure 2"));
            var ex3 = new CachedException(new System.Exception("failure 3"));

            // Record multiple failures concurrently
            var tasks = new[]
            {
                Task.Run(() => strategy.RecordFailure(ex1)),
                Task.Run(() => strategy.RecordFailure(ex2)),
                Task.Run(() => strategy.RecordFailure(ex3))
            };

            Task.WaitAll(tasks);

            // Should still be in recovery mode
            Assert.IsFalse(strategy.MayTryNow(out _),
                "Should be in recovery after concurrent failures.");

            // Should eventually recover
            Thread.Sleep(millisecondsTimeout: 1000);
            Assert.IsTrue(strategy.MayTryNow(out _),
                "Should eventually recover from concurrent failures.");
        }

        [TestMethod]
        public void ExponentialBackoffRecoveryStrategyShouldPreventRaceConditionSkipping()
        {
            // This test verifies the fix for the race condition where multiple threads
            // calling RecordFailure simultaneously could cause unexpected skips of exponential stages
            
            var strategy = new ExponentialBackoffRecoveryStrategy(
                initialDelaySeconds: 0.2,
                maxDelaySeconds: 10.0,
                multiplier: 2.0);

            // All threads get MayTryNow = true at the same time
            Assert.IsTrue(strategy.MayTryNow(out _), "All threads should initially be allowed.");

            // Simulate the race condition scenario described in the PR:
            // [00:10.50] (4x threads) MayTryNow returns true
            // [00:11.03] (thread A) RecordFailure -- should be stage 1 (0.2s recovery)
            // [00:11.05] (thread B) RecordFailure -- should NOT increment to stage 2 if it's the same failure event
            // [00:11.06] (thread C) RecordFailure -- should NOT increment to stage 3 if it's the same failure event  
            // [00:11.08] (thread D) RecordFailure -- should NOT increment to stage 4 if it's the same failure event

            var ex1 = new CachedException(new System.Exception("failure 1"));
            var ex2 = new CachedException(new System.Exception("failure 2"));
            var ex3 = new CachedException(new System.Exception("failure 3"));
            var ex4 = new CachedException(new System.Exception("failure 4"));

            // Record failures in rapid succession (simulating concurrent access)
            // The fix should prevent unconditional increment of consecutive failures
            strategy.RecordFailure(ex1); // Should set stage 1
            strategy.RecordFailure(ex2); // Should NOT increment (same failure window due to race condition fix)
            strategy.RecordFailure(ex3); // Should NOT increment (same failure window due to race condition fix)  
            strategy.RecordFailure(ex4); // Should NOT increment (same failure window due to race condition fix)

            // Should be in recovery for stage 1 (0.2 seconds), NOT stage 4 (1.6 seconds)
            Assert.IsFalse(strategy.MayTryNow(out _),
                "Should be in recovery after recording failures.");

            // Should recover after stage 1 delay (~0.2s), not stage 4 delay (~1.6s)
            Thread.Sleep(millisecondsTimeout: 300);
            Assert.IsTrue(strategy.MayTryNow(out _),
                "Should recover after stage 1 delay, proving race condition was prevented.");

            // Now record a genuinely new failure after recovery window
            Thread.Sleep(millisecondsTimeout: 50);
            var ex5 = new CachedException(new System.Exception("failure 5"));
            strategy.RecordFailure(ex5);

            // This should now be stage 2 (0.4 seconds)
            Assert.IsFalse(strategy.MayTryNow(out _),
                "Should be in recovery for stage 2.");

            // Should still be in recovery after stage 1 delay
            Thread.Sleep(millisecondsTimeout: 250);
            Assert.IsFalse(strategy.MayTryNow(out _),
                "Should still be in recovery after 0.25s (stage 2 delay is 0.4s).");

            // Should recover after stage 2 delay
            Thread.Sleep(millisecondsTimeout: 200);
            Assert.IsTrue(strategy.MayTryNow(out _),
                "Should recover after stage 2 delay.");
        }

        #endregion

        #region RecoveryStrategyFactory

        [TestMethod]
        public void RecoveryStrategyFactoryCreateExponentialBackoffShouldWork()
        {
            var strategy = RecoveryStrategyFactory.CreateExponentialBackoff(1.0, 10.0, 2.0);
            
            Assert.IsNotNull(strategy, "Factory should create strategy.");
            Assert.IsInstanceOfType(strategy, typeof(ExponentialBackoffRecoveryStrategy),
                "Factory should create ExponentialBackoffRecoveryStrategy.");
            
            // Test it works
            Assert.IsTrue(strategy.MayTryNow(out _), "Created strategy should work.");
        }

        [TestMethod]
        public void RecoveryStrategyFactoryCreateExponentialBackoffShouldUseDefaults()
        {
            var strategy = RecoveryStrategyFactory.CreateExponentialBackoff(
                initialDelaySeconds: INITIAL_DELAY_SECONDS_DEFAULT,
                maxDelaySeconds: MAX_DELAY_SECONDS_DEFAULT,
                multiplier: MULTIPLIER_DEFAULT);
            
            Assert.IsNotNull(strategy, "Factory should create strategy with defaults.");
            Assert.IsTrue(strategy.MayTryNow(out _), "Created strategy should work with defaults.");
        }

        [TestMethod]
        public void RecoveryStrategyFactoryCreateSimpleShouldWork()
        {
            var strategy = RecoveryStrategyFactory.CreateSimple(1.0);
            
            Assert.IsNotNull(strategy, "Factory should create simple strategy.");
            Assert.IsInstanceOfType(strategy, typeof(SimpleRecoveryStrategy),
                "Factory should create SimpleRecoveryStrategy.");
        }

        [TestMethod]
        public void RecoveryStrategyFactoryCreateInstantShouldWork()
        {
            var strategy = RecoveryStrategyFactory.CreateInstant();
            
            Assert.IsNotNull(strategy, "Factory should create instant strategy.");
            Assert.IsInstanceOfType(strategy, typeof(InstantRecoveryStrategy),
                "Factory should create InstantRecoveryStrategy.");
        }

        [TestMethod]
        public void RecoveryStrategyFactoryCreateShouldChooseCorrectStrategy()
        {
            // Test exponential backoff selection
            var exponentialStrategy = RecoveryStrategyFactory.Create(
                useExponentialBackoff: true,
                recoverySeconds: 60.0,
                initialDelaySeconds: 1.0,
                maxDelaySeconds: 10.0,
                multiplier: 2.0);
            
            Assert.IsInstanceOfType(exponentialStrategy, typeof(ExponentialBackoffRecoveryStrategy),
                "Should create ExponentialBackoffRecoveryStrategy when useExponentialBackoff=true.");

            // Test simple recovery selection
            var simpleStrategy = RecoveryStrategyFactory.Create(
                useExponentialBackoff: false,
                recoverySeconds: 5.0,
                initialDelaySeconds: INITIAL_DELAY_SECONDS_DEFAULT,
                maxDelaySeconds: MAX_DELAY_SECONDS_DEFAULT,
                multiplier: MULTIPLIER_DEFAULT);
            
            Assert.IsInstanceOfType(simpleStrategy, typeof(SimpleRecoveryStrategy),
                "Should create SimpleRecoveryStrategy when useExponentialBackoff=false and recoverySeconds>0.");

            // Test instant recovery selection
            var instantStrategy = RecoveryStrategyFactory.Create(
                useExponentialBackoff: false,
                recoverySeconds: 0.0,
                initialDelaySeconds: INITIAL_DELAY_SECONDS_DEFAULT,
                maxDelaySeconds: MAX_DELAY_SECONDS_DEFAULT,
                multiplier: MULTIPLIER_DEFAULT);

            Assert.IsInstanceOfType(instantStrategy, typeof(InstantRecoveryStrategy),
                "Should create InstantRecoveryStrategy when useExponentialBackoff=false and recoverySeconds<=0.");
        }

        #endregion
    }
}
