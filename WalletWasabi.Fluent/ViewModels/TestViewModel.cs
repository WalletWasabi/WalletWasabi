using System.Collections.Generic;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels
{
	// TODO: For testing only.
	[Property("IsVisible", typeof(bool))]
	[Property("Title", typeof(string), false, PropertyModifier.Public, PropertyModifier.Internal)]
	[Property("Items", typeof(List<string>), false, PropertyModifier.Public, PropertyModifier.Private)]
	[Property("Count", typeof(int), isReadOnly: true)]
	[NavigationMetaData(
		Searchable = true,
		Title = "Test",
		Caption = "Test caption",
		IconName = "info_regular",
		Order = 0,
		Category = "General",
		Keywords = new [] { "Test", "ViewModel" },
		NavBarPosition = NavBarPosition.None)]
	public partial class TestViewModel
	{
		// [AutoNotify] private bool _test;
	}
}