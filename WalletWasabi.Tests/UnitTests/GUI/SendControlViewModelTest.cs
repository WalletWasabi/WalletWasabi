using Xunit;
using WalletWasabi.Gui.Controls.WalletExplorer;

namespace WalletWasabi.Tests.UnitTests.GUI
{
	public class SendControlViewModelTest
	{
		[Theory]
		[InlineData(false, "0")]
		[InlineData(false, "0.1")]
		[InlineData(true, "1.2099999997690000")]
		[InlineData(true, "1.20999999976900000000000000000000000000000000000000000000000000000000000000000000")]
		[InlineData(true, "1.209999999769000000000000000000000000000000000000000000000000000000000000000000001")]
		[InlineData(false, "1.209999999769000000000000000000000000000000000000000000000000000000000000000000001a")]
		[InlineData(true, "1")]
		[InlineData(true, "1.")]
		[InlineData(false, "-1")]
		[InlineData(true, "47")]
		[InlineData(true, "47.0")]
		[InlineData(true, "1111111111111")]
		[InlineData(false, "11111111111111")]
		[InlineData(false, "2099999997690000")]
		[InlineData(false, "2099999997690001")]
		[InlineData(false, "111111111111111111111111111")]
		[InlineData(false, "99999999999999999999999999999999999999999999")]
		[InlineData(false, "abc")]
		[InlineData(false, "1a2b")]
		[InlineData(false, "")]
		[InlineData(false, null)]
		[InlineData(false, "  ")]
		[InlineData(true, "     2")]
		[InlineData(true, "1.1")]
		[InlineData(true, "1.1 ")]
		[InlineData(false, "1,1")]
		[InlineData(false, "1. 1")]
		[InlineData(false, "1 .1")]
		[InlineData(false, "0.             1")]
		[InlineData(false, "csszáőüó@")]
		public void SendControlViewModel_Check_Test_Fees(bool isValid, string feeText)
		{
			Assert.Equal(isValid, SendControlViewModel.TryParseUserFee(feeText, out var _));
		}
	}
}
