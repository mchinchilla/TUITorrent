using System.ComponentModel;
using Serilog;
using Spectre.Console;
using Spectre.Console.Cli;
using TUITorrent.Infrastructure.Daemon;
using TUITorrent.Infrastructure.Torrent;

namespace TUITorrent.Presentation.Commands;

public class DaemonStartCommand : AsyncCommand<DaemonStartCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandOption("--exit-when-done")]
        [Description("Automatically shut down the daemon when all downloads complete")]
        public bool ExitWhenDone { get; set; }
    }

    public override async Task<int> ExecuteAsync(
        CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                Path.Combine(DaemonPaths.ConfigDir, "daemon.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7)
            .CreateLogger();

        AnsiConsole.MarkupLine(settings.ExitWhenDone
            ? "[cyan]Starting TUITorrent daemon (exit-when-done)...[/]"
            : "[cyan]Starting TUITorrent daemon...[/]");

        await using var manager = new MonoTorrentManager();
        await using var server = new DaemonServer(manager, logger);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            AnsiConsole.MarkupLine("[yellow]Shutting down daemon...[/]");
            cts.Cancel();
        };

        await server.RunAsync(cts.Token, exitWhenDone: settings.ExitWhenDone);
        return 0;
    }
}

public class DaemonStopCommand : AsyncCommand<DaemonStopCommand.Settings>
{
    private readonly DaemonClient _daemonClient;

    public DaemonStopCommand(DaemonClient daemonClient)
    {
        _daemonClient = daemonClient;
    }

    public class Settings : CommandSettings;

    public override async Task<int> ExecuteAsync(
        CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        try
        {
            await _daemonClient.ShutdownAsync(cancellationToken);
            AnsiConsole.MarkupLine("[green]Daemon stopped.[/]");
        }
        catch
        {
            AnsiConsole.MarkupLine("[yellow]Daemon is not running.[/]");
        }
        return 0;
    }
}

public class DaemonStatusCommand : AsyncCommand<DaemonStatusCommand.Settings>
{
    public class Settings : CommandSettings;

    public override async Task<int> ExecuteAsync(
        CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        if (!File.Exists(DaemonPaths.PidFile) || !File.Exists(DaemonPaths.SocketPath))
        {
            AnsiConsole.MarkupLine("[yellow]Daemon is not running.[/]");
            return 1;
        }

        var pid = await File.ReadAllTextAsync(DaemonPaths.PidFile, cancellationToken);
        AnsiConsole.MarkupLine($"[green]Daemon is running.[/] PID: [bold]{pid.Trim()}[/]");
        AnsiConsole.MarkupLine($"[dim]Socket: {DaemonPaths.SocketPath}[/]");
        return 0;
    }
}
