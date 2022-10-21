using System.Globalization;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
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

		AutoPaster(AssociatedObject, "PART_LeftEntryBox", IsValidBtc)
			.DisposeWith(disposable);

		AutoPaster(AssociatedObject, "PART_RightEntryBox", IsValidUsd)
			.DisposeWith(disposable);
	}

	private static bool IsValidUsd(string s)
	{
		var canParse = decimal.TryParse(s, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var d);
		return canParse && CountDecimalPlaces(d) <= 2 && d > 0;
	}

	private static bool IsValidBtc(string s)
	{
		var canParseAsDecimal = decimal.TryParse(s, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var d);

		if (canParseAsDecimal)
		{
			var lessThanMaximum = d < Constants.MaximumNumberOfSatoshis;
			var hasValidDecimalPlaces = CountDecimalPlaces(d) <= 8;
			return lessThanMaximum && hasValidDecimalPlaces && d > 0;
		}

		return false;
	}

	private static decimal CountDecimalPlaces(decimal dec)
	{
		Console.Write("{0}: ", dec);
		var bits = decimal.GetBits(dec);
		ulong lowInt = (uint) bits[0];
		ulong midInt = (uint) bits[1];
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

	private static IObservable<string> GetClipboard()
	{
		if (Application.Current is { Clipboard: { } clipboard })
		{
			return clipboard.GetTextAsync().ToObservable().Select(x => x ?? "");
		}

		return Observable.Return("");
	}

	private IDisposable AutoPaster(TemplatedControl dualCurrencyEntryBox, string templatePartName, Func<string, bool> isValidAmount)
	{
		return dualCurrencyEntryBox
			.OnEvent<TemplateAppliedEventArgs>(nameof(dualCurrencyEntryBox.TemplateApplied))
			.Select(x => x.EventArgs.NameScope.Find<CurrencyEntryBox>(templatePartName))
			.Select(entryBox => LeftButtonPressed(entryBox).Select(_ => entryBox))
			.Switch()
			.SelectMany(textBox => GetClipboard().Select(str => new { Text = str, TextBox = textBox }))
			.Where(x => isValidAmount(x.Text))
			.Do(x => x.TextBox.Text = x.Text)
			.Subscribe();
	}

	private IObservable<EventPattern<PointerPressedEventArgs>> LeftButtonPressed(IInteractive interactive)
	{
		return interactive
			.OnEvent(InputElement.PointerPressedEvent, RoutingStrategies.Tunnel)
			.Where(q => q.EventArgs.GetCurrentPoint(AssociatedObject).Properties.IsLeftButtonPressed);
	}
}
