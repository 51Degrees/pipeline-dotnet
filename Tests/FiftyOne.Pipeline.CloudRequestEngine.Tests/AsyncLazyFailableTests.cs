using FiftyOne.Pipeline.CloudRequestEngine.FailHandling;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FiftyOne.Pipeline.CloudRequestEngine.Tests
{
    [TestClass]
    public class AsyncLazyFailableTests
    {
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ThrowOnNullDelegate()
        {
            _ = new AsyncLazyFailable<int>(null);
        }

        [TestMethod]
        public async Task ReturnResult()
        {
            var lazyBox = new AsyncLazyFailable<string>(() => "42");
            Assert.AreEqual("42", await lazyBox.GetValueAsync(default));
        }

        [TestMethod]
        public async Task ExternalCallsAwaitSharedActiveTask()
        {
            var counter = 0;
            var lazyBox = new AsyncLazyFailable<int>(() =>
            {
                int result = Interlocked.Increment(ref counter);
                Thread.Sleep(millisecondsTimeout: 500);
                return result;
            });
            var t1 = lazyBox.GetValueAsync(default);
            Thread.Sleep(millisecondsTimeout: 100);
            Assert.AreEqual(1, counter, 
                "Call count mismatch after first access");

            var t2 = lazyBox.GetValueAsync(default);
            Assert.IsFalse(t1.IsCompleted,
                "First task finished by the time of second access.");

            Assert.AreEqual(1, await t1, "First result mismatch.");
            Assert.AreEqual(1, await t2, "Second result mismatch.");
            Assert.AreEqual(1, counter, "Delegate call count mismatch.");
        }

        [TestMethod]
        public async Task DelegateCalledOnlyOnce()
        {
            var counter = 0;
            var lazyBox = new AsyncLazyFailable<int>(() =>
                Interlocked.Increment(ref counter));
            Assert.AreEqual(1, await lazyBox.GetValueAsync(default),
                "First result mismatch.");
            Assert.AreEqual(1, counter,
                "Delegate call count mismatch after first access.");

            Assert.AreEqual(1, await lazyBox.GetValueAsync(default),
                "Second result mismatch.");
            Assert.AreEqual(1, counter,
                "Delegate call count mismatch after second access.");
        }

        [TestMethod]
        public async Task PropagatesExceptionsToBothChildren()
        {
            var counter = 0;
            var lazyBox = new AsyncLazyFailable<int>((Func<int>)(() =>
            {
                int result = Interlocked.Increment(ref counter);
                Thread.Sleep(millisecondsTimeout: 500);
                throw new Exception(result.ToString());
            }));

            var t1 = lazyBox.GetValueAsync(default);
            Thread.Sleep(millisecondsTimeout: 100);
            Assert.AreEqual(1, counter,
                "Delegate call count mismatch after first access.");

            var t2 = lazyBox.GetValueAsync(default);
            Assert.IsFalse(t1.IsCompleted,
                "First task finished by the time of second access.");

            try
            {
                _ = await t1;
                Assert.Fail($"{nameof(t1)} didn't throw.");
            }
            catch (Exception ex)
            {
                Assert.AreEqual("1", ex.Message, 
                    "Unexpected exception on first access.");
            }
            try
            {
                _ = await t2;
                Assert.Fail($"{nameof(t2)} didn't throw.");
            }
            catch (Exception ex)
            {
                Assert.AreEqual("1", ex.Message,
                    "Unexpected exception on second access.");
            }
            Assert.AreEqual(1, counter,
                "Delegate call total count mismatch.");
        }

        [TestMethod]
        public async Task RecoversAfterFailure()
        {
            var counter = 0;
            var lazyBox = new AsyncLazyFailable<int>(() =>
            {
                int result = Interlocked.Increment(ref counter);
                switch (result)
                {
                    case 2: return result;
                    default: throw new Exception(result.ToString());
                }
            });

            try
            {
                _ = await lazyBox.GetValueAsync(default);
                Assert.Fail($"First iteration didn't throw.");
            }
            catch (Exception ex)
            {
                Assert.AreEqual("1", ex.Message,
                    "Unexpected exception on first access.");
            }
            Assert.AreEqual(1, counter,
                "Delegate call count mismatch after first access.");

            Assert.AreEqual(2, await lazyBox.GetValueAsync(default),
                "Unexpected value on second access.");
            Assert.AreEqual(2, counter,
                "Delegate call count mismatch after second access.");

            Assert.AreEqual(2, await lazyBox.GetValueAsync(default),
                "Unexpected value on third access.");
            Assert.AreEqual(2, counter,
                "Delegate call count mismatch after third access.");
        }
    }
}
