using System;
using System.Threading;
using System.Threading.Tasks;
using FiftyOne.Pipeline.CloudRequestEngine.FailHandling.TaskWaitingCancellation;

namespace FiftyOne.Pipeline.CloudRequestEngine.FailHandling
{
    /// <summary>
    /// Uses the provided throwing delegate 
    /// to evaluate the value on demand.
    /// 
    /// Once the delegate succeeds, the result
    /// is saved for further use
    /// without re-evaluation.
    /// 
    /// At most one task calling to the delegate
    /// will exist at any given time.
    /// 
    /// External calls will be made to await
    /// for the active task to finish or fail.
    /// 
    /// If no saved result exists yet,
    /// and no active task evaluates the delegate,
    /// a single new task will be created.
    /// </summary>
    /// <typeparam name="TResult">
    /// Type of the value returned to the caller.
    /// </typeparam>
    public class AsyncLazyFailable<TResult>
    {
        private readonly Func<TResult> _mainFunc;
        private readonly object _taskLock = new object();
        private Task<TResult> _activeTask;
        private Task _errorHandlingTask;
        private TResult _result;

        // volatile:
        // - written after _result
        // - read before _result
        // see https://learn.microsoft.com/en-us/archive/msdn-magazine/2012/december/csharp-the-csharp-memory-model-in-theory-and-practice
        private volatile bool _hasResult;

        /// <summary>
        /// Designated constructor.
        /// </summary>
        /// <param name="mainFunc">
        /// Delegate to evaluate the value on demand.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="mainFunc"/>
        /// is null.
        /// </exception>
        public AsyncLazyFailable(Func<TResult> mainFunc)
        {
            if (mainFunc is null)
            {
                throw new ArgumentNullException(nameof(mainFunc));
            }
            _mainFunc = mainFunc;
        }

        /// <summary>
        /// Get the result or fallback value.
        /// </summary>
        /// <param name="token">
        /// Trip to cancel waiting for the
        /// current evaluation of the delegate to finish.
        /// </param>
        /// <returns>
        /// Saved result if delegate evaluated successfully.
        /// </returns>
        /// <exception cref="OperationCanceledException">
        /// <paramref name="token"/> was tripped.
        /// </exception>
        public Task<TResult> GetValueAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            // volatile read, can’t be reordered with subsequent operations
            if (_hasResult)
            {
                return Task.FromResult(_result);
            }
            var activeTask = GetOrBuildActiveTask();
            if (activeTask.IsCompleted)
            {
                return activeTask;
            }
            return activeTask.WithCancellation(token);
        }

        private Task<TResult> GetOrBuildActiveTask()
        {
            lock (_taskLock)
            {
                if (_activeTask is object) // i.e. is not null
                {
                    return _activeTask;
                }
                _activeTask = Task.Run(TryGetNewValue);
                _errorHandlingTask = ConsumeActiveTaskFailure(_activeTask);
                return _activeTask;
            }
        }

        private TResult TryGetNewValue()
        {
            try
            {
                _result = _mainFunc();

                // volatile write, can’t be reordered with prior operations
                _hasResult = true;

                return _result;
            }
            catch
            {
                lock (_taskLock)
                {
                    _activeTask = null;
                    _errorHandlingTask = null;
                }
                throw;
            }
        }

        private async Task ConsumeActiveTaskFailure(Task<TResult> mainTask)
        {
            try
            {
                _ = await mainTask;
            }
            catch
            {
                // nop -- just consume the failure
            }
        }
    }
}
