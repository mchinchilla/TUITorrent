using System.Text.Json;
using TUITorrent.Application.Models;
using TUITorrent.Domain.Interfaces;

namespace TUITorrent.Infrastructure.Persistence;

public class JsonSettingsRepository : ISettingsRepository
{
    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".config", "tuitorrent");

    private static readonly string ConfigFile = Path.Combine(ConfigDir, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(ConfigFile))
            return new AppSettings();

        await using var stream = File.OpenRead(ConfigFile);
        return await JsonSerializer.DeserializeAsync<AppSettings>(stream, JsonOptions, cancellationToken)
               ?? new AppSettings();
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(ConfigDir);
        await using var stream = File.Create(ConfigFile);
        await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, cancellationToken);
    }
}
