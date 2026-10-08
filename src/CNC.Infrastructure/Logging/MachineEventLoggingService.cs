using CNC.Core.Configuration;
using CNC.Core.Machine;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CNC.Infrastructure.Logging;

/// <summary>Writes machine state transitions and configuration changes to the application log.</summary>
public sealed class MachineEventLoggingService : IHostedService
{
    private readonly IMachineStateMachine _stateMachine;
    private readonly IMachineConfigurationService _configuration;
    private readonly ILogger<MachineEventLoggingService> _logger;

    public MachineEventLoggingService(
        IMachineStateMachine stateMachine,
        IMachineConfigurationService configuration,
        ILogger<MachineEventLoggingService> logger)
    {
        _stateMachine = stateMachine;
        _configuration = configuration;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _stateMachine.StateChanged += OnStateChanged;
        _stateMachine.TransitionRejected += OnTransitionRejected;
        _configuration.Changed += OnConfigurationChanged;
        _logger.LogInformation("Machine state is {State}", _stateMachine.Current);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _stateMachine.StateChanged -= OnStateChanged;
        _stateMachine.TransitionRejected -= OnTransitionRejected;
        _configuration.Changed -= OnConfigurationChanged;
        return Task.CompletedTask;
    }

    private void OnStateChanged(object? sender, MachineStateChangedEventArgs e)
    {
        var level = e.Current is MachineState.EmergencyStop or MachineState.Alarm ? LogLevel.Warning : LogLevel.Information;
        _logger.Log(level, "Machine state {Previous} -> {Current}: {Reason}", e.Previous, e.Current, e.Reason);
    }

    private void OnTransitionRejected(object? sender, MachineTransitionRejectedEventArgs e)
    {
        _logger.LogWarning("Rejected machine state transition {Current} -> {Requested}: {Reason}", e.Current, e.Requested, e.Reason);
    }

    private void OnConfigurationChanged(object? sender, MachineConfigurationChangedEventArgs e)
    {
        _logger.LogInformation(
            "Machine configuration applied: '{Name}', units {Units}, {AxisCount} axes",
            e.Current.Name, e.Current.Units, e.Current.Axes.Count);
    }
}
