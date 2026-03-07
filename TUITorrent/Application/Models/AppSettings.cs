using System.Text.Json.Serialization;
using TUITorrent.Domain.Enums;

namespace TUITorrent.Application.Models;

public class AppSettings
{
    public string OutputDirectory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

    public int ListenPort { get; set; } = 55123;

    public int MaxDownloadSpeedKbps { get; set; } // 0 = unlimited

    public int MaxUploadSpeedKbps { get; set; } // 0 = unlimited

    public int MaxConnections { get; set; } = 200;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public EncryptionMode Encryption { get; set; } = EncryptionMode.Prefer;

    public bool SeedAfterDownload { get; set; } = true;
}
