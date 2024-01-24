using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models.Currency;

namespace WalletWasabi.Fluent.Controls;

public partial class CurrencyEntryBox : TextBox
{
	public static readonly StyledProperty<CurrencyValue> ValueProperty =
		AvaloniaProperty.Register<CurrencyEntryBox, CurrencyValue>(nameof(Value), defaultValue: CurrencyValue.EmptyValue, enableDataValidation: true);

	public static readonly StyledProperty<CurrencyFormat> CurrencyFormatProperty =
		AvaloniaProperty.Register<CurrencyEntryBox, CurrencyFormat>(nameof(CurrencyFormat), defaultValue: CurrencyFormat.Btc);

	public static readonly StyledProperty<decimal?> MaxValueProperty =
		AvaloniaProperty.Register<CurrencyEntryBox, decimal?>(nameof(MaxValue));

	public static readonly StyledProperty<string?> ClipboardSuggestionProperty =
		AvaloniaProperty.Register<CurrencyEntryBox, string?>(nameof(ClipboardSuggestion), defaultBindingMode: BindingMode.TwoWay);

	private CompositeDisposable _disposables = new();
	private bool _isUpdating;
	private bool _isUpdatingSelection;

	public CurrencyEntryBox()
	{
		SetCurrentValue(TextProperty, "");

		AddHandler(KeyDownEvent, CustomOnKeyDown, RoutingStrategies.Tunnel);

		this.GetObservable(CurrencyFormatProperty)
			.Do(cf =>
			{
				_disposables.Dispose();
				_disposables = new();

				if (CurrencyFormat is not { })
				{
					return;
				}

				CurrencyFormat.WhenAnyValue(x => x.InsertPosition)
							  .BindTo(this, x => x.CaretIndex)
							  .DisposeWith(_disposables);

				CurrencyFormat.WhenAnyValue(x => x.Text)
							  .BindTo(this, x => x.Text)
							  .DisposeWith(_disposables);

				CurrencyFormat.WhenAnyValue(x => x.Value)
							  .Do(v => SetCurrentValue(ValueProperty, v))
							  .Subscribe()
							  .DisposeWith(_disposables);

				CurrencyFormat.WhenAnyValue(x => x.SelectionStart, x => x.SelectionEnd)
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
						CurrencyFormat?.SetInsertPosition(x);
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

						CurrencyFormat.SetSelection(t.First, t.Second);

						_isUpdatingSelection = false;
					})
					.Subscribe()
					.DisposeWith(_disposables);
			})
			.Subscribe();

		// Handle copying full text to the clipboard
		Observable.FromEventPattern<RoutedEventArgs>(this, nameof(CopyingToClipboard))
			  .Select(x => x.EventArgs)
			  .Where(_ => CurrencyFormat?.Value is { })
			  .Where(_ => SelectedText == Text)
			  .DoAsync(OnCopyingFullTextToClipboardAsync)
			  .Subscribe();

		// Handle pasting full text from clipboard
		Observable.FromEventPattern<RoutedEventArgs>(this, nameof(PastingFromClipboard))
			.Select(x => x.EventArgs)
			.Where(_ => string.IsNullOrWhiteSpace(Text) || SelectedText == Text)
			.Throttle(TimeSpan.FromMilliseconds(50))
			.ObserveOn(RxApp.MainThreadScheduler)
			.Do(_ => SelectAll())
			.Subscribe();

		// Set MaxLength according to CurrencyFormat
		this.GetObservable(CurrencyFormatProperty)
			.WhereNotNull()
			.Select(x => x.MaxLength)
			.WhereNotNull()
			.Do(maxLength => SetCurrentValue(MaxLengthProperty, maxLength))
			.Subscribe();
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

	public decimal? MaxValue
	{
		get => GetValue(MaxValueProperty);
		set => SetValue(MaxValueProperty, value);
	}

	public string? ClipboardSuggestion
	{
		get => GetValue(ClipboardSuggestionProperty);
		set => SetValue(ClipboardSuggestionProperty, value);
	}

	protected override void UpdateDataValidation(AvaloniaProperty property, BindingValueType state, Exception? error)
	{
		if (property == ValueProperty)
		{
			DataValidationErrors.SetError(this, error);
		}
	}

	private void CustomOnKeyDown(object? sender, KeyEventArgs e)
	{
		_isUpdating = true;

		var enableSelection = e.KeyModifiers == KeyModifiers.Shift;

		var isPaste =
		  Application.Current?.PlatformSettings?.HotkeyConfiguration.Paste.Any(g => g.Matches(e)) ?? false;

		if (isPaste)
		{
			ModifiedPasteAsync();
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
				CurrencyFormat.Insert(input);
			}
			else if (e.Key == Key.Back)
			{
				CurrencyFormat.RemovePrevious();
			}
			else if (e.Key == Key.Delete)
			{
				CurrencyFormat.RemoveNext();
			}
			else if (e.Key == Key.Left)
			{
				CurrencyFormat.MoveBack(enableSelection);
			}
			else if (e.Key == Key.Right)
			{
				CurrencyFormat.MoveForward(enableSelection);
			}
			else if (e.Key == Key.Home)
			{
				CurrencyFormat.MoveToStart(enableSelection);
			}
			else if (e.Key == Key.End)
			{
				CurrencyFormat.MoveToEnd(enableSelection);
			}
			else if (e.Key is Key.OemPeriod or Key.OemComma or Key.Decimal)
			{
				CurrencyFormat.InsertDecimalSeparator();
			}
		}

		e.Handled = true;

		_isUpdating = false;
	}

	public async void ModifiedPasteAsync()
	{
		if (ApplicationHelper.Clipboard is not { } clipboard)
		{
			return;
		}

		if (CurrencyFormat is not { })
		{
			return;
		}

		var text = await clipboard.GetTextAsync();

		if (string.IsNullOrEmpty(text))
		{
			return;
		}

		text = text.Replace("\r", "").Replace("\n", "").Trim();

		if (Regex.IsMatch(text, @"[^0-9,\.٫٬⎖·']"))
		{
			return;
		}

		// TODO: it is very hard to cover all cases for different localizations, including group and decimal separators.
		// We really need to leave that to the .NET runtime by removing invariant localization
		// and letting it decide what value does the text on the clipboard really represent

		// Correct amount
		Regex digitsOnly = new(@"[^\d.,٫٬⎖·\']");

		// Make it digits and .,٫٬⎖·\ only.
		text = digitsOnly.Replace(text, "");

		// https://en.wikipedia.org/wiki/Decimal_separator
		text = text.Replace(",", CurrencyFormat.DecimalSeparator);
		text = text.Replace("٫", CurrencyFormat.DecimalSeparator);
		text = text.Replace("٬", CurrencyFormat.DecimalSeparator);
		text = text.Replace("⎖", CurrencyFormat.DecimalSeparator);
		text = text.Replace("·", CurrencyFormat.DecimalSeparator);
		text = text.Replace("'", CurrencyFormat.DecimalSeparator);

		// Prevent inserting multiple '.'
		if (Regex.Matches(text, @"\.").Count > 1)
		{
			return;
		}

		CurrencyFormat.Insert(text);
	}

	/// <summary>
	/// Specialized copy to clipboard that copies the Value, formatted according to localization rules
	/// </summary>
	private async Task OnCopyingFullTextToClipboardAsync(RoutedEventArgs e)
	{
		if (ApplicationHelper.Clipboard is not { } clipboard || CurrencyFormat?.Value is not { } value)
		{
			return;
		}

		await clipboard.SetTextAsync(Text);

		e.Handled = true;
	}
}
