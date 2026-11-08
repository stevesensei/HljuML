using Avalonia;
using Avalonia.Controls;
using TKCode.Arc;
using TKCode.Arc.DesignTokens;

namespace HljuML.App.Controls;

/// <summary>标题、说明和操作插槽；继承调用方的数据上下文，不承担业务状态。</summary>
public partial class PageHeader : UserControl
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<PageHeader, string?>(nameof(Title));

    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<PageHeader, string?>(nameof(Description));

    public static readonly StyledProperty<object?> ActionsProperty =
        AvaloniaProperty.Register<PageHeader, object?>(nameof(Actions));

    public static readonly StyledProperty<double> TitleFontSizeProperty =
        AvaloniaProperty.Register<PageHeader, double>(nameof(TitleFontSize));

    public static readonly DirectProperty<PageHeader, bool> HasDescriptionProperty =
        AvaloniaProperty.RegisterDirect<PageHeader, bool>(nameof(HasDescription), header => header.HasDescription);

    private bool _hasDescription;

    public PageHeader()
    {
        ArcTokens.Bind(this, TitleFontSizeProperty, ArcDesignTokenKind.Type2XL);
        InitializeComponent();
    }

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string? Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public object? Actions
    {
        get => GetValue(ActionsProperty);
        set => SetValue(ActionsProperty, value);
    }

    public double TitleFontSize
    {
        get => GetValue(TitleFontSizeProperty);
        set => SetValue(TitleFontSizeProperty, value);
    }

    public bool HasDescription => _hasDescription;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DescriptionProperty)
        {
            SetAndRaise(HasDescriptionProperty, ref _hasDescription, !string.IsNullOrEmpty(Description));
        }
    }
}