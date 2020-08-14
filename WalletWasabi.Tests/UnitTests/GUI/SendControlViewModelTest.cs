using Xunit;
using WalletWasabi.Gui.Controls.WalletExplorer;

namespace WalletWasabi.Tests.UnitTests.GUI
{
	public class SendControlViewModelTest
	{
		[Theory]
		[InlineData(true, "47")]
		[InlineData(true, "1")]
		[InlineData(true, "2099999997690000")]
		[InlineData(false, "2099999997690001")]
		[InlineData(false, "0")]
		[InlineData(false, "47.0")]
		[InlineData(false, "0.1")]
		public void SendControlViewModel_Check_Test_Fees(bool isValid, string feeText)
		{
			Assert.Equal(isValid, SendControlViewModel.TryParseUserFeeCore(feeText, out var _));
		}
	}
}
