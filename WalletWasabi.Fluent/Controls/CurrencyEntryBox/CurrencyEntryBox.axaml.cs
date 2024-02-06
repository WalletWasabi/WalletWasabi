using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Layout;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.Xaml.Interactivity;
using ReactiveUI;
using WalletWasabi.Fluent.Behaviors;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Models.Currency;
using WalletWasabi.Fluent.ViewModels.Wallets.Send.CurrencyConversion;

namespace WalletWasabi.Fluent.Controls;

public partial class CurrencyEntryBox : TextBox
{
	public static readonly StyledProperty<CurrencyInputViewModel?> ViewModelProperty =
		AvaloniaProperty.Register<CurrencyEntryBox, CurrencyInputViewModel?>(nameof(ViewModel));

	public static readonly StyledProperty<CurrencyFormat> CurrencyFormatProperty =
		AvaloniaProperty.Register<CurrencyEntryBox, CurrencyFormat>(nameof(CurrencyFormat), defaultValue: CurrencyFormat.Btc);

	public static readonly StyledProperty<CurrencyValue> ValueProperty =
		AvaloniaProperty.Register<CurrencyEntryBox, CurrencyValue>(nameof(Value), enableDataValidation: true);

	// TODO: these would be better as attached properties of the Behavior
	public static readonly StyledProperty<string?> ClipboardSuggestionProperty =
		AvaloniaProperty.Register<CurrencyEntryBox, string?>(nameof(ClipboardSuggestion), defaultBindingMode: BindingMode.TwoWay);

	public static readonly StyledProperty<ICommand> ApplySuggestionCommandProperty =
		AvaloniaProperty.Register<CurrencyEntryBox, ICommand>(nameof(ApplySuggestionCommand));

	private CompositeDisposable _disposables = new();
	private bool _isUpdating;
	private bool _isUpdatingSelection;
	private bool _isUpdatingCaretIndex;

	public CurrencyEntryBox()
	{
		SetCurrentValue(TextProperty, "");

		AddHandler(KeyDownEvent, CustomOnKeyDown, RoutingStrategies.Tunnel);
		AddHandler(PointerMovedEvent, CustomPointerMove, RoutingStrategies.Tunnel);
		AddHandler(PointerPressedEvent, CustomPointerPressed, RoutingStrategies.Tunnel);

		this.GetObservable(ViewModelProperty)
			.Do(viewModel =>
			{
				_disposables.Dispose();
				_disposables = new();

				if (viewModel is not { })
				{
					return;
				}

				SetCurrentValue(CurrencyFormatProperty, viewModel.CurrencyFormat);

				viewModel.WhenAnyValue(x => x.InsertPosition)
						 .Subscribe(x =>
						 {
							 if (_isUpdatingCaretIndex)
							 {
								 return;
							 }

							 _isUpdatingCaretIndex = true;
							 Dispatcher.UIThread.Post(() => CaretIndex = x, DispatcherPriority.Input);
							 _isUpdatingCaretIndex = false;
						 })
						 .DisposeWith(_disposables);

				viewModel.WhenAnyValue(x => x.Text)
						 .Subscribe(x => Dispatcher.UIThread.Post(() => Text = x, DispatcherPriority.Input))
						 .DisposeWith(_disposables);

				viewModel.WhenAnyValue(x => x.Value)
						 .Do(v => SetCurrentValue(ValueProperty, v))
						 .Subscribe()
						 .DisposeWith(_disposables);

				viewModel.WhenAnyValue(x => x.SelectionStart, x => x.SelectionEnd)
						 .Where(_ => !_isUpdatingSelection)
						 .Do(t =>
						 {
							 var (start, end) = t;

							 if (start is null || end is null)
							 {
								 SelectionStart = CaretIndex;
								 SelectionEnd = CaretIndex;
							 }
							 else
							 {
								 SelectionStart = start.Value;
								 SelectionEnd = end.Value;
							 }
						 })
						 .Subscribe()
						 .DisposeWith(_disposables);

				this.GetObservable(CaretIndexProperty)
					.Do(x =>
					{
						if (_isUpdatingCaretIndex)
						{
							return;
						}

						_isUpdatingCaretIndex = true;
						viewModel.SetInsertPosition(x);
						_isUpdatingCaretIndex = false;
					})
					.Subscribe()
					.DisposeWith(_disposables);

				this.GetObservable(SelectionStartProperty)
					.CombineLatest(this.GetObservable(SelectionEndProperty))
					.Throttle(TimeSpan.FromMilliseconds(50), RxApp.MainThreadScheduler)
					.Do(t =>
					{
						if (_isUpdating || t.First == t.Second || CurrencyFormat is null)
						{
							return;
						}

						_isUpdatingSelection = true;

						viewModel.SetSelection(t.First, t.Second);

						_isUpdatingSelection = false;
					})
					.Subscribe()
					.DisposeWith(_disposables);

				this.GetObservable(IsFocusedProperty)
					.DistinctUntilChanged()
					.Do(f =>
					{
						if (f)
						{
							ClearSelection();
							SelectAll();
							viewModel.ClearSelection();
							viewModel.SelectAll();
						}
						else
						{
							ClearSelection();
							viewModel.ClearSelection();
						}
					})
					.Subscribe()
					.DisposeWith(_disposables);
			})
			.Subscribe();

		// Set MaxLength according to CurrencyFormat
		this.GetObservable(CurrencyFormatProperty)
			.WhereNotNull()
			.Select(x => x.MaxLength)
			.WhereNotNull()
			.Do(maxLength => SetCurrentValue(MaxLengthProperty, maxLength))
			.Subscribe();

		CustomCutCommand = ReactiveCommand.CreateFromTask(() => OnCutAsync());
		CustomCopyCommand = ReactiveCommand.CreateFromTask(() => OnCopyAsync());
		CustomPasteCommand = ReactiveCommand.CreateFromTask(() => OnPasteAsync());
	}

	public ICommand CustomCutCommand { get; }
	public ICommand CustomCopyCommand { get; }
	public ICommand CustomPasteCommand { get; }

	public CurrencyInputViewModel? ViewModel
	{
		get => GetValue(ViewModelProperty);
		set => SetValue(ViewModelProperty, value);
	}

	public CurrencyValue Value
	{
		get => GetValue(ValueProperty);
		set => SetValue(ValueProperty, value);
	}

	public CurrencyFormat CurrencyFormat
	{
		get => GetValue(CurrencyFormatProperty);
		set => SetValue(CurrencyFormatProperty, value);
	}

	public string? ClipboardSuggestion
	{
		get => GetValue(ClipboardSuggestionProperty);
		set => SetValue(ClipboardSuggestionProperty, value);
	}

	public ICommand ApplySuggestionCommand
	{
		get => GetValue(ApplySuggestionCommandProperty);
		set => SetValue(ApplySuggestionCommandProperty, value);
	}

	protected override void UpdateDataValidation(AvaloniaProperty property, BindingValueType state, Exception? error)
	{
		if (property == ValueProperty)
		{
			DataValidationErrors.SetError(this, error);
		}
	}

	protected override void OnTextInput(TextInputEventArgs e)
	{
		e.Handled = true;
	}

	private void CustomOnKeyDown(object? sender, KeyEventArgs e)
	{
		if (ViewModel is null)
		{
			return;
		}

		_isUpdating = true;

		var enableSelection = e.KeyModifiers == KeyModifiers.Shift;

		if (e.IsMatch(x => x.Paste))
		{
			_ = OnPasteAsync(e);
		}
		else if (e.IsMatch(x => x.Copy))
		{
			_ = OnCopyAsync(e);
		}
		else if (e.IsMatch(x => x.Cut))
		{
			_ = OnCutAsync(e);
		}
		else if (e.IsMatch(x => x.SelectAll))
		{
			ViewModel.SelectAll();
		}
		else if (e.Key is Key.Enter or Key.Tab)
		{
			// Let these pass through so it can be handled elsewhere

			_isUpdating = false;
			return;
		}
		else
		{
			var input =
				e.Key switch
				{
					Key.D0 or Key.NumPad0 => "0",
					Key.D1 or Key.NumPad1 => "1",
					Key.D2 or Key.NumPad2 => "2",
					Key.D3 or Key.NumPad3 => "3",
					Key.D4 or Key.NumPad4 => "4",
					Key.D5 or Key.NumPad5 => "5",
					Key.D6 or Key.NumPad6 => "6",
					Key.D7 or Key.NumPad7 => "7",
					Key.D8 or Key.NumPad8 => "8",
					Key.D9 or Key.NumPad9 => "9",
					_ => null
				};

			if (input is { })
			{
				ViewModel.Insert(input);
			}
			else if (e.Key == Key.Back)
			{
				ViewModel.RemovePrevious();
			}
			else if (e.Key == Key.Delete)
			{
				ViewModel.RemoveNext();
			}
			else if (e.Key == Key.Left)
			{
				ViewModel.MoveBack(enableSelection);
			}
			else if (e.Key == Key.Right)
			{
				ViewModel.MoveForward(enableSelection);
			}
			else if (e.Key == Key.Home)
			{
				ViewModel.MoveToStart(enableSelection);
			}
			else if (e.Key == Key.End)
			{
				ViewModel.MoveToEnd(enableSelection);
			}
			else if (e.Key is Key.OemPeriod or Key.OemComma or Key.Decimal)
			{
				ViewModel.InsertDecimalSeparator();
			}
		}

		e.Handled = true;

		_isUpdating = false;
	}

	private void CustomPointerMove(object? sender, PointerEventArgs e)
	{
		// TODO: Mouse selection as implemented in TextBox control is incompatible with CurrencyEntryBox.
		e.Handled = true;
	}

	private void CustomPointerPressed(object? sender, PointerPressedEventArgs e)
	{
		if (e.ClickCount == 2 && ViewModel is { })
		{
			ViewModel.SelectAll();
			e.Handled = true;
		}
	}

	public async Task OnPasteAsync(RoutedEventArgs? e = null)
	{
		if (ViewModel is { })
		{
			await ViewModel.PasteFromClipboardAsync();
		}

		if (e is { })
		{
			e.Handled = true;
		}
	}

	protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
	{
		base.OnApplyTemplate(e);

		var root = e.NameScope.Find<DockPanel>("Root");
		if (root is not { })
		{
			return;
		}

		var behavior = Interaction.GetBehaviors(root).OfType<FlyoutSuggestionBehavior>().FirstOrDefault();

		if (behavior is not { })
		{
			return;
		}

		this.GetObservable(HorizontalContentAlignmentProperty)
			.Do(hz =>
			{
				behavior.PlacementMode =
					hz switch
					{
						HorizontalAlignment.Left => PlacementMode.BottomEdgeAlignedLeft,
						HorizontalAlignment.Right => PlacementMode.BottomEdgeAlignedRight,
						_ => PlacementMode.Center
					};
			})
			.Subscribe();
	}


	private async Task OnCopyAsync(RoutedEventArgs? e = null)
	{
		if (ViewModel is { })
		{
			await ViewModel.CopySelectionToClipboardAsync();
		}

		if (e is { })
		{
			e.Handled = true;
		}
	}

	private async Task OnCutAsync(RoutedEventArgs? e = null)
	{
		if (ViewModel is { })
		{
			await ViewModel.CutSelectionToClipboardAsync();
		}

		if (e is { })
		{
			e.Handled = true;
		}
	}
}
