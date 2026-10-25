using System.Drawing.Drawing2D;

namespace SnailCommander;

internal static class Theme
{
    public static readonly Color Background = Color.FromArgb(11, 16, 25);
    public static readonly Color Sidebar = Color.FromArgb(14, 21, 32);
    public static readonly Color Card = Color.FromArgb(22, 31, 45);
    public static readonly Color Raised = Color.FromArgb(31, 43, 59);
    public static readonly Color Border = Color.FromArgb(45, 60, 79);
    public static readonly Color Text = Color.FromArgb(239, 247, 255);
    public static readonly Color Secondary = Color.FromArgb(156, 174, 194);
    public static readonly Color Accent = Color.FromArgb(70, 220, 192);
    public static readonly Color AccentDark = Color.FromArgb(19, 99, 93);
    public static readonly Color Warning = Color.FromArgb(246, 184, 92);

    public static Label TextLabel(string text, float size = 10, bool bold = false, Color? color = null)
        => new()
        {
            Text = text,
            Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular),
            ForeColor = color ?? Text,
            BackColor = Color.Transparent,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            Dock = DockStyle.Fill,
            AutoEllipsis = true
        };

    public static TextBox Field(string? initial = null)
        => new()
        {
            Text = initial ?? "",
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Raised,
            ForeColor = Text,
            Font = new Font("Segoe UI", 10),
            Margin = new Padding(0, 3, 0, 3)
        };

    public static ComboBox DropDown()
        => new()
        {
            Dock = DockStyle.Fill,
            BackColor = Raised,
            ForeColor = Text,
            FlatStyle = FlatStyle.Flat,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 10),
            Margin = new Padding(0, 3, 0, 3)
        };

    public static TableLayoutPanel Table(params (SizeType Type, float Value)[] rows)
    {
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = rows.Length,
            ColumnCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (var row in rows)
            table.RowStyles.Add(new RowStyle(row.Type, row.Value));
        return table;
    }
}

internal sealed class SurfacePanel : Panel
{
    public int CornerRadius { get; set; } = 14;
    public SurfacePanel()
    {
        DoubleBuffered = true;
        BackColor = Theme.Card;
        Padding = new Padding(16);
        Margin = new Padding(5);
    }

    protected override void OnResize(EventArgs eventargs)
    {
        base.OnResize(eventargs);
        if (Width <= 2 || Height <= 2) return;
        using var path = NewPath(new Rectangle(0, 0, Width, Height), CornerRadius);
        var oldRegion = Region;
        Region = new Region(path);
        oldRegion?.Dispose();
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var border = new Pen(Theme.Border);
        using var path = NewPath(new Rectangle(0, 0, Width - 1, Height - 1), CornerRadius);
        e.Graphics.DrawPath(border, path);
    }

    private static GraphicsPath NewPath(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        int d = Math.Max(2, Math.Min(radius * 2, Math.Min(rect.Width, rect.Height)));
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal sealed class ModernButton : Button
{
    public Color IdleBackColor { get; set; }
    public Color HoverBackColor { get; set; }

    public ModernButton(string text, bool primary = false)
    {
        Text = text;
        IdleBackColor = primary ? Theme.Accent : Theme.Raised;
        HoverBackColor = primary ? Color.FromArgb(110, 239, 212) : Color.FromArgb(47, 63, 83);
        ForeColor = primary ? Theme.Background : Theme.Text;
        BackColor = IdleBackColor;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
        Cursor = Cursors.Hand;
        Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        Height = 39;
        Margin = new Padding(3);
        MouseEnter += (_, _) => { if (Enabled) BackColor = HoverBackColor; };
        MouseLeave += (_, _) => { BackColor = IdleBackColor; };
    }
}
