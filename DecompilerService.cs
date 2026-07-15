using System.Diagnostics;
using System.Text;

namespace LuaDecompilerDesktop;

internal readonly record struct FileDecompileResult(
    bool Success,
    string Error,
    int CharacterCount,
    int ConvertedSegments);

internal sealed class DecompilerService
{
    public string JavaPath { get; set; } = "java";
    public string JarPath { get; set; }
    public string OpcodeMapPath { get; set; }

    public DecompilerService()
    {
        JarPath = Path.Combine(AppContext.BaseDirectory, "third_party", "unluac.jar");
        OpcodeMapPath = Path.Combine(AppContext.BaseDirectory, "third_party", "lua53.opmap");
    }

    public async Task<FileDecompileResult> DecompileToFileAsync(
        string inputPath,
        string outputPath,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(JarPath))
            return new(false, $"找不到反编译引擎：{JarPath}", 0, 0);

        string? normalizedPath = null;
        Process? process = null;
        try
        {
            normalizedPath = NormalizeChunkHeader(inputPath);
            var engineInput = normalizedPath ?? inputPath;
            var startInfo = CreateStartInfo(engineInput, normalizedPath is not null);
            process = new Process { StartInfo = startInfo };
            process.Start();
            var stderrTask = process.StandardError.ReadToEndAsync();

            long characterCount = 0;
            var convertedSegments = 0;
            await using (var writer = new StreamWriter(outputPath, false, new UTF8Encoding(false), 64 * 1024))
            {
                writer.NewLine = "\r\n";
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var line = await process.StandardOutput.ReadLineAsync()
                        .WaitAsync(cancellationToken)
                        .ConfigureAwait(false);
                    if (line is null) break;
                    var decoded = LuaStringDecoder.DecodeSuspectedChinese(line, cancellationToken);
                    await writer.WriteLineAsync(decoded.Text)
                        .WaitAsync(cancellationToken)
                        .ConfigureAwait(false);
                    characterCount += decoded.Text.Length + writer.NewLine.Length;
                    convertedSegments += decoded.ConvertedSegments;
                }
            }

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            var stderr = await stderrTask.ConfigureAwait(false);
            if (process.ExitCode != 0 || characterCount == 0)
            {
                TryDeleteFile(outputPath);
                var error = string.IsNullOrWhiteSpace(stderr)
                    ? $"引擎退出码：{process.ExitCode}"
                    : stderr.Trim();
                return new(false, error, 0, 0);
            }

            return new(true, stderr.Trim(), (int)Math.Min(int.MaxValue, characterCount), convertedSegments);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            TryDeleteFile(outputPath);
            throw;
        }
        catch (Exception ex)
        {
            TryKill(process);
            TryDeleteFile(outputPath);
            return new(false, $"无法启动或读取反编译引擎：{ex.Message}", 0, 0);
        }
        finally
        {
            process?.Dispose();
            if (normalizedPath is not null) TryDeleteFile(normalizedPath);
        }
    }

    public async Task<(bool Success, string Output, string Error)> DecompileAsync(
        string inputPath, CancellationToken cancellationToken)
    {
        if (!File.Exists(JarPath))
            return (false, "", $"找不到反编译引擎：{JarPath}");

        string? normalizedPath = null;
        var engineInput = inputPath;
        try
        {
            normalizedPath = NormalizeChunkHeader(inputPath);
            if (normalizedPath is not null) engineInput = normalizedPath;
        }
        catch (Exception ex)
        {
            return (false, "", $"无法创建兼容副本：{ex.Message}");
        }

        var startInfo = CreateStartInfo(engineInput, normalizedPath is not null);

        try
        {
            using var process = new Process { StartInfo = startInfo };
            process.Start();
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync(cancellationToken);
            var stdout = await stdoutTask;
            var stderr = await stderrTask;
            return process.ExitCode == 0 && !string.IsNullOrWhiteSpace(stdout)
                ? (true, stdout, stderr)
                : (false, stdout, string.IsNullOrWhiteSpace(stderr) ? $"引擎退出码：{process.ExitCode}" : stderr.Trim());
        }
        catch (OperationCanceledException)
        {
            return (false, "", "用户已取消");
        }
        catch (Exception ex)
        {
            return (false, "", $"无法启动反编译引擎：{ex.Message}");
        }
        finally
        {
            if (normalizedPath is not null)
            {
                try { File.Delete(normalizedPath); }
                catch { /* 临时文件会由系统清理。 */ }
            }
        }
    }

    private static string? NormalizeChunkHeader(string inputPath)
    {
        var source = File.ReadAllBytes(inputPath);
        if (source.Length < 17 || source[0] != 0x1B || source[1] != (byte)'L' ||
            source[2] != (byte)'u' || source[3] != (byte)'a' || source[4] != 0x53 ||
            source[5] != 1 || source[12] != 4 || source[13] != 4 ||
            source[14] != 8 || source[15] != 8 || source[16] != 0x78)
            return null;

        // Normalize a known non-standard Lua 5.3 header layout for unluac.
        // The input file is never modified; conversion uses a temporary copy.
        var normalized = new byte[source.Length + 1];
        Buffer.BlockCopy(source, 0, normalized, 0, 14);
        normalized[5] = 0;
        normalized[14] = 4;
        Buffer.BlockCopy(source, 14, normalized, 15, source.Length - 14);
        var tempPath = Path.Combine(Path.GetTempPath(), $"lua-decompiler-{Guid.NewGuid():N}.luac");
        File.WriteAllBytes(tempPath, normalized);
        return tempPath;
    }

    private ProcessStartInfo CreateStartInfo(string engineInput, bool useOpcodeMap)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = JavaPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = new UTF8Encoding(false),
            StandardErrorEncoding = new UTF8Encoding(false)
        };
        startInfo.ArgumentList.Add("-jar");
        startInfo.ArgumentList.Add(JarPath);
        if (useOpcodeMap && File.Exists(OpcodeMapPath))
        {
            startInfo.ArgumentList.Add("--opmap");
            startInfo.ArgumentList.Add(OpcodeMapPath);
        }
        startInfo.ArgumentList.Add(engineInput);
        return startInfo;
    }

    private static void TryKill(Process? process)
    {
        try
        {
            if (process is not null && !process.HasExited) process.Kill(true);
        }
        catch
        {
            // 进程可能已自然退出。
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
            // 临时文件稍后由会话目录统一清理。
        }
    }
}
