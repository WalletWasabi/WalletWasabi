using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace WalletWasabi.Fluent.Controls
{
    /// <summary>
    /// Manages and hosts dialogs when it's bound to <see cref="IContent"/> objects.
    /// </summary>
    public class Dialog : ContentControl
    {
        public static readonly StyledProperty<bool> IsDialogOpenProperty =
            AvaloniaProperty.Register<Dialog, bool>(nameof(IsDialogOpen));

        public bool IsDialogOpen
        {
            get => GetValue(IsDialogOpenProperty);
            set => SetValue(IsDialogOpenProperty, value);
        }

        protected override void OnPropertyChanged<T>(AvaloniaPropertyChangedEventArgs<T> change)
        {
	        base.OnPropertyChanged(change);

	        if (change.Property == IsDialogOpenProperty)
	        {
		        PseudoClasses.Set(":open", change.NewValue.GetValueOrDefault<bool>());
	        }
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            var overlayButton = e.NameScope.Find<Panel>("PART_Overlay");
            overlayButton.PointerPressed += (_, __) => IsDialogOpen = false;
        }
    }
}
