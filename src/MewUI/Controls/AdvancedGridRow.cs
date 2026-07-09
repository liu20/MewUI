using Aprillz.MewUI.Rendering;

namespace Aprillz.MewUI.Controls;

// NOTE: 照抄 GridView.Row(GridView.cs:1384-1612)及其内嵌 Cell(GridView.cs:1614-1695),
// 改为独立类(行) + 内嵌 Cell。经 AdvancedGridView 的 internal 桥接成员访问外层状态。

/// <summary>
/// <see cref="AdvancedGridView"/> 的数据行容器。Phase 1 行为与 <c>GridView.Row</c> 一致:
/// 按列布局单元格、绑定/回收、绘制选中/hover 底色与网格线。
/// </summary>
internal sealed class AdvancedGridRow : Panel
{
    private readonly AdvancedGridView _owner;
    private readonly List<Cell> _cells = new();
    private int _rowIndex;
    private uint _lastDpi;
    private int _lastColumnsVersion = -1;
    private int _lastColumnStart = -1;
    private int _lastColumnCount = -1;
    private Theme? _lastTheme;

    // Phase 2 固定列:此行渲染的列范围 [ColumnStart, ColumnStart+ColumnCount)。
    // 默认 0/0 表示"未设置"——首次 EnsureColumns 时取全部列(无固定列时行为如旧)。
    // 中间 presenter 的行 = 中间列范围;左/右 frozen overlay 的行 = 对应固定列范围。
    internal int ColumnStart { get; set; }
    internal int ColumnCount { get; set; }

    public AdvancedGridRow(AdvancedGridView owner)
    {
        _owner = owner;
        IsHitTestVisible = true;
    }

    // OnRender 直接读 IsMouseOver(无 style trigger),框架的 visual-state 路径不会为我们失效。
    // 显式调度一次渲染。
    protected override void OnMouseEnter() => InvalidateVisual();

    protected override void OnMouseLeave() => InvalidateVisual();

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (e.Handled || e.Button != MouseButton.Left)
        {
            return;
        }

        if (!_owner.IsEffectivelyEnabled)
        {
            return;
        }

        _owner.HandleRowPointerDown(_rowIndex, e);
    }

    public void EnsureDpi(uint dpi)
    {
        if (_lastDpi == dpi)
        {
            return;
        }

        var old = _lastDpi;
        _lastDpi = dpi;

        VisualTree.Visit(this, e =>
        {
            if (e is FrameworkElement fe)
            {
                fe.NotifyDpiChanged(old, dpi);
            }
        });

        InvalidateMeasure();
    }

    public void EnsureColumns(IReadOnlyList<GridView.GridViewCore.ColumnDefinition> columns, int columnsVersion)
    {
        // 解析本行渲染的列范围 [start, start+count)。
        // ColumnStart==0 && ColumnCount<=0 表示"未设置"(默认),取全部列(无固定列场景,行为如旧)。
        // 由 owner 显式设范围(中间行=中间列;overlay 行=固定列)。中间列范围经 SetFrozenColumns 保证 >=1 列。
        int start = ColumnStart;
        int count = (start == 0 && ColumnCount <= 0) ? columns.Count : Math.Max(0, ColumnCount);
        // clamp 到合法范围。
        if (start < 0) start = 0;
        if (start > columns.Count) start = columns.Count;
        if (start + count > columns.Count) count = columns.Count - start;

        if (_lastColumnsVersion == columnsVersion && _lastColumnStart == start && _lastColumnCount == count)
        {
            return;
        }

        _lastColumnsVersion = columnsVersion;
        _lastColumnStart = start;
        _lastColumnCount = count;

        while (_cells.Count < count)
        {
            var ctx = new TemplateContext();
            var cell = new Cell(this, ctx);
            _cells.Add(cell);
            Add(cell.View);
        }

        while (_cells.Count > count)
        {
            int idx = _cells.Count - 1;
            _cells[idx].Unbind();
            _cells[idx].Context.Dispose();
            RemoveAt(idx);
            _cells.RemoveAt(idx);
        }

        for (int i = 0; i < count; i++)
        {
            _cells[i].Template = columns[start + i].CellTemplate;
            _cells[i].EnsureViewBuilt(this);
        }

        InvalidateMeasure();
    }

    public void EnsureTheme(Theme theme)
    {
        if (ReferenceEquals(_lastTheme, theme))
        {
            return;
        }

        // 若此行在主题变更期间被回收,它不在窗口视觉树中会错过广播。
        // 复用时同步整个子树,避免模板用陈旧缓存 ThemeInternal 渲染。
        _lastTheme = theme;
        VisualTree.Visit(this, e =>
        {
            if (e is FrameworkElement fe && !ReferenceEquals(fe.ThemeInternal, theme))
            {
                fe.NotifyThemeChanged(fe.ThemeInternal, theme);
            }
        });
    }

    public void Bind(object? item, int index)
    {
        _rowIndex = index;
        for (int i = 0; i < _cells.Count; i++)
        {
            _cells[i].Bind(item, index);
        }

        InvalidateMeasure();
    }

    public void Recycle()
    {
        for (int i = 0; i < _cells.Count; i++)
        {
            _cells[i].Unbind();
        }

        InvalidateMeasure();
    }

    protected override Size MeasureContent(Size availableSize)
    {
        var pad = _owner.CellPadding;
        double padH = pad.HorizontalThickness;
        double padV = pad.VerticalThickness;
        var columns = _owner.CoreColumns;
        int start = _lastColumnStart;
        double maxCellH = 0;
        for (int i = 0; i < _cells.Count; i++)
        {
            double w = Math.Max(0, columns[start + i].Width - padH);
            double h = double.IsPositiveInfinity(availableSize.Height)
                ? double.PositiveInfinity
                : Math.Max(0, availableSize.Height - padV);
            _cells[i].View.Measure(new Size(w, h));
            if (_cells[i].View.DesiredSize.Height > maxCellH)
            {
                maxCellH = _cells[i].View.DesiredSize.Height;
            }
        }

        // 上报测得的最大单元格高 + padding。FixedHeightItemsPresenter 忽略此值用自有 ItemHeight;
        // VariableHeightItemsPresenter 用它作实际行高(prefix-sum 与视口布局)。
        double rowH = double.IsPositiveInfinity(availableSize.Height)
            ? maxCellH + padV
            : availableSize.Height;
        return new Size(availableSize.Width, rowH);
    }

    protected override void ArrangeContent(Rect bounds)
    {
        double x = bounds.X;
        var pad = _owner.CellPadding;
        var columns = _owner.CoreColumns;
        int start = _lastColumnStart;
        for (int i = 0; i < _cells.Count; i++)
        {
            double w = Math.Max(0, columns[start + i].Width);
            var cellRect = new Rect(
                x + pad.Left,
                bounds.Y + pad.Top,
                Math.Max(0, w - pad.HorizontalThickness),
                Math.Max(0, bounds.Height - pad.VerticalThickness));
            _cells[i].View.Arrange(cellRect);
            x += w;
        }
    }

    protected override void OnRender(IGraphicsContext context)
    {
        var theme = Theme;
        var snapped = GetSnappedBorderBounds(Bounds);
        var isSelected = _owner.IsItemSelectedCore(_rowIndex);

        var r = theme.Metrics.ControlCornerRadius - 2;
        if (isSelected)
        {
            if (r > 0)
            {
                context.FillRoundedRectangle(snapped, r, r, theme.Palette.SelectionBackground);
            }
            else
            {
                context.FillRectangle(snapped, theme.Palette.SelectionBackground);
            }
        }
        else if (IsMouseOver && _owner.IsEffectivelyEnabled)
        {
            var hoverBg = theme.Palette.ControlBackground.Lerp(theme.Palette.Accent, 0.15);

            if (r > 0)
            {
                context.FillRoundedRectangle(snapped, r, r, hoverBg);
            }
            else
            {
                context.FillRectangle(snapped, hoverBg);
            }
        }

        if (_owner.ShowGridLines)
        {
            var stroke = theme.Palette.ControlBorder;
            context.DrawLine(new Point(snapped.X, snapped.Bottom - 1), new Point(snapped.Right, snapped.Bottom - 1), stroke, 1, pixelSnap: true);

            double x = snapped.X;
            var columns = _owner.CoreColumns;
            int start = _lastColumnStart;
            // 画范围内列的右边界竖线(最后一列的右边界 = snapped.Right,跳过)。
            for (int i = 0; i < _cells.Count; i++)
            {
                x += Math.Max(0, columns[start + i].Width);
                if (x >= snapped.Right - 0.5)
                {
                    break;
                }

                context.DrawLine(new Point(x, snapped.Y), new Point(x, snapped.Bottom), stroke, 1, pixelSnap: true);
            }
        }
    }

    private sealed class Cell
    {
        private readonly AdvancedGridRow _row;
        private bool _built;

        public Cell(AdvancedGridRow row, TemplateContext context)
        {
            _row = row;
            Context = context;
            View = new TextBlock();
        }

        public TemplateContext Context { get; }

        public IDataTemplate? Template { get; set; }

        public FrameworkElement View { get; private set; }

        public void Bind(object? item, int index)
        {
            Context.BindTemplate(View, Template!, item, index);
        }

        public void Unbind()
        {
            Context.UnbindTemplate(View);
        }

        public void EnsureViewBuilt(AdvancedGridRow row)
        {
            if (_built || Template == null)
            {
                return;
            }

            var built = Template.Build(Context);
            built.Parent = row;

            int idx = -1;
            for (int i = 0; i < row.Children.Count; i++)
            {
                if (ReferenceEquals(row.Children[i], View))
                {
                    idx = i;
                    break;
                }
            }

            if (idx >= 0)
            {
                row.RemoveAt(idx);
                row.Insert(idx, built);
            }

            View = built;
            _built = true;

            // MouseDown 沿视觉树冒泡,故根 view 上一个 handler 即可捕获所有子元素的点击。
            View.MouseDown += OnCellMouseDown;
        }

        private void OnCellMouseDown(MouseEventArgs e)
        {
            if (e.Button != MouseButton.Left)
            {
                return;
            }

            if (e.Handled)
            {
                return;
            }

            if (!_row._owner.IsEffectivelyEnabled)
            {
                return;
            }

            _row._owner.HandleRowPointerDown(_row._rowIndex, e);
        }
    }
}
