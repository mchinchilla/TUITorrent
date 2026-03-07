using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using TUITorrent.Infrastructure.Daemon;

namespace TUITorrent.Presentation.Commands;

public class StopTorrentCommand : AsyncCommand<StopTorrentCommand.Settings>
{
    private readonly DaemonClient _daemonClient;

    public StopTorrentCommand(DaemonClient daemonClient)
    {
        _daemonClient = daemonClient;
    }

    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<id>")]
        [Description("Torrent ID to stop")]
        public string Id { get; set; } = string.Empty;
    }

    public override async Task<int> ExecuteAsync(
        CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        await _daemonClient.EnsureDaemonRunningAsync(cancellationToken: cancellationToken);
        await _daemonClient.StopAsync(settings.Id, cancellationToken);
        AnsiConsole.MarkupLine($"[green]Torrent '{settings.Id}' stopped.[/]");
        return 0;
    }
}
