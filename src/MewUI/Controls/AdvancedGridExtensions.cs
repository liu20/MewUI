namespace Aprillz.MewUI.Controls;

// NOTE: 对照 GridViewExtensions,提供与 GridView 完全同名同签名的 fluent API,
// 使 OvenNet 调用方由 GridView 零改动迁移到 AdvancedGridView(仅改类型名 + using)。

/// <summary>
/// 配置 <see cref="AdvancedGridView"/> 与 <see cref="AdvancedGridColumn{TItem}"/> 的 fluent 扩展方法。
/// </summary>
public static class AdvancedGridExtensions
{
    /// <summary>
    /// 设置行高。
    /// </summary>
    public static AdvancedGridView RowHeight(this AdvancedGridView gridView, double rowHeight)
    {
        ArgumentNullException.ThrowIfNull(gridView);
        gridView.RowHeight = rowHeight;
        return gridView;
    }

    /// <summary>
    /// 设置表头行高。
    /// </summary>
    public static AdvancedGridView HeaderHeight(this AdvancedGridView gridView, double headerHeight)
    {
        ArgumentNullException.ThrowIfNull(gridView);
        gridView.HeaderHeight = headerHeight;
        return gridView;
    }

    /// <summary>
    /// 设置每个单元格的 padding。
    /// </summary>
    public static AdvancedGridView CellPadding(this AdvancedGridView gridView, Thickness cellPadding)
    {
        ArgumentNullException.ThrowIfNull(gridView);
        gridView.CellPadding = cellPadding;
        return gridView;
    }

    /// <summary>
    /// 设置自动尺寸视口的最大高度。
    /// </summary>
    public static AdvancedGridView MaxAutoViewportHeight(this AdvancedGridView gridView, double value)
    {
        gridView.MaxAutoViewportHeight = value;
        return gridView;
    }

    /// <summary>
    /// 设置选中行索引。
    /// </summary>
    public static AdvancedGridView SelectedIndex(this AdvancedGridView gridView, int value)
    {
        gridView.SelectedIndex = value;
        return gridView;
    }

    /// <summary>
    /// 添加选择变更 handler。
    /// </summary>
    public static AdvancedGridView OnSelectionChanged(this AdvancedGridView gridView, Action<object?> handler)
    {
        gridView.SelectionChanged += handler;
        return gridView;
    }

    /// <summary>
    /// 启用/禁用斑马纹。
    /// </summary>
    public static AdvancedGridView ZebraStriping(this AdvancedGridView gridView, bool enabled = true)
    {
        ArgumentNullException.ThrowIfNull(gridView);
        gridView.ZebraStriping = enabled;
        return gridView;
    }

    /// <summary>
    /// 启用/禁用网格线。
    /// </summary>
    public static AdvancedGridView ShowGridLines(this AdvancedGridView gridView, bool enabled = true)
    {
        ArgumentNullException.ThrowIfNull(gridView);
        gridView.ShowGridLines = enabled;
        return gridView;
    }

    /// <summary>
    /// 添加一列或多列。
    /// </summary>
    public static AdvancedGridView Columns<TItem>(this AdvancedGridView gridView, params AdvancedGridColumn<TItem>[] columns)
    {
        ArgumentNullException.ThrowIfNull(gridView);
        ArgumentNullException.ThrowIfNull(columns);
        gridView.AddColumns(columns);
        return gridView;
    }

    /// <summary>
    /// 从列表设置 items source。
    /// </summary>
    public static AdvancedGridView ItemsSource<TItem>(this AdvancedGridView gridView, IReadOnlyList<TItem> items)
    {
        ArgumentNullException.ThrowIfNull(gridView);
        ArgumentNullException.ThrowIfNull(items);
        gridView.ItemsSource = ItemsView.Create(items);
        return gridView;
    }

    /// <summary>
    /// 从 <see cref="ItemsView{T}"/> 设置 items source。
    /// </summary>
    public static AdvancedGridView ItemsSource<TItem>(this AdvancedGridView gridView, ItemsView<TItem> itemsView)
    {
        ArgumentNullException.ThrowIfNull(gridView);
        ArgumentNullException.ThrowIfNull(itemsView);
        gridView.ItemsSource = itemsView;
        return gridView;
    }

    /// <summary>
    /// 添加一列(模板版)。
    /// </summary>
    public static AdvancedGridView AddColumn<TItem>(
        this AdvancedGridView gridView,
        string header,
        double width,
        IDataTemplate<TItem> template,
        double minWidth = 0,
        bool resizable = true)
    {
        ArgumentNullException.ThrowIfNull(gridView);
        ArgumentNullException.ThrowIfNull(template);

        gridView.AddColumns(new AdvancedGridColumn<TItem>
        {
            Header = header ?? string.Empty,
            Width = width,
            CellTemplate = template,
            MinWidth = minWidth,
            IsResizable = resizable,
        });
        return gridView;
    }

    /// <summary>
    /// 添加一列(委托模板版)。
    /// </summary>
    public static AdvancedGridView AddColumn<TItem>(
        this AdvancedGridView gridView,
        string header,
        double width,
        Func<TemplateContext, FrameworkElement> build,
        Action<FrameworkElement, TItem, int, TemplateContext> bind,
        double minWidth = 0,
        bool resizable = true)
        => AddColumn(gridView, header, width, new DelegateTemplate<TItem>(build, bind), minWidth, resizable);

    public static AdvancedGridView AddColumn<TItem>(
        this AdvancedGridView gridView,
        string header,
        double width,
        Func<TemplateContext, FrameworkElement> build,
        Action<FrameworkElement, TItem, int, TemplateContext> bind,
        Action<FrameworkElement, TItem, int, TemplateContext> unbind,
        double minWidth = 0,
        bool resizable = true)
        => AddColumn(gridView, header, width, new DelegateTemplate<TItem>(build, bind, unbind), minWidth, resizable);

    /// <summary>
    /// 创建列定义。
    /// </summary>
    public static AdvancedGridColumn<TItem> Column<TItem>(
        string header,
        double width,
        IDataTemplate<TItem> template)
        => new AdvancedGridColumn<TItem> { Header = header ?? string.Empty, Width = width, CellTemplate = template };

    /// <summary>
    /// 创建列定义(委托模板版)。
    /// </summary>
    public static AdvancedGridColumn<TItem> Column<TItem>(
        string header,
        double width,
        Func<TemplateContext, FrameworkElement> build,
        Action<FrameworkElement, TItem, int, TemplateContext> bind,
        Action<FrameworkElement, TItem, int, TemplateContext>? unbind = null)
        => new AdvancedGridColumn<TItem> { Header = header ?? string.Empty, Width = width, CellTemplate = new DelegateTemplate<TItem>(build, bind, unbind) };

    /// <summary>
    /// 设置列表头文本。
    /// </summary>
    public static AdvancedGridColumn<TItem> Header<TItem>(this AdvancedGridColumn<TItem> column, string header)
    {
        ArgumentNullException.ThrowIfNull(column);
        column.Header = header ?? string.Empty;
        return column;
    }

    /// <summary>
    /// 设置列宽。
    /// </summary>
    public static AdvancedGridColumn<TItem> Width<TItem>(this AdvancedGridColumn<TItem> column, double width)
    {
        ArgumentNullException.ThrowIfNull(column);
        column.Width = width;
        return column;
    }

    /// <summary>
    /// 设置列最小宽度。
    /// </summary>
    public static AdvancedGridColumn<TItem> MinWidth<TItem>(this AdvancedGridColumn<TItem> column, double minWidth)
    {
        ArgumentNullException.ThrowIfNull(column);
        column.MinWidth = minWidth;
        return column;
    }

    /// <summary>
    /// 设置列是否可调整宽度。
    /// </summary>
    public static AdvancedGridColumn<TItem> Resizable<TItem>(this AdvancedGridColumn<TItem> column, bool resizable = true)
    {
        ArgumentNullException.ThrowIfNull(column);
        column.IsResizable = resizable;
        return column;
    }

    /// <summary>
    /// 设置列是否可调整宽度。
    /// </summary>
    public static AdvancedGridColumn<TItem> IsResizable<TItem>(
        this AdvancedGridColumn<TItem> column,
        bool value = true)
        => column.Resizable(value);

    /// <summary>
    /// 设置单元格模板。
    /// </summary>
    public static AdvancedGridColumn<TItem> CellTemplate<TItem>(
        this AdvancedGridColumn<TItem> column,
        IDataTemplate<TItem>? template)
    {
        column.CellTemplate = template;
        return column;
    }

    /// <summary>
    /// 设置单元格模板。
    /// </summary>
    public static AdvancedGridColumn<TItem> Bind<TItem>(
        this AdvancedGridColumn<TItem> column,
        IDataTemplate<TItem> template)
    {
        ArgumentNullException.ThrowIfNull(column);
        ArgumentNullException.ThrowIfNull(template);

        column.CellTemplate = template;
        return column;
    }

    /// <summary>
    /// 设置单元格模板。<see cref="Bind{TItem}(AdvancedGridColumn{TItem}, IDataTemplate{TItem})"/> 的别名。
    /// </summary>
    public static AdvancedGridColumn<TItem> Template<TItem>(
        this AdvancedGridColumn<TItem> column,
        IDataTemplate<TItem> template)
        => Bind(column, template);

    /// <summary>
    /// 设置单元格模板(委托模板版)。
    /// </summary>
    public static AdvancedGridColumn<TItem> Bind<TItem, TElement>(
        this AdvancedGridColumn<TItem> column,
        Func<TemplateContext, TElement> build,
        Action<TElement, TItem, int, TemplateContext> bind,
        Action<TElement, TItem, int, TemplateContext>? unbind = null) where TElement : FrameworkElement
        => Bind(
            column,
            new DelegateTemplate<TItem>(
                build,
                (view, item, index, context) => bind((TElement)view, item, index, context),
                unbind == null
                    ? null
                    : (view, item, index, context) => unbind((TElement)view, item, index, context)));

    /// <summary>
    /// 设置单元格模板(委托模板版,简化 bind)。
    /// </summary>
    public static AdvancedGridColumn<TItem> Bind<TItem, TElement>(
        this AdvancedGridColumn<TItem> column,
        Func<TemplateContext, TElement> build,
        Action<TElement, TItem> bind) where TElement : FrameworkElement
        => Bind(column, new DelegateTemplate<TItem>(build, (view, item, index, ctx) => bind((TElement)view, item)));

    /// <summary>
    /// 设置单元格模板(委托模板版)。
    /// </summary>
    public static AdvancedGridColumn<TItem> Template<TItem, TElement>(
        this AdvancedGridColumn<TItem> column,
        Func<TemplateContext, TElement> build,
        Action<TElement, TItem, int, TemplateContext> bind,
        Action<TElement, TItem, int, TemplateContext>? unbind = null) where TElement : FrameworkElement
        => Bind(column, build, bind, unbind);

    /// <summary>
    /// 设置单元格模板(委托模板版,简化 bind)。<see cref="Bind{TItem, TElement}(AdvancedGridColumn{TItem}, Func{TemplateContext, TElement}, Action{TElement, TItem})"/> 的别名。
    /// </summary>
    public static AdvancedGridColumn<TItem> Template<TItem, TElement>(
        this AdvancedGridColumn<TItem> column,
        Func<TemplateContext, TElement> build,
        Action<TElement, TItem> bind) where TElement : FrameworkElement
        => Bind(column, build, bind);

    /// <summary>
    /// 设置基于文本的单元格模板,显示 <paramref name="textSelector"/> 结果。
    /// </summary>
    public static AdvancedGridColumn<TItem> Text<TItem>(
        this AdvancedGridColumn<TItem> column,
        Func<TItem, string> textSelector)
    {
        ArgumentNullException.ThrowIfNull(column);
        ArgumentNullException.ThrowIfNull(textSelector);

        return Template(
            column,
            build: _ => new TextBlock().CenterVertical(),
            bind: (TextBlock tb, TItem item) => tb.Text = textSelector(item) ?? string.Empty);
    }
}
