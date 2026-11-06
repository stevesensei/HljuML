using System;
using Avalonia;
using Avalonia.Controls;

namespace HljuML.App.Controls;

/// <summary>
/// Places each visible child in the shortest available group of adjacent columns.
/// </summary>
public sealed class MasonryPanel : Panel
{
    public static readonly StyledProperty<double> MinimumColumnWidthProperty =
        AvaloniaProperty.Register<MasonryPanel, double>(
            nameof(MinimumColumnWidth), 260, validate: width => double.IsFinite(width) && width > 0);

    public static readonly StyledProperty<int> MaximumColumnsProperty =
        AvaloniaProperty.Register<MasonryPanel, int>(
            nameof(MaximumColumns), 3, validate: columns => columns > 0);

    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<MasonryPanel, double>(
            nameof(Spacing), 16, validate: spacing => double.IsFinite(spacing) && spacing >= 0);

    public static readonly AttachedProperty<int> ColumnSpanProperty =
        AvaloniaProperty.RegisterAttached<MasonryPanel, Control, int>(
            "ColumnSpan", 1, validate: span => span > 0);

    static MasonryPanel()
    {
        AffectsMeasure<MasonryPanel>(MinimumColumnWidthProperty, MaximumColumnsProperty, SpacingProperty);
        AffectsParentMeasure<MasonryPanel>(ColumnSpanProperty);
    }

    public double MinimumColumnWidth
    {
        get => GetValue(MinimumColumnWidthProperty);
        set => SetValue(MinimumColumnWidthProperty, value);
    }

    public int MaximumColumns
    {
        get => GetValue(MaximumColumnsProperty);
        set => SetValue(MaximumColumnsProperty, value);
    }

    public double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    public static int GetColumnSpan(Control control) => control.GetValue(ColumnSpanProperty);

    public static void SetColumnSpan(Control control, int span) => control.SetValue(ColumnSpanProperty, span);

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : MinimumColumnWidth;
        return new Size(width, LayoutChildren(width, arrange: false));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        LayoutChildren(finalSize.Width, arrange: true);
        return finalSize;
    }

    private double LayoutChildren(double width, bool arrange)
    {
        var spacing = Spacing;
        var minimumWidth = MinimumColumnWidth;
        var extraColumns = Math.Floor(Math.Max(0, width - minimumWidth) / (minimumWidth + spacing));
        var columnCount = 1 + (int)Math.Min(MaximumColumns - 1, extraColumns);
        var columnWidth = (width - spacing * (columnCount - 1)) / columnCount;
        var nextTop = new double[columnCount];
        var height = 0d;

        foreach (var child in Children)
        {
            if (!child.IsVisible)
            {
                continue;
            }

            var span = Math.Min(GetColumnSpan(child), columnCount);
            var (column, top) = FindShortestRun(nextTop, span);
            var childWidth = columnWidth * span + spacing * (span - 1);
            child.Measure(new Size(childWidth, double.PositiveInfinity));

            var childHeight = child.DesiredSize.Height;
            if (arrange)
            {
                var left = column * (columnWidth + spacing);
                child.Arrange(new Rect(left, top, childWidth, childHeight));
            }

            var bottom = top + childHeight;
            Array.Fill(nextTop, bottom + spacing, column, span);
            height = Math.Max(height, bottom);
        }

        return height;
    }

    private static (int Column, double Top) FindShortestRun(double[] nextTop, int span)
    {
        var bestColumn = 0;
        var bestTop = double.PositiveInfinity;

        for (var start = 0; start <= nextTop.Length - span; start++)
        {
            var top = nextTop[start];
            for (var column = start + 1; column < start + span; column++)
            {
                top = Math.Max(top, nextTop[column]);
            }

            if (top < bestTop)
            {
                bestColumn = start;
                bestTop = top;
            }
        }

        return (bestColumn, bestTop);
    }
}
