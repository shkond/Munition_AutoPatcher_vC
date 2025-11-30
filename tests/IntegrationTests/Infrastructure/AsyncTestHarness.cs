// Copyright (c) Munition AutoPatcher contributors. Licensed under the MIT License.

using IntegrationTests.Infrastructure.Models;
using Xunit.Abstractions;

namespace IntegrationTests.Infrastructure;

/// <summary>
/// T010: Coordinates CancellationToken, timeout enforcement, and orderly disposal
/// around MapperViewModel execution in E2E tests.
/// </summary>
public sealed class AsyncTestHarness : IAsyncDisposable
{
    private readonly CancellationTokenSource _cts;
    private readonly ITestOutputHelper? _output;
    private readonly List<IAsyncDisposable> _disposables = [];
    private readonly List<IDisposable> _syncDisposables = [];
    private bool _disposed;

    /// <summary>
    /// Creates a new AsyncTestHarness with the specified timeout.
    /// </summary>
    /// <param name="timeoutSeconds">Timeout in seconds (default 300).</param>
    /// <param name="output">Optional xUnit test output helper for logging.</param>
    public AsyncTestHarness(int timeoutSeconds = 300, ITestOutputHelper? output = null)
    {
        _cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        _output = output;
        TimeoutSeconds = timeoutSeconds;
    }

    /// <summary>
    /// Gets the timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; }

    /// <summary>
    /// Gets the cancellation token that will be cancelled after the timeout.
    /// </summary>
    public CancellationToken CancellationToken => _cts.Token;

    /// <summary>
    /// Gets whether cancellation has been requested (timeout or manual).
    /// </summary>
    public bool IsCancellationRequested => _cts.IsCancellationRequested;

    /// <summary>
    /// Registers an IAsyncDisposable for cleanup when the harness is disposed.
    /// </summary>
    public T RegisterForDisposal<T>(T disposable) where T : IAsyncDisposable
    {
        _disposables.Add(disposable);
        return disposable;
    }

    /// <summary>
    /// Registers an IDisposable for cleanup when the harness is disposed.
    /// </summary>
    public T RegisterForSyncDisposal<T>(T disposable) where T : IDisposable
    {
        _syncDisposables.Add(disposable);
        return disposable;
    }

    /// <summary>
    /// Manually triggers cancellation (e.g., on test failure).
    /// </summary>
    public void Cancel()
    {
        if (!_cts.IsCancellationRequested)
        {
            _output?.WriteLine("AsyncTestHarness: Cancellation requested.");
            _cts.Cancel();
        }
    }

    /// <summary>
    /// Executes an async operation with timeout and cancellation support.
    /// Returns a result indicating success/failure and capturing any exception.
    /// </summary>
    public async Task<ExecutionResult<T>> ExecuteWithTimeoutAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        string operationName = "Operation")
    {
        var startTime = DateTime.UtcNow;
        _output?.WriteLine($"AsyncTestHarness: Starting '{operationName}' with {TimeoutSeconds}s timeout.");

        try
        {
            var result = await operation(_cts.Token);
            var duration = DateTime.UtcNow - startTime;
            _output?.WriteLine($"AsyncTestHarness: '{operationName}' completed in {duration.TotalSeconds:F2}s.");
            return ExecutionResult<T>.Success(result, duration);
        }
        catch (OperationCanceledException) when (_cts.IsCancellationRequested)
        {
            var duration = DateTime.UtcNow - startTime;
            var message = $"'{operationName}' timed out after {duration.TotalSeconds:F2}s (limit: {TimeoutSeconds}s).";
            _output?.WriteLine($"AsyncTestHarness: {message}");
            return ExecutionResult<T>.Timeout(duration, message);
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow - startTime;
            _output?.WriteLine($"AsyncTestHarness: '{operationName}' failed after {duration.TotalSeconds:F2}s: {ex.GetType().Name}: {ex.Message}");
            return ExecutionResult<T>.Failure(ex, duration);
        }
    }

    /// <summary>
    /// Executes an async operation without a return value.
    /// </summary>
    public async Task<ExecutionResult> ExecuteWithTimeoutAsync(
        Func<CancellationToken, Task> operation,
        string operationName = "Operation")
    {
        var result = await ExecuteWithTimeoutAsync(async ct =>
        {
            await operation(ct);
            return true;
        }, operationName);

        return new ExecutionResult(result.IsSuccess, result.Duration, result.Exception, result.TimedOut, result.ErrorMessage);
    }

    /// <summary>
    /// Creates a linked CancellationTokenSource that will cancel if either the harness
    /// times out or the provided token is cancelled.
    /// </summary>
    public CancellationTokenSource CreateLinkedSource(CancellationToken other)
    {
        return CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, other);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _output?.WriteLine("AsyncTestHarness: Disposing registered resources...");

        // Dispose async resources first (environments, etc.)
        var asyncExceptions = new List<Exception>();
        foreach (var disposable in _disposables)
        {
            try
            {
                await disposable.DisposeAsync();
            }
            catch (Exception ex)
            {
                asyncExceptions.Add(ex);
                _output?.WriteLine($"AsyncTestHarness: Error disposing async resource: {ex.Message}");
            }
        }
        _disposables.Clear();

        // Then dispose sync resources
        var syncExceptions = new List<Exception>();
        foreach (var disposable in _syncDisposables)
        {
            try
            {
                disposable.Dispose();
            }
            catch (Exception ex)
            {
                syncExceptions.Add(ex);
                _output?.WriteLine($"AsyncTestHarness: Error disposing sync resource: {ex.Message}");
            }
        }
        _syncDisposables.Clear();

        // Finally dispose the CTS
        _cts.Dispose();

        if (asyncExceptions.Count > 0 || syncExceptions.Count > 0)
        {
            _output?.WriteLine($"AsyncTestHarness: Disposal completed with {asyncExceptions.Count + syncExceptions.Count} errors.");
        }
        else
        {
            _output?.WriteLine("AsyncTestHarness: Disposal completed successfully.");
        }
    }

    /// <summary>
    /// Creates a harness from an E2EScenarioDefinition.
    /// </summary>
    public static AsyncTestHarness FromScenario(E2EScenarioDefinition scenario, ITestOutputHelper? output = null)
    {
        return new AsyncTestHarness(scenario.GetEffectiveTimeoutSeconds(), output);
    }
}

/// <summary>
/// Result of an async operation execution.
/// </summary>
public readonly record struct ExecutionResult(
    bool IsSuccess,
    TimeSpan Duration,
    Exception? Exception,
    bool TimedOut,
    string? ErrorMessage)
{
    public static ExecutionResult Success(TimeSpan duration) =>
        new(true, duration, null, false, null);

    public static ExecutionResult Timeout(TimeSpan duration, string message) =>
        new(false, duration, null, true, message);

    public static ExecutionResult Failure(Exception ex, TimeSpan duration) =>
        new(false, duration, ex, false, ex.Message);
}

/// <summary>
/// Result of an async operation execution with a return value.
/// </summary>
public readonly record struct ExecutionResult<T>(
    bool IsSuccess,
    TimeSpan Duration,
    Exception? Exception,
    bool TimedOut,
    string? ErrorMessage,
    T? Value)
{
    public static ExecutionResult<T> Success(T value, TimeSpan duration) =>
        new(true, duration, null, false, null, value);

    public static ExecutionResult<T> Timeout(TimeSpan duration, string message) =>
        new(false, duration, null, true, message, default);

    public static ExecutionResult<T> Failure(Exception ex, TimeSpan duration) =>
        new(false, duration, ex, false, ex.Message, default);
}
