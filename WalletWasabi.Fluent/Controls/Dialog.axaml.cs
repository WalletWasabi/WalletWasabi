using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
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
		private Panel? _dismissPanel;
		private bool _canCancelOnPointerPressed = false;

		public static readonly StyledProperty<bool> IsDialogOpenProperty =
			AvaloniaProperty.Register<Dialog, bool>(nameof(IsDialogOpen));

		public static readonly StyledProperty<bool> IsBusyProperty =
			AvaloniaProperty.Register<Dialog, bool>(nameof(IsBusy));

		public static readonly StyledProperty<bool> IsBackEnabledProperty =
			AvaloniaProperty.Register<Dialog, bool>(nameof(IsBackEnabled));

		public static readonly StyledProperty<bool> IsCancelEnabledProperty =
			AvaloniaProperty.Register<Dialog, bool>(nameof(IsCancelEnabled));

		public static readonly StyledProperty<bool> EnableCancelOnPressedProperty =
			AvaloniaProperty.Register<Dialog, bool>(nameof(EnableCancelOnPressed));

		public static readonly StyledProperty<bool> EnableCancelOnEscapeProperty =
			AvaloniaProperty.Register<Dialog, bool>(nameof(EnableCancelOnEscape));

		public static readonly StyledProperty<double> MaxContentHeightProperty =
			AvaloniaProperty.Register<Dialog, double>(nameof(MaxContentHeight), double.PositiveInfinity);

		public static readonly StyledProperty<double> MaxContentWidthProperty =
			AvaloniaProperty.Register<Dialog, double>(nameof(MaxContentWidth), double.PositiveInfinity);

		public bool IsDialogOpen
		{
			get => GetValue(IsDialogOpenProperty);
			set => SetValue(IsDialogOpenProperty, value);
		}

		public bool IsBusy
		{
			get => GetValue(IsBusyProperty);
			set => SetValue(IsBusyProperty, value);
		}

		public bool IsBackEnabled
		{
			get => GetValue(IsBackEnabledProperty);
			set => SetValue(IsBackEnabledProperty, value);
		}

		public bool IsCancelEnabled
		{
			get => GetValue(IsCancelEnabledProperty);
			set => SetValue(IsCancelEnabledProperty, value);
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

		private CancellationTokenSource CancelPointerPressedDelay { get; set; }

		public Dialog()
		{
			this.GetObservable(IsDialogOpenProperty).Subscribe(UpdateDelay);
		}

		private void UpdateDelay(bool isDialogOpen)
		{
			_canCancelOnPointerPressed = false;
			CancelPointerPressedDelay?.Cancel();

			if (isDialogOpen)
			{
				CancelPointerPressedDelay = new CancellationTokenSource();

				Task.Delay(TimeSpan.FromSeconds(1), CancelPointerPressedDelay.Token).ContinueWith(_ =>
				{
					_canCancelOnPointerPressed = true;
				});
			}
		}

		protected override void OnPropertyChanged<T>(AvaloniaPropertyChangedEventArgs<T> change)
		{
			base.OnPropertyChanged(change);

			if (change.Property == IsDialogOpenProperty)
			{
				PseudoClasses.Set(":open", change.NewValue.GetValueOrDefault<bool>());
			}

			if (change.Property == IsBusyProperty)
			{
				PseudoClasses.Set(":busy", change.NewValue.GetValueOrDefault<bool>());
			}
		}

		protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
		{
			base.OnApplyTemplate(e);

			_dismissPanel = e.NameScope.Find<Panel>("PART_Dismiss");

			if (this.GetVisualRoot() is TopLevel topLevel)
			{
				topLevel.AddHandler(PointerPressedEvent, CancelPointerPressed, RoutingStrategies.Tunnel);
				topLevel.AddHandler(KeyDownEvent, CancelKeyDown, RoutingStrategies.Tunnel);
			}
		}

		private void Close()
		{
			IsDialogOpen = false;
		}

		private void CancelPointerPressed(object? sender, PointerPressedEventArgs e)
		{
			if (IsDialogOpen && EnableCancelOnPressed && !IsBusy && IsCancelEnabled && _dismissPanel is not null && _canCancelOnPointerPressed)
			{
				var point = e.GetPosition(_dismissPanel);
				if (!_dismissPanel.Bounds.Contains(point))
				{
					Close();
				}
			}
		}

		private void CancelKeyDown(object? sender, KeyEventArgs e)
		{
			if (e.Key == Key.Escape && EnableCancelOnEscape && !IsBusy && IsCancelEnabled)
			{
				Close();
			}
		}
	}
}
