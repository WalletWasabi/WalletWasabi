using System.Collections.Generic;
using WalletWasabi.Fluent.ViewModels.NavBar;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send
{
	[NavigationMetaData(
		Title = "Send",
		Caption = "",
		IconName = "wallet_action_send",
		NavBarPosition = NavBarPosition.None,
		Searchable = false,
		NavigationTarget = NavigationTarget.HomeScreen)]
	public partial class SendViewModel : NavBarItemViewModel
	{
		[AutoNotify] private string _to;

		public SendViewModel()
		{

		}

		public double XAxisCurrentValue { get; set; } = 36;

		public double XAxisMinValue { get; set; } = 1;

		public double XAxisMaxValue { get; set; } = 1008;

		public List<string> XAxisLabels => new List<string>()
		{
			"1w",
			"3d",
			"1d",
			"12h",
			"6h",
			"3h",
			"1h",
			"30m",
			"20m",
			"fastest"
		};

		public List<double> XAxisValues => new List<double>()
		{
			1008,
			432,
			144,
			72,
			36,
			18,
			6,
			3,
			2,
			1,
		};

		public List<double> YAxisValues => new List<double>()
		{
			4,
			4,
			7,
			22,
			57,
			97,
			102,
			123,
			123,
			185
		};


	}
}