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

using FiftyOne.Common.Wrappers.IO;
using FiftyOne.Pipeline.Engines.Configuration;
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.Exceptions;
using FiftyOne.Pipeline.Engines.FlowElements;
using FiftyOne.Pipeline.Engines.Services;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;

namespace FiftyOne.Pipeline.Engines.Tests.Services;

/// <summary>
/// A refresh that fails, through the file system watcher and through the
/// timer. The failure must be logged with its reason and reported, nothing
/// may escape the threads the service runs on, and a later file must still
/// be applied.
/// </summary>
/// <remarks>
/// The engine's refresh is made to fail, as a native engine's does when the
/// data file is unusable.
/// </remarks>
[TestClass]
public class DataUpdateServiceFailedRefreshTests
{
    // Every content has the same length, so it can be written in place.
    private const string GoodData = "good data 0";
    private const string BrokenData = "broken data";

    private const int WaitMilliseconds = 30000;

    // Longer than the steps file write times move in.
    private const int WriteTimeStepMilliseconds = 50;

    // UpdatedDataAvailable tries a refresh this many times.
    private const int RefreshTries = 10;

    private string _directory;

    [TestInitialize]
    public void Init()
    {
        _directory = Directory.CreateDirectory(Path.Combine(
            Path.GetTempPath(),
            Path.GetRandomFileName())).FullName;
    }

    [TestCleanup]
    public void Cleanup()
    {
        try
        {
            Directory.Delete(_directory, true);
        }
        catch (IOException)
        {
            // A watcher thread can still hold a file for a moment.
        }
    }

    /// <summary>
    /// The watcher reports a broken file. The failure is logged with its
    /// reason and reported, the watcher keeps watching, and a later good
    /// file is applied.
    /// </summary>
    [TestMethod]
    public void Watcher_RefreshFails_LogsReportsAndAppliesLaterFile()
    {
        var path = Path.Combine(_directory, "data.dat");
        File.WriteAllText(path, GoodData);
        var logger = new CapturingLogger();
        var service = new DataUpdateService(
            logger, new HttpClient(), new RealFileSystem(), NoTimer);
        var engine = new Mock<IOnPremiseAspectEngine>();
        // Reads the file as a real engine would and fails unless it is good.
        engine.Setup(e => e.RefreshData(It.IsAny<string>())).Callback(() =>
        {
            if (ReadShared(path) != GoodData)
            {
                throw new ApplicationException(BrokenData);
            }
        });
        var file = WatchedFile(engine, path);
        var reports = Collect(service);

        try
        {
            service.RegisterDataFile(file);

            Replace(path, BrokenData);
            Assert.IsTrue(
                WaitFor(reports, AutoUpdateStatus.AUTO_UPDATE_REFRESH_FAILED),
                "The failed refresh was not reported.");
            AssertLoggedWithReason(logger);

            Replace(path, GoodData);
            Assert.IsTrue(
                WaitFor(reports, AutoUpdateStatus.AUTO_UPDATE_SUCCESS),
                "The later good file was not applied.");
        }
        finally
        {
            file.Dispose();
        }
    }

    /// <summary>
    /// The watcher reports a file that cannot be opened yet. The engine is
    /// only refreshed once it can be.
    /// </summary>
    [TestMethod]
    public void Watcher_FileCannotBeOpened_RefreshesOnlyOnceItOpens()
    {
        var path = Path.Combine(_directory, "data.dat");
        File.WriteAllText(path, GoodData);
        var opens = 0;
        var opened = false;
        var fileWrapper = new Mock<IFileWrapper>();
        fileWrapper.Setup(w => w.GetLastWriteTimeUtc(It.IsAny<string>()))
            .Returns((string p) => File.GetLastWriteTimeUtc(p));
        fileWrapper.Setup(w => w.OpenRead(It.IsAny<string>()))
            .Returns((string p) =>
            {
                if (Interlocked.Increment(ref opens) <= 2)
                {
                    throw new IOException("The file is in use.");
                }
                Volatile.Write(ref opened, true);
                return Stream.Null;
            });
        var fileSystem = new Mock<IFileSystem>();
        fileSystem.Setup(f => f.File).Returns(fileWrapper.Object);
        var service = new DataUpdateService(
            new CapturingLogger(), new HttpClient(), fileSystem.Object, NoTimer);
        var refreshedBeforeOpen = false;
        var engine = new Mock<IOnPremiseAspectEngine>();
        engine.Setup(e => e.RefreshData(It.IsAny<string>())).Callback(() =>
        {
            if (Volatile.Read(ref opened) == false)
            {
                refreshedBeforeOpen = true;
            }
        });
        var file = WatchedFile(engine, path);
        var reports = Collect(service);

        try
        {
            service.RegisterDataFile(file);

            Replace(path, "good data 1");
            Assert.IsTrue(
                WaitFor(reports, AutoUpdateStatus.AUTO_UPDATE_SUCCESS),
                "The file was not applied once it could be opened.");
            Assert.IsFalse(
                refreshedBeforeOpen,
                "The engine was refreshed before the file could be opened.");
        }
        finally
        {
            file.Dispose();
        }
    }

    /// <summary>
    /// An exception from a <see cref="DataUpdateService.CheckForUpdateComplete"/>
    /// handler is logged, and the watcher keeps watching.
    /// </summary>
    [TestMethod]
    public void Watcher_CompleteHandlerThrows_LogsAndKeepsWatching()
    {
        var path = Path.Combine(_directory, "data.dat");
        File.WriteAllText(path, GoodData);
        var logger = new CapturingLogger();
        var service = new DataUpdateService(
            logger, new HttpClient(), new RealFileSystem(), NoTimer);
        var applied = new BlockingCollection<string>();
        var engine = new Mock<IOnPremiseAspectEngine>();
        engine.Setup(e => e.RefreshData(It.IsAny<string>()))
            .Callback(() => applied.Add(ReadShared(path)));
        var file = WatchedFile(engine, path);
        service.CheckForUpdateComplete += (sender, e) =>
            throw new InvalidOperationException("The handler failed.");

        try
        {
            service.RegisterDataFile(file);

            Replace(path, "good data 1");
            Assert.IsTrue(
                WaitFor(applied, "good data 1"),
                "The first file was not applied.");
            Replace(path, "good data 2");
            Assert.IsTrue(
                WaitFor(applied, "good data 2"),
                "The watcher stopped after the handler failed.");
            Assert.IsTrue(
                logger.Errors.Any(e => e.Exception is InvalidOperationException),
                "The handler's exception was not logged.");
        }
        finally
        {
            file.Dispose();
        }
    }

    /// <summary>
    /// The timer finds a newer file that fails to load. The failure is
    /// logged with its reason.
    /// </summary>
    [TestMethod]
    public void Timer_RefreshFails_LogsTheReason()
    {
        var logger = new CapturingLogger();
        var service = new DataUpdateService(
            logger, new HttpClient(), NewerFileSystem(), FireOnce());
        var engine = new Mock<IOnPremiseAspectEngine>();
        engine.Setup(e => e.RefreshData(It.IsAny<string>()))
            .Throws(new ApplicationException(BrokenData));
        var file = PolledFile(engine);
        var reports = Collect(service);

        try
        {
            service.RegisterDataFile(file);

            Assert.IsTrue(
                WaitFor(reports, AutoUpdateStatus.AUTO_UPDATE_REFRESH_FAILED),
                "The failed refresh was not reported.");
            AssertLoggedWithReason(logger);
        }
        finally
        {
            file.Dispose();
        }
    }

    /// <summary>
    /// The timer finds a newer file that fails to load. The timer is set to
    /// check again, and the next check applies the file once it loads.
    /// </summary>
    [TestMethod]
    public void Timer_RefreshFails_ChecksAgainAndAppliesLaterFile()
    {
        var service = new DataUpdateService(
            new CapturingLogger(), new HttpClient(), NewerFileSystem(), FireOnce());
        var refreshes = 0;
        var engine = new Mock<IOnPremiseAspectEngine>();
        // The first check fails every try, and the next one works.
        engine.Setup(e => e.RefreshData(It.IsAny<string>())).Callback(() =>
        {
            if (Interlocked.Increment(ref refreshes) <= RefreshTries)
            {
                throw new ApplicationException(BrokenData);
            }
        });
        var file = PolledFile(engine);
        var reports = Collect(service);

        try
        {
            service.RegisterDataFile(file);

            Assert.IsTrue(
                WaitFor(reports, AutoUpdateStatus.AUTO_UPDATE_REFRESH_FAILED),
                "The failed refresh was not reported.");
            Assert.IsTrue(
                WaitFor(reports, AutoUpdateStatus.AUTO_UPDATE_SUCCESS),
                "The timer did not check again after the failed refresh.");
            Assert.AreEqual(RefreshTries + 1, Volatile.Read(ref refreshes));
        }
        finally
        {
            file.Dispose();
        }
    }

    /// <summary>
    /// A refresh that only works on its last try is a success, and nothing
    /// is logged as an error.
    /// </summary>
    [TestMethod]
    public void Timer_RefreshWorksOnLastTry_ReportsSuccess()
    {
        var logger = new CapturingLogger();
        var service = new DataUpdateService(
            logger, new HttpClient(), NewerFileSystem(), FireOnce());
        var refreshes = 0;
        var engine = new Mock<IOnPremiseAspectEngine>();
        engine.Setup(e => e.RefreshData(It.IsAny<string>())).Callback(() =>
        {
            if (Interlocked.Increment(ref refreshes) < RefreshTries)
            {
                throw new ApplicationException(BrokenData);
            }
        });
        var file = PolledFile(engine);
        var reports = Collect(service);

        try
        {
            service.RegisterDataFile(file);

            Assert.IsTrue(
                WaitFor(reports, AutoUpdateStatus.AUTO_UPDATE_SUCCESS),
                "The refresh that worked was not reported.");
            Assert.AreEqual(RefreshTries, Volatile.Read(ref refreshes));
            Assert.AreEqual(
                0,
                logger.Errors.Count,
                "A refresh that worked was logged as an error.");
        }
        finally
        {
            file.Dispose();
        }
    }

    // A timer factory for tests of the watcher alone.
    private static Timer NoTimer(TimerCallback callback, object state, TimeSpan dueTime)
        => null;

    // Fires the first timer at once. Later timers, created after a
    // successful update, never fire.
    private static Func<TimerCallback, object, TimeSpan, Timer> FireOnce()
    {
        var created = 0;
        return (callback, state, dueTime) =>
            Interlocked.Increment(ref created) == 1
                ? new Timer(callback, state, 0, Timeout.Infinite)
                : null;
    }

    // A data file that is always newer than the engine's temporary copy.
    private static IFileSystem NewerFileSystem()
    {
        var fileWrapper = new Mock<IFileWrapper>();
        fileWrapper.Setup(w => w.GetLastWriteTimeUtc("data.dat"))
            .Returns(new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc));
        fileWrapper.Setup(w => w.GetLastWriteTimeUtc("temp.dat"))
            .Returns(new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc));
        var fileSystem = new Mock<IFileSystem>();
        fileSystem.Setup(f => f.File).Returns(fileWrapper.Object);
        return fileSystem.Object;
    }

    private static AspectEngineDataFile WatchedFile(
        Mock<IOnPremiseAspectEngine> engine,
        string path)
    {
        var file = new AspectEngineDataFile()
        {
            Engine = engine.Object,
            Configuration = new DataFileConfiguration()
            {
                AutomaticUpdatesEnabled = true,
                FileSystemWatcherEnabled = true,
                DataFilePath = path,
            },
        };
        engine.Setup(e => e.GetDataFileMetaData(It.IsAny<string>())).Returns(file);
        return file;
    }

    private static AspectEngineDataFile PolledFile(Mock<IOnPremiseAspectEngine> engine)
    {
        var file = new AspectEngineDataFile()
        {
            Engine = engine.Object,
            Configuration = new DataFileConfiguration()
            {
                AutomaticUpdatesEnabled = true,
                FileSystemWatcherEnabled = false,
                DataFilePath = "data.dat",
                PollingIntervalSeconds = 1,
                MaxRandomisationSeconds = 0,
            },
            TempDataFilePath = "temp.dat",
        };
        engine.Setup(e => e.GetDataFileMetaData(It.IsAny<string>())).Returns(file);
        return file;
    }

    private static BlockingCollection<AutoUpdateStatus> Collect(DataUpdateService service)
    {
        var reports = new BlockingCollection<AutoUpdateStatus>();
        service.CheckForUpdateComplete += (sender, e) => reports.Add(e.Status);
        return reports;
    }

    // Takes items until one matches, ignoring the others, such as the extra
    // reports a watcher raises for one file.
    private static bool WaitFor<T>(BlockingCollection<T> items, T expected)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(WaitMilliseconds);
        while (true)
        {
            var remaining = (int)Math.Max(
                0, (deadline - DateTime.UtcNow).TotalMilliseconds);
            if (items.TryTake(out var item, remaining) == false)
            {
                return false;
            }
            if (EqualityComparer<T>.Default.Equals(item, expected))
            {
                return true;
            }
        }
    }

    // Every error logged carries the exception that says why.
    private static void AssertLoggedWithReason(CapturingLogger logger)
    {
        var errors = logger.Errors;
        Assert.IsTrue(errors.Count > 0, "The failed refresh was not logged.");
        foreach (var error in errors)
        {
            Assert.IsTrue(
                error.Exception is DataUpdateException &&
                error.Exception.InnerException is ApplicationException,
                $"The error '{error.Message}' does not carry the reason. " +
                $"Exception attached: {error.Exception?.GetType().Name ?? "none"}.");
        }
    }

    // Writes over the data file in place with one write, so a refresh
    // reads either the old content or the new. The service only refreshes
    // for a later write time, so the write waits until the clock is clear
    // of the last one, and it tries again while the service has the file
    // open to check it.
    private static void Replace(string path, string content)
    {
        var bytes = Encoding.ASCII.GetBytes(content);
        Assert.AreEqual(GoodData.Length, bytes.Length);
        var last = File.GetLastWriteTimeUtc(path);
        while ((DateTime.UtcNow - last).TotalMilliseconds < WriteTimeStepMilliseconds)
        {
            Thread.Sleep(10);
        }
        var deadline = DateTime.UtcNow.AddMilliseconds(WaitMilliseconds);
        while (true)
        {
            try
            {
                using (var stream = new FileStream(
                    path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite))
                {
                    stream.Write(bytes, 0, bytes.Length);
                }
                return;
            }
            catch (IOException) when (DateTime.UtcNow < deadline)
            {
                Thread.Sleep(10);
            }
        }
    }

    private static string ReadShared(string path)
    {
        using (var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete))
        using (var reader = new StreamReader(stream))
        {
            return reader.ReadToEnd();
        }
    }

    // Records entries from any thread, with the exception passed to the log.
    private sealed class CapturingLogger : ILogger<DataUpdateService>
    {
        private readonly ConcurrentQueue<Entry> _entries = new ConcurrentQueue<Entry>();

        public IReadOnlyList<Entry> Errors =>
            _entries.Where(e => e.Level == LogLevel.Error).ToList();

        public IDisposable BeginScope<TState>(TState state) => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception exception,
            Func<TState, Exception, string> formatter)
        {
            _entries.Enqueue(new Entry(logLevel, formatter(state, exception), exception));
        }
    }

    private sealed record Entry(LogLevel Level, string Message, Exception Exception);
}
