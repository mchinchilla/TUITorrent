using System.Collections.Concurrent;
using MonoTorrent;
using MonoTorrent.Client;
using TUITorrent.Application.Models;
using TUITorrent.Domain.Interfaces;
using TUITorrent.Domain.ValueObjects;

namespace TUITorrent.Infrastructure.Torrent;

public class MonoTorrentManager : ITorrentManager, IAsyncDisposable
{
    private static readonly HttpClient HttpClient = new();
    private ClientEngine? _engine;
    private readonly ConcurrentDictionary<string, ManagedTorrent> _torrents = new();
    private readonly SemaphoreSlim _engineLock = new(1, 1);

    private sealed record ManagedTorrent(
        TorrentManager Manager,
        string Id,
        string SourceDisplay,
        string OutputDirectory,
        bool SeedAfterDownload,
        DateTime AddedAt);

    private async Task<ClientEngine> GetOrCreateEngineAsync(DownloadConfiguration config)
    {
        if (_engine is not null)
            return _engine;

        await _engineLock.WaitAsync();
        try
        {
            if (_engine is not null)
                return _engine;

            var settingsBuilder = new EngineSettingsBuilder
            {
                ListenEndPoints = new Dictionary<string, System.Net.IPEndPoint>
                {
                    ["ipv4"] = new(System.Net.IPAddress.Any, config.Port)
                },
                MaximumConnections = config.MaxConnections,
                MaximumDownloadRate = config.MaxDownloadSpeedKbps * 1024,
                MaximumUploadRate = config.MaxUploadSpeedKbps * 1024,
                CacheDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".config", "tuitorrent", "cache")
            };

            _engine = new ClientEngine(settingsBuilder.ToSettings());
            return _engine;
        }
        finally
        {
            _engineLock.Release();
        }
    }

    public async Task<string> AddAsync(DownloadConfiguration config, CancellationToken cancellationToken = default)
    {
        var engine = await GetOrCreateEngineAsync(config);
        var id = Guid.NewGuid().ToString("N")[..8];

        if (!Directory.Exists(config.OutputDirectory))
            Directory.CreateDirectory(config.OutputDirectory);

        TorrentManager manager;

        if (config.Source.Type == TorrentSourceType.Magnet)
        {
            var magnet = MagnetLink.Parse(config.Source.Value);
            manager = await engine.AddAsync(magnet, config.OutputDirectory);
        }
        else if (config.Source.Type == TorrentSourceType.Url)
        {
            var torrentBytes = await HttpClient.GetByteArrayAsync(config.Source.Value, cancellationToken);
            var torrent = await MonoTorrent.Torrent.LoadAsync(torrentBytes);
            manager = await engine.AddAsync(torrent, config.OutputDirectory);
        }
        else
        {
            var torrent = await MonoTorrent.Torrent.LoadAsync(config.Source.Value);
            manager = await engine.AddAsync(torrent, config.OutputDirectory);
        }

        var managed = new ManagedTorrent(
            manager, id, config.Source.DisplayName,
            config.OutputDirectory, config.SeedAfterDownload, DateTime.UtcNow);

        _torrents[id] = managed;

        await manager.StartAsync();

        return id;
    }

    public Task<IReadOnlyList<TorrentInfo>> ListAsync(CancellationToken cancellationToken = default)
    {
        var list = _torrents.Values.Select(ToTorrentInfo).ToList();
        return Task.FromResult<IReadOnlyList<TorrentInfo>>(list);
    }

    public Task<TorrentInfo?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        if (_torrents.TryGetValue(id, out var managed))
            return Task.FromResult<TorrentInfo?>(ToTorrentInfo(managed));
        return Task.FromResult<TorrentInfo?>(null);
    }

    public async Task StopAsync(string id, CancellationToken cancellationToken = default)
    {
        if (_torrents.TryGetValue(id, out var managed))
            await managed.Manager.StopAsync();
    }

    public async Task RemoveAsync(string id, CancellationToken cancellationToken = default)
    {
        if (_torrents.TryRemove(id, out var managed))
        {
            await managed.Manager.StopAsync();
            if (_engine is not null)
                await _engine.RemoveAsync(managed.Manager);
        }
    }

    public async Task PurgeAsync(string id, CancellationToken cancellationToken = default)
    {
        if (_torrents.TryRemove(id, out var managed))
        {
            await managed.Manager.StopAsync();

            var savePath = managed.OutputDirectory;
            var torrentName = managed.Manager.Torrent?.Name;

            if (_engine is not null)
                await _engine.RemoveAsync(managed.Manager);

            if (torrentName is not null)
            {
                var contentPath = Path.Combine(savePath, torrentName);
                if (Directory.Exists(contentPath))
                    Directory.Delete(contentPath, recursive: true);
                else if (File.Exists(contentPath))
                    File.Delete(contentPath);
            }
        }
    }

    private static TorrentInfo ToTorrentInfo(ManagedTorrent m)
    {
        var state = m.Manager.State switch
        {
            TorrentState.Downloading => TorrentStatus.Downloading,
            TorrentState.Seeding => TorrentStatus.Seeding,
            TorrentState.Hashing => TorrentStatus.Hashing,
            TorrentState.Stopped => TorrentStatus.Stopped,
            TorrentState.Error => TorrentStatus.Error,
            _ => TorrentStatus.Starting
        };

        return new TorrentInfo(
            Id: m.Id,
            Name: m.Manager.Torrent?.Name ?? "Fetching metadata...",
            Source: m.SourceDisplay,
            OutputDirectory: m.OutputDirectory,
            State: state,
            ProgressPercent: m.Manager.Progress,
            DownloadRateKbps: m.Manager.Monitor.DownloadRate / 1024.0,
            UploadRateKbps: m.Manager.Monitor.UploadRate / 1024.0,
            Peers: m.Manager.Peers.Available,
            Seeds: m.Manager.Peers.Seeds,
            TotalSizeBytes: m.Manager.Torrent?.Size ?? 0,
            AddedAt: m.AddedAt);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var managed in _torrents.Values)
            await managed.Manager.StopAsync();

        _engine?.Dispose();
        _engineLock.Dispose();
    }
}
