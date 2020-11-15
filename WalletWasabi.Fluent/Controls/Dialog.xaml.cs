using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace WalletWasabi.Fluent.Controls
{
	/// <summary>
	/// A simple overlay Dialog control.
	/// </summary>
	public class Dialog : ContentControl
	{
		public static readonly StyledProperty<bool> IsDialogOpenProperty =
			AvaloniaProperty.Register<Dialog, bool>(nameof(IsDialogOpen));

		public static readonly StyledProperty<double> MaxContentHeightProperty =
			AvaloniaProperty.Register<Dialog, double>(nameof(MaxContentHeight), double.PositiveInfinity);

		public static readonly StyledProperty<double> MaxContentWidthProperty =
			AvaloniaProperty.Register<Dialog, double>(nameof(MaxContentWidth), double.PositiveInfinity);

		public bool IsDialogOpen
		{
			get => GetValue(IsDialogOpenProperty);
			set => SetValue(IsDialogOpenProperty, value);
		}

		public double MaxContentHeight
		{
			get { return GetValue(MaxContentHeightProperty); }
			set { SetValue(MaxContentHeightProperty, value); }
		}

		public double MaxContentWidth
		{
			get { return GetValue(MaxContentWidthProperty); }
			set { SetValue(MaxContentWidthProperty, value); }
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