using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using TUITorrent.Infrastructure.Daemon;

namespace TUITorrent.Presentation.Commands;

public class RemoveTorrentCommand : AsyncCommand<RemoveTorrentCommand.Settings>
{
    private readonly DaemonClient _daemonClient;

    public RemoveTorrentCommand(DaemonClient daemonClient)
    {
        _daemonClient = daemonClient;
    }

    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<id>")]
        [Description("Torrent ID to remove")]
        public string Id { get; set; } = string.Empty;
    }

    public override async Task<int> ExecuteAsync(
        CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        await _daemonClient.EnsureDaemonRunningAsync(cancellationToken: cancellationToken);
        await _daemonClient.RemoveAsync(settings.Id, cancellationToken);
        AnsiConsole.MarkupLine($"[green]Torrent '{settings.Id}' removed.[/]");
        return 0;
    }
}
