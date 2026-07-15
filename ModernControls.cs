using System.Drawing.Drawing2D;

namespace LuaDecompilerDesktop;

internal sealed class RoundedButton : Button
{
    private bool _hovered;

    public bool Primary { get; set; }
    public int CornerRadius { get; set; } = 8;

    public RoundedButton()
    {
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
        Cursor = Cursors.Hand;
        AutoSize = true;
        Height = 34;
        ForeColor = Color.White;
        Padding = new Padding(12, 3, 12, 3);
    }

    protected override void OnMouseEnter(EventArgs e) { _hovered = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hovered = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = RoundedRect(bounds, CornerRadius);
        var color = !Enabled
            ? Color.FromArgb(49, 53, 61)
            : Primary
                ? (_hovered ? Color.FromArgb(75, 145, 244) : Color.FromArgb(58, 126, 232))
                : (_hovered ? Color.FromArgb(66, 72, 83) : Color.FromArgb(52, 57, 66));
        using var brush = new SolidBrush(color);
        e.Graphics.FillPath(brush, path);
        TextRenderer.DrawText(e.Graphics, Text, Font, bounds, Enabled ? ForeColor : Color.FromArgb(125, 132, 145),
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        var diameter = Math.Max(2, radius * 2);
        var path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal sealed class ModernTabControl : TabControl
{
    public ModernTabControl()
    {
        DrawMode = TabDrawMode.OwnerDrawFixed;
        SizeMode = TabSizeMode.Fixed;
        ItemSize = new Size(112, 38);
        Padding = new Point(18, 6);
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        var selected = e.Index == SelectedIndex;
        var bounds = GetTabRect(e.Index);
        using var background = new SolidBrush(selected ? Color.FromArgb(37, 42, 51) : Color.FromArgb(27, 30, 36));
        e.Graphics.FillRectangle(background, bounds);
        if (selected)
        {
            using var accent = new SolidBrush(Color.FromArgb(79, 146, 244));
            e.Graphics.FillRectangle(accent, bounds.Left + 12, bounds.Bottom - 3, bounds.Width - 24, 3);
        }
        TextRenderer.DrawText(e.Graphics, TabPages[e.Index].Text, Font, bounds,
            selected ? Color.White : Color.FromArgb(151, 159, 173),
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }
}
