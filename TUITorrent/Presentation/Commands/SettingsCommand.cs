using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using TUITorrent.Application.Services;
using TUITorrent.Domain.Enums;
using TUITorrent.Presentation.Rendering;

namespace TUITorrent.Presentation.Commands;

public class SettingsCommand : AsyncCommand<SettingsCommand.Settings>
{
    private readonly SettingsAppService _settingsService;

    public SettingsCommand(SettingsAppService settingsService)
    {
        _settingsService = settingsService;
    }

    public class Settings : CommandSettings
    {
        [CommandOption("--show")]
        [Description("Display current settings")]
        public bool Show { get; set; }

        [CommandOption("--output")]
        [Description("Set default output directory")]
        public string? OutputDirectory { get; set; }

        [CommandOption("--port")]
        [Description("Set default listening port")]
        public int? Port { get; set; }

        [CommandOption("--dl-limit")]
        [Description("Set default max download speed in KB/s (0 = unlimited)")]
        public int? MaxDownloadSpeed { get; set; }

        [CommandOption("--ul-limit")]
        [Description("Set default max upload speed in KB/s (0 = unlimited)")]
        public int? MaxUploadSpeed { get; set; }

        [CommandOption("--max-connections")]
        [Description("Set default max connections")]
        public int? MaxConnections { get; set; }

        [CommandOption("--encryption")]
        [Description("Set encryption mode (None, Prefer, Require)")]
        public string? Encryption { get; set; }

        [CommandOption("--seed")]
        [Description("Enable or disable seeding after download (true/false)")]
        public string? Seed { get; set; }
    }

    public override async Task<int> ExecuteAsync(
        CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var hasModification = HasAnyOption(settings);

        if (hasModification)
        {
            EncryptionMode? encryptionMode = null;
            if (settings.Encryption is not null)
            {
                if (!Enum.TryParse<EncryptionMode>(settings.Encryption, ignoreCase: true, out var mode))
                {
                    AnsiConsole.MarkupLine("[red]Invalid encryption mode. Use: None, Prefer, or Require[/]");
                    return 1;
                }
                encryptionMode = mode;
            }

            bool? seedValue = null;
            if (settings.Seed is not null)
            {
                if (!bool.TryParse(settings.Seed, out var sv))
                {
                    AnsiConsole.MarkupLine("[red]Invalid seed value. Use: true or false[/]");
                    return 1;
                }
                seedValue = sv;
            }

            var updated = await _settingsService.UpdateSettingsAsync(
                outputDirectory: settings.OutputDirectory,
                port: settings.Port,
                maxDownloadSpeed: settings.MaxDownloadSpeed,
                maxUploadSpeed: settings.MaxUploadSpeed,
                maxConnections: settings.MaxConnections,
                encryption: encryptionMode,
                seedAfterDownload: seedValue,
                cancellationToken: cancellationToken);

            AnsiConsole.MarkupLine("[green]Settings saved.[/]");
            DisplaySettings(updated);
        }
        else
        {
            var appSettings = await _settingsService.GetSettingsAsync(cancellationToken);
            DisplaySettings(appSettings);
        }

        return 0;
    }

    private static bool HasAnyOption(Settings settings) =>
        settings.OutputDirectory is not null ||
        settings.Port is not null ||
        settings.MaxDownloadSpeed is not null ||
        settings.MaxUploadSpeed is not null ||
        settings.MaxConnections is not null ||
        settings.Encryption is not null ||
        settings.Seed is not null;

    private static void DisplaySettings(Application.Models.AppSettings appSettings)
    {
        AnsiConsole.Write(
            new Panel(TorrentProgressRenderer.RenderSettingsTable(appSettings))
                .Header("[bold yellow]TUITorrent - Settings[/]")
                .Border(BoxBorder.Double));

        AnsiConsole.MarkupLine(
            $"[dim]Config file: {Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "tuitorrent", "settings.json")}[/]");
    }
}
