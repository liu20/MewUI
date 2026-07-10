using Aprillz.MewUI.Rendering;

namespace Aprillz.MewUI.Controls;

// NOTE: 照抄 GridView.HeaderRow(GridView.cs:1185-1382),改为独立类。
// 因不再嵌套于 AdvancedGridView 内,原对外层私有字段的直接访问改为经 AdvancedGridView 的 internal 桥接成员。

/// <summary>
/// <see cref="AdvancedGridView"/> 的表头行。Phase 1:单行表头 + 拖拽分隔条调宽,行为与 <c>GridView.HeaderRow</c> 一致。
/// Phase 2 将扩展为多行合并表头。
/// </summary>
internal sealed class AdvancedGridHeaderRow : Panel
{
    private const double SeparatorHitWidth = 6;

    private readonly AdvancedGridView _owner;
    // 列名行单元格(每列一个)。单行表头时占满全高;两行表头时占下半行。
    private readonly List<TextBlock> _cells = new();
    // Phase 2:分组行单元格(每个合并表头分组一个),占上半行。仅 HasGroups 时非空。
    private readonly List<TextBlock> _groupCells = new();
    private List<HeaderGroup> _groups = new();
    // 每列表头水平对齐("left"/"center"/"right"),由调用方按 columnList.headerAlign 传入。
    // 索引与列序对齐;越界/空 → left。仅作用于列名 cell(_cells);分组标题 cell 保持居中。
    private List<string> _headerAligns = new();
    private double _horizontalOffset;
    // Phase 2 固定列:左/右固定列数与总宽。0 = 无该侧固定。中间列跟水平偏移;固定列钉视口左/右。
    private int _frozenLeft;
    private int _frozenRight;
    private double _frozenLeftWidth;
    private double _frozenRightWidth;

    // 列宽拖拽状态
    private int _resizeColumnIndex = -1;
    private double _resizeDragStartX;
    private double _resizeDragStartWidth;

    public AdvancedGridHeaderRow(AdvancedGridView owner)
    {
        _owner = owner;
        IsHitTestVisible = true;
    }

    public double HorizontalOffset
    {
        get => _horizontalOffset;
        set
        {
            if (SetDouble(ref _horizontalOffset, value))
            {
                InvalidateArrange();
                InvalidateVisual();
            }
        }
    }

    private bool HasGroups => _groups.Count > 0;

    /// <summary>设置固定列信息(列数 + 总宽)。中间列跟水平偏移;左/右固定列钉视口左/右不随偏移。</summary>
    public void SetFrozen(int left, int right, double leftWidth, double rightWidth)
    {
        _frozenLeft = left;
        _frozenRight = right;
        _frozenLeftWidth = leftWidth;
        _frozenRightWidth = rightWidth;
        InvalidateArrange();
        InvalidateVisual();
    }

    private bool HasFrozen => _frozenLeft > 0 || _frozenRight > 0;

    // 预计算的每列左沿屏幕 x(三段:左固定钉 bounds.X、中间跟偏移、右固定钉 bounds.Right-rightW)。
    // 由 ComputeColumnXs 一次 O(n) 填充,供 ArrangeContent/OnRender/HitTestSeparator O(1) 查询,
    // 取代逐列 O(colIndex) 的 ColumnX(在逐列循环里调用会退化为 O(n²),即使无固定列也回退)。
    private double[] _colX = Array.Empty<double>();

    /// <summary>
    /// 一次 O(n) 计算所有列在 bounds 内的左沿 x,填入 <see cref="_colX"/>。三段:
    /// 左固定列(0.._frozenLeft):钉 bounds.X;中间列(_frozenLeft..rightStart):bounds.X+leftW-offset;
    /// 右固定列(rightStart..colCount):钉 bounds.Right-rightW。无固定列时全部 bounds.X-offset(行为如旧)。
    /// </summary>
    private void ComputeColumnXs(Rect bounds, IReadOnlyList<GridView.GridViewCore.ColumnDefinition> columns)
    {
        int count = columns.Count;
        if (_colX.Length < count)
        {
            _colX = new double[count];
        }

        if (!HasFrozen)
        {
            double x = bounds.X - HorizontalOffset;
            for (int i = 0; i < count; i++)
            {
                _colX[i] = x;
                x += Math.Max(0, columns[i].Width);
            }
            return;
        }

        int rightStart = count - _frozenRight;
        int i2 = 0;
        // 左固定段:钉 bounds.X。
        double lx = bounds.X;
        for (; i2 < _frozenLeft && i2 < count; i2++)
        {
            _colX[i2] = lx;
            lx += Math.Max(0, columns[i2].Width);
        }
        // 中间段:bounds.X + leftW - offset。
        double mx = bounds.X + _frozenLeftWidth - HorizontalOffset;
        for (; i2 < rightStart && i2 < count; i2++)
        {
            _colX[i2] = mx;
            mx += Math.Max(0, columns[i2].Width);
        }
        // 右固定段:bounds.Right - rightW。
        double rx = bounds.Right - _frozenRightWidth;
        for (; i2 < count; i2++)
        {
            _colX[i2] = rx;
            rx += Math.Max(0, columns[i2].Width);
        }
    }

    /// <summary>设置复杂表头分组(合并表头)。空列表 = 单行表头。</summary>
    public void SetHeaderGroups(IReadOnlyList<HeaderGroup> groups)
    {
        _groups = groups is null ? new() : new List<HeaderGroup>(groups);

        // 同步分组单元格数量。
        while (_groupCells.Count < _groups.Count)
        {
            var text = new TextBlock();
            _groupCells.Add(text);
            Add(text);
        }

        while (_groupCells.Count > _groups.Count)
        {
            RemoveAt(_cells.Count + _groupCells.Count); // 分组 cell 在列名 cell 之后加入,故在末尾
            _groupCells.RemoveAt(_groupCells.Count - 1);
        }

        for (int i = 0; i < _groups.Count; i++)
        {
            _groupCells[i].Text = _groups[i].Label;
            _groupCells[i].FontWeight = FontWeight.Bold;
            // 分组标题居中显示(arrange 给的宽度为跨列总宽,Center 让文本在该范围内水平居中)。
            _groupCells[i].HorizontalAlignment = HorizontalAlignment.Center;
            _groupCells[i].Margin = new Thickness(6, 0, 6, 0);
        }

        InvalidateMeasure();
        InvalidateVisual();
    }

    /// <summary>
    /// 设置每列表头水平对齐(对应 <c>columnList.headerAlign</c>)。索引与列序对齐,取值
    /// <c>"left"</c>/<c>"center"</c>/<c>"right"</c>;越界/空/<c>null</c> 降级 <c>left</c>。
    /// 仅作用于列名 cell(<see cref="_cells"/>);合并表头的分组标题 cell 保持居中不变。
    /// 列重建(<see cref="SetColumns"/>)后自动重应用,使 cell 回收/新建后对齐跟进。
    /// </summary>
    public void SetColumnAlignments(IReadOnlyList<string>? aligns)
    {
        _headerAligns = aligns is null ? new() : new List<string>(aligns);
        ApplyHeaderAlignments();
        InvalidateVisual();
    }

    /// <summary>把 <see cref="_headerAligns"/> 应用到列名 cell(逐列设 HorizontalAlignment)。</summary>
    private void ApplyHeaderAlignments()
    {
        for (int i = 0; i < _cells.Count; i++)
        {
            var a = i < _headerAligns.Count ? _headerAligns[i] : null;
            _cells[i].HorizontalAlignment = a switch
            {
                "center" => HorizontalAlignment.Center,
                "right" => HorizontalAlignment.Right,
                _ => HorizontalAlignment.Left,
            };
        }
    }

    public void SetColumns(IReadOnlyList<GridView.GridViewCore.ColumnDefinition> columns)
    {
        while (_cells.Count < columns.Count)
        {
            var text = new TextBlock();
            _cells.Add(text);
            Add(text);
        }

        while (_cells.Count > columns.Count)
        {
            // 列名 cell 在子列表前段(先于分组 cell 加入),移除时需保序:移除对应索引。
            RemoveAt(_cells.Count - 1);
            _cells.RemoveAt(_cells.Count - 1);
        }

        for (int i = 0; i < columns.Count; i++)
        {
            _cells[i].Text = columns[i].Header;
            _cells[i].Margin = new Thickness(6, 0, 6, 0);
        }

        // 列重建后重应用表头对齐(新建/回收的 cell 默认左对齐,需按 _headerAligns 跟进)。
        ApplyHeaderAlignments();
    }

    protected override Size MeasureContent(Size availableSize)
    {
        var columns = _owner.CoreColumns;
        double rowH = HasGroups ? availableSize.Height / 2 : availableSize.Height;
        double fullH = HasGroups ? availableSize.Height : availableSize.Height;

        // 列名行:孤立列跨两行按全高约束,分组列按单行高约束。
        for (int i = 0; i < _cells.Count; i++)
        {
            double colWidth = i < columns.Count
                ? Math.Max(0, columns[i].Width)
                : double.PositiveInfinity;
            double h = HasGroups && !IsColumnGrouped(i) ? fullH : rowH;
            _cells[i].Measure(new Size(colWidth, h));
        }

        // 分组行:每组按跨列总宽约束。
        if (HasGroups)
        {
            for (int i = 0; i < _groups.Count; i++)
            {
                double spanWidth = ComputeGroupWidth(_groups[i], columns);
                _groupCells[i].Measure(new Size(Math.Max(0, spanWidth), rowH));
            }
        }

        return new Size(availableSize.Width, availableSize.Height);
    }

    /// <summary>计算某分组跨列的总宽(StartColumn 起 ColumnSpan 列宽度之和)。</summary>
    private static double ComputeGroupWidth(HeaderGroup g, IReadOnlyList<GridView.GridViewCore.ColumnDefinition> columns)
    {
        double w = 0;
        int end = Math.Min(columns.Count, g.StartColumn + g.ColumnSpan);
        for (int i = g.StartColumn; i < end; i++)
        {
            w += Math.Max(0, columns[i].Width);
        }

        return w;
    }

    protected override void ArrangeContent(Rect bounds)
    {
        var columns = _owner.CoreColumns;
        double rowH = HasGroups ? bounds.Height / 2 : bounds.Height;
        double groupY = bounds.Y;
        double nameY = HasGroups ? bounds.Y + rowH : bounds.Y;

        // 一次 O(n) 预计算每列左沿 x(三段),供 arrange 查询,避免逐列 O(colIndex) 累加退化为 O(n²)。
        ComputeColumnXs(bounds, columns);

        // 列名行。孤立列(不在任何分组覆盖范围内)的列名跨两行占满全高;分组列的列名占下半行。
        // Phase 2 固定列:左/右固定钉视口,中间列跟偏移。中间列 cell 文本溢出固定列区的部分由
        // RenderSubtree 的中间区 clip 裁掉(此处 arrange 用原 x,不改变文本位置)。
        for (int i = 0; i < _cells.Count; i++)
        {
            double w = Math.Max(0, columns[i].Width);
            double x = _colX[i];
            if (HasGroups && !IsColumnGrouped(i))
            {
                // 孤立列:列名跨两行(上半行 + 下半行),上半行不属任何分组。
                _cells[i].Arrange(new Rect(x, groupY, w, bounds.Height));
            }
            else
            {
                _cells[i].Arrange(new Rect(x, nameY, w, rowH));
            }
        }

        // 分组行(跨列合并)。
        if (HasGroups)
        {
            // Phase 2:分组起始列 x 用预计算值。约束(JNPF):complexHeader 不跨固定列边界,
            // 故分组的列全在同一段(左/中/右)内,_colX 给的连续 x 正确。
            for (int i = 0; i < columns.Count; i++)
            {
                for (int gi = 0; gi < _groups.Count; gi++)
                {
                    if (_groups[gi].StartColumn == i)
                    {
                        double spanW = ComputeGroupWidth(_groups[gi], columns);
                        double gx = _colX[i];
                        _groupCells[gi].Arrange(new Rect(gx, groupY, spanW, rowH));
                        break;
                    }
                }
            }
        }
    }

    /// <summary>列 i 是否落在某分组的覆盖范围 [StartColumn, StartColumn+ColumnSpan) 内。</summary>
    private bool IsColumnGrouped(int i)
    {
        foreach (var g in _groups)
        {
            if (i >= g.StartColumn && i < g.StartColumn + g.ColumnSpan)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>列 i 的分组标识(分组在 _groups 中的索引);孤立列返回 -1。用于判断相邻列是否同组。</summary>
    private int GroupIdOf(int i)
    {
        for (int gi = 0; gi < _groups.Count; gi++)
        {
            var g = _groups[gi];
            if (i >= g.StartColumn && i < g.StartColumn + g.ColumnSpan)
            {
                return gi;
            }
        }
        return -1;
    }

    protected override void RenderSubtree(IGraphicsContext context)
    {
        if (!HasFrozen)
        {
            base.RenderSubtree(context);
            return;
        }

        // Phase 2 固定列:中间列 cell clip 到中间区 [leftW, rightStart] 渲染,避免文本溢出到固定列区
        // (TextBlock 不裁剪到 arrange 矩形,滚动时中间表头文本会画到固定列表头之上)。固定列 cell 无 clip。
        // Children 顺序 = _cells 顺序 = 列序,故按索引判断中间/固定。
        var columns = _owner.CoreColumns;
        var dpiScale = GetDpi() / 96.0;
        var bounds = GetSnappedBorderBounds(Bounds);
        double midLeft = bounds.X + _frozenLeftWidth;
        double midRight = bounds.Right - _frozenRightWidth;
        int rightStart = columns.Count - _frozenRight;
        var midClip = LayoutRounding.MakeClipRect(new Rect(midLeft, bounds.Y, Math.Max(0, midRight - midLeft), bounds.Height), dpiScale);

        context.Save();
        context.SetClip(midClip);
        try
        {
            // 中间列 cell(frozenLeft <= i < rightStart)在 clip 下渲染。
            for (int i = _frozenLeft; i < rightStart && i < _cells.Count; i++)
            {
                _cells[i].Render(context);
            }

            // 中间段分组标题 cell(分组起始列落在中间段 [frozenLeft, rightStart) 内)同样在 clip 下渲染,
            // 避免分组标题文本溢出到固定列区。分组不跨固定列边界(complexHeader 约束),故按起始列判定段即可。
            for (int gi = 0; gi < _groups.Count; gi++)
            {
                int start = _groups[gi].StartColumn;
                if (start >= _frozenLeft && start < rightStart)
                {
                    _groupCells[gi].Render(context);
                }
            }
        }
        finally
        {
            context.Restore();
        }

        // 固定列 cell(左段 + 右段)无 clip 渲染(在上层)。
        for (int i = 0; i < _frozenLeft && i < _cells.Count; i++)
        {
            _cells[i].Render(context);
        }
        for (int i = rightStart; i < _cells.Count; i++)
        {
            _cells[i].Render(context);
        }

        // 固定段分组标题 cell(分组起始列落在左段 < frozenLeft 或右段 >= rightStart)无 clip 渲染。
        for (int gi = 0; gi < _groups.Count; gi++)
        {
            int start = _groups[gi].StartColumn;
            if (start < _frozenLeft || start >= rightStart)
            {
                _groupCells[gi].Render(context);
            }
        }
    }

    protected override void OnRender(IGraphicsContext context)
    {
        var theme = Theme;
        var bounds = GetSnappedBorderBounds(Bounds);
        var bg = theme.Palette.ButtonFace;

        context.FillRectangle(bounds, bg);

        var stroke = theme.Palette.ControlBorder;
        var dpiScale = GetDpi() / 96.0;
        var thickness = LayoutRounding.SnapThicknessToPixels(1.0 / dpiScale, dpiScale, 1);

        // 底部分隔线(表头与行区之间)。
        var bottomRect = LayoutRounding.SnapBoundsRectToPixels(
            new Rect(bounds.X, bounds.Bottom - thickness, Math.Max(0, bounds.Width), thickness),
            dpiScale);
        context.FillRectangle(bottomRect, stroke);

        var columns = _owner.CoreColumns;
        double rowH = HasGroups ? bounds.Height / 2 : bounds.Height;
        // 一次 O(n) 预计算每列左沿 x(三段),供本帧竖线/分段绘制 O(1) 查询,避免逐列 O(colIndex) 退化。
        ComputeColumnXs(bounds, columns);

        if (HasGroups)
        {
            // 分组行与列名行之间的分隔线:仅画在分组列上方。孤立列列名跨两行,其上方不画此线
            // (否则会切断列名)。逐列画水平线段,跳过孤立列。
            double midY = bounds.Y + rowH - thickness;
            for (int i = 0; i < columns.Count; i++)
            {
                if (IsColumnGrouped(i))
                {
                    double w = Math.Max(0, columns[i].Width);
                    double mx = _colX[i];
                    var seg = LayoutRounding.SnapBoundsRectToPixels(
                        new Rect(mx, midY, w, thickness), dpiScale);
                    context.FillRectangle(seg, stroke);
                }
            }

            // 分组行竖线:相邻两列分组归属不同才画(同组内不画)。每列右边界 = _colX[i]+width。
            // 中间列竖线裁剪到中间区,避免滚出时画到固定列区。
            double grpTop = bounds.Y;
            double grpBottom = bounds.Y + rowH;
            int gRightStart = columns.Count - _frozenRight;
            for (int i = 0; i < columns.Count; i++)
            {
                double curR = _colX[i] + Math.Max(0, columns[i].Width);
                if (i + 1 < columns.Count && GroupIdOf(i) != GroupIdOf(i + 1))
                {
                    bool isMiddle = HasFrozen && i >= _frozenLeft && i < gRightStart;
                    if (isMiddle)
                    {
                        double midLeft = bounds.X + _frozenLeftWidth;
                        double midRight = bounds.Right - _frozenRightWidth;
                        if (curR < midLeft - 0.5 || curR > midRight + 0.5)
                        {
                            continue;
                        }
                    }
                    context.DrawLine(new Point(curR, grpTop), new Point(curR, grpBottom), stroke, 1, pixelSnap: true);
                }
            }

            // 列名行竖线:每列右边界画,贯穿下半行。中间列竖线裁剪到中间区。
            double nameTop = bounds.Y + rowH;
            int nRightStart = columns.Count - _frozenRight;
            for (int i = 0; i < columns.Count; i++)
            {
                double curR = _colX[i] + Math.Max(0, columns[i].Width);
                bool isMiddle = HasFrozen && i >= _frozenLeft && i < nRightStart;
                if (isMiddle)
                {
                    double midLeft = bounds.X + _frozenLeftWidth;
                    double midRight = bounds.Right - _frozenRightWidth;
                    if (curR < midLeft - 0.5 || curR > midRight + 0.5)
                    {
                        continue;
                    }
                }
                context.DrawLine(new Point(curR, nameTop), new Point(curR, bounds.Bottom), stroke, 1, pixelSnap: true);
            }

            // Phase 2:固定列分段强调竖线(左固定右沿、右固定左沿),贯穿全高,区分固定列与中间列。
            if (HasFrozen)
            {
                if (_frozenLeft > 0)
                {
                    double sepX = bounds.X + _frozenLeftWidth;
                    context.DrawLine(new Point(sepX, bounds.Y), new Point(sepX, bounds.Bottom), stroke, 1, pixelSnap: true);
                }
                if (_frozenRight > 0)
                {
                    double sepX = bounds.Right - _frozenRightWidth;
                    context.DrawLine(new Point(sepX, bounds.Y), new Point(sepX, bounds.Bottom), stroke, 1, pixelSnap: true);
                }
            }
        }
        else
        {
            // 单行表头:每列右边界画竖线。列右边界 = _colX[i]+width。
            // 固定列竖线在其固定区内画;中间列竖线裁剪到中间区 [bounds.X+leftW, bounds.Right-rightW],
            // 避免中间列滚出时竖线画到固定列区域(与固定列重叠)。
            double inset = Math.Min(6, Math.Max(0, (bounds.Height - 2) / 2));
            int rightStart = columns.Count - _frozenRight;
            for (int i = 0; i < columns.Count; i++)
            {
                double curR = _colX[i] + Math.Max(0, columns[i].Width);
                bool isMiddle = HasFrozen && i >= _frozenLeft && i < rightStart;
                if (isMiddle)
                {
                    double midLeft = bounds.X + _frozenLeftWidth;
                    double midRight = bounds.Right - _frozenRightWidth;
                    if (curR < midLeft - 0.5 || curR > midRight + 0.5)
                    {
                        continue;
                    }
                }
                context.DrawLine(new Point(curR, bounds.Y + inset), new Point(curR, bounds.Bottom - inset), stroke, 1, pixelSnap: true);
            }

            // Phase 2:固定列分段强调竖线。
            if (HasFrozen)
            {
                if (_frozenLeft > 0)
                {
                    double sepX = bounds.X + _frozenLeftWidth;
                    context.DrawLine(new Point(sepX, bounds.Y), new Point(sepX, bounds.Bottom), stroke, 1, pixelSnap: true);
                }
                if (_frozenRight > 0)
                {
                    double sepX = bounds.Right - _frozenRightWidth;
                    context.DrawLine(new Point(sepX, bounds.Y), new Point(sepX, bounds.Bottom), stroke, 1, pixelSnap: true);
                }
            }
        }
    }

    /// <summary>
    /// 返回右边缘分隔条接近给定 X 位置的列索引,无可调整分隔条命中时返回 -1。
    /// </summary>
    private int HitTestSeparator(double localX)
    {
        var columns = _owner.CoreColumns;
        var b = GetSnappedBorderBounds(Bounds);
        // 一次 O(n) 预计算每列右沿(三段),单遍扫描命中。避免逐列 O(colIndex) 累加退化(鼠标移动频繁触发)。
        ComputeColumnXs(b, columns);
        for (int i = 0; i < columns.Count; i++)
        {
            double colRight = _colX[i] + Math.Max(0, columns[i].Width);
            double local = colRight - b.X;
            if (Math.Abs(localX - local) <= SeparatorHitWidth / 2 && columns[i].IsResizable)
            {
                return i;
            }
        }
        return -1;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Handled || e.Button != MouseButton.Left) return;

        var pos = e.GetPosition(this);
        int col = HitTestSeparator(pos.X);
        if (col < 0) return;

        _resizeColumnIndex = col;
        _resizeDragStartX = pos.X;
        _resizeDragStartWidth = _owner.CoreColumns[col].Width;
        Cursor = CursorType.SizeWE;

        if (_owner.FindVisualRoot() is Window window)
            window.CaptureMouse(this);

        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        var pos = e.GetPosition(this);

        if (_resizeColumnIndex >= 0)
        {
            double delta = pos.X - _resizeDragStartX;
            double newWidth = _resizeDragStartWidth + delta;

            _owner.SetColumnWidthCore(_resizeColumnIndex, newWidth);
            _owner.InvalidateGridItemBindingsCore();
            // 变高行会按内容重算高度;列宽变化可能改变换行断点-行高变化。通知 presenter 丢弃
            // 缓存高度以重测前缀和。(定高 presenter 无影响,保留对齐 GridView 写法。)
            if (_owner.TryGetVariableHeightPresenter(out var variableHeightPresenter))
            {
                variableHeightPresenter.InvalidateHeights();
            }
            // Phase 2:列宽变化后固定列 overlay/表头/页脚的宽度缓存需同步(SetColumnWidth 不触发 ColumnsChanged)。
            _owner.OnColumnWidthChangedCore();
            _owner.InvalidateMeasure();
            _owner.InvalidateVisual();
            InvalidateArrange();
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        // 按分隔条悬停更新光标
        Cursor = HitTestSeparator(pos.X) >= 0
            ? CursorType.SizeWE
            : null;
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        if (_resizeColumnIndex < 0) return;

        _resizeColumnIndex = -1;
        Cursor = null;

        if (_owner.FindVisualRoot() is Window window)
            window.ReleaseMouseCapture();

        e.Handled = true;
    }
}
