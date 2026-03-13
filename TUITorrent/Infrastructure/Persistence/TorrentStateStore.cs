using System.Text.Json;
using System.Text.Json.Serialization;
using TUITorrent.Domain.Enums;
using TUITorrent.Infrastructure.Daemon;

namespace TUITorrent.Infrastructure.Persistence;

public sealed record PersistedTorrent(
    string Id,
    string Source,
    string SourceDisplay,
    string OutputDirectory,
    bool SeedAfterDownload,
    TorrentPriority Priority,
    bool WasDownloading,
    DateTime AddedAt);

public class TorrentStateStore
{
    private static readonly string StateFilePath = Path.Combine(DaemonPaths.ConfigDir, "torrents.json");
    private static readonly string TorrentCacheDir = Path.Combine(DaemonPaths.ConfigDir, "torrents");

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string GetCachedTorrentPath(string id) =>
        Path.Combine(TorrentCacheDir, $"{id}.torrent");

    public async Task SaveAsync(IEnumerable<PersistedTorrent> torrents)
    {
        Directory.CreateDirectory(DaemonPaths.ConfigDir);
        var json = JsonSerializer.Serialize(torrents.ToList(), Options);
        await File.WriteAllTextAsync(StateFilePath, json);
    }

    public async Task<List<PersistedTorrent>> LoadAsync()
    {
        if (!File.Exists(StateFilePath))
            return [];

        var json = await File.ReadAllTextAsync(StateFilePath);
        return JsonSerializer.Deserialize<List<PersistedTorrent>>(json, Options) ?? [];
    }

    public async Task CacheTorrentFileAsync(string id, byte[] torrentBytes)
    {
        Directory.CreateDirectory(TorrentCacheDir);
        await File.WriteAllBytesAsync(GetCachedTorrentPath(id), torrentBytes);
    }

    public void DeleteCachedTorrentFile(string id)
    {
        var path = GetCachedTorrentPath(id);
        if (File.Exists(path))
            File.Delete(path);
    }
}
