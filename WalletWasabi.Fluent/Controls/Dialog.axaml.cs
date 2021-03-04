using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace WalletWasabi.Fluent.Controls
{
	/// <summary>
	/// A simple overlay Dialog control.
	/// </summary>
	public class Dialog : ContentControl
	{
		public static readonly StyledProperty<bool> IsDialogOpenProperty =
			AvaloniaProperty.Register<Dialog, bool>(nameof(IsDialogOpen));

		public static readonly StyledProperty<bool> EnableCancelOnPressedProperty =
			AvaloniaProperty.Register<Dialog, bool>(nameof(EnableCancelOnPressed));

		public static readonly StyledProperty<bool> EnableCancelOnEscapeProperty =
			AvaloniaProperty.Register<Dialog, bool>(nameof(EnableCancelOnEscape));

		public static readonly StyledProperty<double> MaxContentHeightProperty =
			AvaloniaProperty.Register<Dialog, double>(nameof(MaxContentHeight), double.PositiveInfinity);

		public static readonly StyledProperty<double> MaxContentWidthProperty =
			AvaloniaProperty.Register<Dialog, double>(nameof(MaxContentWidth), double.PositiveInfinity);

		public static readonly StyledProperty<double> MinContentHeightProperty =
			AvaloniaProperty.Register<Dialog, double>(nameof(MinContentHeight));

		public static readonly StyledProperty<double> MinContentWidthProperty =
			AvaloniaProperty.Register<Dialog, double>(nameof(MinContentWidth));

		public bool IsDialogOpen
		{
			get => GetValue(IsDialogOpenProperty);
			set => SetValue(IsDialogOpenProperty, value);
		}

		public bool EnableCancelOnPressed
		{
			get => GetValue(EnableCancelOnPressedProperty);
			set => SetValue(EnableCancelOnPressedProperty, value);
		}

		public bool EnableCancelOnEscape
		{
			get => GetValue(EnableCancelOnEscapeProperty);
			set => SetValue(EnableCancelOnEscapeProperty, value);
		}

		public double MaxContentHeight
		{
			get => GetValue(MaxContentHeightProperty);
			set => SetValue(MaxContentHeightProperty, value);
		}

		public double MaxContentWidth
		{
			get => GetValue(MaxContentWidthProperty);
			set => SetValue(MaxContentWidthProperty, value);
		}

		public double MinContentHeight
		{
			get => GetValue(MaxContentHeightProperty);
			set => SetValue(MaxContentHeightProperty, value);
		}

		public double MinContentWidth
		{
			get => GetValue(MaxContentWidthProperty);
			set => SetValue(MaxContentWidthProperty, value);
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
			overlayButton.PointerPressed += (_, _) =>
			{
				if (EnableCancelOnPressed)
				{
					Close();
				}
			};

			if (this.GetVisualRoot() is TopLevel topLevel)
			{
				topLevel.AddHandler(KeyDownEvent, CancelKeyDown, RoutingStrategies.Tunnel);
			}
		}

		private void Close()
		{
			IsDialogOpen = false;
		}

		private void CancelKeyDown(object? sender, KeyEventArgs e)
		{
			if (e.Key == Key.Escape && EnableCancelOnEscape)
			{
				Close();
			}
		}
	}
}
