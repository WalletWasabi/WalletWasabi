using System.Collections.Generic;

namespace WalletWasabi.Fluent.ViewModels
{
	// TODO: For testing only.
	[Property("IsVisible", typeof(bool))]
	[Property("Title", typeof(string), false, PropertyModifier.None, PropertyModifier.Internal)]
	[Property("Items", typeof(List<string>), false, PropertyModifier.None, PropertyModifier.Private)]
	[Property("Count", typeof(int), isReadOnly: true)]
	public partial class TestViewModel1
	{
		// [AutoNotify] private bool _test;
	}
}