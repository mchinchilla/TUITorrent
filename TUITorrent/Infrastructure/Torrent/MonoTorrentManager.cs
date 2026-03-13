using System.Collections.Concurrent;
using MonoTorrent;
using MonoTorrent.Client;
using TUITorrent.Application.Models;
using TUITorrent.Domain.Enums;
using TUITorrent.Domain.Interfaces;
using TUITorrent.Domain.ValueObjects;
using TUITorrent.Infrastructure.Persistence;

namespace TUITorrent.Infrastructure.Torrent;

public class MonoTorrentManager : ITorrentManager, IAsyncDisposable
{
    private static readonly HttpClient HttpClient = new();
    private ClientEngine? _engine;
    private readonly ConcurrentDictionary<string, ManagedTorrent> _torrents = new();
    private readonly SemaphoreSlim _engineLock = new(1, 1);
    private readonly SemaphoreSlim _persistLock = new(1, 1);
    private readonly TorrentStateStore _stateStore = new();

    private sealed record ManagedTorrent(
        TorrentManager Manager,
        string Id,
        string PersistedSource,
        string SourceDisplay,
        string OutputDirectory,
        bool SeedAfterDownload,
        DateTime AddedAt)
    {
        public TorrentPriority Priority { get; set; } = TorrentPriority.Normal;
    };

    private async Task<ClientEngine> GetOrCreateEngineAsync(
        int port, int maxConnections, int maxDownloadSpeedKbps, int maxUploadSpeedKbps)
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
                    ["ipv4"] = new(System.Net.IPAddress.Any, port)
                },
                MaximumConnections = maxConnections,
                MaximumDownloadRate = maxDownloadSpeedKbps * 1024,
                MaximumUploadRate = maxUploadSpeedKbps * 1024,
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

    public async Task<int> RestoreAsync(CancellationToken ct = default)
    {
        var persisted = await _stateStore.LoadAsync();
        if (persisted.Count == 0)
            return 0;

        var settingsRepo = new JsonSettingsRepository();
        var settings = await settingsRepo.LoadAsync(ct);

        var engine = await GetOrCreateEngineAsync(
            settings.ListenPort, settings.MaxConnections,
            settings.MaxDownloadSpeedKbps, settings.MaxUploadSpeedKbps);

        var restored = 0;
        foreach (var p in persisted)
        {
            try
            {
                TorrentManager manager;

                if (p.Source.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase))
                {
                    var magnet = MagnetLink.Parse(p.Source);
                    manager = await engine.AddAsync(magnet, p.OutputDirectory);
                }
                else
                {
                    if (!File.Exists(p.Source))
                        continue;
                    var torrent = await MonoTorrent.Torrent.LoadAsync(p.Source);
                    manager = await engine.AddAsync(torrent, p.OutputDirectory);
                }

                var managed = new ManagedTorrent(
                    manager, p.Id, p.Source, p.SourceDisplay,
                    p.OutputDirectory, p.SeedAfterDownload, p.AddedAt)
                {
                    Priority = p.Priority
                };

                _torrents[p.Id] = managed;

                if (p.WasDownloading)
                    await manager.StartAsync();

                restored++;
            }
            catch
            {
                // Skip torrents that fail to restore
            }
        }

        return restored;
    }

    public async Task<string> AddAsync(DownloadConfiguration config, CancellationToken cancellationToken = default)
    {
        var engine = await GetOrCreateEngineAsync(
            config.Port, config.MaxConnections,
            config.MaxDownloadSpeedKbps, config.MaxUploadSpeedKbps);

        var id = Guid.NewGuid().ToString("N")[..8];

        if (!Directory.Exists(config.OutputDirectory))
            Directory.CreateDirectory(config.OutputDirectory);

        TorrentManager manager;
        string persistedSource;

        if (config.Source.Type == TorrentSourceType.Magnet)
        {
            var magnet = MagnetLink.Parse(config.Source.Value);
            manager = await engine.AddAsync(magnet, config.OutputDirectory);
            persistedSource = config.Source.Value;
        }
        else
        {
            byte[] torrentBytes;
            if (config.Source.Type == TorrentSourceType.Url)
                torrentBytes = await HttpClient.GetByteArrayAsync(config.Source.Value, cancellationToken);
            else
                torrentBytes = await File.ReadAllBytesAsync(config.Source.Value, cancellationToken);

            var torrent = await MonoTorrent.Torrent.LoadAsync(torrentBytes);
            manager = await engine.AddAsync(torrent, config.OutputDirectory);
            await _stateStore.CacheTorrentFileAsync(id, torrentBytes);
            persistedSource = TorrentStateStore.GetCachedTorrentPath(id);
        }

        var managed = new ManagedTorrent(
            manager, id, persistedSource, config.Source.DisplayName,
            config.OutputDirectory, config.SeedAfterDownload, DateTime.UtcNow);

        _torrents[id] = managed;
        await manager.StartAsync();
        await PersistStateAsync();

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
        {
            await managed.Manager.StopAsync();
            await PersistStateAsync();
        }
    }

    public async Task ResumeAsync(string id, CancellationToken cancellationToken = default)
    {
        if (_torrents.TryGetValue(id, out var managed))
        {
            await managed.Manager.StartAsync();
            await PersistStateAsync();
        }
    }

    public async Task RemoveAsync(string id, CancellationToken cancellationToken = default)
    {
        if (_torrents.TryRemove(id, out var managed))
        {
            await managed.Manager.StopAsync();
            if (_engine is not null)
                await _engine.RemoveAsync(managed.Manager);
            _stateStore.DeleteCachedTorrentFile(id);
            await PersistStateAsync();
        }
    }

    public async Task SetPriorityAsync(string id, TorrentPriority priority, CancellationToken cancellationToken = default)
    {
        if (_torrents.TryGetValue(id, out var managed))
        {
            managed.Priority = priority;
            await PersistStateAsync();
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

            _stateStore.DeleteCachedTorrentFile(id);
            await PersistStateAsync();
        }
    }

    public async Task PersistStateAsync()
    {
        await _persistLock.WaitAsync();
        try
        {
            var state = _torrents.Values
                .Where(m => m.Manager.Progress < 100.0)
                .Select(m => new PersistedTorrent(
                    Id: m.Id,
                    Source: m.PersistedSource,
                    SourceDisplay: m.SourceDisplay,
                    OutputDirectory: m.OutputDirectory,
                    SeedAfterDownload: m.SeedAfterDownload,
                    Priority: m.Priority,
                    WasDownloading: m.Manager.State is TorrentState.Downloading
                        or TorrentState.Hashing or TorrentState.Metadata,
                    AddedAt: m.AddedAt))
                .ToList();

            await _stateStore.SaveAsync(state);
        }
        finally
        {
            _persistLock.Release();
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
            AddedAt: m.AddedAt,
            Priority: m.Priority);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var managed in _torrents.Values)
            await managed.Manager.StopAsync();

        _engine?.Dispose();
        _engineLock.Dispose();
        _persistLock.Dispose();
    }
}
