using System.Diagnostics.CodeAnalysis;
using Aprillz.MewUI.Input;
using Aprillz.MewUI.Rendering;

namespace Aprillz.MewUI.Controls;

// NOTE: 照抄 GridView(GridView.cs)的实现,改为继承 ScrollableItemsBase 的独立 public 控件。
// 因放在 MewUI 程序集内,可复用 internal 基础设施:GridViewCore(列/选择/ItemsSource)、
// FixedHeightItemsPresenter(虚拟化)、SelectionSync(选择同步)、ItemsViewportMath(滚动数学)、
// PendingTabFocusHelper(Tab 焦点)、IFocusIntoViewHost/IVirtualizedTabNavigationHost(焦点路由)。
// HeaderRow/Row/Cell 是 GridView 的 private 嵌套类不可复用,故自写 AdvancedGridHeaderRow/AdvancedGridRow。
// Phase 1: 与 GridView 行为 100% 一致。Phase 2: 加固定列/合并表头/页脚。

/// <summary>
/// 扩展网格视图:在 <see cref="GridView"/> 基础上(行为一致)预留固定列 / 合并表头 / 页脚行的扩展点。
/// Phase 1 行为与 <see cref="GridView"/> 一致;通过 <see cref="AdvancedGridExtensions"/> 的同名 fluent API,
/// 调用方可由 <see cref="GridView"/> 零改动迁移。
/// </summary>
public sealed class AdvancedGridView : ScrollableItemsBase, IFocusIntoViewHost, IVirtualizedTabNavigationHost, ISelector, IIndexedSelector, IMultiSelector
{
    public static readonly MewProperty<bool> ZebraStripingProperty =
        MewProperty<bool>.Register<AdvancedGridView>(nameof(ZebraStriping), true, MewPropertyOptions.AffectsRender);

    public static readonly MewProperty<bool> ShowGridLinesProperty =
        MewProperty<bool>.Register<AdvancedGridView>(nameof(ShowGridLines), false, MewPropertyOptions.AffectsRender);

    public static readonly MewProperty<double> RowHeightProperty =
        MewProperty<double>.Register<AdvancedGridView>(nameof(RowHeight), double.NaN, MewPropertyOptions.AffectsLayout);

    public static readonly MewProperty<double> HeaderHeightProperty =
        MewProperty<double>.Register<AdvancedGridView>(nameof(HeaderHeight), double.NaN, MewPropertyOptions.AffectsLayout);

    public static readonly MewProperty<Thickness> CellPaddingProperty =
        MewProperty<Thickness>.Register<AdvancedGridView>(nameof(CellPadding), default, MewPropertyOptions.AffectsLayout,
            static (self, _, _) => self.OnCellPaddingChanged());

    public static readonly MewProperty<double> MaxAutoViewportHeightProperty =
        MewProperty<double>.Register<AdvancedGridView>(nameof(MaxAutoViewportHeight), 320.0, MewPropertyOptions.AffectsLayout);

    public static readonly MewProperty<int> SelectedIndexProperty =
        MewProperty<int>.Register<AdvancedGridView>(nameof(SelectedIndex), -1,
            MewPropertyOptions.BindsTwoWayByDefault,
            static (self, _, newVal) => self.OnSelectedIndexPropertyChanged(newVal));

    public static readonly MewProperty<ItemsSelectionMode> SelectionModeProperty =
        MewProperty<ItemsSelectionMode>.Register<AdvancedGridView>(nameof(SelectionMode), ItemsSelectionMode.Single,
            MewPropertyOptions.None,
            static (self, _, newVal) => self.OnSelectionModePropertyChanged(newVal));

    public static readonly MewProperty<object?> SelectedItemProperty =
        MewProperty<object?>.Register<AdvancedGridView>(nameof(SelectedItem), null,
            MewPropertyOptions.BindsTwoWayByDefault,
            static (self, _, newVal) => self.OnSelectedItemPropertyChanged(newVal));

    private static readonly MewPropertyKey<IReadOnlyList<object?>> SelectedItemsPropertyKey =
        MewProperty<IReadOnlyList<object?>>.RegisterReadOnly<AdvancedGridView>(nameof(SelectedItems), Array.Empty<object?>());
    public static readonly MewProperty<IReadOnlyList<object?>> SelectedItemsProperty = SelectedItemsPropertyKey.Property;

    private object? _itemTypeToken;
    private readonly GridView.GridViewCore _core = new();
    private readonly SelectionSync _selection;
    private readonly FixedHeightItemsPresenter _presenter;
    private readonly AdvancedGridHeaderRow _header;
    private readonly AdvancedGridFooterRow _footer;
    private readonly IDataTemplate _rowTemplate;
    // Phase 2:每列页脚配置(因 GridViewCore.ColumnDefinition 不含 footer 字段,在此单独存)。
    // 索引与 _core.Columns 对齐。null = 该列无页脚。
    private List<FooterConfig?> _footerConfigs = new();
    // Phase 2:复杂表头分组(合并表头)。空 = 单行表头;非空 = 两行(分组行 + 列名行)。
    // 每个分组跨 [StartColumn, StartColumn+ColumnSpan) 的连续列(由调用方按 complexHeader.childColumns
    // 匹配列 id 算出;JNPF 设计器约束 childColumns 对应相邻列)。
    private List<HeaderGroup> _headerGroups = new();

    // Phase 2:固定列(路径 E)。左固定 _frozenLeft 列 + 右固定 _frozenRight 列;中间列走虚拟化 presenter
    // 并水平滚动。两个 AdvancedGridFrozenPane 是 overlay(全实例化行),垂直跟随主 ScrollViewer,水平钉死。
    private AdvancedGridFrozenPane? _leftFrozen;
    private AdvancedGridFrozenPane? _rightFrozen;
    private int _frozenLeft;
    private int _frozenRight;

    private double _rowsExtentHeight;
    private double _columnsExtentWidth;
    private double _rowsViewportHeight;
    private double _rowsViewportWidth;

    public AdvancedGridView()
    {
        _selection = new SelectionSync(() => _core.ItemsSource,
            value => SetValue(SelectedIndexProperty, value),
            value => SetValue(SelectedItemProperty, value),
            value => SetValue(SelectedItemsPropertyKey, value));

        CellPadding = Theme.Metrics.ItemPadding;

        _scrollViewer.Padding = new Thickness(0);
        _scrollViewer.CornerRadius = 0;

        _header = new AdvancedGridHeaderRow(this) { Parent = this };
        _footer = new AdvancedGridFooterRow(this) { Parent = this };

        _rowTemplate = new DelegateTemplate<object?>(
            build: _ => new AdvancedGridRow(this),
            bind: BindRowTemplate);

        _presenter = new FixedHeightItemsPresenter();
        InitializePresenter(_presenter);

        _scrollViewer.Content = _presenter;
        _scrollViewer.ScrollChanged += () =>
        {
            _header.HorizontalOffset = _scrollViewer.HorizontalOffset;
            _footer.HorizontalOffset = _scrollViewer.HorizontalOffset;
            // Phase 2:固定列 overlay 垂直跟随主滚动(水平钉死,不动)。
            double v = _scrollViewer.VerticalOffset;
            _leftFrozen?.SetVerticalOffset(v);
            _rightFrozen?.SetVerticalOffset(v);
        };

        _core.ItemsChanged += OnItemsChanged;
        _core.SelectionChanged += _ => OnItemsSelectionChanged();
        _core.SelectedIndicesChanged += () =>
        {
            _selection.SyncFromModel();
            SelectedIndicesChanged?.Invoke();
            InvalidateItemBindings();
            InvalidateVisual();
        };
        _core.ColumnsChanged += () =>
        {
            _header.SetColumns(_core.Columns);
            _footer.SetColumnCount(_core.Columns.Count);
            UpdateFooter();
            _presenter.RecycleAll();
            // Phase 2:列变更后固定列范围可能失效,重配 overlay。
            ConfigureFrozenPanes();
            InvalidateItemBindings();
            InvalidateMeasure();
            InvalidateArrange();
            InvalidateVisual();
        };

        _tabFocusHelper = new PendingTabFocusHelper(
            getWindow: () => FindVisualRoot() as Window,
            getContainer: idx =>
            {
                FrameworkElement? container = null;
                _presenter.VisitRealized((i, el) => { if (i == idx) container = el; });
                return container;
            });
    }

    public event Action<object?>? SelectionChanged;

    public bool ZebraStriping
    {
        get => GetValue(ZebraStripingProperty);
        set => SetValue(ZebraStripingProperty, value);
    }

    public bool ShowGridLines
    {
        get => GetValue(ShowGridLinesProperty);
        set => SetValue(ShowGridLinesProperty, value);
    }

    public double RowHeight
    {
        get => GetValue(RowHeightProperty);
        set => SetValue(RowHeightProperty, value);
    }

    public double HeaderHeight
    {
        get => GetValue(HeaderHeightProperty);
        set => SetValue(HeaderHeightProperty, value);
    }

    public Thickness CellPadding
    {
        get => GetValue(CellPaddingProperty);
        set => SetValue(CellPaddingProperty, value);
    }

    public double MaxAutoViewportHeight
    {
        get => GetValue(MaxAutoViewportHeightProperty);
        set => SetValue(MaxAutoViewportHeightProperty, value);
    }

    public int SelectedIndex
    {
        get => GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    /// <summary>
    /// 选择模式。需多选能力的 items source(如经
    /// <see cref="ItemsView.Create{T}(IReadOnlyList{T}, System.Func{T, string}, System.Func{T, object})"/> 创建);
    /// 否则保持 <see cref="ItemsSelectionMode.Single"/>。
    /// </summary>
    public ItemsSelectionMode SelectionMode
    {
        get => GetValue(SelectionModeProperty);
        set => SetValue(SelectionModeProperty, value);
    }

    private void OnSelectionModePropertyChanged(ItemsSelectionMode mode)
        => _core.SelectionMode = mode;

    /// <summary>升序的选中行索引。</summary>
    public IReadOnlyList<int> SelectedIndices => _selection.SelectedIndices;

    /// <summary>升序索引的选中项(只读,可绑定)。</summary>
    public IReadOnlyList<object?> SelectedItems => GetValue(SelectedItemsProperty);

    /// <summary>返回 <paramref name="index"/> 行是否选中。</summary>
    public bool IsSelected(int index) => _selection.IsSelected(index);

    /// <summary>全选(仅多选模式;否则空操作)。</summary>
    public void SelectAll() => _selection.SelectAll();

    /// <summary>清空选择。</summary>
    public void ClearSelection() => _selection.ClearSelection();

    /// <summary>替换当前选择为闭区间 [start, end](仅多选)。</summary>
    public void SelectRange(int start, int end) => _selection.SelectRange(start, end);

    /// <summary>选中行集合变化时触发(多选)。</summary>
    public event Action? SelectedIndicesChanged;

    // 行/单元格 pointer-down 的共享选择入口。设 Handled 使行与单元格 handler 不重复应用。
    internal void HandleRowPointerDown(int rowIndex, MouseEventArgs e)
    {
        if (!IsEffectivelyEnabled)
        {
            return;
        }

        var multi = _core.MultiView;
        if (multi != null && multi.SelectionMode != ItemsSelectionMode.Single)
        {
            ItemsSelectionInput.HandleClick(multi, rowIndex, e.Modifiers);
        }
        else
        {
            SelectedIndex = rowIndex;
        }

        e.Handled = true;
    }

    protected override void OnThemeChanged(Theme oldTheme, Theme newTheme)
    {
        base.OnThemeChanged(oldTheme, newTheme);
        InvalidateItemBindings();
        InvalidateVisual();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Handled || !IsEffectivelyEnabled)
        {
            return;
        }

        int count = _core.ItemsSource.Count;
        if (count <= 0)
        {
            return;
        }

        int current = SelectedIndex >= 0 ? SelectedIndex : 0;
        var multi = _core.MultiView;

        if (multi != null && multi.SelectionMode != ItemsSelectionMode.Single && IsSelectAllShortcut(e))
        {
            multi.SelectRange(0, count - 1, clearExisting: true);
            e.Handled = true;
            Focus();
            InvalidateVisual();
            return;
        }

        int target = current;
        switch (e.Key)
        {
            case Key.Up:
            case Key.Down:
            case Key.Home:
            case Key.End:
            case Key.PageUp:
            case Key.PageDown:
                e.Handled = TryGetListNavigationTarget(e.Key, current, count, ResolvePageStep(count), supportsPaging: true, out target);
                break;
        }

        if (e.Handled)
        {
            if (multi != null && multi.SelectionMode != ItemsSelectionMode.Single)
            {
                ItemsSelectionInput.HandleKeyboardMove(multi, target, (e.Modifiers & ModifierKeys.Shift) != 0);
            }
            else
            {
                SelectedIndex = target;
            }

            Focus();
            InvalidateVisual();
        }
    }

    private void OnCellPaddingChanged() => InvalidateItemBindings();

    private int ResolvePageStep(int count)
    {
        double rowH = GetPixelAlignedRowHeight();
        if (rowH <= 0)
        {
            return 1;
        }

        double viewport = _rowsViewportHeight;
        if (viewport <= 0 || double.IsNaN(viewport) || double.IsInfinity(viewport))
        {
            return 1;
        }

        int step = (int)Math.Floor(viewport / rowH);
        return Math.Clamp(step, 1, Math.Max(1, count));
    }

    bool IFocusIntoViewHost.OnDescendantFocused(UIElement focusedElement)
    {
        if (focusedElement == this)
        {
            return false;
        }

        EnsureHorizontalIntoView(focusedElement);

        if (!TryFindRealizedIndex(_presenter, focusedElement, out int found, out _) || found >= _core.ItemsSource.Count)
        {
            return false;
        }

        if (SelectedIndex != found)
        {
            SelectedIndex = found;
        }
        else
        {
            ScrollIntoView(found);
        }

        return true;
    }

    private void EnsureHorizontalIntoView(UIElement focusedElement)
    {
        if (_core.Columns.Count == 0)
        {
            return;
        }

        if (!TryGetContentBounds(out var contentLocal, out double headerH))
        {
            return;
        }

        double viewportW = Math.Max(0, contentLocal.Width);
        if (viewportW <= 0 || double.IsNaN(viewportW) || double.IsInfinity(viewportW))
        {
            return;
        }

        double extentW = _columnsExtentWidth;
        if (extentW <= 0 || double.IsNaN(extentW) || double.IsInfinity(extentW))
        {
            extentW = ComputeColumnsExtentWidth();
        }

        if (extentW <= viewportW + 0.5)
        {
            return;
        }

        var size = focusedElement.RenderSize;
        var localRect = new Rect(0, 0, size.Width, size.Height);

        Rect rectInGrid;
        try
        {
            rectInGrid = focusedElement.TranslateRect(localRect, this);
        }
        catch (InvalidOperationException)
        {
            return;
        }

        // rectInGrid 与 contentLocal 都在本 AdvancedGridView 坐标空间。
        double viewportLeft = contentLocal.X;
        double viewportRight = viewportLeft + viewportW;

        double oldOffset = _scrollViewer.HorizontalOffset;
        double newOffset = oldOffset;

        if (rectInGrid.Left < viewportLeft)
        {
            newOffset = oldOffset - (viewportLeft - rectInGrid.Left);
        }
        else if (rectInGrid.Right > viewportRight)
        {
            newOffset = oldOffset + (rectInGrid.Right - viewportRight);
        }
        else
        {
            return;
        }

        newOffset = Math.Clamp(newOffset, 0, Math.Max(0, extentW - viewportW));

        if (!newOffset.Equals(oldOffset))
        {
            _scrollViewer.SetScrollOffsets(newOffset, _scrollViewer.VerticalOffset);
        }
    }

    bool IVirtualizedTabNavigationHost.TryMoveFocusFromDescendant(UIElement focusedElement, bool moveForward)
    {
        if (!IsEffectivelyEnabled || _core.ItemsSource.Count == 0)
        {
            return false;
        }

        if (!TryFindRealizedIndex(_presenter, focusedElement, out int found, out var foundContainer))
        {
            return false;
        }

        // 检查此容器内是否还有更多可聚焦元素。
        var edge = moveForward
            ? FocusManager.FindLastFocusable(foundContainer)
            : FocusManager.FindFirstFocusable(foundContainer);
        bool hasMoreFocusable = edge != null && !ReferenceEquals(edge, focusedElement);

        if (hasMoreFocusable)
        {
            if (IsItemInViewport(found))
            {
                // 项在屏上-让正常 Tab 导航处理项内移动。
                return false;
            }

            // 项离屏(焦点固定)。此处不能返 false,因为 FocusManager 的扁平 Tab 列表会在同一容器内移动,
            // 然后 ScrollViewer.OnDescendantFocused 先于我们触发并用元素陈旧 Bounds-导致无垂直滚动。
            // 改为:把此项滚入视图并自行移动焦点。
            ScrollIntoView(found);
            var next = FindNextFocusableInContainer(foundContainer, focusedElement, moveForward);
            if (next != null && FindVisualRoot() is Window window)
            {
                window.FocusManager.SetFocus(next);
                return true;
            }

            return false;
        }

        int targetIndex = moveForward ? found + 1 : found - 1;
        if (targetIndex < 0 || targetIndex >= _core.ItemsSource.Count)
        {
            return false;
        }

        SelectedIndex = targetIndex;
        ScrollIntoView(targetIndex);
        _tabFocusHelper.Schedule(targetIndex, moveForward);
        return true;
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);

        if (e.Handled)
        {
            return;
        }

        bool canVScroll = _rowsExtentHeight > _rowsViewportHeight + 0.5;
        // 水平滚动判定:有固定列时只看中间列 extent vs 中间区宽(固定列不滚,不应让全部列宽误判可水平滚,
        // 否则固定列存在但中间列不超宽时,水平滑轮触发无效滚动并可能吞掉垂直滑轮)。
        double hExtent = HasFrozenColumns ? Math.Max(0, _columnsExtentWidth - FrozenLeftWidth - FrozenRightWidth) : _columnsExtentWidth;
        bool canHScroll = hExtent > _rowsViewportWidth + 0.5;
        bool handled = false;

        if (canVScroll && e.Delta.Y != 0)
        {
            _scrollViewer.ScrollBy(-e.Delta.Y);
            handled = true;
        }
        if (canHScroll && e.Delta.X != 0)
        {
            _scrollViewer.ScrollByHorizontal(-e.Delta.X);
            handled = true;
        }
        if (handled)
        {
            e.Handled = true;
        }
    }

    /// <summary>
    /// 数据源。赋 <see cref="ItemsView{T}"/>(如经
    /// <see cref="ItemsView.Create{T}(IReadOnlyList{T}, System.Func{T, string}, System.Func{T, object})"/>)
    /// 以在类型化列旁显示类型化行。
    /// </summary>
    public ISelectableItemsView ItemsSource
    {
        get => _core.ItemsSource;
        set => _core.SetItems(value ?? ItemsView.EmptySelectable);
    }

    public void SetColumns<TItem>(IReadOnlyList<AdvancedGridColumn<TItem>> columns)
    {
        ArgumentNullException.ThrowIfNull(columns);
        EnsureConfiguredFor<TItem>();
        // 先填 footer 配置,再 SetColumns(后者触发 ColumnsChanged → UpdateFooter 读此配置)。
        _footerConfigs = ExtractFooterConfigs(columns);
        _core.SetColumns(ConvertColumns(columns));
    }

    /// <summary>
    /// 尝试在此控件坐标空间内指定位置找到项(行)索引。
    /// </summary>
    public bool TryGetItemIndexAt(Point position, out int index)
        => TryGetItemIndexAtCore(position, out index);

    /// <summary>
    /// 尝试为经窗口输入路由器路由的鼠标事件找到项(行)索引。
    /// </summary>
    public bool TryGetItemIndexAt(MouseEventArgs e, out int index)
    {
        ArgumentNullException.ThrowIfNull(e);
        return TryGetItemIndexAtCore(e.GetPosition(this), out index);
    }

    private bool TryGetItemIndexAtCore(Point position, out int index)
    {
        index = -1;

        // 不把滚动条交互当项命中/激活。
        var windowPoint = new Point(Bounds.X + position.X, Bounds.Y + position.Y);
        if (_scrollViewer.HitTest(windowPoint) is ScrollBar)
        {
            return false;
        }

        if (!TryGetContentBounds(out var contentBounds, out double headerH))
        {
            return false;
        }

        double rowsHeight = Math.Max(0, contentBounds.Height - headerH);
        double rowsY = contentBounds.Y + headerH;
        if (rowsHeight <= 0)
        {
            return false;
        }

        if (position.Y < rowsY || position.Y >= rowsY + rowsHeight)
        {
            return false;
        }

        double rowH = GetPixelAlignedRowHeight();
        if (rowH <= 0)
        {
            return false;
        }

        return ItemsViewportMath.TryGetItemIndexAtY(
            position.Y,
            rowsY,
            _scrollViewer.VerticalOffset,
            rowH,
            _core.ItemsSource.Count,
            out index);
    }

    /// <summary>
    /// 尝试在此控件坐标空间内指定位置找到列索引。仅当位置在表头或行区上时返回 true。
    /// </summary>
    public bool TryGetColumnIndexAt(Point position, out int columnIndex)
        => TryGetColumnIndexAtCore(position, out columnIndex);

    /// <summary>
    /// 尝试为经窗口输入路由器路由的鼠标事件找到列索引。
    /// </summary>
    public bool TryGetColumnIndexAt(MouseEventArgs e, out int columnIndex)
    {
        ArgumentNullException.ThrowIfNull(e);
        return TryGetColumnIndexAtCore(e.GetPosition(this), out columnIndex);
    }

    private bool TryGetColumnIndexAtCore(Point position, out int columnIndex)
    {
        columnIndex = -1;

        // 不把滚动条交互当列命中。
        var windowPoint = new Point(Bounds.X + position.X, Bounds.Y + position.Y);
        if (_scrollViewer.HitTest(windowPoint) is ScrollBar)
        {
            return false;
        }

        if (!TryGetContentBounds(out var contentBounds, out double headerH))
        {
            return false;
        }

        double y0 = contentBounds.Y;
        double y1 = contentBounds.Y + contentBounds.Height;
        if (position.Y < y0 || position.Y >= y1)
        {
            return false;
        }

        return TryGetColumnIndexAtX(position.X, contentBounds.X, contentBounds.Width, out columnIndex);
    }

    /// <summary>
    /// 尝试在此控件坐标空间内指定位置找到单元格(行/列)索引。位置在表头上时返回 true 且
    /// <paramref name="isHeader"/> 置位、<paramref name="rowIndex"/> 置 -1。
    /// </summary>
    public bool TryGetCellIndexAt(Point position, out int rowIndex, out int columnIndex, out bool isHeader)
        => TryGetCellIndexAtCore(position, out rowIndex, out columnIndex, out isHeader);

    /// <summary>
    /// 尝试为经窗口输入路由器路由的鼠标事件找到单元格(行/列)索引。
    /// </summary>
    public bool TryGetCellIndexAt(MouseEventArgs e, out int rowIndex, out int columnIndex, out bool isHeader)
    {
        ArgumentNullException.ThrowIfNull(e);
        return TryGetCellIndexAtCore(e.GetPosition(this), out rowIndex, out columnIndex, out isHeader);
    }

    private bool TryGetCellIndexAtCore(Point position, out int rowIndex, out int columnIndex, out bool isHeader)
    {
        rowIndex = -1;
        columnIndex = -1;
        isHeader = false;

        // 不把滚动条交互当单元格命中。
        var windowPoint = new Point(Bounds.X + position.X, Bounds.Y + position.Y);
        if (_scrollViewer.HitTest(windowPoint) is ScrollBar)
        {
            return false;
        }

        if (!TryGetContentBounds(out var contentBounds, out double headerH))
        {
            return false;
        }

        double headerY0 = contentBounds.Y;
        double headerY1 = contentBounds.Y + headerH;
        if (position.Y >= headerY0 && position.Y < headerY1)
        {
            if (!TryGetColumnIndexAtX(position.X, contentBounds.X, contentBounds.Width, out columnIndex))
            {
                return false;
            }

            isHeader = true;
            rowIndex = -1;
            return true;
        }

        if (!TryGetItemIndexAtCore(position, out rowIndex))
        {
            return false;
        }

        if (!TryGetColumnIndexAtX(position.X, contentBounds.X, contentBounds.Width, out columnIndex))
        {
            return false;
        }

        isHeader = false;
        return true;
    }

    private bool TryGetContentBounds(out Rect contentBounds, out double headerHeight)
    {
        contentBounds = default;
        headerHeight = 0;

        var bounds = GetSnappedBorderBounds(new Rect(0, 0, Bounds.Width, Bounds.Height));
        var dpiScale = GetDpi() / 96.0;
        var innerBounds = bounds.Deflate(new Thickness(GetBorderVisualInset()));
        var viewportBounds = innerBounds;
        // 视口/裁剪矩形不应因边缘圆角收缩;向外对齐。
        contentBounds = LayoutRounding.SnapViewportRectToPixels(viewportBounds.Deflate(Padding), dpiScale);
        headerHeight = ResolveHeaderHeight();

        if (contentBounds.Width <= 0 || contentBounds.Height <= 0 || headerHeight < 0 ||
            double.IsNaN(contentBounds.Width) || double.IsNaN(contentBounds.Height) ||
            double.IsInfinity(contentBounds.Width) || double.IsInfinity(contentBounds.Height))
        {
            return false;
        }

        return true;
    }

    private bool TryGetColumnIndexAtX(double x, double contentX, double contentWidth, out int columnIndex)
    {
        columnIndex = -1;

        if (x < contentX || x >= contentX + contentWidth)
        {
            return false;
        }

        // 计入水平滚动:列从 (contentX - offset) 起排。
        x += _scrollViewer.HorizontalOffset;

        // 按累计宽度命中列。
        double cur = contentX;
        for (int i = 0; i < _core.Columns.Count; i++)
        {
            double w = Math.Max(0, _core.Columns[i].Width);
            double next = cur + w;
            if (x >= cur && x < next)
            {
                columnIndex = i;
                return true;
            }
            cur = next;
        }

        return false;
    }

    public void AddColumns<TItem>(params AdvancedGridColumn<TItem>[] columns)
    {
        ArgumentNullException.ThrowIfNull(columns);
        EnsureConfiguredFor<TItem>();
        // 追加 footer 配置(AddColumns 触发 ColumnsChanged → UpdateFooter)。
        _footerConfigs.AddRange(ExtractFooterConfigs(columns));
        _core.AddColumns(ConvertColumns(columns));
    }

    private void EnsureConfiguredFor<TItem>()
    {
        if (_itemTypeToken == null)
        {
            _itemTypeToken = typeof(TItem);
            return;
        }

        if (!ReferenceEquals(_itemTypeToken, typeof(TItem)))
        {
            throw new InvalidOperationException($"AdvancedGridView is already configured for item type '{((Type)_itemTypeToken).Name}'. Create a new AdvancedGridView for a different TItem.");
        }
    }

    private static IReadOnlyList<GridView.GridViewCore.ColumnDefinition> ConvertColumns<TItem>(IReadOnlyList<AdvancedGridColumn<TItem>> columns)
    {
        var list = new List<GridView.GridViewCore.ColumnDefinition>(columns.Count);
        for (int i = 0; i < columns.Count; i++)
        {
            var c = columns[i];
            if (c.CellTemplate == null)
            {
                throw new InvalidOperationException("AdvancedGridColumn.CellTemplate is required.");
            }

            list.Add(new GridView.GridViewCore.ColumnDefinition(c.Header, c.Width, c.MinWidth, c.IsResizable, c.CellTemplate));
        }

        return list;
    }

    /// <summary>从列定义提取每列页脚配置(FooterText / FooterAggregator)。索引与列对齐。</summary>
    private static List<FooterConfig?> ExtractFooterConfigs<TItem>(IReadOnlyList<AdvancedGridColumn<TItem>> columns)
    {
        var list = new List<FooterConfig?>(columns.Count);
        for (int i = 0; i < columns.Count; i++)
        {
            var c = columns[i];
            list.Add(c.HasFooter ? new FooterConfig(c.FooterText, c.FooterAggregator) : null);
        }

        return list;
    }

    /// <summary>是否有任何列配置了页脚(决定是否渲染 footer 行 + 预留底部空间)。</summary>
    private bool HasFooter => _footerConfigs.Count > 0 && _footerConfigs.Any(c => c is not null);

    private double ResolveFooterHeight()
    {
        if (!HasFooter)
        {
            return 0;
        }

        // 页脚行高与表头行高一致(都是单行文本)。
        if (!double.IsNaN(HeaderHeight) && HeaderHeight > 0)
        {
            return HeaderHeight;
        }

        return Math.Max(Theme.Metrics.BaseControlHeight, 24);
    }

    /// <summary>
    /// Phase 2:设置复杂表头分组(合并表头)。建列后调用:每个分组跨 [startColumn, startColumn+columnSpan)
    /// 的连续列(调用方按 complexHeader.childColumns 匹配列 id 算出)。空列表 = 单行表头(默认)。
    /// JNPF 只支持两层,设非空后表头渲染两行(分组行 + 列名行)。
    /// </summary>
    public void SetHeaderGroups(IReadOnlyList<HeaderGroup>? groups)
    {
        _headerGroups = groups is null || groups.Count == 0
            ? new()
            : new List<HeaderGroup>(groups);
        _header.SetHeaderGroups(_headerGroups);
        InvalidateMeasure();
        InvalidateArrange();
        InvalidateVisual();
    }

    /// <summary>是否有复杂表头分组(决定表头渲染一行还是两行)。</summary>
    internal bool HasHeaderGroups => _headerGroups.Count > 0;

    /// <summary>
    /// Phase 2:设置固定列。<paramref name="left"/> = 左固定的列数(从第 0 列起),
    /// <paramref name="right"/> = 右固定的列数(贴 viewport 右边的最后 N 列)。
    /// 中间列(范围 [left, columns.Count-right))走虚拟化 presenter 水平滚动;固定列 overlay 始终钉视口左/右。
    /// JNPF 约束:fixed=left 的列在列序左端、fixed=right 的列在右端。设 (0,0) 退化无固定列。
    /// 在建列(SetColumns/AddColumns)后调用;列变更/ItemsSource 变化时自动重配 overlay。
    /// </summary>
    public void SetFrozenColumns(int left, int right)
    {
        if (left < 0) left = 0;
        if (right < 0) right = 0;

        int colCount = _core.Columns.Count;
        // 固定列不能重叠:左+右 < 总列数(否则中间无列可滚动,降级为只固定不重叠的部分)。
        if (left + right >= colCount && colCount > 0)
        {
            // 优先保左固定,右固定取剩余。
            right = Math.Max(0, colCount - left - 1);
        }

        bool changed = _frozenLeft != left || _frozenRight != right;
        _frozenLeft = left;
        _frozenRight = right;

        EnsureFrozenPanes();
        ConfigureFrozenPanes();

        if (changed)
        {
            // 列范围变了,中间行的列范围也要更新(经 InvalidateItemBindings + RecycleAll)。
            _presenter.RecycleAll();
            InvalidateItemBindings();
            InvalidateMeasure();
            InvalidateArrange();
            InvalidateVisual();
        }
    }

    /// <summary>是否有固定列(决定是否启用 overlay + 三段表头/页脚)。</summary>
    internal bool HasFrozenColumns => _frozenLeft > 0 || _frozenRight > 0;

    /// <summary>左固定列数。</summary>
    internal int FrozenLeftCount => _frozenLeft;

    /// <summary>右固定列数。</summary>
    internal int FrozenRightCount => _frozenRight;

    /// <summary>左固定列总宽(DIP)。</summary>
    internal double FrozenLeftWidth
    {
        get
        {
            double w = 0;
            var cols = _core.Columns;
            for (int i = 0; i < _frozenLeft && i < cols.Count; i++)
            {
                w += Math.Max(0, cols[i].Width);
            }
            return w;
        }
    }

    /// <summary>右固定列总宽(DIP)。</summary>
    internal double FrozenRightWidth
    {
        get
        {
            double w = 0;
            var cols = _core.Columns;
            int start = cols.Count - _frozenRight;
            for (int i = start; i < cols.Count && i >= 0; i++)
            {
                w += Math.Max(0, cols[i].Width);
            }
            return w;
        }
    }

    private void EnsureFrozenPanes()
    {
        if (HasFrozenColumns)
        {
            if (_leftFrozen == null && _frozenLeft > 0)
            {
                _leftFrozen = new AdvancedGridFrozenPane(this, rightSide: false) { Parent = this };
            }
            if (_rightFrozen == null && _frozenRight > 0)
            {
                _rightFrozen = new AdvancedGridFrozenPane(this, rightSide: true) { Parent = this };
            }
        }
    }

    /// <summary>把当前列范围/行高/数据项数推给 overlay(列变更/ItemsSource 变化时调)。</summary>
    private void ConfigureFrozenPanes()
    {
        // 先把固定列信息推给表头/页脚(三段 arrange/render 需要),无固定列时清零。
        double leftW = FrozenLeftWidth;
        double rightW = FrozenRightWidth;
        _header.SetFrozen(_frozenLeft, _frozenRight, leftW, rightW);
        _footer.SetFrozen(_frozenLeft, _frozenRight, leftW, rightW);

        if (!HasFrozenColumns)
        {
            return;
        }

        int count = _core.ItemsSource.Count;
        double rowH = GetPixelAlignedRowHeight();
        int colCount = _core.Columns.Count;

        if (_leftFrozen != null && _frozenLeft > 0)
        {
            _leftFrozen.Configure(0, _frozenLeft, rowH);
            _leftFrozen.SetItemCount(count, rowH);
            _leftFrozen.BindRows(ItemBindingGeneration, rowH);
            _leftFrozen.SetVerticalOffset(_scrollViewer.VerticalOffset);
        }

        if (_rightFrozen != null && _frozenRight > 0)
        {
            _rightFrozen.Configure(colCount - _frozenRight, _frozenRight, rowH);
            _rightFrozen.SetItemCount(count, rowH);
            _rightFrozen.BindRows(ItemBindingGeneration, rowH);
            _rightFrozen.SetVerticalOffset(_scrollViewer.VerticalOffset);
        }
    }

    /// <summary>
    /// 绑定一个 overlay 行(EnsureDpi/EnsureColumns/EnsureTheme/Bind)。复用 BindRowTemplate 的逻辑,
    /// 供 AdvancedGridFrozenPane 在 BindRows 时对每个固定行调用。
    /// </summary>
    internal void PrepareFrozenRow(AdvancedGridRow row, object? item, int index)
    {
        row.EnsureDpi(GetDpi());
        row.EnsureColumns(_core.Columns, _core.ColumnsVersion);
        row.EnsureTheme(ThemeInternal);
        row.Bind(item, index);
    }

    /// <summary>中间 presenter 行的列范围(供 row 模板在 EnsureColumns 前设 ColumnStart/ColumnCount)。</summary>
    internal int MiddleColumnStart => _frozenLeft;
    internal int MiddleColumnCount => Math.Max(0, _core.Columns.Count - _frozenLeft - _frozenRight);

    /// <summary>
    /// 为已添加的某列配置页脚(建列后按索引后配置,避免改 AddColumn 签名)。
    /// <paramref name="text"/> 非空时用静态文本(如首列"合计");否则用 <paramref name="aggregator"/>
    /// (接收所有行返回文本)。两者皆 null 则清除该列页脚。配置后调用方应调 <see cref="UpdateFooter"/> 刷新。
    /// </summary>
    public void SetColumnFooter(int index, string? text, Func<IReadOnlyList<object?>, string>? aggregator)
    {
        if ((uint)index >= (uint)_footerConfigs.Count)
        {
            return;
        }

        _footerConfigs[index] = (text is not null || aggregator is not null)
            ? new FooterConfig(text, aggregator)
            : null;
    }

    /// <summary>
    /// 重算并刷新页脚行各列文本。遍历当前 <see cref="ItemsSource"/> 按列
    /// <see cref="FooterConfig.Aggregator"/> 求值(或静态 <see cref="FooterConfig.Text"/>),
    /// 推给 <see cref="AdvancedGridFooterRow"/>。无页脚列时为空操作。
    /// 调用时机:列变更(ColumnsChanged)、ItemsSource 变更(OnItemsChanged)后;调用方在数据行变更后应主动调。
    /// </summary>
    public void UpdateFooter()
    {
        if (!HasFooter)
        {
            return;
        }

        int colCount = _core.Columns.Count;
        if (colCount == 0)
        {
            return;
        }

        // 收集当前所有行(供 aggregator)。ISelectableItemsView 提供 Count/GetItem。
        var items = _core.ItemsSource;
        var rows = new List<object?>(items.Count);
        for (int i = 0; i < items.Count; i++)
        {
            rows.Add(items.GetItem(i));
        }

        var texts = new string[colCount];
        for (int i = 0; i < colCount; i++)
        {
            var cfg = i < _footerConfigs.Count ? _footerConfigs[i] : null;
            if (cfg is null)
            {
                texts[i] = string.Empty;
            }
            else if (cfg.Text is not null)
            {
                texts[i] = cfg.Text;
            }
            else if (cfg.Aggregator is not null)
            {
                texts[i] = cfg.Aggregator(rows) ?? string.Empty;
            }
            else
            {
                texts[i] = string.Empty;
            }
        }

        _footer.SetColumnCount(colCount);
        _footer.SetFooterValues(texts);
    }

    protected override bool VisitScrollChildren(Func<Element, bool> visitor)
    {
        if (!visitor(_header))
        {
            return false;
        }

        if (!visitor(_scrollViewer))
        {
            return false;
        }

        if (HasFooter && !visitor(_footer))
        {
            return false;
        }

        // Phase 2:固定列 overlay 纳入子树遍历(在 scrollViewer 之后,确保画在上层覆盖中间列)。
        if (_leftFrozen != null && _frozenLeft > 0 && !visitor(_leftFrozen))
        {
            return false;
        }

        if (_rightFrozen != null && _frozenRight > 0 && !visitor(_rightFrozen))
        {
            return false;
        }

        return true;
    }

    protected override Size MeasureContent(Size availableSize)
    {
        var dpiScale = GetDpi() / 96.0;
        var borderInset = GetBorderVisualInset();

        double widthLimit = double.IsPositiveInfinity(availableSize.Width)
            ? double.PositiveInfinity
            : Math.Max(0, availableSize.Width - Padding.HorizontalThickness - borderInset * 2);

        _columnsExtentWidth = 0;
        for (int i = 0; i < _core.Columns.Count; i++)
        {
            _columnsExtentWidth += Math.Max(0, _core.Columns[i].Width);
        }

        double contentWidth = double.IsPositiveInfinity(widthLimit)
            ? _columnsExtentWidth
            : Math.Min(_columnsExtentWidth, widthLimit);

        // Phase 2 固定列:左右固定列总宽(钉视口,不滚动)。中间区宽 = contentWidth - leftW - rightW。
        double leftW = HasFrozenColumns ? FrozenLeftWidth : 0;
        double rightW = HasFrozenColumns ? FrozenRightWidth : 0;
        double middleWidth = HasFrozenColumns
            ? Math.Max(0, contentWidth - leftW - rightW)
            : contentWidth;
        // 中间 extent = 中间列总宽(固定列不计入,只滚中间)。
        double middleExtent = HasFrozenColumns
            ? Math.Max(0, _columnsExtentWidth - leftW - rightW)
            : _columnsExtentWidth;

        double headerH = ResolveHeaderHeight();
        double footerH = ResolveFooterHeight();
        double rowH = ResolveRowHeight();
        double alignedRowH = GetPixelAlignedRowHeight();

        int count = _core.ItemsSource.Count;
        _rowsExtentHeight = count > 0 && alignedRowH > 0 ? count * alignedRowH : 0;

        double desiredRowsHeight;
        if (double.IsPositiveInfinity(availableSize.Height))
        {
            desiredRowsHeight = _rowsExtentHeight <= 0 ? 0 : Math.Min(_rowsExtentHeight, MaxAutoViewportHeight);
        }
        else
        {
            desiredRowsHeight = Math.Max(0, availableSize.Height - headerH - footerH - Padding.VerticalThickness - borderInset * 2);
        }

        _presenter.ItemHeightHint = rowH;
        // 中间 presenter 的 extent 只含中间列(固定列不滚)。
        _presenter.ExtentWidth = middleExtent;

        _header.HorizontalOffset = _scrollViewer.HorizontalOffset;
        _header.Measure(new Size(Math.Max(0, contentWidth), headerH));

        if (HasFooter)
        {
            _footer.HorizontalOffset = _scrollViewer.HorizontalOffset;
            _footer.Measure(new Size(Math.Max(0, contentWidth), footerH));
        }

        _scrollViewer.Measure(new Size(
            double.IsPositiveInfinity(middleWidth) ? double.PositiveInfinity : Math.Max(0, middleWidth),
            double.IsPositiveInfinity(desiredRowsHeight) ? double.PositiveInfinity : Math.Max(0, desiredRowsHeight)));

        // Phase 2:固定列 overlay measure(宽=固定列总宽,高=行区高)。
        if (_leftFrozen != null && _frozenLeft > 0)
        {
            _leftFrozen.Measure(new Size(Math.Max(0, leftW), Math.Max(0, desiredRowsHeight)));
            _leftFrozen.BindRows(ItemBindingGeneration, alignedRowH);
        }
        if (_rightFrozen != null && _frozenRight > 0)
        {
            _rightFrozen.Measure(new Size(Math.Max(0, rightW), Math.Max(0, desiredRowsHeight)));
            _rightFrozen.BindRows(ItemBindingGeneration, alignedRowH);
        }

        var desired = new Size(Math.Max(0, contentWidth), Math.Max(0, headerH + desiredRowsHeight + footerH));
        return desired
            .Inflate(Padding)
            .Inflate(new Thickness(borderInset));
    }

    protected override void ArrangeContent(Rect bounds)
    {
        var dpiScale = GetDpi() / 96.0;
        var borderInset = GetBorderVisualInset();

        var snapped = GetSnappedBorderBounds(bounds);
        var innerBounds = snapped.Deflate(new Thickness(borderInset));
        var contentBounds = innerBounds.Deflate(Padding);

        double headerH = ResolveHeaderHeight();
        double footerH = ResolveFooterHeight();

        // Phase 2 固定列:左右固定列总宽。
        double leftW = HasFrozenColumns ? FrozenLeftWidth : 0;
        double rightW = HasFrozenColumns ? FrozenRightWidth : 0;
        double middleWidth = Math.Max(0, contentBounds.Width - leftW - rightW);

        // _rowsViewportWidth 用于水平滚动判定;固定列时取中间区宽(只中间列滚)。
        _rowsViewportWidth = HasFrozenColumns
            ? LayoutRounding.RoundToPixel(Math.Max(0, middleWidth), dpiScale)
            : LayoutRounding.RoundToPixel(Math.Max(0, contentBounds.Width), dpiScale);
        _rowsViewportHeight = LayoutRounding.RoundToPixel(Math.Max(0, contentBounds.Height - headerH - footerH), dpiScale);

        _header.HorizontalOffset = _scrollViewer.HorizontalOffset;
        _header.Arrange(new Rect(contentBounds.X, contentBounds.Y, Math.Max(0, contentBounds.Width), headerH));

        // 中间 presenter 视口:从固定左列右沿起,宽=中间宽。
        double middleX = contentBounds.X + (HasFrozenColumns ? leftW : 0);
        var rowsViewport = new Rect(
            middleX,
            contentBounds.Y + headerH,
            Math.Max(0, HasFrozenColumns ? middleWidth : contentBounds.Width),
            Math.Max(0, contentBounds.Height - headerH - footerH));

        // 在 scroll viewer arrange presenter 之前应用 rebind hint,使本帧 arrange 生效。
        // 之后设 _scrollViewer.Arrange 会把 rebind 推到下一帧,产生一帧闪烁:行的陈旧 _rowIndex 与
        // 刚切换的 SelectedIndex 不一致(可见为选择释放又重新附加)。
        _presenter.ItemBindingGeneration = ItemBindingGeneration;

        _scrollViewer.Arrange(rowsViewport);

        // Phase 2:固定列 overlay arrange。左:贴 contentBounds.X,宽=leftW;右:贴 contentBounds.Right-rightW,宽=rightW。
        // overlay 的 arrange Rect 经 SnapViewportRectToPixels(与中间 presenter 同口径),offset 经 RoundToPixel,
        // 避免 fractional DPI 下 overlay 行与中间行差 1px。
        double rowsH = Math.Max(0, contentBounds.Height - headerH - footerH);
        double alignedOffsetY = LayoutRounding.RoundToPixel(_scrollViewer.VerticalOffset, dpiScale);
        if (_leftFrozen != null && _frozenLeft > 0)
        {
            var paneRect = LayoutRounding.SnapViewportRectToPixels(
                new Rect(contentBounds.X, contentBounds.Y + headerH, Math.Max(0, leftW), rowsH), dpiScale);
            _leftFrozen.SetVerticalOffset(alignedOffsetY);
            _leftFrozen.BindRows(ItemBindingGeneration, GetPixelAlignedRowHeight());
            _leftFrozen.Arrange(paneRect);
        }
        if (_rightFrozen != null && _frozenRight > 0)
        {
            var paneRect = LayoutRounding.SnapViewportRectToPixels(
                new Rect(contentBounds.Right - rightW, contentBounds.Y + headerH, Math.Max(0, rightW), rowsH), dpiScale);
            _rightFrozen.SetVerticalOffset(alignedOffsetY);
            _rightFrozen.BindRows(ItemBindingGeneration, GetPixelAlignedRowHeight());
            _rightFrozen.Arrange(paneRect);
        }

        // Phase 2:页脚固定在行区下方,不参与垂直滚动(合计行始终可见在底部)。
        if (HasFooter)
        {
            _footer.HorizontalOffset = _scrollViewer.HorizontalOffset;
            _footer.Arrange(new Rect(
                contentBounds.X,
                contentBounds.Y + headerH + rowsViewport.Height,
                Math.Max(0, contentBounds.Width),
                footerH));
        }

        if (TryConsumeScrollIntoViewRequest(out var request))
        {
            if (request.Kind == ScrollIntoViewRequestKind.Selected)
            {
                ScrollSelectedIntoView();
            }
            else if (request.Kind == ScrollIntoViewRequestKind.Index)
            {
                ScrollIntoView(request.Index);
            }
        }
    }

    protected override void OnRender(IGraphicsContext context)
    {
        var bounds = GetSnappedBorderBounds(Bounds);
        var bg = GetValue(BackgroundProperty);
        var borderColor = GetValue(BorderBrushProperty);
        DrawBackgroundAndBorder(context, bounds, bg, borderColor, BorderThickness, CornerRadius);
    }

    protected override void RenderSubtree(IGraphicsContext context)
    {
        var bounds = GetSnappedBorderBounds(Bounds);
        var dpiScale = GetDpi() / 96.0;
        var borderInset = GetBorderVisualInset();

        var contentBounds = bounds
            .Deflate(new Thickness(borderInset))
            .Deflate(Padding);

        var clipRect = LayoutRounding.MakeClipRect(contentBounds, dpiScale);
        var clipRadius = Math.Max(0, LayoutRounding.RoundToPixel(CornerRadius, dpiScale) - borderInset);
        clipRadius = Math.Min(clipRadius, Math.Min(clipRect.Width, clipRect.Height) / 2);

        context.Save();
        if (clipRadius > 0)
        {
            context.SetClipRoundedRect(clipRect, clipRadius, clipRadius);
        }
        else
        {
            context.SetClip(clipRect);
        }

        try
        {
            _header.Render(context);
            _scrollViewer.Render(context);
            if (HasFooter)
            {
                _footer.Render(context);
            }
            // Phase 2:固定列 overlay 画在最上层,覆盖中间列的水平滚动内容(钉视口左/右)。
            if (_frozenLeft > 0) _leftFrozen?.Render(context);
            if (_frozenRight > 0) _rightFrozen?.Render(context);
        }
        finally
        {
            context.Restore();
        }
    }

    protected override UIElement? OnHitTest(Point point)
    {
        if (!IsVisible || !IsHitTestVisible || !IsEffectivelyEnabled)
        {
            return null;
        }

        // 重要:因滚动,子元素可能被 arrange 到本控件 bounds 外。命中测试须裁剪到控件 bounds,
        // 避免裁剪内容上的"幽灵"输入。
        if (!Bounds.Contains(point))
        {
            return null;
        }

        // Phase 2:固定列 overlay 命中优先(它在 scrollViewer 之上,且视口左/右两侧)。
        // 必须在 scrollViewer 之前:scrollViewer 视口已缩到中间宽,固定列区域点其 HitTest 会返回
        // scrollViewer 自身(非 null),若先判 scrollViewer 会抢走固定列点击。
        if (_frozenLeft > 0)
        {
            var leftHit = _leftFrozen?.HitTest(point);
            if (leftHit != null)
            {
                return leftHit;
            }
        }
        if (_frozenRight > 0)
        {
            var rightHit = _rightFrozen?.HitTest(point);
            if (rightHit != null)
            {
                return rightHit;
            }
        }

        var scrollHit = _scrollViewer.HitTest(point);
        if (scrollHit != null)
        {
            return scrollHit;
        }

        var headerHit = _header.HitTest(point);
        if (headerHit != null)
        {
            return headerHit;
        }

        return Bounds.Contains(point) ? this : null;
    }

    private void BeforeRowRender(IGraphicsContext context, int index, Rect itemRect)
    {
        if (!ZebraStriping)
        {
            return;
        }

        if ((index & 1) == 1)
        {
            var theme = Theme;
            var snapped = LayoutRounding.SnapViewportRectToPixels(itemRect, GetDpi() / 96.0);
            context.FillRectangle(snapped, theme.Palette.ControlBackground.Lerp(theme.Palette.ButtonFace, theme.IsDark ? 0.45 : 0.33));
        }
    }

    private double ComputeColumnsExtentWidth()
    {
        double total = 0;
        for (int i = 0; i < _core.Columns.Count; i++)
        {
            total += Math.Max(0, _core.Columns[i].Width);
        }
        return total;
    }

    private void BindRowTemplate(FrameworkElement element, object? item, int index, TemplateContext _)
    {
        var row = (AdvancedGridRow)element;
        // Phase 2 固定列:中间 presenter 的行只渲染中间列范围 [MiddleColumnStart, +MiddleColumnCount)。
        // 无固定列时 MiddleColumnStart=0/MiddleColumnCount=全部,行为如旧(AdvancedGridRow 把 0 当全部)。
        row.ColumnStart = MiddleColumnStart;
        row.ColumnCount = MiddleColumnCount;
        row.EnsureDpi(GetDpi());
        row.EnsureColumns(_core.Columns, _core.ColumnsVersion);
        row.EnsureTheme(ThemeInternal);
        row.Bind(item, index);
    }

    private void OnItemsChanged(ItemsChange change)
    {
        _presenter.ItemsSource = _core.ItemsSource;
        // presenter 内部处理 Add/Remove/Replace(重映射已实现索引、更新高度缓存与偏移)。仅对 Reset 强制全回收,
        // Reset 表示整体集合变更。
        if (change.Kind == ItemsChangeKind.Reset)
        {
            _presenter.RecycleAll();
            // 整体交换会重置底层 view 的模式;重新应用控件级设置。
            _core.SelectionMode = SelectionMode;
        }

        // 仅当变更可能移动已实现行索引或其底层数据时,强制重绑可见行。
        // 纯末尾追加(Insert 且 Index == 之前 count)不影响任何已实现行,此时触发重绑会造成不必要的
        // 单元格上下文重置-视觉上每个追加项的选择/hover 闪一下。
        int newCount = _core.ItemsSource.Count;
        int oldCount = newCount - (change.Kind == ItemsChangeKind.Add ? change.Count
                                  : change.Kind == ItemsChangeKind.Remove ? -change.Count
                                  : 0);
        bool needsRebind = change.Kind switch
        {
            ItemsChangeKind.Reset => true,
            ItemsChangeKind.Move => true,
            ItemsChangeKind.Replace => true,
            ItemsChangeKind.Remove => true,
            ItemsChangeKind.Add => change.Index < oldCount,  // 末尾追加时 false
            _ => true
        };
        if (needsRebind)
        {
            InvalidateItemBindings();
        }
        // 集合变更可能在不触发 SelectionChanged 的情况下清空/移动选择(如 SetItems 在已 -1 的 view 上重置为 -1);
        // 重新同步使可绑定属性不偏离 core。
        _selection.SyncFromModel();
        UpdateFooter(); // Phase 2:数据行变更后重算页脚(合计)。
        // Phase 2:固定列 overlay 行数与中间行同步。
        ConfigureFrozenPanes();
        InvalidateMeasure();
        InvalidateArrange();
        InvalidateVisual();
    }

    private void InitializePresenter(FixedHeightItemsPresenter presenter)
    {
        presenter.ItemTemplate = _rowTemplate;
        presenter.ItemsSource = _core.ItemsSource;
        presenter.BeforeItemRender = BeforeRowRender;
        presenter.UseHorizontalExtentForLayout = true;
        // 变高虚拟化在 INCC 突发(锚点上方插入/删除)与重测后会请求滚动偏移修正。不订阅则事件被丢弃,
        // ScrollViewer 陈旧偏移在下一次 arrange 被推回 presenter,导致可见跳动。
        presenter.OffsetCorrectionRequested += OnPresenterOffsetCorrectionRequested;
    }

    private void OnPresenterOffsetCorrectionRequested(Point offset)
        => _scrollViewer.SetScrollOffsets(_scrollViewer.HorizontalOffset, offset.Y);

    private void OnItemsSelectionChanged()
    {
        _selection.SyncFromModel();
        SelectionChanged?.Invoke(_core.SelectedItem);
        InvalidateItemBindings();
        ScrollSelectedIntoView();
        // Phase 2:固定列 overlay 行读 IsItemSelectedCore,选中变化要触发其重绘。
        _leftFrozen?.InvalidateRows();
        _rightFrozen?.InvalidateRows();
        InvalidateVisual();
    }

    private void OnSelectedIndexPropertyChanged(int newIndex) => _selection.PushIndex(newIndex);

    private void OnSelectedItemPropertyChanged(object? item) => _selection.PushItem(item);

    private void ScrollSelectedIntoView()
        => ScrollIntoView(SelectedIndex);

    public void ScrollIntoView(int index)
    {
        int count = _core.ItemsSource.Count;
        if (index < 0 || index >= count)
        {
            return;
        }

        double viewport = _rowsViewportHeight;
        if (viewport <= 0 || double.IsNaN(viewport) || double.IsInfinity(viewport))
        {
            RequestScrollIntoView(ScrollIntoViewRequest.IndexRequest(index));
            return;
        }

        // 优先用 presenter 自己的 y 范围(它知道真实(已测)项边界,如 VariableHeightItemsPresenter 的前缀和)。
        // 仅当 presenter 无法提供范围(如变高模式下尚未实现的项)时回退定高数学。
        double oldOffset = _scrollViewer.VerticalOffset;
        if (_presenter.TryGetItemYRange(index, out double itemTop, out double itemBottom))
        {
            double itemH = Math.Max(1, itemBottom - itemTop);
            double newOffset = ItemsViewportMath.ComputeScrollOffsetToBringItemRangeIntoView(itemTop, itemH, viewport, oldOffset);
            if (!newOffset.Equals(oldOffset))
            {
                _scrollViewer.SetScrollOffsets(_scrollViewer.HorizontalOffset, newOffset);
            }
            return;
        }

        // 未测项-委托 presenter 估算、滚动、再在项实现后细化。(视口未布局时也走此路径。)
        _presenter.RequestScrollIntoView(index);
    }

    private static UIElement? FindNextFocusableInContainer(FrameworkElement container, UIElement current, bool forward)
    {
        var focusable = new List<UIElement>();
        CollectFocusableIn(container, focusable);
        int idx = focusable.IndexOf(current);
        if (idx < 0)
        {
            return null;
        }

        int next = forward ? idx + 1 : idx - 1;
        return next >= 0 && next < focusable.Count ? focusable[next] : null;
    }

    private static void CollectFocusableIn(Element? element, List<UIElement> result)
    {
        if (element is UIElement ui && ui.Focusable && ui.IsEffectivelyEnabled && ui.IsVisible)
        {
            result.Add(ui);
        }

        if (element is IVisualTreeHost host)
        {
            host.VisitChildren(child =>
            {
                CollectFocusableIn(child, result);
                return true;
            });
        }
    }

    private bool IsItemInViewport(int index)
    {
        double rowH = GetPixelAlignedRowHeight();
        if (rowH <= 0 || _rowsViewportHeight <= 0)
        {
            return false;
        }

        double itemTop = index * rowH;
        double itemBottom = itemTop + rowH;
        double offset = _scrollViewer.VerticalOffset;
        return itemBottom > offset && itemTop < offset + _rowsViewportHeight;
    }

    private double ResolveRowHeight()
    {
        if (!double.IsNaN(RowHeight) && RowHeight > 0)
        {
            return RowHeight;
        }

        return Math.Max(Theme.Metrics.BaseControlHeight, 24);
    }

    // 与 FixedHeightItemsPresenter 视觉布局一致的像素对齐行高。使命中测试、extent、视口查询与渲染行一致
    // (尤在 125% DPI 下 24 DIP 行每行圆整为 24.8 DIP)。
    private double GetPixelAlignedRowHeight()
    {
        double rowH = ResolveRowHeight();
        if (rowH <= 0 || double.IsNaN(rowH) || double.IsInfinity(rowH))
        {
            return 0;
        }

        return LayoutRounding.RoundToPixel(rowH, GetDpi() / 96.0);
    }

    private double ResolveHeaderHeight()
    {
        // 单行表头高(也作分组行/列名行各自的高)。
        double single = !double.IsNaN(HeaderHeight) && HeaderHeight > 0
            ? HeaderHeight
            : Math.Max(Theme.Metrics.BaseControlHeight, 24);

        // Phase 2:有复杂表头分组时渲染两行(分组行 + 列名行)。
        return HasHeaderGroups ? single * 2 : single;
    }

    // ---- internal 桥接:供 AdvancedGridHeaderRow / AdvancedGridRow(同程序集友元类)访问内部状态 ----
    // 原 GridView 的 HeaderRow/Row 是 private 嵌套类,可直接读外层私有字段;拆为独立类后经这些成员桥接。

    /// <summary>Core 的列定义(只读视图),供表头/行布局与渲染读取列宽。</summary>
    internal IReadOnlyList<GridView.GridViewCore.ColumnDefinition> CoreColumns => _core.Columns;

    /// <summary>设置某列宽度(拖拽调宽),经 Core 并触发失效。</summary>
    internal void SetColumnWidthCore(int index, double width) => _core.SetColumnWidth(index, width);

    /// <summary>
    /// 列宽变化(拖拽调宽)后重配固定列 overlay 与表头/页脚的固定段宽度。
    /// <see cref="GridView.GridViewCore.SetColumnWidth"/> 不触发 ColumnsChanged,故拖拽调宽路径需显式调本方法
    /// 同步 FrozenLeftWidth/RightWidth 缓存(否则 overlay 宽度、右固定贴边、表头/页脚分段竖线用旧宽错位)。
    /// </summary>
    internal void OnColumnWidthChangedCore()
    {
        if (!HasFrozenColumns)
        {
            return;
        }

        ConfigureFrozenPanes();
        InvalidateMeasure();
        InvalidateArrange();
        InvalidateVisual();
    }

    /// <summary>项绑定失效(等价 GridView.InvalidateGridItemBindings,后者是 private)。</summary>
    internal void InvalidateGridItemBindingsCore() => InvalidateItemBindings();

    /// <summary>返回行 <paramref name="index"/> 是否选中(经 Core),供行渲染读选中态。</summary>
    internal bool IsItemSelectedCore(int index) => _core.IsItemSelected(index);

    /// <summary>尝试取变高 presenter 以调 InvalidateHeights(定高时返 false)。</summary>
    internal bool TryGetVariableHeightPresenter([NotNullWhen(true)] out VariableHeightItemsPresenter? presenter)
    {
        presenter = null;
        return false;
    }
}

// Phase 2:页脚列配置(从 AdvancedGridColumn 提取,因 GridViewCore.ColumnDefinition 不含 footer 字段)。
// Text 优先(静态,如首列"合计");Aggregator 次之(动态求和等,接收所有行返回文本)。
internal sealed class FooterConfig
{
    public string? Text { get; }
    public Func<IReadOnlyList<object?>, string>? Aggregator { get; }

    public FooterConfig(string? text, Func<IReadOnlyList<object?>, string>? aggregator)
    {
        Text = text;
        Aggregator = aggregator;
    }
}

// Phase 2:复杂表头(合并表头)分组。跨 [StartColumn, StartColumn+ColumnSpan) 的连续列。
// 由调用方按 complexHeader.childColumns 匹配列 id 算出(JNPF 设计器约束 childColumns 对应相邻列)。
// Align 为分组文字对齐(left/center/right),与子列各自对齐无关。
public readonly record struct HeaderGroup(string Label, int StartColumn, int ColumnSpan, string Align = "center");
