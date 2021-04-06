using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Microservices;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Microservices
{
	/// <summary>
	/// Tests for <see cref="ProcessAsync"/> class.
	/// </summary>
	public class ProcessAsyncTest
	{
		/// <summary>
		/// Tests that we can start Bitcoin daemon (regtest) using <see cref="ProcessAsync"/> API.
		/// </summary>
		[Fact]
		public async void SendCommandImmediateCancelAsync()
		{
			await Assert.ThrowsAsync<TaskCanceledException>(async () =>
			{
				using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

				var startInfo = new ProcessStartInfo()
				{
					FileName = MicroserviceHelpers.GetBinaryPath("bitcoind"),
					Arguments = "-regtest=1"
				};

				using var process = new ProcessAsync(startInfo);
				process.Start();

				await process.WaitForExitAsync(cts.Token, killOnCancel: true);
			});
		}
	}
}
