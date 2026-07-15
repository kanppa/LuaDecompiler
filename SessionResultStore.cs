using System.Text;

namespace LuaDecompilerDesktop;

internal sealed record SessionResult(
    string TempPath,
    int CharacterCount,
    int ConvertedSegments);

internal sealed class SessionResultStore : IDisposable
{
    private readonly Dictionary<string, SessionResult> _results = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "LuaDecompiler");
    private readonly string _sessionDirectory;
    private int _nextFileNumber;
    private bool _disposed;

    public SessionResultStore()
    {
        _sessionDirectory = Path.Combine(
            _tempRoot,
            $"session-{Environment.ProcessId}-{Guid.NewGuid():N}");
    }

    public int Count => _results.Count;
    public string SessionDirectory => _sessionDirectory;

    public bool Contains(string inputPath) => _results.ContainsKey(inputPath);

    public bool TryGet(string inputPath, out SessionResult result) =>
        _results.TryGetValue(inputPath, out result!);

    public async Task<SessionResult> StoreAsync(
        string inputPath,
        string text,
        int convertedSegments,
        CancellationToken cancellationToken)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(SessionResultStore));
        var tempPath = CreateResultPath();
        await File.WriteAllTextAsync(tempPath, text, new UTF8Encoding(false), cancellationToken);
        return RegisterResult(inputPath, tempPath, text.Length, convertedSegments);
    }

    public string CreateResultPath()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(SessionResultStore));
        Directory.CreateDirectory(_sessionDirectory);
        return Path.Combine(
            _sessionDirectory,
            $"result-{Interlocked.Increment(ref _nextFileNumber):D6}.lua");
    }

    public SessionResult RegisterResult(
        string inputPath,
        string tempPath,
        int characterCount,
        int convertedSegments)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(SessionResultStore));
        var stored = new SessionResult(tempPath, characterCount, convertedSegments);
        if (_results.TryGetValue(inputPath, out var previous)
            && !string.Equals(previous.TempPath, tempPath, StringComparison.OrdinalIgnoreCase))
            TryDeleteFile(previous.TempPath);
        _results[inputPath] = stored;
        return stored;
    }

    public void Remove(string inputPath)
    {
        if (!_results.Remove(inputPath, out var previous)) return;
        TryDeleteFile(previous.TempPath);
    }

    public void DiscardFile(string path) => TryDeleteFile(path);

    public void CopyTo(string inputPath, string outputPath)
    {
        if (!_results.TryGetValue(inputPath, out var result))
            throw new InvalidOperationException("找不到本次会话的反编译结果。");
        File.Copy(result.TempPath, outputPath, true);
    }

    public void Clear()
    {
        _results.Clear();
        DeleteSessionDirectory();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _results.Clear();
        DeleteSessionDirectory();
    }

    private void DeleteSessionDirectory()
    {
        try
        {
            if (!Directory.Exists(_sessionDirectory)) return;
            var fullSession = Path.GetFullPath(_sessionDirectory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var fullRoot = Path.GetFullPath(_tempRoot)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            if (!fullSession.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)) return;
            Directory.Delete(fullSession, true);
        }
        catch
        {
            // 临时目录清理失败时不阻止程序退出；系统稍后仍可回收。
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // 旧会话结果稍后随整个临时目录一起清理。
        }
    }
}
