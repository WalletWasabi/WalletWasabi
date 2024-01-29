using ReactiveUI;
using System.Linq;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models.Currency;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send.CurrencyConversion;

public partial class CurrencyInputViewModel : ViewModelBase
{
	public const string DecimalSeparator = ".";

	[AutoNotify] private decimal _maxValue;
	[AutoNotify] private decimal _minSuggestionValue;
	[AutoNotify] private string _text = "";
	[AutoNotify] private string _formattedText = "";
	[AutoNotify] private int? _decimalSeparatorPosition;
	[AutoNotify] private int? _integralLength;
	[AutoNotify] private int? _fractionalLength;
	[AutoNotify] private CurrencyValue _value = CurrencyValue.EmptyValue;

	public CurrencyInputViewModel(UiContext uiContext, IWalletModel wallet, CurrencyFormat currencyFormat, bool enableClipboardSuggestion = false)
	{
		CurrencyFormat = currencyFormat;
		if (enableClipboardSuggestion)
		{
			Suggestion = new CurrencyInputClipboardListener(uiContext, wallet, this);
		}
	}

	public CurrencyFormat CurrencyFormat { get; }

	public CurrencyInputClipboardListener? Suggestion { get; }

	public int InsertPosition { get; private set; }
	public int? SelectionStart { get; set; }
	public int? SelectionEnd { get; set; }

	public bool HasSelection => SelectionEnd is { } && SelectionEnd is { };

	public string GroupSeparator { get; init; } = " ";

	public void Insert(string text)
	{
		// Ignore allowed input rules if there was a selection.
		var hadSelection = HasSelection;

		if (HasSelection)
		{
			RemoveSelection();
		}

		var left = Text[..InsertPosition];

		var right =
			InsertPosition < Text.Length
			? Text[InsertPosition..]
			: "";

		if (!hadSelection)
		{
			text = GetAllowedInput(text);
		}

		Text = left + text + right;

		InsertPosition += text.Length;

		ClearSelection();

		SetDecimalSeparatorPosition();
	}

	public void InsertDecimalSeparator()
	{
		if (HasSelection)
		{
			RemoveSelection();
		}

		// step-through decimal separator
		if (DecimalSeparatorPosition is { } dsp && InsertPosition == dsp)
		{
			InsertPosition++;
			this.RaisePropertyChanged(nameof(InsertPosition));
		}
		if (DecimalSeparatorPosition is null)
		{
			// Allow user to start input with "."
			if (Text == "")
			{
				Insert("0.");
			}
			else
			{
				Insert(".");
			}

			SetDecimalSeparatorPosition();
		}
	}

	public void InsertRaw(string? text)
	{
		text ??= "";

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
		text = text.Replace(",", DecimalSeparator);
		text = text.Replace("٫", DecimalSeparator);
		text = text.Replace("٬", DecimalSeparator);
		text = text.Replace("⎖", DecimalSeparator);
		text = text.Replace("·", DecimalSeparator);
		text = text.Replace("'", DecimalSeparator);

		// Prevent inserting multiple '.'
		if (Regex.Matches(text, @"\.").Count > 1)
		{
			return;
		}

		if (HasSelection)
		{
			RemoveSelection();
		}

		// do not allow pasting decimal separator if it already exists
		if (Text.Contains(DecimalSeparator) && text.Contains(DecimalSeparator))
		{
			return;
		}

		Insert(text);
	}

	public void RemoveNext()
	{
		if (HasSelection)
		{
			RemoveSelection();
		}
		else if (InsertPosition < Text.Length)
		{
			var left = Text[..InsertPosition];
			var right =
				InsertPosition < Text.Length
				? Text[(InsertPosition + 1)..]
				: "";

			Text = left + right;

			SkipGroupSeparator(true);

			SetDecimalSeparatorPosition();
		}
	}

	public void RemovePrevious()
	{
		if (HasSelection)
		{
			RemoveSelection();
		}
		else if (InsertPosition > 0)
		{
			InsertPosition--;
			SkipGroupSeparator(false);

			var left = Text[..InsertPosition];
			var right =
				InsertPosition < Text.Length
				? Text[(InsertPosition + 1)..]
				: "";

			Text = left + right;

			SetDecimalSeparatorPosition();
		}
	}

	public void MoveBack(bool enableSelection)
	{
		var moveInsertPosition = !HasSelection || (HasSelection && enableSelection) || (HasSelection && !enableSelection && SelectionStart != InsertPosition);

		if (HasSelection && !enableSelection)
		{
			ClearSelection();
		}

		if (InsertPosition == 0)
		{
			return;
		}

		if (HasSelection && enableSelection)
		{
			if (SelectionStart == InsertPosition)
			{
				SelectionStart--;
			}
			else if (SelectionEnd == InsertPosition)
			{
				SelectionEnd--;
			}
		}
		else if (enableSelection)
		{
			SelectionStart = InsertPosition - 1;
			SelectionEnd = InsertPosition;
		}

		if (moveInsertPosition)
		{
			InsertPosition--;
		}

		if (SelectionStart == InsertPosition && SelectionEnd == InsertPosition)
		{
			ClearSelection();
		}

		if (InsertPosition == SelectionEnd && Text.Substring(InsertPosition - 1, 1) == GroupSeparator)
		{
			MoveBack(enableSelection);
		}
		else if (InsertPosition == SelectionStart && Text.Substring(InsertPosition, 1) == GroupSeparator)
		{
			MoveBack(enableSelection);
		}
		else if (!enableSelection && moveInsertPosition)
		{
			SkipGroupSeparator(false);
		}

		this.RaisePropertyChanged(nameof(InsertPosition));
		this.RaisePropertyChanged(nameof(SelectionStart));
		this.RaisePropertyChanged(nameof(SelectionEnd));
	}

	public void MoveForward(bool enableSelection)
	{
		var moveInsertPosition = !HasSelection || (HasSelection && enableSelection) || (HasSelection && !enableSelection && SelectionEnd != InsertPosition);

		if (HasSelection && !enableSelection)
		{
			ClearSelection();
		}

		if (InsertPosition >= Text.Length)
		{
			return;
		}

		if (HasSelection && enableSelection)
		{
			if (SelectionEnd == InsertPosition)
			{
				SelectionEnd++;
			}
			else if (SelectionStart == InsertPosition)
			{
				SelectionStart++;
			}
		}
		else if (enableSelection)
		{
			SelectionStart = InsertPosition;
			SelectionEnd = InsertPosition + 1;
		}

		if (moveInsertPosition)
		{
			InsertPosition++;
		}

		if (SelectionStart == InsertPosition && SelectionEnd == InsertPosition)
		{
			ClearSelection();
		}

		if (InsertPosition == SelectionEnd && Text.Substring(InsertPosition - 1, 1) == GroupSeparator)
		{
			MoveForward(enableSelection);
		}
		else if (InsertPosition == SelectionStart && Text.Substring(InsertPosition, 1) == GroupSeparator)
		{
			MoveForward(enableSelection);
		}
		else if (!enableSelection && moveInsertPosition)
		{
			SkipGroupSeparator(true);
		}

		this.RaisePropertyChanged(nameof(InsertPosition));
		this.RaisePropertyChanged(nameof(SelectionStart));
		this.RaisePropertyChanged(nameof(SelectionEnd));
	}

	public void MoveToStart(bool enableSelection) => MoveTo(0, enableSelection);

	public void MoveToEnd(bool enableSelection) => MoveTo(Text.Length, enableSelection);

	public void MoveTo(int position, bool enableSelection)
	{
		if (HasSelection && !enableSelection)
		{
			ClearSelection();
		}

		if (position >= 0 && position <= Text.Length)
		{
			var selectionStart = InsertPosition;
			var selectionEnd = InsertPosition;

			if (enableSelection)
			{
				if (InsertPosition == SelectionEnd && SelectionStart > position)
				{
					selectionEnd = SelectionStart ?? InsertPosition;
					selectionStart = position;
				}
				else if (InsertPosition == SelectionStart && SelectionEnd < position)
				{
					selectionStart = SelectionEnd ?? InsertPosition;
					selectionEnd = position;
				}
				else
				{
					selectionStart = Math.Min(position, SelectionStart ?? InsertPosition);
					selectionEnd = Math.Max(position, SelectionEnd ?? InsertPosition);
				}
			}

			InsertPosition = position;

			if (enableSelection)
			{
				SelectionStart = selectionStart;
				SelectionEnd = selectionEnd;
			}
		}

		SkipGroupSeparator(false);

		this.RaisePropertyChanged(nameof(InsertPosition));
		this.RaisePropertyChanged(nameof(SelectionStart));
		this.RaisePropertyChanged(nameof(SelectionEnd));
	}

	public void RemoveSelection()
	{
		if (SelectionStart is null || SelectionEnd is null)
		{
			return;
		}

		var left = Text[..SelectionStart.Value];
		var right = Text[SelectionEnd.Value..];

		Text = left + right;

		InsertPosition = SelectionStart.Value;
		this.RaisePropertyChanging(nameof(InsertPosition));

		ClearSelection();

		SetDecimalSeparatorPosition();
	}

	public void ClearSelection()
	{
		SelectionStart = null;
		SelectionEnd = null;

		this.RaisePropertyChanged(nameof(SelectionStart));
		this.RaisePropertyChanged(nameof(SelectionEnd));
	}

	public void SetInsertPosition(int value)
	{
		InsertPosition = value;
		this.RaisePropertyChanged(nameof(InsertPosition));
	}

	public void SetSelection(int start, int end)
	{
		var actualStart = Math.Min(start, end);
		var actualEnd = Math.Max(start, end);

		SelectionStart = actualStart;
		SelectionEnd = actualEnd;

		this.RaisePropertyChanged(nameof(SelectionStart));
		this.RaisePropertyChanged(nameof(SelectionEnd));
	}

	public void SetValue(CurrencyValue value)
	{
		Value = value;
		if (value is CurrencyValue.Empty or CurrencyValue.Invalid)
		{
			Text = "";
		}
		else if (value is CurrencyValue.Valid v)
		{
			var hasFractional = Math.Floor(v.Value) != v.Value;
			var maxIntegral = CurrencyFormat.MaxIntegralDigits ?? 10;
			var maxFractional = CurrencyFormat.MaxFractionalDigits ?? 2;

			var format = new string('#', maxIntegral);
			if (hasFractional)
			{
				format += DecimalSeparator;
				format += new string('#', maxFractional);
				var formatted = v.Value.ToString(format);
				if (formatted.StartsWith(DecimalSeparator))
				{
					formatted = "0" + formatted;
				}
				Text = formatted;
			}
		}

		SetDecimalSeparatorPosition();
	}

	private void SetDecimalSeparatorPosition()
	{
		DecimalSeparatorPosition =
			Text.Contains(DecimalSeparator)
			? Text.IndexOf(DecimalSeparator)
			: null;

		IntegralLength =
			DecimalSeparatorPosition is { } dsp
			? Text[..dsp].Replace(GroupSeparator, "").Length
			: Text.Replace(GroupSeparator, "").Length;

		FractionalLength =
			DecimalSeparatorPosition is { } ds
			? Text[(ds + 1)..].Replace(GroupSeparator, "").Length
			: null;

		Value = GetValue();

		Format();
	}

	private CurrencyValue GetValue()
	{
		var text = Text.Replace(GroupSeparator, "");

		if (string.IsNullOrEmpty(text))
		{
			return CurrencyValue.EmptyValue;
		}

		if (text.StartsWith(DecimalSeparator))
		{
			return new CurrencyValue.Invalid();
		}

		if (CurrencyFormat.MaxIntegralDigits is { } maxInt && IntegralLength > maxInt)
		{
			return new CurrencyValue.Invalid();
		}

		if (CurrencyFormat.MaxFractionalDigits is { } maxFract && FractionalLength > maxFract)
		{
			return new CurrencyValue.Invalid();
		}

		if (!decimal.TryParse(text, out var value))
		{
			return new CurrencyValue.Invalid();
		}

		return new CurrencyValue.Valid(value);
	}

	private void Format()
	{
		var currentSpacesBeforeCaret =
			Text.Take(InsertPosition)
				.Count(x => new string(x, 1) == GroupSeparator);

		if (Value is not CurrencyValue.Valid)
		{
			FormattedText = Text.Replace(GroupSeparator, "");
		}
		else
		{
			var unformattedText = Text.Replace(GroupSeparator, "");

			var integralText = unformattedText[..(IntegralLength ?? Text.Length)];

			var formattedText =
				string.Join(GroupSeparator,
					integralText.Reverse()
								.Chunk(3)
								.Reverse()
								.Select(x => new string(x.Reverse().ToArray())));

			if (unformattedText.Contains(DecimalSeparator))
			{
				formattedText += DecimalSeparator;

				var fractionalStart = unformattedText.IndexOf(DecimalSeparator) + 1;
				var fractionalText = unformattedText[fractionalStart..];

				var formattedFractionalText =
					string.Join(GroupSeparator,
					fractionalText.Chunk(4)
					.Select(x => new string(x)));

				formattedText += formattedFractionalText;
			}

			FormattedText = formattedText;
		}

		var newSpacesBeforeCaret =
			FormattedText.Take(InsertPosition)
					 .Count(x => new string(x, 1) == GroupSeparator);

		var newCaretPosition = InsertPosition + (newSpacesBeforeCaret - currentSpacesBeforeCaret);

		InsertPosition =
		   Math.Clamp(newCaretPosition, 0, FormattedText.Length);

		Text = FormattedText;

		SkipGroupSeparator(true);

		this.RaisePropertyChanged(nameof(InsertPosition));
	}

	private void SkipGroupSeparator(bool moveForward)
	{
		var add = moveForward ? 1 : -1;

		// Don't let me stand on top of the space
		if (InsertPosition < Text.Length && Text.Substring(InsertPosition, 1) == GroupSeparator)
		{
			InsertPosition += add;
		}
	}

	private string GetAllowedInput(string input)
	{
		if (input.Contains(DecimalSeparator) && DecimalSeparatorPosition is null)
		{
			return input;
		}

		var unformattedText = Text.Replace(GroupSeparator, "");
		int? unformattedDecimalSeparatorPosition =
			unformattedText.Contains(DecimalSeparator)
			? unformattedText.IndexOf(DecimalSeparator)
			: null;

		if (CurrencyFormat.MaxIntegralDigits is { } mInt)
		{
			// validate max integral digits when no dot
			if (unformattedDecimalSeparatorPosition is null)
			{
				var allowedLength = Math.Max(0, mInt - unformattedText.Length);
				allowedLength = Math.Min(allowedLength, input.Length);
				return input[..allowedLength];
			}
			else if (InsertPosition <= DecimalSeparatorPosition)
			{
				var allowedLength = Math.Max(0, mInt - unformattedDecimalSeparatorPosition.Value);
				allowedLength = Math.Min(allowedLength, input.Length);
				return input[..allowedLength];
			}
		}

		if (CurrencyFormat.MaxFractionalDigits is { } mFrac)
		{
			if (DecimalSeparatorPosition is { } dsp && InsertPosition > dsp && unformattedDecimalSeparatorPosition is { })
			{
				var allowedLength = Math.Max(0, mFrac - (unformattedText.Length - (unformattedDecimalSeparatorPosition.Value + 1)));
				allowedLength = Math.Min(allowedLength, input.Length);
				return input[..allowedLength];
			}
		}

		return input;
	}
}
