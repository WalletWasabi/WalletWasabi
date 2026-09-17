using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using WalletWasabi.Fluent.Mobile.Views;
using Xunit;

namespace WalletWasabi.Fluent.Mobile.Tests;

public sealed class MobileAdapterContextTests
{
	[AvaloniaTheory]
	[InlineData("wallet")]
	[InlineData("address")]
	[InlineData("fee")]
	public void AdapterRootDoesNotInheritTheRawParentContext(string name)
	{
		UserControl view = name switch
		{
			"wallet" => new MobileWalletView(),
			"address" => new MobileReceiveAddressView(),
			"fee" => new MobileSendFeeView(),
			_ => throw new ArgumentOutOfRangeException(nameof(name))
		};
		var source = new object();
		view.DataContext = source;
		Assert.Same(source, view.DataContext);
		var root = view.FindControl<Control>("Root");
		Assert.NotNull(root);
		Assert.Null(root.DataContext);
		view.DataContext = new object();
		Assert.Null(root.DataContext);
	}
}
