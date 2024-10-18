using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FiftyOne.Pipeline.CloudRequestEngine.FailHandling.TaskWaitingCancellation;

namespace FiftyOne.Pipeline.CloudRequestEngine.Tests
{
    [TestClass]
    public class TaskWaitingCancellationTests
    {
        [TestMethod]
        public async Task ShouldReturnResult()
        {
            var mainTask = Task.Run(() =>
            {
                Thread.Sleep(millisecondsTimeout: 100);
                return 42;
            });
            var waitingTask = mainTask.WithCancellation(CancellationToken.None);
            Assert.AreEqual(42, await waitingTask);
        }

        [TestMethod]
        public async Task ShouldForwardExceptions()
        {
            var msg = "dummy message";
            Exception ex1 = new Exception(msg);
            var mainTask = Task.Run((Func<string>)(() =>
            {
                Thread.Sleep(millisecondsTimeout: 100);
                throw ex1;
            }));
            var waitingTask = mainTask.WithCancellation(CancellationToken.None);
            try
            {
                _ = await waitingTask;
                Assert.Fail("Waiting wask didn't throw.");
            }
            catch (Exception ex2)
            {
                Assert.AreEqual(ex1, ex2);
                Assert.AreEqual(msg, ex2.Message);
            }
        }

        [TestMethod]
        public async Task ShouldStopWithTrippedToken()
        {
            var mainTask = Task.Run(() =>
            {
                Thread.Sleep(millisecondsTimeout: 300);
                return 29;
            });
            var cancellationSource = new CancellationTokenSource();
            var waitingTask = mainTask.WithCancellation(cancellationSource.Token);

            Thread.Sleep(millisecondsTimeout: 25);

            Assert.IsFalse(mainTask.IsCompleted);
            cancellationSource.Cancel();

            try
            {
                _ = await waitingTask;
                Assert.Fail("Waiting task finished running.");
            }
            catch (OperationCanceledException)
            {
                // nop
            }
            Assert.IsFalse(mainTask.IsCompleted);
            Assert.AreEqual(29, await mainTask);
        }
    }
}
