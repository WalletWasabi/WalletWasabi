using System.Reactive.Linq;
using ReactiveUI;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;

namespace WalletWasabi.Fluent.Controls.DestinationEntry.ViewModels;

public class MutableAddressHost : ReactiveValidationObject, IMutableAddressHost
{
    private string text;

    public MutableAddressHost(IAddressParser addressParser)
    {
        text = "";
        var parser = addressParser;
        ParsedAddress = this.WhenAnyValue(s => s.Text, s => parser.GetAddress(s));
        TextChanged = this.WhenAnyValue(x => x.Text);
        this.ValidationRule(x => x.Text,
            TextChanged.CombineLatest(ParsedAddress, (txt, address) => string.IsNullOrWhiteSpace(txt) || address is not null), "The address is invalid");
    }

    public IObservable<string> TextChanged { get; }

    public string Text
    {
        get => text;
        set => this.RaiseAndSetIfChanged(ref text, value);
    }

    public IObservable<Address?> ParsedAddress { get; }
}
