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


using FiftyOne.Pipeline.CloudRequestEngine.FailHandling.ExceptionCaching;
using FiftyOne.Pipeline.CloudRequestEngine.FailHandling.Recovery;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading;

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
    }
}
