namespace WalletWasabi.Fluent.Controls.DestinationEntry.ViewModels;

public static class Factory
{
    private static readonly ClipboardObserver ClipboardObserver;

    static Factory()
    {
        ClipboardObserver = new ClipboardObserver();
    }

    public static PaymentViewModel Create(IAddressParser parser, Func<decimal, bool> isAmountValid)
    {
        var newContentsChanged = ClipboardObserver.ContentChanged;
        IMutableAddressHost mutableAddressHost = new MutableAddressHost(parser);
        return new PaymentViewModel(newContentsChanged, mutableAddressHost,
            new ContentChecker<string>(newContentsChanged, mutableAddressHost.TextChanged,
                s => parser.GetAddress(s) is not null), isAmountValid);
    }
}
