using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Microservices;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Microservices
{
	public class ProcessAsyncTest
	{
		[Fact]
		public async void SendCommandImmediateCancelAsync()
		{
			await Assert.ThrowsAsync<TaskCanceledException>(async () =>
			{
				using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

				var startInfo = new ProcessStartInfo()
				{
					FileName = MicroserviceHelpers.GetBinaryPath("bitcoind"),
					Arguments = "-testnet=1"
				};

				using var process = new ProcessAsync(startInfo);
				process.Start();

				// killOnCancel is necessary on Windows. It seems, it ignores Process.Close() call.
				await process.WaitForExitAsync(cts.Token, killOnCancel: true);
			});
		}
	}
}
