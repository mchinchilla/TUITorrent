using System.Net.Sockets;
using Serilog;
using TUITorrent.Application.Models;
using TUITorrent.Domain.Interfaces;
using TUITorrent.Domain.ValueObjects;
using TUITorrent.Infrastructure.Torrent;

namespace TUITorrent.Infrastructure.Daemon;

public class DaemonServer : IAsyncDisposable
{
    private readonly MonoTorrentManager _manager;
    private readonly ILogger _logger;
    private Socket? _listener;
    private bool _exitWhenDone;
    private bool _hasTorrents;
    private CancellationTokenSource? _serverCts;

    public DaemonServer(MonoTorrentManager manager, ILogger logger)
    {
        _manager = manager;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken cancellationToken, bool exitWhenDone = false)
    {
        _exitWhenDone = exitWhenDone;

        Directory.CreateDirectory(DaemonPaths.ConfigDir);

        if (File.Exists(DaemonPaths.SocketPath))
            File.Delete(DaemonPaths.SocketPath);

        await File.WriteAllTextAsync(DaemonPaths.PidFile,
            Environment.ProcessId.ToString(), cancellationToken);

        var endpoint = new UnixDomainSocketEndPoint(DaemonPaths.SocketPath);
        _listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        _listener.Bind(endpoint);
        _listener.Listen(10);

        _logger.Information("Daemon listening on {SocketPath} (PID {Pid}){ExitMode}",
            DaemonPaths.SocketPath, Environment.ProcessId,
            exitWhenDone ? " [exit-when-done]" : "");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _serverCts = cts;

        if (exitWhenDone)
            _ = MonitorCompletionAsync(cts);

        try
        {
            while (!cts.Token.IsCancellationRequested)
            {
                var client = await _listener.AcceptAsync(cts.Token);
                _ = HandleClientAsync(client, cts.Token);
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            await CleanupAsync();
        }
    }

    private async Task MonitorCompletionAsync(CancellationTokenSource cts)
    {
        while (!cts.Token.IsCancellationRequested)
        {
            await Task.Delay(3000, cts.Token)
                .ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);

            if (cts.Token.IsCancellationRequested)
                break;

            var torrents = await _manager.ListAsync(cts.Token);

            if (torrents.Count > 0)
                _hasTorrents = true;

            if (!_hasTorrents)
                continue;

            var allDone = torrents.Count > 0 && torrents.All(t =>
                t.State is TorrentStatus.Seeding or TorrentStatus.Stopped or TorrentStatus.Error);

            if (!allDone)
                continue;

            _logger.Information("All downloads complete. Shutting down daemon (exit-when-done).");
            await cts.CancelAsync();
        }
    }

    private async Task HandleClientAsync(Socket client, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = new NetworkStream(client, ownsSocket: true);
            using var reader = new StreamReader(stream);
            await using var writer = new StreamWriter(stream) { AutoFlush = true };

            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null) return;

            var request = DaemonSerializer.DeserializeRequest(line);
            var response = await ProcessRequestAsync(request, cancellationToken);
            await writer.WriteLineAsync(DaemonSerializer.SerializeResponse(response));
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error handling client connection");
        }
    }

    private async Task<DaemonResponse> ProcessRequestAsync(
        DaemonRequest? request, CancellationToken cancellationToken)
    {
        try
        {
            return request switch
            {
                AddTorrentRequest add => await HandleAddAsync(add, cancellationToken),
                ListTorrentsRequest => await HandleListAsync(cancellationToken),
                GetTorrentRequest get => await HandleGetAsync(get, cancellationToken),
                StopTorrentRequest stop => await HandleStopAsync(stop, cancellationToken),
                ResumeTorrentRequest resume => await HandleResumeAsync(resume, cancellationToken),
                RemoveTorrentRequest remove => await HandleRemoveAsync(remove, cancellationToken),
                PurgeTorrentRequest purge => await HandlePurgeAsync(purge, cancellationToken),
                SetPriorityRequest pri => await HandleSetPriorityAsync(pri, cancellationToken),
                SetExitWhenDoneRequest ewd => HandleSetExitWhenDone(ewd),
                ShutdownRequest => HandleShutdown(),
                _ => new DaemonResponse(false, Error: "Unknown request type")
            };
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error processing request");
            return new DaemonResponse(false, Error: ex.Message);
        }
    }

    private async Task<DaemonResponse> HandleAddAsync(
        AddTorrentRequest req, CancellationToken ct)
    {
        var source = TorrentSource.Parse(req.Source);
        var config = new DownloadConfiguration(
            source, req.OutputDirectory, req.Port,
            req.MaxDownloadSpeedKbps, req.MaxUploadSpeedKbps,
            req.MaxConnections, req.SeedAfterDownload);

        var id = await _manager.AddAsync(config, ct);
        _logger.Information("Added torrent {Id}: {Source}", id, source.DisplayName);
        return new DaemonResponse(true, Id: id);
    }

    private async Task<DaemonResponse> HandleListAsync(CancellationToken ct)
    {
        var list = await _manager.ListAsync(ct);
        var dtos = list.Select(TorrentInfoDto.FromDomain).ToList();
        return new DaemonResponse(true, Torrents: dtos);
    }

    private async Task<DaemonResponse> HandleGetAsync(GetTorrentRequest req, CancellationToken ct)
    {
        var info = await _manager.GetAsync(req.Id, ct);
        if (info is null)
            return new DaemonResponse(false, Error: $"Torrent '{req.Id}' not found");
        return new DaemonResponse(true, Torrent: TorrentInfoDto.FromDomain(info));
    }

    private async Task<DaemonResponse> HandleStopAsync(StopTorrentRequest req, CancellationToken ct)
    {
        var info = await _manager.GetAsync(req.Id, ct);
        if (info is null)
            return new DaemonResponse(false, Error: $"Torrent '{req.Id}' not found");

        await _manager.StopAsync(req.Id, ct);
        _logger.Information("Stopped torrent {Id}", req.Id);
        return new DaemonResponse(true);
    }

    private async Task<DaemonResponse> HandleResumeAsync(ResumeTorrentRequest req, CancellationToken ct)
    {
        var info = await _manager.GetAsync(req.Id, ct);
        if (info is null)
            return new DaemonResponse(false, Error: $"Torrent '{req.Id}' not found");

        await _manager.ResumeAsync(req.Id, ct);
        _logger.Information("Resumed torrent {Id}", req.Id);
        return new DaemonResponse(true);
    }

    private async Task<DaemonResponse> HandleRemoveAsync(RemoveTorrentRequest req, CancellationToken ct)
    {
        var info = await _manager.GetAsync(req.Id, ct);
        if (info is null)
            return new DaemonResponse(false, Error: $"Torrent '{req.Id}' not found");

        await _manager.RemoveAsync(req.Id, ct);
        _logger.Information("Removed torrent {Id}", req.Id);
        return new DaemonResponse(true);
    }

    private async Task<DaemonResponse> HandlePurgeAsync(PurgeTorrentRequest req, CancellationToken ct)
    {
        var info = await _manager.GetAsync(req.Id, ct);
        if (info is null)
            return new DaemonResponse(false, Error: $"Torrent '{req.Id}' not found");

        await _manager.PurgeAsync(req.Id, ct);
        _logger.Information("Purged torrent {Id} (data deleted)", req.Id);
        return new DaemonResponse(true);
    }

    private async Task<DaemonResponse> HandleSetPriorityAsync(SetPriorityRequest req, CancellationToken ct)
    {
        var info = await _manager.GetAsync(req.Id, ct);
        if (info is null)
            return new DaemonResponse(false, Error: $"Torrent '{req.Id}' not found");

        await _manager.SetPriorityAsync(req.Id, req.Priority, ct);
        _logger.Information("Set priority of torrent {Id} to {Priority}", req.Id, req.Priority);
        return new DaemonResponse(true);
    }

    private DaemonResponse HandleSetExitWhenDone(SetExitWhenDoneRequest req)
    {
        if (req.Enable && !_exitWhenDone)
        {
            _exitWhenDone = true;
            if (_serverCts is not null)
                _ = MonitorCompletionAsync(_serverCts);
            _logger.Information("Exit-when-done enabled");
        }
        else if (!req.Enable)
        {
            _exitWhenDone = false;
            _logger.Information("Exit-when-done disabled");
        }

        return new DaemonResponse(true);
    }

    private DaemonResponse HandleShutdown()
    {
        _logger.Information("Shutdown requested");
        _ = Task.Run(async () =>
        {
            await Task.Delay(200);
            Environment.Exit(0);
        });
        return new DaemonResponse(true);
    }

    private async Task CleanupAsync()
    {
        _listener?.Dispose();

        if (File.Exists(DaemonPaths.SocketPath))
            File.Delete(DaemonPaths.SocketPath);

        if (File.Exists(DaemonPaths.PidFile))
            File.Delete(DaemonPaths.PidFile);

        await _manager.DisposeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await CleanupAsync();
    }
}
