# TUITorrent

A terminal-based BitTorrent client built with .NET, featuring a daemon architecture for managing multiple simultaneous downloads via CLI.

## Features

- Magnet link and `.torrent` file support
- Multiple simultaneous downloads managed by a background daemon
- Real-time progress tracking with live-updating tables
- Persistent settings with per-download overrides
- Auto-start daemon when adding downloads
- Auto-shutdown daemon when all downloads complete (`--exit-when-done`)
- Speed limiting (download/upload)
- Connection encryption (None, Prefer, Require)

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download)

## Build & Run

```bash
# Clone and build
git clone <repo-url>
cd TUITorrent
dotnet build

# Run
dotnet run --project TUITorrent -- <command>
```

Or publish as a single binary:

```bash
dotnet publish TUITorrent -c Release -o dist
./dist/tuitorrent <command>
```

## Commands

### `download` (alias: `dl`)

Add a torrent to the download queue. Automatically starts the daemon if it is not running.

```
tuitorrent download <source> [OPTIONS]
```

| Option | Description |
|---|---|
| `<source>` | Magnet URI or path to a `.torrent` file (required) |
| `-o, --output <dir>` | Destination directory |
| `-p, --port <port>` | Listening port for incoming connections |
| `--dl-limit <KB/s>` | Max download speed in KB/s (0 = unlimited) |
| `--ul-limit <KB/s>` | Max upload speed in KB/s (0 = unlimited) |
| `--max-connections <n>` | Maximum number of connections |
| `--no-seed` | Do not seed after download completes |
| `-f, --follow` | Follow download progress in real-time |
| `--exit-when-done` | Shut down daemon when all downloads complete |

**Examples:**

```bash
# Download a magnet link
tuitorrent download "magnet:?xt=urn:btih:..."

# Download and follow progress in real-time
tuitorrent dl "magnet:?xt=urn:btih:..." -f

# Download a .torrent file to a specific directory
tuitorrent download ubuntu.torrent -o ~/Downloads/ISOs

# Download with speed limits and no seeding
tuitorrent dl "magnet:?xt=urn:btih:..." --dl-limit 2048 --ul-limit 512 --no-seed

# Download and auto-shutdown daemon when done
tuitorrent dl "magnet:?xt=urn:btih:..." --exit-when-done -f

# Multiple downloads (daemon stays running between commands)
tuitorrent dl "magnet:?xt=urn:btih:abc..." --exit-when-done
tuitorrent dl "magnet:?xt=urn:btih:def..."
tuitorrent dl fedora.torrent -o /data/isos
```

---

### `list` (alias: `ls`)

List all active torrents in the daemon.

```
tuitorrent list [OPTIONS]
```

| Option | Description |
|---|---|
| `-w, --watch` | Continuously refresh the list (live mode) |

**Examples:**

```bash
# Show all active torrents
tuitorrent list

# Live-updating dashboard
tuitorrent ls --watch
```

**Output:**

```
╔═ TUITorrent - Active Downloads ══════════════════════════════════════════════╗
║ ┌──────────┬──────────────────────┬─────────────┬──────────┬────────┬──────┐ ║
║ │ ID       │ Name                 │ State       │ Progress │ DL     │ Peers│ ║
║ ├──────────┼──────────────────────┼─────────────┼──────────┼────────┼──────┤ ║
║ │ a3f2b1c8 │ Ubuntu 24.04 ISO     │ Downloading │ 45.2%    │ 2.3 MB │ 12   │ ║
║ │ e7d4c9a1 │ Fedora 40 Workst...  │ Seeding     │ 100.0%   │ 0.0 KB │  8   │ ║
║ └──────────┴──────────────────────┴─────────────┴──────────┴────────┴──────┘ ║
╚══════════════════════════════════════════════════════════════════════════════╝
```

---

### `status`

Show detailed information about a specific torrent.

```
tuitorrent status <id> [OPTIONS]
```

| Option | Description |
|---|---|
| `<id>` | Torrent ID (8-character hex, shown by `list` or `download`) |
| `-f, --follow` | Follow progress in real-time |

**Examples:**

```bash
# Show current status
tuitorrent status a3f2b1c8

# Follow progress live until complete
tuitorrent status a3f2b1c8 -f
```

---

### `stop`

Stop (pause) a torrent download.

```
tuitorrent stop <id>
```

**Examples:**

```bash
tuitorrent stop a3f2b1c8
```

---

### `remove` (alias: `rm`)

Remove a torrent from the daemon. Stops the download if active.

```
tuitorrent remove <id>
```

**Examples:**

```bash
tuitorrent remove a3f2b1c8
tuitorrent rm e7d4c9a1
```

---

### `settings` (alias: `config`)

View or modify persistent application settings. When called without options, displays current settings. Pass options to update values.

```
tuitorrent settings [OPTIONS]
```

| Option | Description |
|---|---|
| `--show` | Display current settings |
| `--output <dir>` | Set default output directory |
| `--port <port>` | Set default listening port |
| `--dl-limit <KB/s>` | Set default max download speed (0 = unlimited) |
| `--ul-limit <KB/s>` | Set default max upload speed (0 = unlimited) |
| `--max-connections <n>` | Set default max connections |
| `--encryption <mode>` | Set encryption mode: `None`, `Prefer`, or `Require` |
| `--seed <bool>` | Enable/disable seeding after download: `true` or `false` |

**Examples:**

```bash
# View current settings
tuitorrent settings

# Change output directory
tuitorrent settings --output ~/Downloads/torrents

# Set speed limits and disable seeding by default
tuitorrent config --dl-limit 5120 --ul-limit 1024 --seed false

# Change port and require encryption
tuitorrent config --port 6881 --encryption Require

# Set multiple options at once
tuitorrent settings --output /data/downloads --max-connections 100 --port 55555
```

**Default settings:**

| Setting | Default |
|---|---|
| Output Directory | `~/Downloads` |
| Listen Port | `55123` |
| Max DL Speed | Unlimited |
| Max UL Speed | Unlimited |
| Max Connections | `200` |
| Encryption | `Prefer` |
| Seed After Download | `true` |

Settings are stored in `~/.config/tuitorrent/settings.json`.

---

### `daemon`

Manage the background daemon process. The daemon is automatically started when using `download`, `list`, `status`, `stop`, or `remove`. These subcommands are for manual control.

#### `daemon start`

Start the daemon in the foreground.

```
tuitorrent daemon start [OPTIONS]
```

| Option | Description |
|---|---|
| `--exit-when-done` | Automatically shut down when all downloads complete |

**Examples:**

```bash
# Start daemon (runs in foreground, Ctrl+C to stop)
tuitorrent daemon start

# Start daemon that exits when all downloads finish
tuitorrent daemon start --exit-when-done
```

#### `daemon stop`

Gracefully stop the running daemon. All active downloads will be stopped.

```bash
tuitorrent daemon stop
```

#### `daemon status`

Check if the daemon is running.

```bash
tuitorrent daemon status
```

## Architecture

TUITorrent follows Domain-Driven Design (DDD) with full Dependency Injection and async/await throughout.

```
TUITorrent/
├── Domain/                          # Core business logic, no external dependencies
│   ├── Enums/
│   │   └── EncryptionMode.cs
│   ├── ValueObjects/
│   │   ├── TorrentSource.cs         # Validates magnet vs .torrent
│   │   └── DownloadConfiguration.cs # Immutable download parameters
│   └── Interfaces/
│       ├── ISettingsRepository.cs   # Persistence contract
│       └── ITorrentManager.cs       # Multi-download manager contract
│
├── Application/                     # Use cases and orchestration
│   ├── Models/
│   │   ├── AppSettings.cs           # Settings model
│   │   ├── TorrentInfo.cs           # Download status snapshot
│   │   └── TorrentProgress.cs       # Status enum
│   └── Services/
│       ├── DownloadService.cs       # Build config + add torrent
│       └── SettingsAppService.cs    # Settings CRUD
│
├── Infrastructure/                  # External implementations
│   ├── Persistence/
│   │   └── JsonSettingsRepository.cs
│   ├── Torrent/
│   │   └── MonoTorrentManager.cs    # MonoTorrent engine wrapper
│   └── Daemon/
│       ├── DaemonProtocol.cs        # Socket message types
│       ├── DaemonServer.cs          # Unix socket server
│       └── DaemonClient.cs          # Unix socket client
│
├── Presentation/                    # CLI and rendering
│   ├── Commands/                    # Spectre.Console.Cli commands
│   ├── Rendering/
│   │   └── TorrentProgressRenderer.cs
│   └── Infrastructure/
│       ├── TypeRegistrar.cs         # DI bridge
│       └── TypeResolver.cs
│
└── Program.cs                       # Entry point and DI setup
```

### Daemon Communication

CLI commands communicate with the daemon via a Unix domain socket at `~/.config/tuitorrent/daemon.sock` using newline-delimited JSON messages. The daemon manages a shared MonoTorrent `ClientEngine` instance and tracks all downloads concurrently.

### Files and Paths

| Path | Description |
|---|---|
| `~/.config/tuitorrent/settings.json` | Persistent settings |
| `~/.config/tuitorrent/daemon.sock` | Unix domain socket |
| `~/.config/tuitorrent/daemon.pid` | Daemon process ID |
| `~/.config/tuitorrent/daemon.log` | Daemon log (daily rolling, 7 days) |
| `~/.config/tuitorrent/cache/` | MonoTorrent engine cache |

## Tech Stack

| Library | Purpose |
|---|---|
| [MonoTorrent](https://github.com/alanmcgovern/monotorrent) | BitTorrent protocol engine |
| [Spectre.Console](https://spectreconsole.net/) | Terminal UI rendering |
| [Spectre.Console.Cli](https://spectreconsole.net/cli/) | CLI argument parsing |
| [Serilog](https://serilog.net/) | Structured logging |
| [Microsoft.Extensions.DependencyInjection](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection) | Dependency injection |

## License

MIT
