using System.Reactive.Linq;
using ReactiveUI;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;

namespace WalletWasabi.Fluent.Controls.DestinationEntry.ViewModels;

public class AddressViewModel : ReactiveValidationObject
{
	private string _text;

	public AddressViewModel(IAddressParser addressParser)
	{
		_text = "";
		var parser = addressParser;
		ParsedAddress = this.WhenAnyValue(s => s.Text, s => parser.GetAddress(s));
		TextChanged = this.WhenAnyValue(x => x.Text);
		this.ValidationRule(
			x => x.Text,
			TextChanged.CombineLatest(
				ParsedAddress,
				(txt, address) => string.IsNullOrWhiteSpace(txt) || address.IsSuccess),
			"The address is invalid");
	}

	public IObservable<string> TextChanged { get; }

	public string Text
	{
		get => _text;
		set => this.RaiseAndSetIfChanged(ref _text, value);
	}

	public IObservable<Result<Address>> ParsedAddress { get; }
}
