using System.Globalization;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using WalletWasabi.Fluent.Controls;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Helpers;

namespace WalletWasabi.Fluent.Behaviors;

public class AutoPasteAmountBehavior : AttachedToVisualTreeBehavior<DualCurrencyEntryBox>
{
	protected override void OnAttachedToVisualTree(CompositeDisposable disposable)
	{
		if (AssociatedObject is null)
		{
			return;
		}

		if (!Services.UiConfig.AutoPaste)
		{
			return;
		}

		AutoPaster(AssociatedObject, "PART_LeftEntryBox", IsValidBtc)
			.DisposeWith(disposable);

		AutoPaster(AssociatedObject, "PART_RightEntryBox", IsValidUsd)
			.DisposeWith(disposable);
	}

	private static bool IsValidUsd(string s)
	{
		if (!decimal.TryParse(s, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var d))
		{
			return false;
		}

		var hasValidDecimalPlaces = CountDecimalPlaces(d) <= 2;
		var isGreaterThanZero = d > 0;

		return hasValidDecimalPlaces && isGreaterThanZero;
	}

	private static bool IsValidBtc(string s)
	{
		if (!decimal.TryParse(s, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var d))
		{
			return false;
		}

		var lessThanMaximum = d < Constants.MaximumNumberOfBitcoins;
		var hasValidDecimalPlaces = CountDecimalPlaces(d) <= 8;
		var isGreaterThanZero = d > 0;

		return lessThanMaximum && hasValidDecimalPlaces && isGreaterThanZero;
	}

	private static decimal CountDecimalPlaces(decimal dec)
	{
		Console.Write("{0}: ", dec);
		var bits = decimal.GetBits(dec);
		ulong lowInt = (uint)bits[0];
		ulong midInt = (uint)bits[1];
		var exponent = (bits[3] & 0x00FF0000) >> 16;
		var result = exponent;
		var lowDecimal = lowInt | (midInt << 32);
		while (result > 0 && lowDecimal % 10 == 0)
		{
			result--;
			lowDecimal /= 10;
		}

		return result;
	}

	private IDisposable AutoPaster(TemplatedControl dualCurrencyEntryBox, string templatePartName, Func<string, bool> isValidAmount)
	{
		return dualCurrencyEntryBox
			.OnEvent<TemplateAppliedEventArgs>(nameof(dualCurrencyEntryBox.TemplateApplied))
			.Select(x => x.EventArgs.NameScope.Find<CurrencyEntryBox>(templatePartName))
			.Select(entryBox => entryBox.OnEvent(InputElement.GotFocusEvent).Select(_ => entryBox))
			.Switch()
			.SelectMany(textBox => ApplicationUtils.GetClipboard().Select(str => new { Text = str, TextBox = textBox }))
			.Where(x => isValidAmount(x.Text) && x.TextBox.Text.Trim() == "")
			.Do(x => x.TextBox.Text = x.Text)
			.Subscribe();
	}
}
