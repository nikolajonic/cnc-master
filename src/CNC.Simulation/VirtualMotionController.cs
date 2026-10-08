using CNC.Core.Abstractions;
using CNC.Core.Axes;
using CNC.Core.Common;
using CNC.Core.Configuration;
using CNC.Core.Geometry;
using CNC.Hardware.Motion;
using CNC.Motion.Limits;
using CNC.Motion.Planning;
using CNC.Simulation.Machine;
using Microsoft.Extensions.Logging;

namespace CNC.Simulation;

/// <summary>
/// <see cref="IMotionController"/> backed by a <see cref="VirtualMachine"/>. Motion is interpolated
/// over time with the configured velocity and acceleration limits; nothing teleports.
/// </summary>
public sealed class VirtualMotionController : IMotionController
{
    private readonly IMachineConfigurationService _configuration;
    private readonly IClock _clock;
    private readonly ILogger<VirtualMotionController> _logger;
    private readonly VirtualMotionControllerOptions _options;
    private readonly VirtualMachine _machine;
    private readonly object _lifecycleSync = new();
    private readonly object _publishSync = new();
    private CancellationTokenSource? _loopCancellation;
    private Task? _loop;
    private volatile bool _isConnected;
    private MotionState _lastPublishedState = MotionState.Disconnected;
    private TimeSpan _sinceLastStatus;
    private bool _disposed;

    public VirtualMotionController(
        IMachineConfigurationService configuration,
        IClock clock,
        ILogger<VirtualMotionController> logger,
        VirtualMotionControllerOptions options)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(options);

        _configuration = configuration;
        _clock = clock;
        _logger = logger;
        _options = options;

        var current = configuration.Current;
        _machine = new VirtualMachine(MotionLimits.FromConfiguration(current), InitialPosition(current));
        _configuration.Changed += OnConfigurationChanged;
    }

    public string Name => "Virtual machine";

    public ControllerMode Mode => ControllerMode.Simulation;

    public bool IsConnected => _isConnected;

    public MotionStatus Status
    {
        get
        {
            var status = _machine.GetStatus();
            return _isConnected || status.State == MotionState.EmergencyStop
                ? status
                : status with { State = MotionState.Disconnected };
        }
    }

    public event EventHandler<MotionStatusChangedEventArgs>? StatusChanged;

    public event EventHandler<MotionCompletedEventArgs>? MotionCompleted;

    public event EventHandler<MotionFaultEventArgs>? FaultRaised;

    public Task<Result> ConnectAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_lifecycleSync)
        {
            if (_isConnected)
            {
                return Task.FromResult(Result.Success());
            }

            _isConnected = true;
            if (_options.RunRealTimeLoop)
            {
                _loopCancellation = new CancellationTokenSource();
                _loop = Task.Run(() => RunLoopAsync(_loopCancellation.Token), CancellationToken.None);
            }
        }

        _logger.LogInformation("Connected to {Controller} ({Mode} mode) at {Position}", Name, Mode, _machine.Position);
        PublishStatus(force: true);
        return Task.FromResult(Result.Success());
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        Task? loop;
        CancellationTokenSource? cancellation;
        lock (_lifecycleSync)
        {
            if (!_isConnected)
            {
                return;
            }

            _isConnected = false;
            loop = _loop;
            cancellation = _loopCancellation;
            _loop = null;
            _loopCancellation = null;
        }

        if (_machine.GetStatus().IsBusy)
        {
            _logger.LogWarning("Disconnecting while motion is in progress; motion aborted");
        }

        _machine.AbortMotion();
        if (cancellation is not null)
        {
            await cancellation.CancelAsync().ConfigureAwait(false);
            if (loop is not null)
            {
                await loop.WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            cancellation.Dispose();
        }

        _logger.LogInformation("Disconnected from {Controller}", Name);
        PublishEvents();
        PublishStatus(force: true);
    }

    public Result Execute(MotionPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var result = RequireConnected();
        if (result.IsSuccess)
        {
            result = _machine.Execute(plan);
        }

        if (result.IsSuccess)
        {
            _logger.LogInformation(
                "Executing motion plan: {Segments} segments, {Length:0.###} units, estimated {Duration}",
                plan.Segments.Count,
                plan.TotalLength,
                plan.IsEmpty ? TimeSpan.Zero : plan.EstimateDuration(Status.FeedOverride > 0 ? Status.FeedOverride : 1));
        }
        else
        {
            _logger.LogWarning("Motion plan rejected: {Reason}", result.Error);
        }

        PublishEvents();
        PublishStatus(force: true);
        return result;
    }

    public Result FeedHold() => Command("Feed hold", _machine.FeedHold);

    public Result ResumeMotion() => Command("Resume", _machine.Resume);

    public Result StopMotion() => Command("Stop", _machine.Stop);

    public void EmergencyStop()
    {
        _machine.EmergencyStop();
        _logger.LogWarning("EMERGENCY STOP: simulated motion halted at {Position}", _machine.Position);
        PublishEvents();
        PublishStatus(force: true);
    }

    public Result Reset()
    {
        var result = _machine.Reset();
        if (result.IsSuccess)
        {
            _logger.LogInformation("Controller reset; emergency stop and faults cleared");
        }
        else
        {
            _logger.LogWarning("Reset rejected: {Reason}", result.Error);
        }

        PublishStatus(force: true);
        return result;
    }

    public Result SetFeedOverride(double factor)
    {
        var result = _machine.SetFeedOverride(factor);
        if (result.IsSuccess)
        {
            _logger.LogInformation("Feed override set to {Override:0}%", factor * 100);
            PublishStatus(force: true);
        }

        return result;
    }

    /// <summary>Advances simulated time. Called by the real-time loop, or directly by tests.</summary>
    public void Step(TimeSpan elapsed)
    {
        _machine.Advance(elapsed);
        PublishEvents();
        PublishStatus(force: false, elapsed);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _configuration.Changed -= OnConfigurationChanged;
        lock (_lifecycleSync)
        {
            _isConnected = false;
            _loopCancellation?.Cancel();
        }

        _machine.AbortMotion();
        try
        {
            _loop?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
            // The loop has already logged its failure; disposal must not throw.
        }

        _loopCancellation?.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(_options.TickInterval);
        var last = _clock.Elapsed;
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                var now = _clock.Elapsed;
                var elapsed = now - last;
                last = now;
                Step(elapsed > _options.MaxTickDuration ? _options.MaxTickDuration : elapsed);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Simulation loop failed; triggering emergency stop");
            EmergencyStop();
        }
    }

    private Result Command(string name, Func<Result> action)
    {
        var result = RequireConnected();
        if (result.IsSuccess)
        {
            result = action();
        }

        if (result.IsSuccess)
        {
            _logger.LogInformation("{Command} requested", name);
        }
        else
        {
            _logger.LogWarning("{Command} rejected: {Reason}", name, result.Error);
        }

        PublishEvents();
        PublishStatus(force: true);
        return result;
    }

    private Result RequireConnected() =>
        _isConnected ? Result.Success() : Result.Failure("The controller is not connected.");

    private void PublishEvents()
    {
        var (completions, faults) = _machine.DrainEvents();
        foreach (var fault in faults)
        {
            _logger.LogError("Motion fault ({Kind}): {Message}", fault.Fault.Kind, fault.Fault.Message);
            FaultRaised?.Invoke(this, fault);
        }

        foreach (var completion in completions)
        {
            _logger.LogInformation("Motion {Completion} at line {Line}", completion.Completion, completion.LastSourceLine);
            MotionCompleted?.Invoke(this, completion);
        }
    }

    private void PublishStatus(bool force, TimeSpan elapsed = default)
    {
        var status = Status;
        lock (_publishSync)
        {
            _sinceLastStatus += elapsed;
            var stateChanged = status.State != _lastPublishedState;
            if (!force && !stateChanged && !(status.IsMoving && _sinceLastStatus >= _options.StatusInterval))
            {
                return;
            }

            _lastPublishedState = status.State;
            _sinceLastStatus = TimeSpan.Zero;
        }

        StatusChanged?.Invoke(this, new MotionStatusChangedEventArgs(status));
    }

    private void OnConfigurationChanged(object? sender, MachineConfigurationChangedEventArgs e) =>
        _machine.UpdateLimits(MotionLimits.FromConfiguration(e.Current));

    /// <summary>Until homing exists (Phase 8) the simulated machine powers up at its home position.</summary>
    private static Position InitialPosition(MachineConfiguration configuration)
    {
        var position = Position.Zero;
        foreach (var axis in LinearAxes.All)
        {
            var config = configuration.GetAxis(axis);
            position = position.With(axis, Math.Clamp(config.HomePosition, config.MinPosition, config.MaxPosition));
        }

        return position;
    }
}
