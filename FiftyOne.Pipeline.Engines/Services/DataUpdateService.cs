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

using FiftyOne.Common.Wrappers.IO;
using FiftyOne.Pipeline.Engines.Configuration;
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Engines.Exceptions;
using FiftyOne.Pipeline.Engines.FlowElements;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FiftyOne.Pipeline.Engines.Services
{
    /// <summary>
    /// Service that manages updates to data files that are used by
    /// <see cref="IAspectEngine"/> instances.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/features/data-updates.md">Specification</see>
    /// </summary>
    public class DataUpdateService : IDataUpdateService
	{
		#region Fields
		private ILogger<DataUpdateService> _logger;

		/// <summary>
		/// The HttpClient to use when checking for updates.
		/// </summary>
		private HttpClient _httpClient;

        // System wrappers
        private IFileSystem _fileSystem;

        // random number generator
        private Random _rnd = new Random();

		// All registered configurations
		private List<AspectEngineDataFile> _configurations;

		// The factory function used to create Timer objects.
		private Func<TimerCallback, object, TimeSpan, Timer> _timerFactory;

		/// <summary>
		/// The event handler fired when a call to CheckForUpdate is completed.
		/// </summary>
		public event EventHandler<DataUpdateCompleteArgs> CheckForUpdateComplete;
		/// <summary>
		/// The event handler fired when a call to CheckForUpdate is started.
		/// </summary>
		public event EventHandler<DataUpdateEventArgs> CheckForUpdateStarted;
		#endregion

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="logger">
		/// The logger to use
		/// </param>
		/// <param name="httpClient">
		/// The <see cref="HttpClient"/> to use when requesting an update
		/// from a URL.
		/// Note that only one HttpClient instance should be used throughout 
		/// the application, as described in the documentation: 
		/// https://msdn.microsoft.com/library/system.net.http.httpclient(v=vs.110).aspx
		/// </param>
		public DataUpdateService(
			ILogger<DataUpdateService> logger,
			HttpClient httpClient) : this(logger, httpClient, null, null)
		{ }

        /// <summary>
        /// Internal constructor. Should only be called directly 
        /// by unit tests 
        /// </summary>
        /// <param name="logger">
        /// The logger to use
        /// </param>
        /// <param name="httpClient">
        /// The <see cref="HttpClient"/> to use when requesting an update
        /// from a URL.
        /// Note that only one HttpClient instance should be used throughout 
        /// the application, as described in the documentation: 
        /// https://msdn.microsoft.com/library/system.net.http.httpclient(v=vs.110).aspx
        /// </param>
        /// <param name="fileSystemWrapper">
        /// A wrapper for file system.
        /// In normal operation this will usually be a <see cref="RealFileSystem"/>
        /// instance. If null is passed, a new <see cref="RealFileSystem"/>
        /// instance is created.
        /// </param>
        /// <param name="timerFactory">
        /// A factory method for creating <see cref="Timer"/> instances.
        /// Parameters are: callback method, state, time until callback 
        /// triggered.
        /// </param>
        internal DataUpdateService(
			ILogger<DataUpdateService> logger,
			HttpClient httpClient,
			IFileSystem fileSystemWrapper,
			Func<TimerCallback, object, TimeSpan, Timer> timerFactory)
		{
			_logger = logger;
			_httpClient = httpClient;
			_configurations = new List<AspectEngineDataFile>();

			if(fileSystemWrapper == null)
			{
                _fileSystem = new RealFileSystem();
			}
			else
			{
                _fileSystem = fileSystemWrapper;
			}
			if (timerFactory == null)
			{
				_timerFactory = TimerFactory;
			}
			else
			{
				_timerFactory = timerFactory;
			}
		}

		#region Public methods

		/// <summary>
		/// Enables logging debug information (intermediate states and results).
		/// </summary>
		public bool DebugLoggingEnabled { get; set; }

        /// <summary>
        /// Called when <see cref="CheckForUpdate(object)"/> starts
        /// </summary>
        public event Action OnTimeredCheckForUpdateEntered;

        /// <summary>
        /// Called when <see cref="CheckForUpdate(object)"/> will exit
        /// </summary>
        public event Action OnTimeredCheckForUpdateWillExit;

        /// <summary>
        /// Called when <see cref="DataFileUpdated(object, FileSystemEventArgs)"/> starts
        /// </summary>
        public event Action OnDataFileUpdatedEntered;

        /// <summary>
        /// Called when <see cref="DataFileUpdated(object, FileSystemEventArgs)"/> will exit
        /// </summary>
        public event Action OnDataFileUpdatedWillExit;

		/// <summary>
		/// Register an data file for automatic updates.
		/// </summary>
		/// <param name="dataFile">
		/// The details of the data file to register.
		/// </param>
		public void RegisterDataFile(AspectEngineDataFile dataFile)
		{
			if (dataFile == null)
			{
				throw new ArgumentNullException(nameof(dataFile));
			}

			bool alreadyRegistered = dataFile.IsRegistered;
			bool setTimer = true;
			dataFile.SetDataUpdateService(this);
            LogInfoMessage(Messages.MessageAutoUpdateRegisterDataFile, dataFile);

            if (dataFile != null)
			{
				LogDebugMessage(() => "dataFile is not null.", dataFile);

				// If the data file is configured to refresh the data
				// file on startup then download an update immediately.
				// We also want to do this synchronously so that execution
				// will block until the engine is ready.
				if (dataFile.Configuration.UpdateOnStartup &&
					alreadyRegistered == false)
                {
                    LogInfoMessage(Messages.MessageAutoUpdateOnStartup, dataFile);
                    var result = CheckForUpdate(dataFile, true);
					LogDebugMessage(() => $"{nameof(CheckForUpdate)} resulted in {result}", dataFile);
					if (result == AutoUpdateStatus.AUTO_UPDATE_SUCCESS)
					{
						// If the update was successful then the timer 
						// and watcher will already have been set up if 
						// needed.
						setTimer = false;
					}
					else if (result != AutoUpdateStatus.AUTO_UPDATE_NOT_NEEDED)
					{
						var msg = string.Format(CultureInfo.InvariantCulture,
							Messages.ExceptionAutoUpdateOnStartupFailed,
							dataFile.Identifier,
							Enum.GetName(typeof(AutoUpdateStatus), result));
						throw new DataUpdateException(msg, result);
					}
				}
				if(setTimer)
				{
					LogDebugMessage(() => $"{nameof(setTimer)} is {setTimer}", dataFile);

					// Only create an automatic update timer if auto updates are 
					// enabled for this engine and there is not already an associated 
					// timer.
					if (dataFile.AutomaticUpdatesEnabled &&
						dataFile.Timer == null)
                    {
                        LogInfoMessage(Messages.MessageAutoUpdateCreateTimer, dataFile);

                        TimeSpan timeToUpdate = GetInterval(dataFile.Configuration);
                        if (dataFile.UpdateAvailableTime > DateTime.UtcNow)
                        {
                            timeToUpdate = dataFile.UpdateAvailableTime
                                .Subtract(DateTime.UtcNow);
                            timeToUpdate = ApplyIntervalRandomisation(
                                timeToUpdate, dataFile.Configuration);
                        }
						// Create a timer that will go off when the engine expects
						// updated data to be available.
						var timer = _timerFactory(
							CheckForUpdate,
							dataFile,
							timeToUpdate);
						dataFile.Timer = timer;
					}

					// If file system watcher is enabled then set it up.
					if (dataFile.Configuration.FileSystemWatcherEnabled &&
						dataFile.FileWatcher == null &&
						string.IsNullOrEmpty(dataFile.DataFilePath) == false)
                    {
                        LogInfoMessage(Messages.MessageAutoUpdateCreateWatcher, dataFile);

                        FileSystemWatcher watcher = new FileSystemWatcher(
							Path.GetDirectoryName(dataFile.DataFilePath),
							Path.GetFileName(dataFile.DataFilePath));

						//on macos NotifyFilters.FileName filter and Created subscription are needed
						//because it seems to recreate the file when copying over
						watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName;
						watcher.Changed += DataFileUpdated;
						watcher.Created += DataFileUpdated;
						watcher.EnableRaisingEvents = true;
						dataFile.FileWatcher = watcher;
					}

					lock (_configurations)
					{
						// Add the configuration to the list of configurations.
						if (_configurations.Contains(dataFile) == false)
						{
							_configurations.Add(dataFile);
						}
					}
				}
			}
		}

		/// <summary>
		/// Unregister a data file.
		/// </summary>
		/// <param name="dataFile">
		/// The data file to unregister
		/// </param>
		public void UnRegisterDataFile(AspectEngineDataFile dataFile)
		{
			lock (_configurations)
			{
				var configs = _configurations.Where(c => c == dataFile).ToList();
				foreach (var config in configs)
				{
					_configurations.Remove(config);
				}
			}
		}

        /// <summary>
        /// Check if there are updates for the specified engine.
        /// </summary>
        /// <param name="engine">
        /// The engine to check for
        /// </param>
        /// <param name="dataFileIdentifier">
        /// The identifier of the data file to check for.
        /// If the engine has only one data file then this parameter is ignored.
        /// </param>
        /// <returns>
        /// The result of the update check.
        /// </returns>
        /// <exception cref="DataUpdateException">
        /// Thrown if some problem occurs during the update process.
        /// </exception>
		/// <exception cref="ArgumentNullException">
		/// Thrown if the parameter is null
		/// </exception>        
		public AutoUpdateStatus CheckForUpdate(
			IOnPremiseAspectEngine engine, 
			string dataFileIdentifier = null)
		{
			if (engine == null)
			{
				throw new ArgumentNullException(nameof(engine));
			}

			AutoUpdateStatus result;
			var dataFile = engine.GetDataFileMetaData(dataFileIdentifier);
			if(dataFile != null)
			{ 
				dataFile.Configuration.AutomaticUpdatesEnabled = false;
				result = CheckForUpdate(dataFile, true);
			}
			else
			{
				result = AutoUpdateStatus.AUTO_UPDATE_NO_CONFIGURATION;
			}
			return result;
		}

		/// <summary>
		/// Update the specified data file from a <see cref="MemoryStream"/>
		/// </summary>
		/// <param name="dataFile">
		/// The data file to update.
		/// </param>
		/// <param name="data">
		/// The data to update with.
		/// </param>
		/// <exception cref="DataUpdateException">
		/// Thrown if some problem occurs during the update process.
		/// </exception>
		/// <exception cref="ArgumentNullException">
		/// Thrown if arguments are null.
		/// </exception>
		public AutoUpdateStatus UpdateFromMemory(AspectEngineDataFile dataFile,
            MemoryStream data)
		{
			if (dataFile == null)
			{
				throw new ArgumentNullException(nameof(dataFile));
			}
			if (data == null)
			{
				throw new ArgumentNullException(nameof(data));
			}

			AutoUpdateStatus result = AutoUpdateStatus.AUTO_UPDATE_IN_PROGRESS;
            if (string.IsNullOrEmpty(dataFile.DataFilePath) == false)
            {
                // The engine has an associated data file so update it first.
                try
                {
                    _fileSystem.File.WriteAllBytes(dataFile.DataFilePath, data.GetBuffer());
				}
#pragma warning disable CA1031 // Do not catch general exception types
				// Catch any exceptions as this is a non-critical 
				// operation and we don't want it affecting anything else.
				catch (Exception ex)
				{
                    _logger.LogWarning($"An error occurred when writing to " +
                        $"'{dataFile.DataFilePath}'. The engine will be updated " +
                        $"to use the new data but the file on disk will still " +
                        $"contain old data.", ex);
				}
#pragma warning restore CA1031 // Do not catch general exception types
			}

			if (dataFile.Engine != null)
            {
                try
                {
                    // Refresh the engine using the new data.
                    dataFile.Engine.RefreshData(dataFile.Identifier, data);
                    result = AutoUpdateStatus.AUTO_UPDATE_SUCCESS;
                }
                catch (Exception ex)
                {
                    result = AutoUpdateStatus.AUTO_UPDATE_REFRESH_FAILED;
                    throw new DataUpdateException($"An error occurred when applying a " +
                        $"data update to engine '{dataFile.Engine.GetType().Name}'.", ex, result);
                }
            }
            else
            {
                result = AutoUpdateStatus.AUTO_UPDATE_SUCCESS;
            }

            return result;
        }
        #endregion

        #region Private methods
        /// <summary>
        /// Default method used to create Timer instances when a timer factory
        /// is not provided in the constructor.
        /// </summary>
        /// <param name="callback"></param>
        /// <param name="state"></param>
        /// <param name="dueTime"></param>
        /// <returns></returns>
        private Timer TimerFactory(TimerCallback callback, object state, TimeSpan dueTime)
		{
			return new Timer(callback, state, dueTime, TimeSpan.FromMilliseconds(-1));
		}

		/// <summary>
		/// Called when the 'CheckForUpdate' method is finished.
		/// </summary>
		/// <param name="args"></param>
		private void OnUpdateComplete(DataUpdateCompleteArgs args)
		{
			CheckForUpdateComplete?.Invoke(this, args);
		}

		/// <summary>
		/// Called when the 'CheckForUpdate' method is started.
		/// </summary>
		/// <param name="args"></param>
		private void OnUpdateStarted(DataUpdateEventArgs args)
		{
			CheckForUpdateStarted?.Invoke(this, args);
		}

		/// <summary>
		/// Event handler that is called when the data file is updated.
		/// </summary>
		/// <remarks>
		/// The <see cref="FileSystemWatcher"/> will raise multiple events 
		/// in many cases, for example, if a file is copied over an existing 
		/// file then 3 'changed' events will be raised.
		/// This handler deals with the extra events by using synchronisation 
		/// with a double-check lock to ensure that the update will only be 
		/// done once.
		/// </remarks>
		/// <param name="sender">
		/// The <see cref="FileSystemWatcher"/> sender of the event.
		/// </param>
		/// <param name="e">
		/// The event arguments.
		/// </param>
		private void DataFileUpdated(object sender, FileSystemEventArgs e)
		{
			try
            {
                OnDataFileUpdatedEntered?.Invoke();
            }
            catch
            {
                // nop -- ignore all errors
            }

			// Get the associated update configuration
			AspectEngineDataFile dataFile = null;
			try
			{
				dataFile = _configurations
					.Where(c => c.FileWatcher != null &&
						c.FileWatcher == sender)
					.SingleOrDefault();
			}
			catch (InvalidOperationException)
			{
				dataFile = null;
			}

			try
			{
				DataFileUpdatedInternal(sender, e, dataFile);
            }
			finally
			{
                try
                {
                    OnDataFileUpdatedWillExit?.Invoke();
                }
                catch
                {
                    // nop -- ignore all errors
                }
            }
		}
        private void DataFileUpdatedInternal(object sender, FileSystemEventArgs e, AspectEngineDataFile dataFile)
        {
            AutoUpdateStatus status = AutoUpdateStatus.AUTO_UPDATE_IN_PROGRESS;

			OnUpdateStarted(new DataUpdateEventArgs()
			{
				DataFile = dataFile
			});

			if (dataFile != null)
			{
				// Get the last modified time of the new data file
				DateTime dataTime = _fileSystem.File.GetLastWriteTimeUtc(e.FullPath);
				// Use a lock with a double check on file time to make
				// sure we only run the update once even if multiple
				// events fire for a single file.
				if (dataFile.LastUpdateFileModifiedTime < dataTime)
				{
					lock (dataFile.UpdateSyncLock)
					{
						if (dataFile.LastUpdateFileModifiedTime < dataTime)
						{
							dataFile.LastUpdateFileModifiedTime = dataTime;

							// Make sure we can actually open the file for reading
							// before notifying the engine, otherwise the copy 
							// may still be in progress.
							bool fileLockable = false;
							DateTime timeout = DateTime.UtcNow.AddMinutes(10);

							while (fileLockable == false &&
								timeout > DateTime.UtcNow)
							{
								try
								{
									using (_fileSystem.File.OpenRead(e.FullPath)) { }
									fileLockable = true;
								}
								catch (IOException)
								{
									// Wait for half a second before trying again.
									Task.Delay(500).Wait();
								}

								// Complete the update
								status = UpdatedDataAvailable(dataFile);
							}

							if(fileLockable == false)
							{
								status = AutoUpdateStatus.AUTO_UPDATE_TIMEOUT;
							}
						}
					}
				}
				else
				{
					status = AutoUpdateStatus.AUTO_UPDATE_NOT_NEEDED;
				}
			}

			OnUpdateComplete(new DataUpdateCompleteArgs()
			{
				DataFile = dataFile,
				Status = status
			});
		}

		private void DebugDescribeException(Exception x, bool deep = false)
		{
			if (!DebugLoggingEnabled)
			{
				return;
			}
			Func<string>[] reportPoints = {
				() => $"type: {x.GetType().Name}",
                () => $"message: {x.Message}",
                () => $"inner exception: {x.InnerException}",
                () => $"trace: {x.StackTrace}",
                () => $"full: {x}",
            };
			foreach (var p in reportPoints)
			{
				try
				{
					LogDebugMessage(() => $"Exception (deep={deep}) {p()}", null);
				}
				catch (Exception e)
				{
					if (!deep)
					{
						DebugDescribeException(e, true);
					}
				}
			}
		}

		/// <summary>
		/// Private method called by update timers when an update is believed
		/// to be available.
		/// </summary>
		/// <param name="state">
		/// The <see cref="AspectEngineDataFile"/>
		/// </param>
		private void CheckForUpdate(object state)
        {
			try
			{
				OnTimeredCheckForUpdateEntered?.Invoke();
            }
			catch
			{
				// nop --- ignore all errors
			}

			try
			{
				// This method is called from a background thread so
				// we need to make sure any exceptions that occur
				// are handled here.
				var onUpdateCompleteReports = new List<DataUpdateCompleteArgs>();
				Action<Exception> reportUnknownException = x =>
				{
					AspectEngineDataFile dataFile = state == null ? null :
						state as AspectEngineDataFile;
					string msg = string.Format(CultureInfo.InvariantCulture,
						Messages.MessageAutoUpdateUnhandledError,
						dataFile?.EngineType?.Name ?? "Unknown");
					_logger.LogError(msg, x);
				};
				try
				{
					if (state == null)
					{
						throw new ArgumentNullException(nameof(state));
					}

					CheckForUpdate(state, false, onUpdateCompleteReports.Add);
				}
				catch (DataUpdateException ex)
				{
					LogDebugMessage(() => $"Exception of type '{ex.GetType().Name}' received into {nameof(DataUpdateException)} clause.", null);
					DebugDescribeException(ex);
					_logger.LogError(Messages.ExceptionAutoUpdate, ex);
				}
#pragma warning disable CA1031 // Do not catch general exception types
				// We want to catch any possible exception here so that 
				// the relevant details can be logged.
				catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
				{
					LogDebugMessage(() => $"Exception of type '{ex.GetType().Name}' received into {nameof(Exception)} clause.", null);
					DebugDescribeException(ex);
					reportUnknownException(ex);
				}

				LogDebugMessage(() => $"At midpoint of {nameof(CheckForUpdate)} -- will call {nameof(OnUpdateComplete)} with {onUpdateCompleteReports.Count} reports", null);

				try
				{
					foreach (var report in onUpdateCompleteReports)
					{
						OnUpdateComplete(report);
					}
				}
				catch (Exception ex)
				{
					reportUnknownException(ex);
				}

				LogDebugMessage(() => $"Exiting timer-bound {nameof(CheckForUpdate)}", null);
			}
			finally
			{
				try
				{
					OnTimeredCheckForUpdateWillExit?.Invoke();
				}
				catch
				{
					// nop --- ignore all errors
				}
			}
        }

		/// <summary>
		/// Private method that performs the following actions:
		/// 1. Checks for an update to the data file on disk.
		/// 2. Checks for an update using the update URL.
		/// 3. Refresh engine with new data if available.
		/// 4. Schedule the next update check if needed.
		/// </summary>
		/// <param name="state">
		/// The <see cref="AspectEngineDataFile"/>
		/// </param>
		/// <param name="manualUpdate">
		/// True if this update was requested by the user and or is running 
		/// synchronously from the 'main' thread. 
		/// (For example, when the update on startup option is used)
		/// False if it was triggered by a timer expiring and is therefore
		/// running in a background thread.
		/// </param>
		private AutoUpdateStatus CheckForUpdate(object state, bool manualUpdate)
			=> CheckForUpdate(state, manualUpdate, OnUpdateComplete);
        private AutoUpdateStatus CheckForUpdate(object state, bool manualUpdate, Action<DataUpdateCompleteArgs> onUpdateComplete)
        {
            AutoUpdateStatus result = AutoUpdateStatus.AUTO_UPDATE_IN_PROGRESS;
            AspectEngineDataFile dataFile = state as AspectEngineDataFile;
            LogDebugMessage(() => $"Starting {nameof(CheckForUpdate)} with {nameof(result)} = {result}", dataFile);
            bool newDataAvailable = false;

			OnUpdateStarted(new DataUpdateEventArgs()
			{
				DataFile = dataFile
			});

			try
			{
				if (dataFile != null)
				{
					LogInfoMessage(Messages.MessageAutoUpdateCheck, dataFile);

					// Only check the file system if the file system watcher
					// is not enabled and the engine is using a temporary file.
					if (dataFile.Configuration.FileSystemWatcherEnabled == false &&
						string.IsNullOrEmpty(dataFile.DataFilePath) == false &&
						string.IsNullOrEmpty(dataFile.TempDataFilePath) == false)
					{
						LogInfoMessage(Messages.MessageAutoUpdateCheckFileSystem, dataFile);

						// We use last write time as creation time can be 
						// unreliable for this check.
						// For example, if a file exists with created date 
						// yesterday and a new file is manually copied over 
						// the top then the creation date will not change.
						var fileWriteTime = _fileSystem.File.GetLastWriteTimeUtc(
							dataFile.DataFilePath);
						var tempFileWriteTime = _fileSystem.File.GetLastWriteTimeUtc(
							dataFile.TempDataFilePath);

						// If the data file is newer than the temp file currently
						// being used by the engine the we need to tell the engine
						// to refresh itself.
						if (fileWriteTime > tempFileWriteTime)
						{
							newDataAvailable = true;
						}
					}
					LogDebugMessage(() => $"{nameof(newDataAvailable)} is yet {newDataAvailable}", dataFile);

					if (newDataAvailable == false &&
						string.IsNullOrEmpty(dataFile.Configuration.DataUpdateUrl) == false)
					{
						result = CheckForUpdateFromUrl(dataFile);
						LogDebugMessage(() => $"{nameof(CheckForUpdateFromUrl)} resulted in {result}", dataFile);
						newDataAvailable =
							result == AutoUpdateStatus.AUTO_UPDATE_IN_PROGRESS ||
							result == AutoUpdateStatus.AUTO_UPDATE_SUCCESS;
					}
					LogDebugMessage(() => $"{nameof(newDataAvailable)} is {newDataAvailable}", dataFile);

					if (newDataAvailable == false)
					{
						result = AutoUpdateStatus.AUTO_UPDATE_NOT_NEEDED;
						LogDebugMessage(() => $"{nameof(result)} is {result}", dataFile);
					}
					else if (result == AutoUpdateStatus.AUTO_UPDATE_IN_PROGRESS)
					{
						// Data update was available but engine has not 
						// yet been refreshed.
						result = UpdatedDataAvailable(dataFile);
						LogDebugMessage(() => $"{nameof(UpdatedDataAvailable)} resulted in {result}", dataFile);
					}

					if (result == AutoUpdateStatus.AUTO_UPDATE_SUCCESS)
					{
						// Clear the timer ready for a new one to be created.
						ClearTimer(dataFile);
						// Re-register the engine with the data update service 
						// so it knows when next set of data should be available.
						RegisterDataFile(dataFile);
					}
				}
			}
			// Catch exceptions to make sure that status is set correctly 
			// when firing the UpdateComplete event, then rethrow them
			// so that the caller has visibility of the exception.
			catch (DataUpdateException dex)
			{
				result = dex.Status;
				throw;
			}
			catch (Exception)
			{
				result = AutoUpdateStatus.AUTO_UPDATE_UNKNOWN_ERROR;
				throw;
			}
			finally
			{
				LogDebugMessage(() => $"Entering {nameof(CheckForUpdate)}-finally with {nameof(newDataAvailable)} = {newDataAvailable}, {nameof(result)} = {result}", dataFile);
				if (newDataAvailable == false)
				{
					// No update available.
					// If this was a manual call to update then do nothing.
					// If it was triggered by the timer expiring then modify
					// the timer to check again after the configured interval.
					// This will repeat until the update is acquired.
					if (manualUpdate == false &&
						dataFile != null &&
						dataFile.Timer != null)
					{
						dataFile.Timer.Change(
							GetInterval(dataFile.Configuration),
							TimeSpan.FromMilliseconds(-1));
					}
				}

				onUpdateComplete(new DataUpdateCompleteArgs()
				{
					DataFile = dataFile,
					Status = result
				});
			}

            return result;
        }

		/// <summary>
		/// Download an update for the specified data file from its update URL.
		/// </summary>
		/// <param name="dataFile">
		/// The <see cref="AspectEngineDataFile"/> containing the 
		/// update configuration settings.
		/// </param>
		/// <returns>
		/// An <see cref="AutoUpdateStatus"/> value indicating the result
		/// of the update.
		/// </returns>
		private AutoUpdateStatus CheckForUpdateFromUrl(AspectEngineDataFile dataFile)
		{
			AutoUpdateStatus result = AutoUpdateStatus.AUTO_UPDATE_IN_PROGRESS;
            LogDebugMessage(() => $"Starting {nameof(CheckForUpdateFromUrl)} with {nameof(result)} = {result}", dataFile);

			if (dataFile.Configuration.MemoryOnly)
			{
                LogDebugMessage(() => $"{nameof(dataFile.Configuration.MemoryOnly)} is {dataFile.Configuration.MemoryOnly}", dataFile);
                
				// Perform the update entirely in memory.

				// The uncompressed stream may be read by the engine at a
				// later time. In that case we must not dispose of it 
				// within this method. As such, we cannot use a simple
				// 'using' statement.
#pragma warning disable CA2000 // Dispose objects before losing scope
				var uncompressedStream = new MemoryStream();
#pragma warning restore CA2000 // Dispose objects before losing scope
				bool disposeUncompressedStream = true;
				try
				{
					using (var compressedStream = new MemoryStream())
					{
						result = CheckForUpdateFromUrl(dataFile,
							compressedStream,
							uncompressedStream);
                        LogDebugMessage(() => $"{nameof(CheckForUpdateFromUrl)} resulted in {result}", dataFile);
						if (result == AutoUpdateStatus.AUTO_UPDATE_IN_PROGRESS)
						{
							if (dataFile.Engine != null)
							{
								try
								{
									LogInfoMessage(Messages.MessageAutoUpdateRefreshEngine, dataFile);
									// Tell the engine to refresh itself with
									// the new data.
									dataFile.Engine.RefreshData(dataFile.Identifier, uncompressedStream);
									result = AutoUpdateStatus.AUTO_UPDATE_SUCCESS;
								}
								catch (Exception ex)
								{
									result = AutoUpdateStatus.AUTO_UPDATE_REFRESH_FAILED;
									string msg = string.Format(
										CultureInfo.InvariantCulture,
										Messages.ExceptionAutoUpdateRefreshEngine,
										dataFile.Engine.GetType().Name);
									throw new DataUpdateException(msg, ex, result);
								}
							}
							else
							{
								// No associated engine at the moment so just
								// set the value of the data stream.
								dataFile.Configuration.DataStream = uncompressedStream;
								result = AutoUpdateStatus.AUTO_UPDATE_SUCCESS;
								// Make sure the stream will not be disposed 
								// before the engine is created with it.
								disposeUncompressedStream = false;
							}
						}
					}
				}
				finally
				{
					if (disposeUncompressedStream)
					{
						uncompressedStream?.Dispose();
					}
				}
			}
			else
			{
                if (string.IsNullOrEmpty(dataFile.TempDataDirPath))
                {
                    throw new DataUpdateException($"The data file " +
                        $"'{dataFile.Identifier}' is checking for updates but " +
                        $"does not have a temporary file path configured.", 
						AutoUpdateStatus.AUTO_UPDATE_TEMP_PATH_NOT_SET); 
                }

                // There is a data file path so use the temporary
                // file location to store data while we work on downloading
                // and decompressing it.
                string compressedTempFile = Path.Combine(dataFile.TempDataDirPath,
					$"{dataFile.Identifier}-{Guid.NewGuid()}.tmp");
				string uncompressedTempFile = Path.Combine(dataFile.TempDataDirPath,
					$"{dataFile.Identifier}-{Guid.NewGuid()}.tmp");
                if(_fileSystem.Directory.Exists(dataFile.TempDataDirPath) == false)
                {
                    _fileSystem.Directory.CreateDirectory(dataFile.TempDataDirPath);
                }                

				try
				{
					using (var compressedStream = _fileSystem.File.Create(compressedTempFile))
					using (var uncompressedStream = _fileSystem.File.Create(uncompressedTempFile))
					{
						result = CheckForUpdateFromUrl(dataFile,
							compressedStream,
							uncompressedStream);
                        LogDebugMessage(() => $"{nameof(CheckForUpdateFromUrl)} resulted in {result}", dataFile);
					}

					if (result == AutoUpdateStatus.AUTO_UPDATE_IN_PROGRESS)
					{
						// If this engine has a data file watcher then we need to
						// disable it while the update is occurring.
						if (dataFile != null &&
							dataFile.FileWatcher != null)
						{
							dataFile.FileWatcher.EnableRaisingEvents = false;
                            LogDebugMessage(() => $"Disabled {nameof(dataFile.FileWatcher)} events", dataFile);
						}

						try
						{
                            LogDebugMessage(() => $"Will copy {uncompressedTempFile} into {dataFile.DataFilePath}", dataFile);
                            // Copy the uncompressed file to the engine's 
                            // data file location
                            _fileSystem.File.Copy(uncompressedTempFile,
								dataFile.DataFilePath, true);
                            LogDebugMessage(() => $"Did copy {uncompressedTempFile} into {dataFile.DataFilePath}", dataFile);
							// Ensure creation time of the file is set
							// correctly so that the 'If-Modified-Since' 
							// header will be set to the expected value.
							_fileSystem.File.SetCreationTimeUtc(
								dataFile.DataFilePath, DateTime.UtcNow);
                            LogDebugMessage(() => $"Did update timestamp of {dataFile.DataFilePath}", dataFile);
						}
						catch (Exception ex)
						{
							result = AutoUpdateStatus.AUTO_UPDATE_NEW_FILE_CANT_RENAME;
                            throw new DataUpdateException($"An error occurred when copying a " +
								$"data file to replace the existing one at " +
								$"'{dataFile.DataFilePath}'.", ex, result);
						}
						finally
						{
							// Make sure to enable the file watcher again 
							// if needed.
							if (dataFile != null &&
								dataFile.FileWatcher != null)
							{
								dataFile.FileWatcher.EnableRaisingEvents = true;
                                LogDebugMessage(() => $"Restored {nameof(dataFile.FileWatcher)} events", dataFile);
							}
						}
					}
				}
				finally
				{
                    LogDebugMessage(() => $"Entering {nameof(CheckForUpdateFromUrl)}-finally", dataFile);
					// Make sure the temp files are cleaned up
					if (_fileSystem.File.Exists(compressedTempFile))
					{
                        _fileSystem.File.Delete(compressedTempFile);
					}
					if (_fileSystem.File.Exists(uncompressedTempFile))
					{
                        _fileSystem.File.Delete(uncompressedTempFile);
					}
				}
			}

			return result;
		}

		/// <summary>
		/// Download an update for the specified data file from its update URL.
		/// </summary>
		/// <param name="dataFile">
		/// The <see cref="AspectEngineDataFile"/> containing the 
		/// update configuration settings.
		/// </param>
		/// <param name="compressedDataStream">
		/// A <see cref="Stream"/> to write the data to as it is downloaded.
		/// </param>
		/// <param name="uncompressedDataStream">
		/// A <see cref="Stream"/> to write the uncompressed data to once
		/// download is complete.
		/// If the <see cref="DataFileConfiguration.DecompressContent"/>
		/// flag is set to false then this can be null.
		/// </param>
		/// <returns>
		/// An <see cref="AutoUpdateStatus"/> value indicating the result
		/// of the update.
		/// </returns>
		private AutoUpdateStatus CheckForUpdateFromUrl(
			AspectEngineDataFile dataFile,
			Stream compressedDataStream,
			Stream uncompressedDataStream)
		{
			AutoUpdateStatus result = AutoUpdateStatus.AUTO_UPDATE_IN_PROGRESS;

            string expectedMd5Hash = null;
			// Check if there is an update and download it if there is                   
			result = DownloadFile(dataFile, compressedDataStream, out expectedMd5Hash);
            LogDebugMessage(() => $"{nameof(DownloadFile)} resulted in {result}, {nameof(expectedMd5Hash)} = {expectedMd5Hash}", dataFile);
			// Check data integrity
			if (result == AutoUpdateStatus.AUTO_UPDATE_IN_PROGRESS &&
				dataFile.Configuration.VerifyMd5)
			{
                LogDebugMessage(() => $"Will call {nameof(VerifyMd5)}", dataFile);
				result = VerifyMd5(dataFile, expectedMd5Hash, compressedDataStream);
                LogDebugMessage(() => $"{nameof(VerifyMd5)} resulted in {result}", dataFile);
			}
			// decompress the file
			if (result == AutoUpdateStatus.AUTO_UPDATE_IN_PROGRESS)
			{
				if (dataFile.Configuration.DecompressContent)
				{
                    LogDebugMessage(() => $"Will call {nameof(Decompress)}", dataFile);
					result = Decompress(
						compressedDataStream, uncompressedDataStream);
                    LogDebugMessage(() => $"{nameof(Decompress)} resulted in {result}", dataFile);
				}
				else
				{
					// If decompression is not needed then just replace
					// the uncompressed stream object with the 'compressed' one
					compressedDataStream.Seek(0, 0);
					compressedDataStream.CopyTo(uncompressedDataStream);
                    // Reset the position to the start of the uncompressed
                    // stream
                    uncompressedDataStream.Seek(0, SeekOrigin.Begin);
                }
			}

			return result;
		}

		/// <summary>
		/// Called when a data update is available and the file at 
		/// engine.DataFilePath contains this new data.
		/// 1. Refresh the engine.
		/// 2. Dispose of the existing update timer if there is one.
		/// 3. Re-register the engine with the update service.
		/// </summary>
		/// <param name="dataFile"></param>
		/// <returns></returns>
		private AutoUpdateStatus UpdatedDataAvailable(
			AspectEngineDataFile dataFile)
		{
			AutoUpdateStatus result = AutoUpdateStatus.AUTO_UPDATE_IN_PROGRESS;

			Exception exception = null;
			int tries = 0;

            if (dataFile.Engine != null)
            {
                LogInfoMessage(Messages.MessageAutoUpdateRefreshEngine, dataFile);

                // Try to update the file multiple times to ensure the file is not 
                // locked.
                while (result != AutoUpdateStatus.AUTO_UPDATE_SUCCESS && tries < 10)
				{
					try
					{
						dataFile.Engine.RefreshData(dataFile.Identifier);
						result = AutoUpdateStatus.AUTO_UPDATE_SUCCESS;
					}
#pragma warning disable CA1031 // Do not catch general exception types
					// The code executed by the RefreshData method could
					// be anything at all. Third parties can create their
					// own implementations so we need to be able to handle
					// any exception.
					catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
					{
						exception = ex;
						result = AutoUpdateStatus.AUTO_UPDATE_REFRESH_FAILED;
						Thread.Sleep(200);
					}
					tries++;
				}

				if (tries == 10)
				{
                    throw new DataUpdateException($"An error occurred when applying a " +
							$"data update to engine '{dataFile.Engine.GetType().Name}' " +
							$"after {tries} tries.", exception, result);
				}
			}
			else
			{
				// Engine not yet set so no need to refresh it.
				// We can consider the update a success.
				result = AutoUpdateStatus.AUTO_UPDATE_SUCCESS;
			}

			return result;
		}

		/// <summary>
		/// Clear the timer ready for a new one to be created.
		/// </summary>
		private static void ClearTimer(AspectEngineDataFile dataFile)
		{
			if (dataFile != null &&
				dataFile.Timer != null)
			{
				// Dispose of the old timer object
				dataFile.Timer.Dispose();
				dataFile.Timer = null;
			}
		}

		/// <summary>
		/// Get the most recent data file available from the configured 
		/// update URL.
		/// If the data currently used by the engine is the newest available
		/// then nothing will be downloaded.
		/// </summary>
		/// <param name="dataFile">
		/// The <see cref="AspectEngineDataFile"/> to use.
		/// </param>
		/// <param name="tempStream">
		/// The stream to write the data to.
		/// </param>
		/// <param name="expectedMd5Hash">
		/// Used to output the md5 hash for the file from the 
		/// 'Content-MD5' header.
		/// </param>
		/// <returns>
		/// An <see cref="AutoUpdateStatus"/> value indicating the result
		/// </returns>
		private AutoUpdateStatus DownloadFile(
			AspectEngineDataFile dataFile,
			Stream tempStream,
			out string expectedMd5Hash)
		{
			AutoUpdateStatus result = AutoUpdateStatus.AUTO_UPDATE_IN_PROGRESS;
			expectedMd5Hash = null;
			
			Uri url = dataFile.FormattedUri;
            LogInfoMessage($"Checking for update from {url.AbsoluteUri}", dataFile);

			using (HttpRequestMessage message = new HttpRequestMessage(HttpMethod.Get, url))
			{

				// Get data file published date from meta-data.
				// If it's not been set for some reason then use the 
				// creation date of the file instead.
				DateTime publishDate = dataFile.DataPublishedDateTime;
				if (dataFile.DataPublishedDateTime <= DateTime.MinValue &&
					string.IsNullOrEmpty(dataFile.DataFilePath) == false &&
					 _fileSystem.File.Exists(dataFile.DataFilePath))
				{
					publishDate = _fileSystem.File.GetCreationTimeUtc(dataFile.DataFilePath);
				}

				// Set last-modified header to ensure that a file will only
				// be downloaded if it is newer than the data we already have.
				if (dataFile.Configuration.VerifyModifiedSince == true)
				{
					message.Headers.Add(
						 "If-Modified-Since",
						 publishDate.ToString("R", CultureInfo.InvariantCulture));
				}

				HttpResponseMessage response = null;
				try
				{
					// Send the message
					response = _httpClient.SendAsync(message).Result;
				}
				catch (Exception ex)
				{
					result = AutoUpdateStatus.AUTO_UPDATE_HTTPS_ERR;
					throw new DataUpdateException($"Error accessing data update service at " +
						$"'{url}' for engine '{dataFile.EngineType?.Name}'", ex, result);
				}

				if (result == AutoUpdateStatus.AUTO_UPDATE_IN_PROGRESS)
				{
					if (response == null)
					{
						result = AutoUpdateStatus.AUTO_UPDATE_HTTPS_ERR;
						throw new DataUpdateException($"No response from data update service at " +
							$"'{url}' for engine '{dataFile.EngineType?.Name}'", result);
					}
					else
					{
						if (response.IsSuccessStatusCode)
						{
							_logger.LogInformation(
								$"Downloaded new data from '{url}' for engine " +
								$"'{dataFile.EngineType?.Name}'");

							// If the response is successful then save the content to a 
							// temporary file
							LogDebugMessage(() => $"Will call {nameof(response.Content.ReadAsStreamAsync)}", dataFile);
							using (var dataStream = response.Content.ReadAsStreamAsync().Result)
                            {
                                LogDebugMessage(() => $"Will copy {nameof(dataStream)} into {nameof(tempStream)}", dataFile);
                                dataStream.CopyTo(tempStream);
                                LogDebugMessage(() => $"Did copy {nameof(dataStream)} into {nameof(tempStream)}", dataFile);
                            }
                            LogDebugMessage(() => $"Will test for {nameof(dataFile.Configuration.VerifyMd5)}", dataFile);
                            if (dataFile.Configuration.VerifyMd5)
                            {
                                LogDebugMessage(() => $"Did test for {nameof(dataFile.Configuration.VerifyMd5)}", dataFile);
                                IEnumerable<string> values;
								if (response.Content.Headers.TryGetValues("Content-MD5", out values))
								{
									expectedMd5Hash = values.SingleOrDefault();
									var lastHash = expectedMd5Hash;
                                    LogDebugMessage(() => $"{nameof(expectedMd5Hash)} = {lastHash}", dataFile);
                                }
								else
								{
									_logger.LogWarning(
										$"No MD5 hash included in data update response for " +
										$"'{url}'. Unable to verify data integrity");
								}
							}
						}
						else
						{
							switch (response.StatusCode)
							{
								// Note: needed because TooManyRequests is not available 
								// in some versions of the HttpStatusCode enum.
								case ((HttpStatusCode)429):
									result = AutoUpdateStatus.
										AUTO_UPDATE_ERR_429_TOO_MANY_ATTEMPTS;
									throw new DataUpdateException($"Too many requests to " +
										$"'{url}' for engine '{dataFile.EngineType?.Name}'", result);
								case HttpStatusCode.NotModified:
									result = AutoUpdateStatus.AUTO_UPDATE_NOT_NEEDED;
									_logger.LogInformation($"No data newer than " +
										$"{publishDate} found at '{url}' for engine " +
										$"'{dataFile.EngineType?.Name}'"); ;
									break;
								case HttpStatusCode.Forbidden:
									result = AutoUpdateStatus.AUTO_UPDATE_ERR_403_FORBIDDEN;
									throw new DataUpdateException($"Access denied to data update service at " +
										$"'{url}' for engine '{dataFile.EngineType?.Name}'", result);
								default:
									result = AutoUpdateStatus.AUTO_UPDATE_HTTPS_ERR;
									throw new DataUpdateException($"HTTP status code '{response.StatusCode}' " +
										$"from data update service at " +
										$"'{url}' for engine '{dataFile.EngineType?.Name}'", result);
							}
						}
					}
				}
			}

			return result;
		}

		/// <summary>
		/// Reads a source GZip stream and writes the uncompressed data to 
		/// destination stream.
		/// </summary>
		/// <param name="compressedDataStream">
		/// Stream containing GZipped data to be uncompressed
		/// </param>
		/// <param name="uncompressedDataStream">
		/// Stream to write the uncompressed data to.
		/// </param>
		/// <returns>The current state of the update process.</returns>
		private static AutoUpdateStatus Decompress(
			Stream compressedDataStream,
			Stream uncompressedDataStream)
		{
			AutoUpdateStatus status = AutoUpdateStatus.AUTO_UPDATE_IN_PROGRESS;
			compressedDataStream.Position = 0;
			using (var fis = new GZipStream(
				compressedDataStream, CompressionMode.Decompress, true))
			{
				fis.CopyTo(uncompressedDataStream);
            }
            // Reset the position to the start of the uncompressed stream
            uncompressedDataStream.Seek(0, SeekOrigin.Begin);
            return status;
		}

		/// <summary>
		/// Check that the specified file matches the specified md5 hash
		/// </summary>
		/// <param name="dataFile">
		/// The meta-data relating to the data file to verify
		/// </param>
		/// <param name="serverHash">
		/// The expected md5 hash value
		/// </param>
		/// <param name="compressedDataStream">
		/// The stream containing the data to hash
		/// </param>
		/// <returns>
		/// True if the hashes match, false if not.
		/// </returns>
		private AutoUpdateStatus VerifyMd5(
			AspectEngineDataFile dataFile, 
			string serverHash, Stream compressedDataStream)
		{
			AutoUpdateStatus status = AutoUpdateStatus.AUTO_UPDATE_IN_PROGRESS;
            LogDebugMessage(() => $"Starting {nameof(CheckForUpdate)} with {nameof(status)} = {status}", dataFile);
			string downloadHash = GetMd5(compressedDataStream);
            LogDebugMessage(() => $"{nameof(GetMd5)} resulted in {downloadHash}", dataFile);
			if (serverHash == null ||
				string.Equals(serverHash, downloadHash, StringComparison.Ordinal) == false)
			{
				status = AutoUpdateStatus.AUTO_UPDATE_ERR_MD5_VALIDATION_FAILED;
                LogDebugMessage(() => $"Set {nameof(status)} to {status}", dataFile);
                throw new DataUpdateException(
					$"Integrity check failed. MD5 hash in HTTP response " +
					$"'{serverHash}' for '{dataFile.EngineType?.Name}'" +
                    $"data update does not match calculated hash for the " +
					$"downloaded file '{downloadHash}'.", status);
			} 
			return status;
		}

		/// <summary>
		/// Calculates the MD5 hash of the given data array.
		/// </summary>
		/// <param name="compressedDataStream">
		/// The stream containing the data to hash
		/// </param>
		/// <returns>The MD5 hash of the given data.</returns>
		private string GetMd5(Stream compressedDataStream)
		{
            LogDebugMessage(() => $"Starting {nameof(GetMd5)}", null);
#pragma warning disable CA5351 // Do Not Use Broken Cryptographic Algorithms
			// TODO: Add support for a better hashing algorithm such as SHA512
			using (MD5 md5Hash = MD5.Create())
#pragma warning restore CA5351 // Do Not Use Broken Cryptographic Algorithms
			{
                LogDebugMessage(() => $"Created {nameof(MD5)} instance", null);
				compressedDataStream.Position = 0;
                LogDebugMessage(() => $"Did reset {nameof(compressedDataStream.Position)}", null);
				return GetMd5(md5Hash, compressedDataStream);
			}
		}

		/// <summary>
		/// Calculates the MD5 hash of the given data array.
		/// </summary>
		/// <param name="stream">calculate MD5 of this stream</param>
		/// <param name="md5Hash">instance of MD5 hash calculator</param>
		/// <returns>The MD5 hash of the given data.</returns>
		private string GetMd5(MD5 md5Hash, Stream stream)
		{
            LogDebugMessage(() => $"Starting {nameof(GetMd5)}", null);
			// Convert the input string to a byte array and compute the hash.
			byte[] data = md5Hash.ComputeHash(stream);

            LogDebugMessage(() => $"Did call {nameof(md5Hash.ComputeHash)}", null);
			// Create a new stringbuilder to collect the bytes
			// and create a string.
			StringBuilder sb = new StringBuilder();

			// Loop through each byte of the hashed data 
			// and format each one as a hexadecimal string.
			for (int i = 0; i < data.Length; i++)
			{
				sb.Append(data[i].ToString("x2", CultureInfo.InvariantCulture));
			}

			// Return the hexadecimal string.
			var result = sb.ToString();
            LogDebugMessage(() => $"{nameof(GetMd5)} will return {result}", null);
            return result;
		}
		
		/// <summary>
		/// Get an interval in the form of a <see cref="TimeSpan"/> based
		/// on the configuration object provided.
		/// If configured, a random additional number of seconds is added 
		/// between 0 and the specified maximum.
		/// </summary>
		/// <param name="config">
		/// The configuration to use to generate the interval
		/// </param>
		/// <returns>
		/// A TimeSpan representing time interval.
		/// </returns>
		private TimeSpan GetInterval(IDataFileConfiguration config)
		{
			int seconds = 0;
			if (config.PollingIntervalSeconds > 0)
			{
				seconds = config.PollingIntervalSeconds;
			}
			return ApplyIntervalRandomisation(
				TimeSpan.FromSeconds(seconds), config);
		}

		/// <summary>
		/// Add a random amount of time to the specified interval
		/// </summary>
		/// <param name="interval">
		/// The <see cref="TimeSpan"/> to add a random amount of time to.
		/// </param>
		/// <param name="config">
		/// The <see cref="IDataFileConfiguration"/> object that 
		/// specifies the maximum number of seconds to add.
		/// </param>
		private TimeSpan ApplyIntervalRandomisation(TimeSpan interval, 
			IDataFileConfiguration config)
		{
			int seconds = 0;
			if (config.MaxRandomisationSeconds > 0)
			{
				seconds = (int)(_rnd.NextDouble() *
					config.MaxRandomisationSeconds);
			}
			return interval.Add(TimeSpan.FromSeconds(seconds));
		}

        private void LogInfoMessage(
            string message, 
            IAspectEngineDataFile dataFile)
			=> _logger.LogInformation(BuildLogMessage(message, dataFile));

		private void LogDebugMessage(
			Func<string> message,
			IAspectEngineDataFile dataFile)
		{
			if (DebugLoggingEnabled) {
				_logger.LogDebug(BuildLogMessage(message(), dataFile));
			}
		}

        private static string BuildLogMessage(
            string message,
            IAspectEngineDataFile dataFile)
        {
            StringBuilder fullMessage = new StringBuilder();
            if (dataFile != null)
            {
                fullMessage.Append($"Data file '{dataFile.Identifier}' ");
                fullMessage.Append($"for engine '{dataFile.EngineType?.Name}'");
            }
            fullMessage.Append(message);
            return fullMessage.ToString();
        }
		#endregion
	}
}
