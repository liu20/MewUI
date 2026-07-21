using Aprillz.MewUI.Rendering;

namespace Aprillz.MewUI.Controls;

// NOTE: Phase 2 固定列 overlay 面板(路径 E)。
// 非虚拟化:为每个数据项实例化一个 AdvancedGridRow(固定列通常少量行可见,且列宽固定可裁剪)。
// 垂直跟随主 ScrollViewer.VerticalOffset(钉位:固定列行与中间行同 y);水平钉死(不随水平滚动移动)。
// 选中/hover/网格线经 owner 桥接共享(行读 owner.IsItemSelectedCore/CoreColumns),与中间行一致。
// 仅持有列范围 [ColumnStart, ColumnStart+ColumnCount);范围外列不实例化。
// 列宽变化/ItemsSource 变化由 owner 调 Configure/Rebuild 通知。

internal sealed class AdvancedGridFrozenPane : Panel
{
    private readonly AdvancedGridView _owner;
    private readonly bool _rightSide; // false=左固定(从 0 起);true=右固定(贴 viewport 右边)
    private int _columnStart;
    private int _columnCount;
    private double _verticalOffset; // 跟主 ScrollViewer 垂直偏移
    private double _paneWidth; // 此 pane 的列总宽(用于右固定贴边计算)
    private int _itemCount;
    private double _rowHeight;
    private bool _configured;
    private uint _lastBindingGeneration;
    private bool _needsRebind = true; // 行重建/范围变化后强制重绑

    public AdvancedGridFrozenPane(AdvancedGridView owner, bool rightSide)
    {
        _owner = owner;
        _rightSide = rightSide;
        IsHitTestVisible = true;
        // 固定列行仍需响应点击(选中)。但若点击落在被裁掉的列上,行命中靠 owner 转发。
    }

    /// <summary>固定列的列范围(全列索引空间内)。</summary>
    public int ColumnStart => _columnStart;
    public int ColumnCount => _columnCount;

    /// <summary>配置列范围与行高。列变化(ColumnsChanged)或 ItemsSource 变化时由 owner 调用。</summary>
    public void Configure(int columnStart, int columnCount, double rowHeight)
    {
        _configured = true;
        bool rangeChanged = _columnStart != columnStart || _columnCount != columnCount;
        _columnStart = columnStart;
        _columnCount = columnCount;
        _rowHeight = rowHeight;

        if (rangeChanged)
        {
            RebuildRows();
        }
        else
        {
            ReapplyColumnRangeToRows();
        }

        InvalidateMeasure();
        InvalidateArrange();
        InvalidateVisual();
    }

    /// <summary>数据项数量变化(ItemsChanged)时由 owner 调用,同步行数。</summary>
    public void SetItemCount(int count, double rowHeight)
    {
        _rowHeight = rowHeight;
        _itemCount = count;
        RebuildRows();
        InvalidateMeasure();
        InvalidateArrange();
        InvalidateVisual();
    }

    /// <summary>垂直偏移跟随主 ScrollViewer(钉位:固定列行 y 与中间行一致)。</summary>
    public void SetVerticalOffset(double offset)
    {
        if (SetDouble(ref _verticalOffset, offset))
        {
            InvalidateArrange();
            InvalidateVisual();
        }
    }

    private void RebuildRows()
    {
        // 清空重建。子项数对齐 _itemCount,列范围设到每个行。
        while (Children.Count > 0)
        {
            RemoveAt(Children.Count - 1);
        }

        if (!_configured || _columnCount <= 0 || _itemCount <= 0)
        {
            return;
        }

        for (int i = 0; i < _itemCount; i++)
        {
            var row = new AdvancedGridRow(_owner)
            {
                ColumnStart = _columnStart,
                ColumnCount = _columnCount,
                Parent = this
            };
            Add(row);
        }

        // 计算列总宽。
        _paneWidth = ComputePaneWidth();
        // 新行未绑定,强制下次 BindRows 重绑。
        _needsRebind = true;
    }

    private void ReapplyColumnRangeToRows()
    {
        for (int i = 0; i < Children.Count; i++)
        {
            if (Children[i] is AdvancedGridRow row)
            {
                row.ColumnStart = _columnStart;
                row.ColumnCount = _columnCount;
            }
        }

        _paneWidth = ComputePaneWidth();
        // 列范围变了,EnsureColumns 需重做(列版本不变但行内 _lastColumnStart/_lastColumnCount 失效)。
        _needsRebind = true;
    }

    private double ComputePaneWidth()
    {
        var columns = _owner.CoreColumns;
        double total = 0;
        int end = _columnStart + _columnCount;
        for (int i = _columnStart; i < end && i < columns.Count; i++)
        {
            total += Math.Max(0, columns[i].ActualWidth);
        }
        return total;
    }

    /// <summary>失效所有行的重绘(选中变化),由 owner 选中变化时转发。仅重绘——选中不改行位置/列宽,
    /// 故不触发 InvalidateArrange(其会引发全行 O(N) 重排,方向键连选累积开销)。</summary>
    public void InvalidateRows()
    {
        InvalidateVisual();
    }

    /// <summary>
    /// 按 owner 当前 ItemsSource 与 binding generation 绑定所有行。
    /// generation 变化时重绑全部行(EnsureDpi/EnsureColumns/EnsureTheme/Bind)。
    /// 由 owner 在 InvalidateItemBindings/Arrange 时调用。
    /// </summary>
    public void BindRows(uint bindingGeneration, double rowHeight)
    {
        _rowHeight = rowHeight;
        if (!_needsRebind && bindingGeneration == _lastBindingGeneration && Children.Count == _itemCount)
        {
            return;
        }

        _needsRebind = false;
        _lastBindingGeneration = bindingGeneration;

        var items = _owner.ItemsSource;
        int n = Math.Min(Children.Count, items.Count);
        for (int i = 0; i < n; i++)
        {
            if (Children[i] is AdvancedGridRow row)
            {
                _owner.PrepareFrozenRow(row, items.GetItem(i), i);
            }
        }
    }

    protected override Size MeasureContent(Size availableSize)
    {
        if (!_configured || _columnCount <= 0)
        {
            return new Size(0, Math.Max(0, availableSize.Height));
        }

        double rowH = _rowHeight;
        for (int i = 0; i < Children.Count; i++)
        {
            if (Children[i] is FrameworkElement fe)
            {
                fe.Measure(new Size(_paneWidth, rowH));
            }
        }

        return new Size(_paneWidth, Math.Max(0, availableSize.Height));
    }

    protected override void ArrangeContent(Rect bounds)
    {
        if (!_configured || _columnCount <= 0)
        {
            return;
        }

        // 右固定:面板宽度为 _paneWidth,贴 bounds.Right;左固定:贴 bounds.X。
        double paneX = _rightSide ? bounds.Right - _paneWidth : bounds.X;
        double rowH = _rowHeight;

        for (int i = 0; i < Children.Count; i++)
        {
            if (Children[i] is FrameworkElement fe)
            {
                // 行 y 跟主滚动偏移:第 i 行内容 y = i*rowH,屏幕 y = i*rowH - verticalOffset。
                double y = bounds.Y + i * rowH - _verticalOffset;
                fe.Arrange(new Rect(paneX, y, _paneWidth, rowH));
            }
        }
    }

    protected override void OnRender(IGraphicsContext context)
    {
        var theme = Theme;
        var snapped = GetSnappedBorderBounds(Bounds);
        context.FillRectangle(snapped, theme.Palette.ControlBackground);

        // Phase 2:斑马纹对齐中间 presenter 的 BeforeRowRender(奇数行着色)。固定列 overlay 行不经
        // presenter,故在此补画,使固定列与中间列斑马一致。行 y 跟随垂直偏移(与行 arrange 同公式)。
        // 只画到 _itemCount 行:行区下方空白区(行数不足以铺满视口时)不画斑马纹,与中间 presenter
        // 只画实际行一致(否则 pane 底部空白出现多余条纹)。
        if (_owner.ZebraStriping && _rowHeight > 0 && _itemCount > 0)
        {
            var stripe = theme.Palette.ControlBackground.Lerp(theme.Palette.ButtonFace, theme.IsDark ? 0.45 : 0.33);
            var dpiScale = GetDpi() / 96.0;
            double top = snapped.Y;
            double bottom = snapped.Bottom;
            double baseY = snapped.Y - _verticalOffset;
            int firstVisible = (int)Math.Floor((top - baseY) / _rowHeight);
            if (firstVisible < 0) firstVisible = 0;
            for (int i = firstVisible; i < _itemCount; i++)
            {
                double yTop = baseY + i * _rowHeight;
                if (yTop >= bottom)
                {
                    break;
                }
                if ((i & 1) == 1)
                {
                    double yBottom = yTop + _rowHeight;
                    double drawTop = Math.Max(yTop, top);
                    double drawBottom = Math.Min(yBottom, bottom);
                    if (drawBottom > drawTop)
                    {
                        var rect = LayoutRounding.SnapViewportRectToPixels(
                            new Rect(snapped.X, drawTop, snapped.Width, drawBottom - drawTop), dpiScale);
                        context.FillRectangle(rect, stripe);
                    }
                }
            }
        }

        // Phase 2:固定列与中间列的分段竖线。左固定 pane 画右沿、右固定 pane 画左沿,
        // 贯穿行区高度(数据行 OnRender 的竖线在最后一列右沿 = pane 边沿处被 break 跳过,故在此补)。
        // 与表头/页脚的分段强调线衔接,形成贯穿表头/行区/页脚的分隔线。
        if (_owner.ShowGridLines)
        {
            var stroke = theme.Palette.ControlBorder;
            double sepX = _rightSide ? snapped.X : snapped.Right;
            context.DrawLine(new Point(sepX, snapped.Y), new Point(sepX, snapped.Bottom), stroke, 1, pixelSnap: true);
        }
    }
}
