using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Client.CoinJoin.Client;
using WabiSabi.Crypto;
using WabiSabi.Crypto.ZeroKnowledge;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Client;

public class SmartRequestNodeTests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task CancellationReleasesAnUnresolvedCredentialDependency(bool outputRegistration)
	{
		var unresolved = new TaskCompletionSource<Credential>(TaskCreationOptions.RunContinuationsAsynchronously);
		var node = new SmartRequestNode([unresolved.Task], [], [], []);
		// No API may run before the required credentials are available.
		var bob = new BobClient(uint256.One, null!);
		using var stop = new CancellationTokenSource();
		var operation = outputRegistration
			? node.StartOutputRegistrationAsync(bob, Script.Empty, stop.Token)
			: node.StartReissuanceAsync(bob, [], [], stop.Token);
		Assert.False(operation.IsCompleted);
		stop.Cancel();
		await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation.WaitAsync(TimeSpan.FromSeconds(2)));
		Assert.False(unresolved.Task.IsCompleted);
	}
}
