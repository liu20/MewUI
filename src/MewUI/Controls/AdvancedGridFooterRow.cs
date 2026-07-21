using Aprillz.MewUI.Rendering;

namespace Aprillz.MewUI.Controls;

// NOTE: Phase 2 页脚行。独立渲染层,不进 ItemsSource(告别"合计行伪装成数据行"的 hack)。
// 经 AdvancedGridView.VisitScrollChildren 加入子树(在 _scrollViewer 之后,确保画在行区之上底部)。
// 固定在底部,不参与垂直滚动;横向偏移跟 _scrollViewer.ScrollChanged(与表头同)。
// 职责单一:按列宽 + 给定文本数组渲染页脚单元格。文本由 AdvancedGridView.UpdateFooter() 算好推入。

/// <summary>
/// <see cref="AdvancedGridView"/> 的页脚行(如合计行)。独立于 <see cref="AdvancedGridView.ItemsSource"/>,
/// 不污染数据源。按列宽对齐渲染每列页脚文本;横向偏移跟随水平滚动(与表头同步),固定在底部不随垂直滚动。
/// </summary>
internal sealed class AdvancedGridFooterRow : Panel
{
    private readonly AdvancedGridView _owner;
    private readonly List<TextBlock> _cells = new();
    private double _horizontalOffset;
    private string[] _texts = Array.Empty<string>();
    // Phase 2 固定列:左/右固定列数与总宽(与表头一致的三段逻辑)。
    private int _frozenLeft;
    private int _frozenRight;
    private double _frozenLeftWidth;
    private double _frozenRightWidth;

    public AdvancedGridFooterRow(AdvancedGridView owner)
    {
        _owner = owner;
        IsHitTestVisible = false; // 页脚不响应点击(合计行不可选/不可编辑)
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

    /// <summary>列数变化时同步单元格数(与表头 SetColumns 对齐)。</summary>
    public void SetColumnCount(int count)
    {
        while (_cells.Count < count)
        {
            var text = new TextBlock();
            _cells.Add(text);
            Add(text);
        }

        while (_cells.Count > count)
        {
            RemoveAt(_cells.Count - 1);
            _cells.RemoveAt(_cells.Count - 1);
        }

        if (_texts.Length < count)
        {
            Array.Resize(ref _texts, count);
        }
    }

    /// <summary>设置每列页脚文本(由 AdvancedGridView.UpdateFooter 算好后推入)。长度须 >= 列数。</summary>
    public void SetFooterValues(IReadOnlyList<string> values)
    {
        int count = _cells.Count;
        for (int i = 0; i < count; i++)
        {
            _texts[i] = i < values.Count ? values[i] : string.Empty;
            // 页脚文本加粗(合计行视觉强调,对齐原 __summary__ hack 的加粗表现)。设一次即可。
            _cells[i].FontWeight = FontWeight.Bold;
            _cells[i].Text = _texts[i];
        }

        InvalidateVisual();
    }

    /// <summary>设置固定列信息(与表头同),驱动三段 arrange/render。</summary>
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

    /// <summary>计算列 colIndex 在 bounds 内的 x 起点(与表头 ComputeColumnXs 同构,三段)。</summary>
    private double[] _colX = Array.Empty<double>();

    /// <summary>一次 O(n) 计算所有列在 bounds 内的左沿 x(三段),填入 _colX 供 arrange/render O(1) 查询。</summary>
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
                x += Math.Max(0, columns[i].ActualWidth);
            }
            return;
        }

        int rightStart = count - _frozenRight;
        int i2 = 0;
        double lx = bounds.X;
        for (; i2 < _frozenLeft && i2 < count; i2++)
        {
            _colX[i2] = lx;
            lx += Math.Max(0, columns[i2].ActualWidth);
        }
        double mx = bounds.X + _frozenLeftWidth - HorizontalOffset;
        for (; i2 < rightStart && i2 < count; i2++)
        {
            _colX[i2] = mx;
            mx += Math.Max(0, columns[i2].ActualWidth);
        }
        double rx = bounds.Right - _frozenRightWidth;
        for (; i2 < count; i2++)
        {
            _colX[i2] = rx;
            rx += Math.Max(0, columns[i2].ActualWidth);
        }
    }

    protected override Size MeasureContent(Size availableSize)
    {
        var columns = _owner.CoreColumns;
        for (int i = 0; i < _cells.Count; i++)
        {
            double colWidth = i < columns.Count
                ? Math.Max(0, columns[i].ActualWidth)
                : double.PositiveInfinity;
            _cells[i].Measure(new Size(colWidth, availableSize.Height));
        }

        return new Size(availableSize.Width, availableSize.Height);
    }

    protected override void ArrangeContent(Rect bounds)
    {
        var columns = _owner.CoreColumns;
        ComputeColumnXs(bounds, columns);
        for (int i = 0; i < _cells.Count; i++)
        {
            double w = Math.Max(0, columns[i].ActualWidth);
            double x = _colX[i];
            // 页脚文本右对齐(数值列合计常右对齐);首列等左对齐由调用方经 FooterText 内容体现。
            // 这里统一左对齐 + Margin,与表头一致,避免过度假设。
            _cells[i].Margin = new Thickness(6, 0, 6, 0);
            _cells[i].Arrange(new Rect(x, bounds.Y, w, bounds.Height));
        }
    }

    protected override void RenderSubtree(IGraphicsContext context)
    {
        if (!HasFrozen)
        {
            base.RenderSubtree(context);
            return;
        }

        // Phase 2 固定列:中间列 cell clip 到中间区,固定列 cell 无 clip(与表头同,按索引判断)。
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
            for (int i = _frozenLeft; i < rightStart && i < _cells.Count; i++)
            {
                _cells[i].Render(context);
            }
        }
        finally
        {
            context.Restore();
        }

        for (int i = 0; i < _frozenLeft && i < _cells.Count; i++)
        {
            _cells[i].Render(context);
        }
        for (int i = rightStart; i < _cells.Count; i++)
        {
            _cells[i].Render(context);
        }
    }

    protected override void OnRender(IGraphicsContext context)
    {
        var theme = Theme;
        var bounds = GetSnappedBorderBounds(Bounds);
        var bg = theme.Palette.ButtonFace;

        context.FillRectangle(bounds, bg);

        // 顶部分隔线(与行区分隔)
        var dpiScale = GetDpi() / 96.0;
        var thickness = LayoutRounding.SnapThicknessToPixels(1.0 / dpiScale, dpiScale, 1);
        var topRect = LayoutRounding.SnapBoundsRectToPixels(
            new Rect(bounds.X, bounds.Y, Math.Max(0, bounds.Width), thickness),
            dpiScale);
        context.FillRectangle(topRect, theme.Palette.ControlBorder);

        // 页脚文本加粗已在 SetFooterValues 设(不每帧重设)。
        var columns = _owner.CoreColumns;
        ComputeColumnXs(bounds, columns);
        double inset = Math.Min(6, Math.Max(0, (bounds.Height - 2) / 2));
        int rightStart = columns.Count - _frozenRight;
        for (int i = 0; i < _cells.Count; i++)
        {
            double x = _colX[i] + Math.Max(0, columns[i].ActualWidth);
            // 中间列竖线裁剪到中间区,避免滚出时画到固定列区。
            bool isMiddle = HasFrozen && i >= _frozenLeft && i < rightStart;
            if (isMiddle)
            {
                double midLeft = bounds.X + _frozenLeftWidth;
                double midRight = bounds.Right - _frozenRightWidth;
                if (x < midLeft - 0.5 || x > midRight + 0.5)
                {
                    continue;
                }
            }
            context.DrawLine(new Point(x, bounds.Y + inset), new Point(x, bounds.Bottom - inset), theme.Palette.ControlBorder, 1, pixelSnap: true);
        }

        // Phase 2:固定列分段强调竖线。
        if (HasFrozen)
        {
            if (_frozenLeft > 0)
            {
                double sepX = bounds.X + _frozenLeftWidth;
                context.DrawLine(new Point(sepX, bounds.Y), new Point(sepX, bounds.Bottom), theme.Palette.ControlBorder, 1, pixelSnap: true);
            }
            if (_frozenRight > 0)
            {
                double sepX = bounds.Right - _frozenRightWidth;
                context.DrawLine(new Point(sepX, bounds.Y), new Point(sepX, bounds.Bottom), theme.Palette.ControlBorder, 1, pixelSnap: true);
            }
        }
    }
}
