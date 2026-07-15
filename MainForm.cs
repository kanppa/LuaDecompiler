using System.Diagnostics;
using System.Text;

namespace LuaDecompilerDesktop;

internal sealed class MainForm : Form
{
    private readonly DecompilerService _service = new();
    private readonly SessionResultStore _sessionResults = new();
    private readonly AppSettings _settings = AppSettings.Load();
    private readonly List<LuaFileInfo> _items = new();
    private readonly DataGridView _grid = new();
    private readonly VirtualTextViewer _preview = new();
    private readonly RichTextBox _log = new();
    private readonly TextBox _outputBox = new();
    private readonly ToolStripStatusLabel _statusLabel = new("就绪");
    private readonly ToolStripProgressBar _progress = new() { Width = 180 };
    private readonly RoundedButton _runSelectedButton = new();
    private readonly RoundedButton _runAllButton = new();
    private readonly RoundedButton _saveSelectedButton = new();
    private readonly RoundedButton _saveAllButton = new();
    private readonly RoundedButton _cancelButton = new();
    private readonly FlowLayoutPanel _toolbar = new();
    private readonly TableLayoutPanel _outputPanel = new();
    private readonly Panel _contentHost = new();
    private readonly SplitContainer _split = new();
    private readonly ModernTabControl _tabs = new();
    private readonly Label _fileCountLabel = new();
    private readonly NumericUpDown _spacingBox = new();
    private readonly List<TabPage> _tabPages = new();
    private CancellationTokenSource? _cts;
    private bool _applyingLayout;
    private bool _closeAfterCancellation;

    private static readonly string[] SupportedExtensions = { ".lua", ".luac", ".lub", ".bytes", ".out" };

    public MainForm(string[] startupPaths)
    {
        Text = "Lua 反编译工具";
        MinimumSize = new Size(980, 640);
        Size = new Size(1280, 780);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Microsoft YaHei UI", 9F);
        BackColor = Color.FromArgb(20, 23, 28);
        ForeColor = Color.Gainsboro;
        AllowDrop = true;
        DoubleBuffered = true;

        BuildUi();
        WireEvents();
        _outputBox.Text = _settings.ResolveOutputPath();
        _spacingBox.Value = Math.Clamp(_settings.UiSpacing, (int)_spacingBox.Minimum, (int)_spacingBox.Maximum);
        ApplySpacing();

        Shown += (_, _) =>
        {
            _applyingLayout = true;
            try
            {
                _split.Panel1MinSize = 360;
                _split.Panel2MinSize = 340;
                ApplySplitterRatio();
            }
            finally
            {
                _applyingLayout = false;
            }
            if (startupPaths.Length > 0) AddPaths(startupPaths);
            UpdateEngineStatus();
            if (AppSettings.IsLikelyRunningFromArchiveTemporaryDirectory())
            {
                MessageBox.Show(this,
                    "检测到程序正在从 ZIP 的临时解压目录运行。\r\n\r\n请关闭程序，先把压缩包完整解压到普通文件夹，再运行 LuaDecompiler.exe。否则默认保存目录和会话文件会位于系统临时目录。",
                    "请先解压发布包",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        };
        FormClosing += (_, eventArgs) =>
        {
            if (_cts is null) return;
            eventArgs.Cancel = true;
            if (_closeAfterCancellation) return;
            _closeAfterCancellation = true;
            _statusLabel.Text = "正在取消任务并清理会话临时文件…";
            _cts.Cancel();
        };
        FormClosed += (_, _) => _sessionResults.Dispose();
    }

    private void BuildUi()
    {
        var shell = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(20, 23, 28),
            ColumnCount = 1,
            RowCount = 5,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));
        shell.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        shell.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));

        var header = BuildHeader();

        _toolbar.Dock = DockStyle.Fill;
        _toolbar.AutoSize = true;
        _toolbar.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _toolbar.BackColor = Color.FromArgb(27, 30, 36);
        _toolbar.WrapContents = false;

        var addFiles = MakeButton("添加文件");
        var addFolder = MakeButton("添加文件夹");
        _runSelectedButton.Text = "反编译选中";
        StyleButton(_runSelectedButton, true);
        _runAllButton.Text = "全部反编译";
        StyleButton(_runAllButton, true);
        _saveSelectedButton.Text = "保存选中";
        StyleButton(_saveSelectedButton);
        _saveAllButton.Text = "保存全部";
        StyleButton(_saveAllButton);
        _saveSelectedButton.Enabled = false;
        _saveAllButton.Enabled = false;
        _cancelButton.Text = "取消";
        StyleButton(_cancelButton);
        _cancelButton.Enabled = false;
        var clear = MakeButton("清空列表");
        var settings = MakeButton("引擎设置");
        var spacingLabel = new Label
        {
            Text = "界面间距",
            AutoSize = true,
            ForeColor = Color.FromArgb(159, 167, 181),
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(18, 8, 6, 0)
        };
        _spacingBox.Minimum = 6;
        _spacingBox.Maximum = 24;
        _spacingBox.Increment = 2;
        _spacingBox.Width = 58;
        _spacingBox.BackColor = Color.FromArgb(35, 39, 47);
        _spacingBox.ForeColor = Color.White;
        _spacingBox.BorderStyle = BorderStyle.FixedSingle;
        _spacingBox.Margin = new Padding(0, 5, 0, 0);

        _toolbar.Controls.AddRange(new Control[]
        {
            addFiles, addFolder, _runSelectedButton, _runAllButton,
            _saveSelectedButton, _saveAllButton, _cancelButton, clear, settings
        });

        _outputPanel.Dock = DockStyle.Fill;
        _outputPanel.AutoSize = true;
        _outputPanel.ColumnCount = 6;
        _outputPanel.BackColor = Color.FromArgb(24, 27, 33);
        _outputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _outputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _outputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _outputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 12));
        _outputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _outputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var outputLabel = new Label
        {
            Text = "保存目录",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            ForeColor = Color.FromArgb(172, 180, 194),
            Font = new Font(Font, FontStyle.Bold)
        };
        StyleTextBox(_outputBox);
        _outputBox.Dock = DockStyle.Fill;
        _outputBox.Font = new Font("Consolas", 9.5F);
        var browseOutput = MakeButton("浏览…");
        _outputPanel.Controls.Add(outputLabel, 0, 0);
        _outputPanel.Controls.Add(_outputBox, 1, 0);
        _outputPanel.Controls.Add(browseOutput, 2, 0);
        spacingLabel.Text = "间距";
        spacingLabel.Anchor = AnchorStyles.Left;
        spacingLabel.Margin = new Padding(0, 0, 6, 0);
        _spacingBox.Anchor = AnchorStyles.Left;
        _spacingBox.Margin = Padding.Empty;
        _outputPanel.Controls.Add(spacingLabel, 4, 0);
        _outputPanel.Controls.Add(_spacingBox, 5, 0);

        ConfigureGrid();
        ConfigureEditor(_log);
        _log.ReadOnly = true;

        _tabs.Dock = DockStyle.Fill;
        var previewPage = new TabPage("源码预览") { BackColor = Color.FromArgb(24, 27, 33) };
        var logPage = new TabPage("运行日志") { BackColor = Color.FromArgb(24, 27, 33) };
        previewPage.Controls.Add(_preview);
        logPage.Controls.Add(_log);
        _tabPages.AddRange(new[] { previewPage, logPage });
        _tabs.TabPages.Add(previewPage);
        _tabs.TabPages.Add(logPage);

        _split.Dock = DockStyle.Fill;
        _split.Orientation = Orientation.Vertical;
        _split.SplitterDistance = 520;
        _split.BackColor = Color.FromArgb(20, 23, 28);
        _split.Panel1.Controls.Add(BuildFileCard());
        _split.Panel2.Controls.Add(BuildPreviewCard());

        _contentHost.Dock = DockStyle.Fill;
        _contentHost.BackColor = Color.FromArgb(20, 23, 28);
        _contentHost.Controls.Add(_split);

        var status = new StatusStrip
        {
            Dock = DockStyle.Fill,
            SizingGrip = false,
            BackColor = Color.FromArgb(27, 30, 36),
            ForeColor = Color.FromArgb(170, 178, 192),
            Padding = new Padding(8, 2, 8, 2)
        };
        status.Items.Add(_statusLabel);
        status.Items.Add(new ToolStripStatusLabel { Spring = true });
        status.Items.Add(_progress);

        shell.Controls.Add(header, 0, 0);
        shell.Controls.Add(_toolbar, 0, 1);
        shell.Controls.Add(_outputPanel, 0, 2);
        shell.Controls.Add(_contentHost, 0, 3);
        shell.Controls.Add(status, 0, 4);
        Controls.Add(shell);

        addFiles.Click += (_, _) => PickFiles();
        addFolder.Click += (_, _) => PickFolder();
        browseOutput.Click += (_, _) => PickOutputFolder();
        clear.Click += (_, _) =>
        {
            _items.Clear();
            _sessionResults.Clear();
            _saveSelectedButton.Enabled = false;
            _saveAllButton.Enabled = false;
            RefreshGrid();
            _preview.Clear();
        };
        settings.Click += (_, _) => ShowEngineSettings();
    }

    private Control BuildHeader()
    {
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(31, 36, 44),
            Padding = new Padding(18, 12, 18, 10),
            ColumnCount = 3
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var logo = new Label
        {
            Text = "LUA",
            AutoSize = false,
            Size = new Size(52, 52),
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.FromArgb(58, 126, 232),
            ForeColor = Color.White,
            Font = new Font("Consolas", 11F, FontStyle.Bold),
            Margin = new Padding(0, 1, 14, 0)
        };
        var titles = new TableLayoutPanel
        {
            AutoSize = false,
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty
        };
        titles.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        titles.RowStyles.Add(new RowStyle(SizeType.Absolute, 37));
        titles.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
        titles.Controls.Add(new Label
        {
            Text = "Lua Decompiler",
            Dock = DockStyle.Fill,
            ForeColor = Color.White,
            Font = new Font("Microsoft YaHei UI", 13.5F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        titles.Controls.Add(new Label
        {
            Text = "批量识别、反编译与导出 Lua 字节码",
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(151, 160, 176),
            Font = new Font("Microsoft YaHei UI", 8.5F),
            TextAlign = ContentAlignment.TopLeft
        }, 0, 1);
        var badge = new Label
        {
            Text = "LUA 5.0 — 5.4",
            AutoSize = true,
            Anchor = AnchorStyles.Right,
            BackColor = Color.FromArgb(39, 47, 58),
            ForeColor = Color.FromArgb(111, 177, 255),
            Font = new Font("Consolas", 8.5F, FontStyle.Bold),
            Padding = new Padding(12, 7, 12, 7)
        };
        header.Controls.Add(logo, 0, 0);
        header.Controls.Add(titles, 1, 0);
        header.Controls.Add(badge, 2, 0);
        return header;
    }

    private Control BuildFileCard()
    {
        var card = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(27, 30, 36),
            RowCount = 2,
            Padding = new Padding(1)
        };
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        card.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var header = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(32, 36, 43) };
        header.Controls.Add(new Label
        {
            Text = "文件队列",
            Dock = DockStyle.Left,
            Width = 120,
            Padding = new Padding(12, 0, 0, 0),
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.White,
            Font = new Font(Font, FontStyle.Bold)
        });
        _fileCountLabel.Dock = DockStyle.Right;
        _fileCountLabel.Width = 110;
        _fileCountLabel.Padding = new Padding(0, 0, 12, 0);
        _fileCountLabel.TextAlign = ContentAlignment.MiddleRight;
        _fileCountLabel.ForeColor = Color.FromArgb(126, 160, 208);
        _fileCountLabel.Text = "0 个文件";
        header.Controls.Add(_fileCountLabel);
        card.Controls.Add(header, 0, 0);
        card.Controls.Add(_grid, 0, 1);
        return card;
    }

    private Control BuildPreviewCard()
    {
        var card = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(27, 30, 36), Padding = new Padding(1) };
        card.Controls.Add(_tabs);
        return card;
    }

    private void WireEvents()
    {
        DragEnter += (_, e) => e.Effect = e.Data?.GetDataPresent(DataFormats.FileDrop) == true ? DragDropEffects.Copy : DragDropEffects.None;
        DragDrop += (_, e) =>
        {
            if (e.Data?.GetData(DataFormats.FileDrop) is string[] paths) AddPaths(paths);
        };
        _grid.SelectionChanged += (_, _) => PreviewSelected();
        _grid.CellDoubleClick += (_, _) => OpenSelectedOutput();
        _runSelectedButton.Click += async (_, _) => await RunAsync(GetSelectedItems());
        _runAllButton.Click += async (_, _) => await RunAsync(_items.Where(x => x.Kind == LuaInputKind.Bytecode).ToList());
        _saveSelectedButton.Click += (_, _) => SaveResults(GetSelectedItems());
        _saveAllButton.Click += (_, _) => SaveResults(_items);
        _cancelButton.Click += (_, _) => _cts?.Cancel();
        _spacingBox.ValueChanged += (_, _) => ApplySpacing();
        _split.SplitterMoved += (_, _) =>
        {
            if (Visible && !_applyingLayout)
            {
                _settings.SplitterDistance = _split.SplitterDistance;
                _settings.SplitterRatio = _split.Width > 0
                    ? _split.SplitterDistance / (double)_split.Width
                    : _settings.SplitterRatio;
            }
        };
        _split.SizeChanged += (_, _) =>
        {
            if (!Visible || _applyingLayout) return;
            _applyingLayout = true;
            try { ApplySplitterRatio(); }
            finally { _applyingLayout = false; }
        };
        FormClosing += (_, _) =>
        {
            _cts?.Cancel();
            _settings.OutputPath = _outputBox.Text.Trim();
            try
            {
                _settings.UseCustomOutputPath = !string.Equals(
                    Path.GetFullPath(_settings.OutputPath),
                    Path.GetFullPath(AppSettings.DefaultOutputPath()),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                _settings.UseCustomOutputPath = false;
                _settings.OutputPath = AppSettings.DefaultOutputPath();
            }
            _settings.UiSpacing = (int)_spacingBox.Value;
            _settings.SplitterDistance = _split.SplitterDistance;
            _settings.SplitterRatio = _split.Width > 0
                ? _split.SplitterDistance / (double)_split.Width
                : _settings.SplitterRatio;
            try { _settings.Save(); } catch { /* 设置保存失败不应阻止退出。 */ }
        };
    }

    private void ConfigureGrid()
    {
        _grid.Dock = DockStyle.Fill;
        _grid.BackgroundColor = Color.FromArgb(25, 27, 31);
        _grid.BorderStyle = BorderStyle.None;
        _grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        _grid.GridColor = Color.FromArgb(42, 46, 54);
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AllowUserToResizeRows = false;
        _grid.AllowUserToResizeColumns = true;
        _grid.ReadOnly = true;
        _grid.MultiSelect = true;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.RowHeadersVisible = false;
        _grid.AutoGenerateColumns = false;
        _grid.EnableHeadersVisualStyles = false;
        _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        _grid.ColumnHeadersHeight = 38;
        _grid.RowTemplate.Height = 34;
        _grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.FromArgb(35, 39, 47),
            ForeColor = Color.FromArgb(190, 197, 209),
            SelectionBackColor = Color.FromArgb(35, 39, 47),
            Font = new Font(Font, FontStyle.Bold),
            Alignment = DataGridViewContentAlignment.MiddleLeft,
            Padding = new Padding(6, 0, 0, 0)
        };
        _grid.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.FromArgb(27, 30, 36),
            ForeColor = Color.FromArgb(213, 218, 226),
            SelectionBackColor = Color.FromArgb(48, 82, 129),
            SelectionForeColor = Color.White,
            Padding = new Padding(6, 0, 0, 0)
        };
        _grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(30, 34, 40);
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "文件",
            DataPropertyName = "File",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FillWeight = 68,
            MinimumWidth = 160,
            Resizable = DataGridViewTriState.True
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "格式", DataPropertyName = "Version", Width = 96, MinimumWidth = 72 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "大小", DataPropertyName = "Size", Width = 84, MinimumWidth = 64 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "状态",
            DataPropertyName = "Status",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FillWeight = 32,
            MinimumWidth = 120,
            Resizable = DataGridViewTriState.True
        });
    }

    private static void ConfigureEditor(RichTextBox box)
    {
        box.Dock = DockStyle.Fill;
        box.BorderStyle = BorderStyle.None;
        box.BackColor = Color.FromArgb(24, 27, 33);
        box.ForeColor = Color.FromArgb(220, 224, 230);
        box.Font = new Font("Consolas", 10F);
        box.WordWrap = false;
        box.DetectUrls = false;
    }

    private static Button MakeButton(string text)
    {
        var button = new RoundedButton { Text = text };
        StyleButton(button);
        return button;
    }

    private static void StyleButton(Button button, bool primary = false)
    {
        button.AutoSize = true;
        button.Height = 34;
        button.Padding = new Padding(12, 3, 12, 3);
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        if (button is RoundedButton rounded) rounded.Primary = primary;
        button.BackColor = primary ? Color.FromArgb(58, 126, 232) : Color.FromArgb(52, 57, 66);
        button.ForeColor = Color.White;
        button.Margin = new Padding(3, 0, 3, 0);
    }

    private static void StyleTextBox(TextBox box)
    {
        box.BackColor = Color.FromArgb(35, 39, 47);
        box.ForeColor = Color.Gainsboro;
        box.BorderStyle = BorderStyle.FixedSingle;
    }

    private void ApplySpacing()
    {
        var spacing = (int)_spacingBox.Value;
        var small = Math.Max(3, spacing / 2);
        _toolbar.Padding = new Padding(spacing, small, spacing, small);
        _outputPanel.Padding = new Padding(spacing, small, spacing, small);
        _contentHost.Padding = new Padding(spacing);
        _split.SplitterWidth = Math.Max(4, small);
        _grid.RowTemplate.Height = 24 + spacing;
        _grid.ColumnHeadersHeight = 28 + spacing;
        _grid.DefaultCellStyle.Padding = new Padding(small, 0, small, 0);
        foreach (DataGridViewRow row in _grid.Rows) row.Height = _grid.RowTemplate.Height;
        foreach (var page in _tabPages) page.Padding = new Padding(small);
        foreach (var button in _toolbar.Controls.OfType<Button>())
        {
            button.Padding = new Padding(8 + small, 2 + small / 3, 8 + small, 2 + small / 3);
            button.Margin = new Padding(Math.Max(2, small / 2), 0, Math.Max(2, small / 2), 0);
        }
        _settings.UiSpacing = spacing;
        _contentHost.PerformLayout();
        _grid.Invalidate();
    }

    private void ApplySplitterRatio()
    {
        if (_split.Width <= 0) return;
        var ratio = Math.Clamp(_settings.SplitterRatio, 0.25, 0.75);
        var minimum = Math.Max(1, _split.Panel1MinSize);
        var maximum = Math.Max(minimum, _split.Width - _split.Panel2MinSize - _split.SplitterWidth);
        _split.SplitterDistance = Math.Clamp((int)Math.Round(_split.Width * ratio), minimum, maximum);
    }

    private void PickFiles()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "选择 Lua 文件",
            Filter = "Lua 文件|*.lua;*.luac;*.lub;*.bytes;*.out|所有文件|*.*",
            Multiselect = true
        };
        if (dialog.ShowDialog(this) == DialogResult.OK) AddPaths(dialog.FileNames);
    }

    private void PickFolder()
    {
        using var dialog = new FolderBrowserDialog { Description = "选择包含 Lua 文件的目录", UseDescriptionForTitle = true };
        if (dialog.ShowDialog(this) == DialogResult.OK) AddDirectory(dialog.SelectedPath);
    }

    private void PickOutputFolder()
    {
        using var dialog = new FolderBrowserDialog { Description = "选择反编译输出目录", UseDescriptionForTitle = true };
        if (Directory.Exists(_outputBox.Text)) dialog.SelectedPath = _outputBox.Text;
        if (dialog.ShowDialog(this) == DialogResult.OK) _outputBox.Text = dialog.SelectedPath;
    }

    private void AddPaths(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            if (Directory.Exists(path)) AddDirectory(path);
            else if (File.Exists(path)) AddFile(path, Path.GetFileName(path));
        }
        RefreshGrid();
    }

    private void AddDirectory(string root)
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                         .Where(f => SupportedExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase)))
                AddFile(file, Path.GetRelativePath(root, file));
        }
        catch (Exception ex)
        {
            Log($"扫描目录失败：{root} - {ex.Message}");
        }
    }

    private void AddFile(string path, string relativePath)
    {
        var fullPath = Path.GetFullPath(path);
        if (_items.Any(x => string.Equals(x.FullPath, fullPath, StringComparison.OrdinalIgnoreCase))) return;
        _items.Add(LuaFileInfo.Inspect(fullPath, relativePath));
    }

    private void RefreshGrid()
    {
        var selectedPaths = GetSelectedItems().Select(x => x.FullPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        _grid.DataSource = null;
        _grid.DataSource = _items.Select(x => new
        {
            File = x.RelativePath,
            x.Version,
            Size = FormatSize(x.Size),
            x.Status
        }).ToList();
        for (var i = 0; i < _items.Count; i++)
            if (selectedPaths.Contains(_items[i].FullPath)) _grid.Rows[i].Selected = true;
        _fileCountLabel.Text = $"{_items.Count} 个文件";
        _statusLabel.Text = $"共 {_items.Count} 个文件，字节码 {_items.Count(x => x.Kind == LuaInputKind.Bytecode)} 个";
    }

    private List<LuaFileInfo> GetSelectedItems() => _grid.SelectedRows.Cast<DataGridViewRow>()
        .Select(row => row.Index)
        .Where(i => i >= 0 && i < _items.Count)
        .Select(i => _items[i])
        .Where(x => x.Kind == LuaInputKind.Bytecode)
        .ToList();

    private async Task RunAsync(List<LuaFileInfo> targets)
    {
        if (targets.Count == 0)
        {
            MessageBox.Show(this, "没有可反编译的字节码文件。", "Lua 反编译工具", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        _cts = new CancellationTokenSource();
        SetRunning(true);
        _progress.Minimum = 0;
        _progress.Maximum = targets.Count;
        _progress.Value = 0;
        var success = 0;

        try
        {
            for (var index = 0; index < targets.Count; index++)
            {
                _cts.Token.ThrowIfCancellationRequested();
                var item = targets[index];
                _sessionResults.Remove(item.FullPath);
                ReplaceItem(item, item with { Status = "处理中…", Error = null });
                RefreshGrid();
                _statusLabel.Text = $"正在处理 {index + 1}/{targets.Count}：{item.FileName}";

                var tempPath = _sessionResults.CreateResultPath();
                var result = await _service.DecompileToFileAsync(item.FullPath, tempPath, _cts.Token);
                if (result.Success)
                {
                    var stored = _sessionResults.RegisterResult(
                        item.FullPath,
                        tempPath,
                        result.CharacterCount,
                        result.ConvertedSegments);
                    var status = result.ConvertedSegments > 0
                        ? $"待保存 · 中文 {result.ConvertedSegments}"
                        : "待保存";
                    ReplaceItem(item, item with { Status = status, OutputPath = null });
                    success++;
                    var decodeSummary = result.ConvertedSegments > 0
                        ? $"；已解码 {result.ConvertedSegments} 段疑似中文"
                        : "";
                    Log($"反编译完成（会话临时区）：{item.FullPath}{decodeSummary}；{stored.CharacterCount:N0} 字符");
                }
                else
                {
                    ReplaceItem(item, item with { Status = "失败", Error = result.Error });
                    Log($"失败：{item.FullPath}\r\n  {result.Error}");
                }
                _progress.Value = index + 1;
                RefreshGrid();
            }
            _statusLabel.Text = $"反编译完成：会话结果 {success}，失败 {targets.Count - success}；点击保存后才写入目标目录";
        }
        catch (OperationCanceledException)
        {
            _statusLabel.Text = "已取消";
            Log("任务已取消。 ");
        }
        finally
        {
            var closeWhenReady = _closeAfterCancellation;
            SetRunning(false);
            _cts.Dispose();
            _cts = null;
            RefreshGrid();
            if (closeWhenReady)
                BeginInvoke(Close);
            else
                PreviewSelected();
        }
    }

    private string GetOutputPath(LuaFileInfo item)
    {
        var relative = item.RelativePath;
        var extension = Path.GetExtension(relative);
        relative = string.Equals(extension, ".lua", StringComparison.OrdinalIgnoreCase)
            ? Path.ChangeExtension(relative, null) + ".decompiled.lua"
            : Path.ChangeExtension(relative, ".lua");
        return Path.GetFullPath(Path.Combine(_outputBox.Text, relative));
    }

    private void SaveResults(IEnumerable<LuaFileInfo> requestedItems)
    {
        var targets = requestedItems
            .Where(item => _sessionResults.Contains(item.FullPath))
            .DistinctBy(item => item.FullPath, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (targets.Count == 0)
        {
            MessageBox.Show(this, "没有待保存的反编译结果。", "Lua 反编译工具",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (string.IsNullOrWhiteSpace(_outputBox.Text))
        {
            MessageBox.Show(this, "请先选择保存目录。", "Lua 反编译工具",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var saved = 0;
        foreach (var item in targets)
        {
            try
            {
                var outputPath = GetOutputPath(item);
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
                _sessionResults.CopyTo(item.FullPath, outputPath);
                ReplaceItem(item, item with { Status = "已保存", OutputPath = outputPath, Error = null });
                saved++;
                Log($"已保存：{outputPath}");
            }
            catch (Exception ex)
            {
                ReplaceItem(item, item with { Status = "保存失败", Error = ex.Message });
                Log($"保存失败：{item.FullPath}\r\n  {ex.Message}");
            }
        }

        _statusLabel.Text = $"保存完成：成功 {saved}，失败 {targets.Count - saved}";
        RefreshGrid();
        PreviewSelected();
    }

    private void ReplaceItem(LuaFileInfo oldItem, LuaFileInfo newItem)
    {
        var index = _items.FindIndex(x => string.Equals(x.FullPath, oldItem.FullPath, StringComparison.OrdinalIgnoreCase));
        if (index >= 0) _items[index] = newItem;
    }

    private void SetRunning(bool running)
    {
        _runSelectedButton.Enabled = !running;
        _runAllButton.Enabled = !running;
        _saveSelectedButton.Enabled = !running && _sessionResults.Count > 0;
        _saveAllButton.Enabled = !running && _sessionResults.Count > 0;
        _cancelButton.Enabled = running;
        _grid.Enabled = !running;
    }

    private async void PreviewSelected()
    {
        if (_grid.SelectedRows.Count == 0) return;
        var index = _grid.SelectedRows[0].Index;
        if (index < 0 || index >= _items.Count) return;
        var item = _items[index];

        try
        {
            if (_sessionResults.TryGet(item.FullPath, out var sessionResult))
                await _preview.ShowFileAsync(sessionResult.TempPath);
            else if (!string.IsNullOrWhiteSpace(item.OutputPath) && File.Exists(item.OutputPath))
                await _preview.ShowFileAsync(item.OutputPath);
            else if (item.Kind == LuaInputKind.Source)
                await _preview.ShowFileAsync(item.FullPath);
            else
                _preview.ShowText($"status:{item.FullPath}:{item.Status}:{item.Error}", item.Error is null
                    ? $"-- {item.FileName}\r\n-- {item.Version}\r\n-- 选择文件并点击“反编译选中”。"
                    : $"-- 反编译失败\r\n-- {item.Error}");
        }
        catch (Exception ex)
        {
            _preview.ShowText($"error:{item.FullPath}:{ex.Message}", $"-- 无法预览：{ex.Message}");
        }
    }

    private void OpenSelectedOutput()
    {
        if (_grid.SelectedRows.Count == 0) return;
        var item = _items[_grid.SelectedRows[0].Index];
        if (!string.IsNullOrWhiteSpace(item.OutputPath) && File.Exists(item.OutputPath))
            Process.Start(new ProcessStartInfo(item.OutputPath) { UseShellExecute = true });
        else if (_sessionResults.Contains(item.FullPath))
            _statusLabel.Text = "完整结果保存在本次会话临时区；请点击“保存选中”或“保存全部”写入目标目录";
    }

    private void Log(string message)
    {
        _log.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\r\n");
        _log.SelectionStart = _log.TextLength;
        _log.ScrollToCaret();
    }

    private void UpdateEngineStatus()
    {
        if (!File.Exists(_service.JarPath))
            Log($"未找到 unluac 引擎：{_service.JarPath}");
        else
            Log($"反编译引擎：{_service.JarPath}");
    }

    private void ShowEngineSettings()
    {
        using var dialog = new Form
        {
            Text = "反编译引擎设置",
            Size = new Size(650, 210),
            MinimumSize = new Size(520, 210),
            StartPosition = FormStartPosition.CenterParent,
            BackColor = BackColor,
            ForeColor = ForeColor,
            Font = Font
        };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 3, RowCount = 3 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var java = new TextBox { Text = _service.JavaPath, Dock = DockStyle.Fill };
        var jar = new TextBox { Text = _service.JarPath, Dock = DockStyle.Fill };
        StyleTextBox(java);
        StyleTextBox(jar);
        var browse = MakeButton("浏览…");
        var ok = MakeButton("保存");
        layout.Controls.Add(new Label { Text = "Java：", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        layout.Controls.Add(java, 1, 0);
        layout.Controls.Add(new Label { Text = "unluac.jar：", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        layout.Controls.Add(jar, 1, 1);
        layout.Controls.Add(browse, 2, 1);
        layout.Controls.Add(ok, 2, 2);
        browse.Click += (_, _) =>
        {
            using var picker = new OpenFileDialog { Filter = "Java JAR|*.jar|所有文件|*.*" };
            if (picker.ShowDialog(dialog) == DialogResult.OK) jar.Text = picker.FileName;
        };
        ok.Click += (_, _) =>
        {
            _service.JavaPath = java.Text.Trim();
            _service.JarPath = jar.Text.Trim();
            dialog.DialogResult = DialogResult.OK;
        };
        dialog.Controls.Add(layout);
        if (dialog.ShowDialog(this) == DialogResult.OK) UpdateEngineStatus();
    }

    private static string FormatSize(long value) => value switch
    {
        >= 1024 * 1024 => $"{value / 1024d / 1024d:F1} MB",
        >= 1024 => $"{value / 1024d:F1} KB",
        _ => $"{value} B"
    };
}
