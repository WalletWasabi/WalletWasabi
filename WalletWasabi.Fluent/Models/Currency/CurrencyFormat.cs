using System.Linq;
using ReactiveUI;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Userfacing;

namespace WalletWasabi.Fluent.Models.Currency;

/// <summary>
/// Represents a specific Currency's Parsing and Formatting rules.
/// </summary>
public partial class CurrencyFormat : ReactiveObject
{
	public const string DecimalSeparator = ".";

	public static readonly CurrencyFormat Btc = new()
	{
		CurrencyCode = "BTC",
		IsApproximate = false,
		DefaultWatermark = "0 BTC",
		MaxFractionalDigits = 8,
		MaxIntegralDigits = 8,
		MaxLength = 20,
	};

	public static readonly CurrencyFormat Usd = new()
	{
		CurrencyCode = "USD",
		IsApproximate = true,
		DefaultWatermark = "0.00 USD",
		MaxFractionalDigits = 2,
		MaxIntegralDigits = 12,
		MaxLength = 20,
	};

	public static readonly CurrencyFormat SatsvByte = new()
	{
		CurrencyCode = "sat/vByte",
		IsApproximate = false,
		MaxFractionalDigits = 2,
		MaxIntegralDigits = 8,
		MaxLength = 20,
	};

	[AutoNotify] private string _text = "";
	[AutoNotify] private string _formattedText = "";
	[AutoNotify] private int? _decimalSeparatorPosition;
	[AutoNotify] private int? _integralLength;
	[AutoNotify] private int? _fractionalLength;
	[AutoNotify] private CurrencyValue _value = CurrencyValue.EmptyValue;

	public string CurrencyCode { get; init; }
	public bool IsApproximate { get; init; }
	public int? MaxIntegralDigits { get; init; }
	public int? MaxFractionalDigits { get; init; }
	public int? MaxLength { get; init; }
	public string? DefaultWatermark { get; set; }

	public int InsertPosition { get; private set; }
	public int? SelectionStart { get; set; }
	public int? SelectionEnd { get; set; }

	public bool HasSelection => SelectionEnd is { } && SelectionEnd is { };

	public string GroupSeparator { get; set; } = " ";

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

		if (MaxIntegralDigits is { } maxInt && IntegralLength > maxInt)
		{
			return new CurrencyValue.Invalid();
		}

		if (MaxFractionalDigits is { } maxFract && FractionalLength > maxFract)
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

		if (MaxIntegralDigits is { } mInt)
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

		if (MaxFractionalDigits is { } mFrac)
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
			var maxIntegral = MaxIntegralDigits ?? 10;
			var maxFractional = MaxFractionalDigits ?? 2;

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
}

public abstract record CurrencyValue
{
	public static readonly Empty EmptyValue = new();

	public static CurrencyValue FromBtc(Amount? amount)
	{
		return amount switch
		{
			null => EmptyValue,
			Amount x when x == Amount.Invalid => new Invalid(),
			Amount x => new Valid(x.Btc.ToDecimal(NBitcoin.MoneyUnit.BTC))
		};
	}

	public static CurrencyValue FromUsd(Amount? amount)
	{
		return amount switch
		{
			null => EmptyValue,
			Amount x when x == Amount.Invalid => new Invalid(),
			Amount x => new Valid(x.UsdValue)
		};
	}

	public record Valid(decimal Value) : CurrencyValue;

	public record Invalid : CurrencyValue;

	public record Empty : CurrencyValue;
}
