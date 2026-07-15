using System.Text;

namespace LuaDecompilerDesktop;

internal enum LuaInputKind
{
    Source,
    Bytecode,
    Unsupported
}

internal sealed record LuaFileInfo(
    string FullPath,
    string RelativePath,
    LuaInputKind Kind,
    string Version,
    string Status = "待处理",
    string? OutputPath = null,
    string? Error = null)
{
    public string FileName => Path.GetFileName(FullPath);
    public long Size => new FileInfo(FullPath).Length;

    public static LuaFileInfo Inspect(string path, string relativePath)
    {
        try
        {
            using var stream = File.OpenRead(path);
            Span<byte> header = stackalloc byte[16];
            var count = stream.Read(header);

            if (count >= 5 && header[0] == 0x1B && header[1] == (byte)'L' &&
                header[2] == (byte)'u' && header[3] == (byte)'a')
            {
                var version = header[4] switch
                {
                    0x50 => "Lua 5.0",
                    0x51 => "Lua 5.1",
                    0x52 => "Lua 5.2",
                    0x53 when header[5] == 1 => "Lua 5.3 (定制)",
                    0x53 => "Lua 5.3",
                    0x54 => "Lua 5.4",
                    _ => $"未知 (0x{header[4]:X2})"
                };
                return new LuaFileInfo(path, relativePath, LuaInputKind.Bytecode, version);
            }

            stream.Position = 0;
            var probeLength = (int)Math.Min(stream.Length, 4096);
            var probe = new byte[probeLength];
            var offset = 0;
            while (offset < probe.Length)
            {
                var read = stream.Read(probe, offset, probe.Length - offset);
                if (read == 0) break;
                offset += read;
            }
            if (LooksLikeText(probe))
                return new LuaFileInfo(path, relativePath, LuaInputKind.Source, "Lua 源码", "无需反编译");

            return new LuaFileInfo(path, relativePath, LuaInputKind.Unsupported, "未知格式", "不支持");
        }
        catch (Exception ex)
        {
            return new LuaFileInfo(path, relativePath, LuaInputKind.Unsupported, "读取失败", "失败", Error: ex.Message);
        }
    }

    private static bool LooksLikeText(byte[] bytes)
    {
        if (bytes.Length == 0) return true;
        if (bytes.Any(b => b == 0)) return false;

        try
        {
            _ = new UTF8Encoding(false, true).GetString(bytes);
            var controls = bytes.Count(b => b < 0x20 && b is not (9 or 10 or 13));
            return controls <= Math.Max(1, bytes.Length / 100);
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }
}
