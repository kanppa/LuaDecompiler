using LuaDecompilerDesktop;
using System.Diagnostics;

var source = @"tbl = {
  unidentifiedDisplayName = ""\186\236\201\171\210\169\203\174"",
  unidentifiedDescriptionName = ""\189\171\186\236\201\171\181\196\210\169\178\221\181\183\203\233\214\198\179\201\181\196\204\229\193\166\187\214\184\180\188\193, ^000088\212\188\191\201\187\214\184\180 45\181\227HP^000000"",
  weight = ""\214\216\193\191:^777777 7^000000"",
  newline = ""\r\n""
}";

var result = LuaStringDecoder.DecodeSuspectedChinese(source);
Console.WriteLine(result.Text);
Console.WriteLine($"ConvertedSegments={result.ConvertedSegments}");

if (!result.Text.Contains("红色药水")
    || !result.Text.Contains("将红色的药草捣碎制成的体力恢复剂")
    || !result.Text.Contains("重量")
    || !result.Text.Contains("\\r\\n")
    || result.ConvertedSegments != 5)
{
    Environment.ExitCode = 1;
}

var bulkSource = string.Concat(Enumerable.Repeat(source, 20_000));
var stopwatch = Stopwatch.StartNew();
var bulkResult = LuaStringDecoder.DecodeSuspectedChinese(bulkSource);
stopwatch.Stop();
Console.WriteLine($"BulkCharacters={bulkSource.Length}, ConvertedSegments={bulkResult.ConvertedSegments}, ElapsedMs={stopwatch.ElapsedMilliseconds}");
if (bulkResult.ConvertedSegments != 100_000) Environment.ExitCode = 1;

var defaultSettings = new AppSettings { OutputPath = @"X:\stale\Decompiled", UseCustomOutputPath = false };
var customSettings = new AppSettings { OutputPath = @"X:\chosen", UseCustomOutputPath = true };
Console.WriteLine($"DefaultOutputSibling={defaultSettings.ResolveOutputPath() == AppSettings.DefaultOutputPath()}, CustomOutputRetained={customSettings.ResolveOutputPath() == customSettings.OutputPath}");
if (defaultSettings.ResolveOutputPath() != AppSettings.DefaultOutputPath()
    || customSettings.ResolveOutputPath() != customSettings.OutputPath)
    Environment.ExitCode = 1;
var simulatedArchiveDirectory = Path.Combine(Path.GetTempPath(), "LuaDecompiler.zip.test", "app");
var archiveRunDetected = AppSettings.IsLikelyArchiveTemporaryDirectory(simulatedArchiveDirectory, Path.GetTempPath());
var normalRunDetected = AppSettings.IsLikelyArchiveTemporaryDirectory(AppContext.BaseDirectory, Path.GetTempPath());
Console.WriteLine($"ArchiveRunDetected={archiveRunDetected}, NormalRunDetected={normalRunDetected}");
if (!archiveRunDetected || normalRunDetected) Environment.ExitCode = 1;

var sessionStore = new SessionResultStore();
var sessionDirectory = sessionStore.SessionDirectory;
var sessionStored = await sessionStore.StoreAsync("sample.lub", bulkResult.Text, bulkResult.ConvertedSegments, CancellationToken.None);
Console.WriteLine($"SessionTempExists={File.Exists(sessionStored.TempPath)}, Characters={sessionStored.CharacterCount}");
if (!File.Exists(sessionStored.TempPath) || sessionStored.CharacterCount != bulkResult.Text.Length) Environment.ExitCode = 1;
stopwatch.Restart();
var virtualDocument = VirtualTextDocument.Build(new FileInfo(sessionStored.TempPath), CancellationToken.None);
stopwatch.Stop();
Console.WriteLine($"IndexedLines={virtualDocument.LineCount}, IndexElapsedMs={stopwatch.ElapsedMilliseconds}, FirstLine={virtualDocument.GetLine(0)}");
if (virtualDocument.LineCount < 100_000 || !virtualDocument.GetLine(0).StartsWith("tbl =", StringComparison.Ordinal))
    Environment.ExitCode = 1;
var copiedResult = Path.Combine(Path.GetTempPath(), $"lua-decompiler-copy-test-{Guid.NewGuid():N}.lua");
try
{
    sessionStore.CopyTo("sample.lub", copiedResult);
    Console.WriteLine($"SavedCopyComplete={new FileInfo(copiedResult).Length == new FileInfo(sessionStored.TempPath).Length}");
    if (new FileInfo(copiedResult).Length != new FileInfo(sessionStored.TempPath).Length) Environment.ExitCode = 1;
}
finally
{
    if (File.Exists(copiedResult)) File.Delete(copiedResult);
}
sessionStore.Dispose();
Console.WriteLine($"SessionDeletedAfterDispose={!Directory.Exists(sessionDirectory)}");
if (Directory.Exists(sessionDirectory)) Environment.ExitCode = 1;

if (args.Length == 3)
{
    var service = new DecompilerService
    {
        JarPath = args[1],
        OpcodeMapPath = args[2]
    };
    var streamedPath = Path.Combine(Path.GetTempPath(), $"lua-decompiler-stream-test-{Guid.NewGuid():N}.lua");
    stopwatch.Restart();
    var decompiled = await service.DecompileToFileAsync(args[0], streamedPath, CancellationToken.None);
    stopwatch.Stop();
    Console.WriteLine($"StreamDecompileSuccess={decompiled.Success}, Characters={decompiled.CharacterCount}, ConvertedSegments={decompiled.ConvertedSegments}, ElapsedMs={stopwatch.ElapsedMilliseconds}");
    if (!decompiled.Success)
    {
        Console.WriteLine(decompiled.Error);
        Environment.ExitCode = 1;
    }
    else
    {
        var streamedDocument = VirtualTextDocument.Build(new FileInfo(streamedPath), CancellationToken.None);
        var firstLines = Enumerable.Range(0, Math.Min(100, streamedDocument.LineCount))
            .Select(streamedDocument.GetLine)
            .ToArray();
        var containsRedPotion = firstLines.Any(line => line.Contains("红色药水", StringComparison.Ordinal));
        var containsWeight = firstLines.Any(line => line.Contains("重量", StringComparison.Ordinal));
        Console.WriteLine($"StreamedLines={streamedDocument.LineCount}, ContainsRedPotion={containsRedPotion}, ContainsWeight={containsWeight}");
        if (!containsRedPotion || !containsWeight) Environment.ExitCode = 1;
    }
    if (File.Exists(streamedPath)) File.Delete(streamedPath);
}
