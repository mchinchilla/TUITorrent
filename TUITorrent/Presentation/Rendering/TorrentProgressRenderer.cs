using Spectre.Console;
using TUITorrent.Application.Models;
using TUITorrent.Domain.Enums;
using TUITorrent.Domain.ValueObjects;

namespace TUITorrent.Presentation.Rendering;

public static class TorrentProgressRenderer
{
    public static Table RenderConfigTable(DownloadConfiguration config)
    {
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Parameter");
        table.AddColumn("Value");
        table.AddRow("Source", $"[cyan]{Markup.Escape(config.Source.DisplayName)}[/]");
        table.AddRow("Output", $"[green]{Markup.Escape(config.OutputDirectory)}[/]");
        table.AddRow("Port", $"{config.Port}");
        table.AddRow("DL Limit", config.MaxDownloadSpeedKbps == 0 ? "Unlimited" : $"{config.MaxDownloadSpeedKbps} KB/s");
        table.AddRow("UL Limit", config.MaxUploadSpeedKbps == 0 ? "Unlimited" : $"{config.MaxUploadSpeedKbps} KB/s");
        table.AddRow("Max Connections", $"{config.MaxConnections}");
        table.AddRow("Seed After", config.SeedAfterDownload ? "[green]Yes[/]" : "[red]No[/]");
        return table;
    }

    public static Table RenderProgressTable(TorrentInfo info)
    {
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Metric");
        table.AddColumn("Value");

        var stateColor = GetStateColor(info.State);

        table.AddRow("ID", $"[bold]{info.Id}[/]");
        table.AddRow("Name", $"[bold]{Markup.Escape(info.Name)}[/]");
        table.AddRow("State", $"[{stateColor}]{info.State}[/]");
        table.AddRow("Progress", $"[green]{info.ProgressPercent:F1}%[/]");
        table.AddRow("Download", $"[cyan]{FormatSpeed(info.DownloadRateKbps)}[/]");
        table.AddRow("Upload", $"[yellow]{FormatSpeed(info.UploadRateKbps)}[/]");
        table.AddRow("Peers", $"{info.Peers}");
        table.AddRow("Seeds", $"{info.Seeds}");

        if (info.TotalSizeBytes > 0)
            table.AddRow("Size", FormatSize(info.TotalSizeBytes));

        return table;
    }

    public static Table RenderDetailTable(TorrentInfo info)
    {
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Property");
        table.AddColumn("Value");

        var stateColor = GetStateColor(info.State);

        table.AddRow("ID", $"[bold]{info.Id}[/]");
        table.AddRow("Name", $"[bold]{Markup.Escape(info.Name)}[/]");
        table.AddRow("Source", $"[cyan]{Markup.Escape(info.Source)}[/]");
        table.AddRow("Output", $"[green]{Markup.Escape(info.OutputDirectory)}[/]");
        var priorityColor = GetPriorityColor(info.Priority);
        table.AddRow("State", $"[{stateColor}]{info.State}[/]");
        table.AddRow("Priority", $"[{priorityColor}]{info.Priority}[/]");
        table.AddRow("Progress", $"[green]{info.ProgressPercent:F1}%[/]");
        table.AddRow("Download", $"[cyan]{FormatSpeed(info.DownloadRateKbps)}[/]");
        table.AddRow("Upload", $"[yellow]{FormatSpeed(info.UploadRateKbps)}[/]");
        table.AddRow("Peers", $"{info.Peers}");
        table.AddRow("Seeds", $"{info.Seeds}");

        if (info.TotalSizeBytes > 0)
            table.AddRow("Size", FormatSize(info.TotalSizeBytes));

        table.AddRow("Added", $"{info.AddedAt:yyyy-MM-dd HH:mm:ss} UTC");
        return table;
    }

    public static Table RenderTorrentListTable(IReadOnlyList<TorrentInfo> torrents)
    {
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("ID");
        table.AddColumn("Name");
        table.AddColumn("Priority");
        table.AddColumn("State");
        table.AddColumn("Progress");
        table.AddColumn("DL Speed");
        table.AddColumn("UL Speed");
        table.AddColumn("Peers");
        table.AddColumn("Size");

        var sorted = torrents.OrderByDescending(t => t.Priority).ToList();

        foreach (var t in sorted)
        {
            var stateColor = GetStateColor(t.State);
            var priorityColor = GetPriorityColor(t.Priority);
            var name = t.Name.Length > 35
                ? t.Name[..32] + "..."
                : t.Name;

            table.AddRow(
                $"[bold]{t.Id}[/]",
                Markup.Escape(name),
                $"[{priorityColor}]{t.Priority}[/]",
                $"[{stateColor}]{t.State}[/]",
                $"[green]{t.ProgressPercent:F1}%[/]",
                $"[cyan]{FormatSpeed(t.DownloadRateKbps)}[/]",
                $"[yellow]{FormatSpeed(t.UploadRateKbps)}[/]",
                $"{t.Peers}",
                t.TotalSizeBytes > 0 ? FormatSize(t.TotalSizeBytes) : "-");
        }

        return table;
    }

    public static Table RenderSettingsTable(AppSettings settings)
    {
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Setting");
        table.AddColumn("Value");
        table.AddRow("Output Directory", $"[green]{Markup.Escape(settings.OutputDirectory)}[/]");
        table.AddRow("Listen Port", $"{settings.ListenPort}");
        table.AddRow("Max DL Speed", settings.MaxDownloadSpeedKbps == 0
            ? "Unlimited" : $"{settings.MaxDownloadSpeedKbps} KB/s");
        table.AddRow("Max UL Speed", settings.MaxUploadSpeedKbps == 0
            ? "Unlimited" : $"{settings.MaxUploadSpeedKbps} KB/s");
        table.AddRow("Max Connections", $"{settings.MaxConnections}");
        table.AddRow("Encryption", $"{settings.Encryption}");
        table.AddRow("Seed After DL", settings.SeedAfterDownload
            ? "[green]Yes[/]" : "[red]No[/]");
        return table;
    }

    private static string GetPriorityColor(TorrentPriority priority) => priority switch
    {
        TorrentPriority.High => "red",
        TorrentPriority.Low => "grey",
        _ => "yellow"
    };

    private static string GetStateColor(TorrentStatus state) => state switch
    {
        TorrentStatus.Downloading => "green",
        TorrentStatus.Seeding => "cyan",
        TorrentStatus.Hashing => "yellow",
        TorrentStatus.Error => "red",
        TorrentStatus.Stopped => "grey",
        _ => "white"
    };

    private static string FormatSpeed(double kbps) => kbps switch
    {
        >= 1024 => $"{kbps / 1024.0:F1} MB/s",
        _ => $"{kbps:F1} KB/s"
    };

    private static string FormatSize(long bytes) => bytes switch
    {
        >= 1_073_741_824 => $"{bytes / 1_073_741_824.0:F2} GB",
        >= 1_048_576 => $"{bytes / 1_048_576.0:F2} MB",
        _ => $"{bytes / 1024.0:F2} KB"
    };
}
