using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using TUITorrent.Domain.Enums;
using TUITorrent.Infrastructure.Daemon;

namespace TUITorrent.Presentation.Commands;

public class PriorityCommand : AsyncCommand<PriorityCommand.Settings>
{
    private readonly DaemonClient _daemonClient;

    public PriorityCommand(DaemonClient daemonClient)
    {
        _daemonClient = daemonClient;
    }

    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<id>")]
        [Description("Torrent ID (8-character hex string)")]
        public string Id { get; set; } = string.Empty;

        [CommandArgument(1, "<priority>")]
        [Description("Priority level: low, normal, or high")]
        public string Priority { get; set; } = string.Empty;

        public override ValidationResult Validate()
        {
            if (!Enum.TryParse<TorrentPriority>(Priority, ignoreCase: true, out _))
                return ValidationResult.Error("Priority must be one of: low, normal, high");
            return ValidationResult.Success();
        }
    }

    public override async Task<int> ExecuteAsync(
        CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        await _daemonClient.EnsureDaemonRunningAsync(cancellationToken: cancellationToken);

        var priority = Enum.Parse<TorrentPriority>(settings.Priority, ignoreCase: true);
        await _daemonClient.SetPriorityAsync(settings.Id, priority, cancellationToken);

        var color = priority switch
        {
            TorrentPriority.High => "red",
            TorrentPriority.Low => "grey",
            _ => "yellow"
        };

        AnsiConsole.MarkupLine(
            $"[green]Priority of torrent[/] [bold cyan]{settings.Id}[/] [green]set to[/] [{color}]{priority}[/]");
        return 0;
    }
}
