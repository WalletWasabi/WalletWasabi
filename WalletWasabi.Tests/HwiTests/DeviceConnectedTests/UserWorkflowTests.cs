using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Hwi2;
using Xunit;

namespace WalletWasabi.Tests.HwiTests.DeviceConnectedTests
{
	public class UserWorkflowTests
	{
		[Fact]
		public async Task TrezorTTestsAsync()
		{
			// USER: Connect a wiped Trezor T (hwi.exe enumerate, hwi.exe wipe --fingerprint FINGERPRINT)
			var hwiClient = new HwiClient(Network.Main);
		}
	}
}
