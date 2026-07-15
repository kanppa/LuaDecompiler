using System.Buffers;
using System.Text;

namespace LuaDecompilerDesktop;

internal readonly record struct LuaDecodeResult(string Text, int ConvertedSegments);

internal static class LuaStringDecoder
{
    private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);
    private static readonly Encoding StrictChineseEncoding = CreateChineseEncoding();

    public static LuaDecodeResult DecodeSuspectedChinese(
        string source,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(source)) return new(source, 0);

        var converted = 0;
        StringBuilder? builder = null;
        var copyFrom = 0;

        for (var index = 0; index < source.Length;)
        {
            if ((index & 0xFFFF) == 0) cancellationToken.ThrowIfCancellationRequested();

            if (!TryReadDecimalByteRun(source, index, out var runEnd, out var bytes))
            {
                index++;
                continue;
            }

            var decoded = IsValidUtf8(bytes) ? StrictUtf8.GetString(bytes) : null;
            decoded ??= TryDecode(StrictChineseEncoding, bytes);
            if (decoded is null || !LooksLikeReadableEastAsianText(decoded))
            {
                index = runEnd;
                continue;
            }

            builder ??= new StringBuilder(source.Length);
            builder.Append(source, copyFrom, index - copyFrom);
            builder.Append(EscapeForLuaString(decoded));
            copyFrom = runEnd;
            index = runEnd;
            converted++;
        }

        if (builder is null) return new(source, 0);
        builder.Append(source, copyFrom, source.Length - copyFrom);
        return new(builder.ToString(), converted);
    }

    private static Encoding CreateChineseEncoding()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(
            54936,
            EncoderFallback.ExceptionFallback,
            DecoderFallback.ExceptionFallback);
    }

    private static bool TryReadDecimalByteRun(
        string source,
        int start,
        out int end,
        out byte[] bytes)
    {
        end = start;
        bytes = Array.Empty<byte>();

        var cursor = start;
        var count = 0;
        while (TryReadDecimalByte(source, cursor, out _, out var next))
        {
            count++;
            cursor = next;
        }

        if (count < 2) return false;

        bytes = new byte[count];
        cursor = start;
        for (var index = 0; index < count; index++)
        {
            TryReadDecimalByte(source, cursor, out bytes[index], out cursor);
        }
        end = cursor;
        return true;
    }

    private static bool TryReadDecimalByte(
        string source,
        int start,
        out byte value,
        out int next)
    {
        value = 0;
        next = start;
        if (start + 1 >= source.Length || source[start] != '\\' || !IsAsciiDigit(source[start + 1]))
            return false;

        var number = 0;
        var cursor = start + 1;
        var digits = 0;
        while (cursor < source.Length && digits < 3 && IsAsciiDigit(source[cursor]))
        {
            number = number * 10 + source[cursor] - '0';
            cursor++;
            digits++;
        }

        if (number > byte.MaxValue) return false;
        value = (byte)number;
        next = cursor;
        return true;
    }

    private static bool IsAsciiDigit(char character) => character is >= '0' and <= '9';

    private static string? TryDecode(Encoding encoding, byte[] bytes)
    {
        try
        {
            return encoding.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
    }

    private static bool IsValidUtf8(ReadOnlySpan<byte> bytes)
    {
        while (!bytes.IsEmpty)
        {
            var status = Rune.DecodeFromUtf8(bytes, out _, out var consumed);
            if (status != OperationStatus.Done) return false;
            bytes = bytes[consumed..];
        }
        return true;
    }

    private static bool LooksLikeReadableEastAsianText(string value)
    {
        var hasEastAsianCharacter = false;
        foreach (var rune in value.EnumerateRunes())
        {
            var codePoint = rune.Value;
            if (codePoint is >= 0x4E00 and <= 0x9FFF
                or >= 0x3400 and <= 0x4DBF
                or >= 0x3040 and <= 0x30FF
                or >= 0xAC00 and <= 0xD7AF)
            {
                hasEastAsianCharacter = true;
            }

            if (Rune.IsControl(rune) && codePoint is not 9 and not 10 and not 13)
                return false;
        }
        return hasEastAsianCharacter;
    }

    private static string EscapeForLuaString(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(character switch
            {
                '\\' => "\\\\",
                '"' => "\\\"",
                '\a' => "\\a",
                '\b' => "\\b",
                '\f' => "\\f",
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                '\v' => "\\v",
                _ => character.ToString()
            });
        }
        return builder.ToString();
    }
}
