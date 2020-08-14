using Mono.Options;
using System;
using System.IO;
using WalletWasabi.Gui.CommandLine;
using WalletWasabi.Helpers;
using WalletWasabi.Tests.Helpers;
using Xunit;
using WalletWasabi.Gui.Controls.WalletExplorer;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.TransactionBuilding;
using System.Collections.Generic;
using NBitcoin;
using WalletWasabi.Wallets;

namespace WalletWasabi.Tests.UnitTests.GUI
{
	public class SendControlViewModelTest
	{
		class MockSendControlViewModel : SendControlViewModel
		{
			public MockSendControlViewModel() : base(null, string.Empty, true)
			{
			}

			public override string DoButtonText => throw new NotImplementedException();

			public override string DoingButtonText => throw new NotImplementedException();

			protected override Task BuildTransaction(string password, PaymentIntent payments, FeeStrategy feeStrategy, bool allowUnconfirmed = false, IEnumerable<OutPoint> allowedInputs = null) => throw new NotImplementedException();
		}

		[Theory]
		[InlineData(true, "47")]
		[InlineData(true, "1")]
		[InlineData(true, "2099999997690000")]
		[InlineData(false, "2099999997690001")]
		[InlineData(false, "0")]
		[InlineData(false, "47.0")]
		[InlineData(false, "0.1")]
		public async void SendControlViewModel_Check_Test_Fees(bool isValid, string feeText)
		{
			var vm = new MockSendControlViewModel();

			vm.UserFeeText = feeText;

			Assert.Equal(isValid, vm.TryParseUserFee(out var _));
		}
	}
}
