namespace Aprillz.MewUI.Controls;

// NOTE: 本文件是 OvenNet 项目为扩展 MewUI GridView 而新增的控件,放在 MewUI 程序集内以复用
// internal 基础设施(GridViewCore/FixedHeightItemsPresenter/SelectionSync 等)。不修改任何已有 MewUI 文件。
// Phase 1: 与 GridViewColumn<TItem> 字段完全一致,保证调用方零改动迁移。
// Phase 2: 将在此扩展 IsFrozen(固定列)/HeaderRowSpan/HeaderColSpan(合并表头)/FooterTemplate(页脚)。

/// <summary>
/// <see cref="AdvancedGridView"/> 的列定义。Phase 1 与 <see cref="GridViewColumn{TItem}"/> 字段一致;
/// Phase 2 将扩展固定列 / 合并表头 / 页脚字段。
/// </summary>
public sealed class AdvancedGridColumn<TItem>
{
    public string Header { get; set; } = string.Empty;

    public double Width { get; set; }

    /// <summary>拖拽调整列宽时的下限。默认 0(无下限)。</summary>
    public double MinWidth { get; set; }

    /// <summary>是否可拖拽表头分隔条调整列宽。默认 true。</summary>
    public bool IsResizable { get; set; } = true;

    public IDataTemplate<TItem>? CellTemplate { get; set; }

    // ---- Phase 2:页脚行 ----

    /// <summary>该列页脚单元格的静态文本(如首列"合计")。优先于 <see cref="FooterAggregator"/>。
    /// 为 null 且 <see cref="FooterAggregator"/> 亦为 null 时,该列页脚空白。</summary>
    public string? FooterText { get; set; }

    /// <summary>该列页脚单元格的动态聚合:接收当前所有行,返回页脚显示文本(如求和后按精度格式化)。
    /// 仅当 <see cref="FooterText"/> 为 null 时生效。控件层不解释行结构,聚合逻辑完全由调用方决定
    /// (保持 UI 无关:控件不引用调用方的 DataRow/字段名等概念)。</summary>
    public Func<IReadOnlyList<object?>, string>? FooterAggregator { get; set; }

    /// <summary>该列是否参与页脚(有 <see cref="FooterText"/> 或 <see cref="FooterAggregator"/>)。</summary>
    internal bool HasFooter => FooterText is not null || FooterAggregator is not null;
}
