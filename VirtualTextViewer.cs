using System.Text;

namespace LuaDecompilerDesktop;

internal sealed class VirtualTextViewer : UserControl
{
    private readonly VScrollBar _vertical = new() { Dock = DockStyle.Right, SmallChange = 1 };
    private readonly HScrollBar _horizontal = new() { Dock = DockStyle.Bottom, SmallChange = 24 };
    private readonly Dictionary<string, VirtualTextDocument> _documents = new(StringComparer.OrdinalIgnoreCase);
    private VirtualTextDocument? _document;
    private string[]? _inlineLines = { "" };
    private string? _inlineKey;
    private CancellationTokenSource? _loadCancellation;
    private int _loadGeneration;
    private int _lineHeight;
    private int _characterWidth;

    public VirtualTextViewer()
    {
        Dock = DockStyle.Fill;
        BackColor = Color.FromArgb(24, 27, 33);
        ForeColor = Color.FromArgb(220, 224, 230);
        Font = new Font("Consolas", 10F);
        TabStop = true;
        DoubleBuffered = true;
        SetStyle(ControlStyles.Selectable | ControlStyles.ResizeRedraw, true);
        Controls.Add(_vertical);
        Controls.Add(_horizontal);
        _vertical.Scroll += (_, _) => ScrollChanged();
        _horizontal.Scroll += (_, _) => ScrollChanged();
        UpdateFontMetrics();
    }

    public async Task ShowFileAsync(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var info = new FileInfo(fullPath);
        if (!info.Exists)
        {
            ShowText($"missing:{fullPath}", $"-- 找不到预览文件：{fullPath}");
            return;
        }

        SaveCurrentScrollPosition();
        if (_documents.TryGetValue(fullPath, out var cached) && cached.Matches(info))
        {
            ActivateDocument(cached);
            return;
        }

        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        _loadCancellation = new CancellationTokenSource();
        var token = _loadCancellation.Token;
        var generation = ++_loadGeneration;
        ShowText($"indexing:{fullPath}", $"-- 正在建立全文滚动索引…\r\n-- {fullPath}");

        try
        {
            var document = await Task.Run(() => VirtualTextDocument.Build(info, token), token);
            if (token.IsCancellationRequested || generation != _loadGeneration) return;
            _documents[fullPath] = document;
            TrimDocumentCache(fullPath);
            ActivateDocument(document);
        }
        catch (OperationCanceledException)
        {
            // 快速切换文件时丢弃旧索引任务。
        }
        catch (Exception ex)
        {
            if (generation == _loadGeneration)
                ShowText($"error:{fullPath}", $"-- 无法预览：{ex.Message}");
        }
    }

    public void ShowText(string key, string text)
    {
        if (_document is null && string.Equals(_inlineKey, key, StringComparison.Ordinal)) return;
        SaveCurrentScrollPosition();
        _document = null;
        _inlineKey = key;
        _inlineLines = text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');
        ResetScrollbars();
        Invalidate();
    }

    public void Clear() => ShowText("empty", "");

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        UpdateFontMetrics();
        UpdateScrollbars();
        Invalidate();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        UpdateScrollbars();
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        Focus();
        base.OnMouseDown(e);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        var steps = Math.Max(1, SystemInformation.MouseWheelScrollLines);
        SetVerticalValue(_vertical.Value - Math.Sign(e.Delta) * steps);
        base.OnMouseWheel(e);
    }

    protected override bool IsInputKey(Keys keyData)
    {
        var key = keyData & Keys.KeyCode;
        return key is Keys.Up or Keys.Down or Keys.Left or Keys.Right
            or Keys.PageUp or Keys.PageDown or Keys.Home or Keys.End
            || base.IsInputKey(keyData);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        var page = Math.Max(1, VisibleLineCount - 1);
        switch (e.KeyCode)
        {
            case Keys.Up: SetVerticalValue(_vertical.Value - 1); break;
            case Keys.Down: SetVerticalValue(_vertical.Value + 1); break;
            case Keys.PageUp: SetVerticalValue(_vertical.Value - page); break;
            case Keys.PageDown: SetVerticalValue(_vertical.Value + page); break;
            case Keys.Home when e.Control: SetVerticalValue(0); break;
            case Keys.End when e.Control: SetVerticalValue(Math.Max(0, LineCount - VisibleLineCount)); break;
            case Keys.Left: SetHorizontalValue(_horizontal.Value - 24); break;
            case Keys.Right: SetHorizontalValue(_horizontal.Value + 24); break;
            default: base.OnKeyDown(e); return;
        }
        e.Handled = true;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.Clear(BackColor);
        var viewport = Viewport;
        if (viewport.Width <= 0 || viewport.Height <= 0) return;

        var firstLine = Math.Min(_vertical.Value, Math.Max(0, LineCount - 1));
        var lastLine = Math.Min(LineCount, firstLine + VisibleLineCount + 1);
        var gutterWidth = GetGutterWidth();

        using var gutterBrush = new SolidBrush(Color.FromArgb(21, 24, 29));
        using var dividerPen = new Pen(Color.FromArgb(43, 47, 56));
        e.Graphics.FillRectangle(gutterBrush, 0, 0, gutterWidth, viewport.Height);
        e.Graphics.DrawLine(dividerPen, gutterWidth - 1, 0, gutterWidth - 1, viewport.Height);
        var oldClip = e.Graphics.Clip;
        e.Graphics.SetClip(viewport);

        for (var lineIndex = firstLine; lineIndex < lastLine; lineIndex++)
        {
            var y = (lineIndex - firstLine) * _lineHeight;
            var line = GetLine(lineIndex);
            var numberBounds = new Rectangle(4, y, gutterWidth - 10, _lineHeight);
            TextRenderer.DrawText(e.Graphics, (lineIndex + 1).ToString(), Font, numberBounds,
                Color.FromArgb(105, 115, 130),
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

            var textX = gutterWidth + 8 - _horizontal.Value;
            var textBounds = new Rectangle(textX, y, Math.Max(viewport.Width * 4, 4096), _lineHeight);
            TextRenderer.DrawText(e.Graphics, line, Font, textBounds, ForeColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding |
                TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        }

        e.Graphics.Clip = oldClip;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _loadCancellation?.Cancel();
            _loadCancellation?.Dispose();
        }
        base.Dispose(disposing);
    }

    private Rectangle Viewport => new(
        0,
        0,
        Math.Max(0, ClientSize.Width - _vertical.Width),
        Math.Max(0, ClientSize.Height - _horizontal.Height));

    private int VisibleLineCount => Math.Max(1, Viewport.Height / Math.Max(1, _lineHeight));
    private int LineCount => _document?.LineCount ?? Math.Max(1, _inlineLines?.Length ?? 1);

    private void ActivateDocument(VirtualTextDocument document)
    {
        _document = document;
        _inlineLines = null;
        _inlineKey = null;
        UpdateScrollbars(document.VerticalPosition, document.HorizontalPosition);
        Invalidate();
    }

    private string GetLine(int index)
    {
        if (_document is not null) return _document.GetLine(index);
        return _inlineLines is not null && index >= 0 && index < _inlineLines.Length
            ? _inlineLines[index]
            : "";
    }

    private void UpdateFontMetrics()
    {
        if (Font is null) return;
        _lineHeight = Math.Max(16, TextRenderer.MeasureText("Mg", Font, Size.Empty, TextFormatFlags.NoPadding).Height + 2);
        _characterWidth = Math.Max(6, TextRenderer.MeasureText("M", Font, Size.Empty, TextFormatFlags.NoPadding).Width);
    }

    private int GetGutterWidth()
    {
        var digits = Math.Max(3, LineCount.ToString().Length);
        return digits * _characterWidth + 18;
    }

    private void ResetScrollbars() => UpdateScrollbars(0, 0);

    private void UpdateScrollbars(int? desiredVertical = null, int? desiredHorizontal = null)
    {
        if (_lineHeight <= 0) return;
        var visible = VisibleLineCount;
        var verticalValue = desiredVertical ?? _vertical.Value;
        _vertical.Value = 0;
        _vertical.LargeChange = visible;
        _vertical.Maximum = Math.Max(0, LineCount - 1 + visible - 1);
        SetVerticalValue(verticalValue);

        var viewportWidth = Math.Max(1, Viewport.Width - GetGutterWidth() - 8);
        var estimatedWidth = (_document?.MaxLineBytes ?? GetInlineMaxLength()) * _characterWidth;
        var horizontalValue = desiredHorizontal ?? _horizontal.Value;
        _horizontal.Value = 0;
        _horizontal.LargeChange = viewportWidth;
        _horizontal.Maximum = Math.Max(0, estimatedWidth - 1 + viewportWidth - 1);
        SetHorizontalValue(horizontalValue);
    }

    private int GetInlineMaxLength() => _inlineLines?.Length > 0 ? _inlineLines.Max(line => line.Length) : 0;

    private void SetVerticalValue(int value)
    {
        var maximum = Math.Max(_vertical.Minimum, _vertical.Maximum - _vertical.LargeChange + 1);
        _vertical.Value = Math.Clamp(value, _vertical.Minimum, maximum);
        ScrollChanged();
    }

    private void SetHorizontalValue(int value)
    {
        var maximum = Math.Max(_horizontal.Minimum, _horizontal.Maximum - _horizontal.LargeChange + 1);
        _horizontal.Value = Math.Clamp(value, _horizontal.Minimum, maximum);
        ScrollChanged();
    }

    private void ScrollChanged()
    {
        if (_document is not null)
        {
            _document.VerticalPosition = _vertical.Value;
            _document.HorizontalPosition = _horizontal.Value;
        }
        Invalidate();
    }

    private void SaveCurrentScrollPosition()
    {
        if (_document is null) return;
        _document.VerticalPosition = _vertical.Value;
        _document.HorizontalPosition = _horizontal.Value;
    }

    private void TrimDocumentCache(string activePath)
    {
        if (_documents.Count <= 12) return;
        var remove = _documents.Keys.FirstOrDefault(path =>
            !string.Equals(path, activePath, StringComparison.OrdinalIgnoreCase));
        if (remove is not null) _documents.Remove(remove);
    }
}

internal sealed class VirtualTextDocument
{
    private const int PageSize = 256;
    private readonly long[] _lineOffsets;
    private readonly Dictionary<int, string[]> _pageCache = new();

    private VirtualTextDocument(string path, long length, DateTime lastWriteUtc, long[] lineOffsets, int maxLineBytes)
    {
        Path = path;
        Length = length;
        LastWriteUtc = lastWriteUtc;
        _lineOffsets = lineOffsets;
        MaxLineBytes = maxLineBytes;
    }

    public string Path { get; }
    public long Length { get; }
    public DateTime LastWriteUtc { get; }
    public int LineCount => _lineOffsets.Length;
    public int MaxLineBytes { get; }
    public int VerticalPosition { get; set; }
    public int HorizontalPosition { get; set; }

    public static VirtualTextDocument Build(FileInfo info, CancellationToken cancellationToken)
    {
        var offsets = new List<long>(Math.Max(1024, (int)Math.Min(int.MaxValue, info.Length / 32)));
        offsets.Add(0);
        var buffer = new byte[128 * 1024];
        long position = 0;
        var currentLineBytes = 0;
        var maxLineBytes = 0;

        using var stream = new FileStream(info.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite,
            buffer.Length, FileOptions.SequentialScan);
        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var index = 0; index < read; index++)
            {
                position++;
                currentLineBytes++;
                if (buffer[index] != (byte)'\n') continue;
                maxLineBytes = Math.Max(maxLineBytes, currentLineBytes);
                offsets.Add(position);
                currentLineBytes = 0;
            }
        }
        maxLineBytes = Math.Max(maxLineBytes, currentLineBytes);
        return new VirtualTextDocument(info.FullName, info.Length, info.LastWriteTimeUtc,
            offsets.ToArray(), maxLineBytes);
    }

    public bool Matches(FileInfo info) =>
        info.Exists && info.Length == Length && info.LastWriteTimeUtc == LastWriteUtc;

    public string GetLine(int lineIndex)
    {
        if (lineIndex < 0 || lineIndex >= LineCount) return "";
        var pageStart = lineIndex / PageSize * PageSize;
        if (!_pageCache.TryGetValue(pageStart, out var page))
        {
            page = ReadPage(pageStart);
            if (_pageCache.Count >= 12) _pageCache.Remove(_pageCache.Keys.First());
            _pageCache[pageStart] = page;
        }
        return page[lineIndex - pageStart];
    }

    private string[] ReadPage(int pageStart)
    {
        var count = Math.Min(PageSize, LineCount - pageStart);
        var lines = new string[count];
        using var stream = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite,
            16 * 1024, FileOptions.SequentialScan);
        stream.Seek(_lineOffsets[pageStart], SeekOrigin.Begin);
        using var reader = new StreamReader(stream, new UTF8Encoding(false), true, 16 * 1024, false);
        for (var index = 0; index < count; index++) lines[index] = reader.ReadLine() ?? "";
        return lines;
    }
}
