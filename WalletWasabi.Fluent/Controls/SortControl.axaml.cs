using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.Primitives;

namespace WalletWasabi.Fluent.Controls;

public class SortControl : TemplatedControl
{
    public static readonly StyledProperty<ICommand> StatusAscendingProperty = AvaloniaProperty.Register<SortControl, ICommand>(nameof(StatusAscending));
    public static readonly StyledProperty<ICommand> StatusDescendingProperty = AvaloniaProperty.Register<SortControl, ICommand>(nameof(StatusDescending));
    public static readonly StyledProperty<ICommand> DateAscendingProperty = AvaloniaProperty.Register<SortControl, ICommand>(nameof(DateAscending));
    public static readonly StyledProperty<ICommand> DateDescendingProperty = AvaloniaProperty.Register<SortControl, ICommand>(nameof(DateDescending));
    public static readonly StyledProperty<ICommand> LabelAscendingProperty = AvaloniaProperty.Register<SortControl, ICommand>(nameof(LabelAscending));
    public static readonly StyledProperty<ICommand> LabelDescendingProperty = AvaloniaProperty.Register<SortControl, ICommand>(nameof(LabelDescending));
    public static readonly StyledProperty<ICommand> AmountAscendingProperty = AvaloniaProperty.Register<SortControl, ICommand>(nameof(AmountAscending));
    public static readonly StyledProperty<ICommand> AmountDescendingProperty = AvaloniaProperty.Register<SortControl, ICommand>(nameof(AmountDescending));

    public ICommand AmountAscending
    {
        get => GetValue(AmountAscendingProperty);
        set => SetValue(AmountAscendingProperty, value);
    }

    public ICommand AmountDescending
    {
        get => GetValue(AmountDescendingProperty);
        set => SetValue(AmountDescendingProperty, value);
    }

    public ICommand StatusAscending
    {
        get => GetValue(StatusAscendingProperty);
        set => SetValue(StatusAscendingProperty, value);
    }

    public ICommand StatusDescending
    {
        get => GetValue(StatusDescendingProperty);
        set => SetValue(StatusDescendingProperty, value);
    }

    public ICommand DateAscending
    {
        get => GetValue(DateAscendingProperty);
        set => SetValue(DateAscendingProperty, value);
    }

    public ICommand DateDescending
    {
        get => GetValue(DateDescendingProperty);
        set => SetValue(DateDescendingProperty, value);
    }

    public ICommand LabelAscending
    {
        get => GetValue(LabelAscendingProperty);
        set => SetValue(LabelAscendingProperty, value);
    }

    public ICommand LabelDescending
    {
        get => GetValue(LabelDescendingProperty);
        set => SetValue(LabelDescendingProperty, value);
    }
}
