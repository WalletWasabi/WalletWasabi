using System;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Microservices;
using Xunit;

namespace WalletWasabi.Tests.UnitTests
{
	public class ProcessBridgeTests
	{
		public TimeSpan ReasonableRequestTimeout { get; } = TimeSpan.FromMinutes(1);

		[Fact]
		public async Task ProcessBridgeArgumentLengthTestAsync()
		{
			ProcessBridge pb = new ProcessBridge("dotnet", false);

			using var cts = new CancellationTokenSource(ReasonableRequestTimeout);

			string myString = new string('a', ProcessBridge.MaxArgumentLength);

			var res = await pb.SendCommandAsync(myString, false, cts.Token);
		}
	}
}
