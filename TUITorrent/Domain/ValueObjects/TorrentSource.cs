namespace TUITorrent.Domain.ValueObjects;

public sealed class TorrentSource
{
    public string Value { get; }
    public TorrentSourceType Type { get; }

    private TorrentSource(string value, TorrentSourceType type)
    {
        Value = value;
        Type = type;
    }

    public static TorrentSource Parse(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            throw new ArgumentException("Torrent source cannot be empty.");

        if (input.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase))
            return new TorrentSource(input, TorrentSourceType.Magnet);

        if (Uri.TryCreate(input, UriKind.Absolute, out var uri)
            && uri.Scheme is "http" or "https"
            && uri.AbsolutePath.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase))
        {
            return new TorrentSource(input, TorrentSourceType.Url);
        }

        if (input.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase))
        {
            if (!File.Exists(input))
                throw new FileNotFoundException($"Torrent file not found: {input}");

            return new TorrentSource(Path.GetFullPath(input), TorrentSourceType.File);
        }

        throw new ArgumentException(
            "Source must be a magnet URI (magnet:?...), a URL to a .torrent file, or a local path to a .torrent file.");
    }

    public string DisplayName => Type switch
    {
        TorrentSourceType.Magnet => "Magnet Link",
        TorrentSourceType.File => Path.GetFileName(Value),
        TorrentSourceType.Url => Uri.TryCreate(Value, UriKind.Absolute, out var uri)
            ? Path.GetFileName(uri.AbsolutePath)
            : Value,
        _ => Value
    };
}

public enum TorrentSourceType
{
    Magnet,
    File,
    Url
}
