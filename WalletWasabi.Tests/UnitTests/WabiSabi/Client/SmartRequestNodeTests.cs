using System.Linq;
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
	[InlineData(new long[] { 100, 50, 0, 0 }, new long[] { 0, 100 })]
	[InlineData(new long[] { 100, 50, 0, 0 }, new long[] { 100, 0 })]
	[InlineData(new long[] { 100, 50, 0, 0 }, new long[] { 0, 100, 50 })]
	[InlineData(new long[] { 100, 50, 0, 0 }, new long[] { 100, 0, 50 })]
	[InlineData(new long[] { 100, 50, 0, 0 }, new long[] { 50, 100, 0 })]
	[InlineData(new long[] { 100, 50, 0, 0 }, new long[] { 50, 0, 100 })]
	[InlineData(new long[] { 100, 0, 0, 0 }, new long[] { 0, 0, 100 })]
	[InlineData(new long[] { 100, 100, 0, 0 }, new long[] { 0, 100, 100 })]
	[InlineData(new long[] { 100, 100, 0, 0 }, new long[] { 100 })]
	[InlineData(new long[] { 100, 50, 0, 0 }, new long[] { })]
	public void ReissuedCredentialsFollowGraphOrderAndAreAllocatedOnce(long[] issuedValues, long[] requiredValues)
	{
		// Pure allocation test: these synthetic credentials are never presented to an issuer.
		var issued = issuedValues.Select(value => new Credential(value, default, null!)).ToArray();
		var node = new SmartRequestNode([], [], [], []);
		var (required, extra) = node.SeparateExtraCredentials(issued, requiredValues);
		var selected = required.ToArray();
		var remaining = extra.ToArray();
		Assert.Equal(requiredValues, selected.Select(credential => credential.Value));
		Assert.Equal(issued.Length, selected.Length + remaining.Length);
		foreach (var credential in issued)
		{
			Assert.Single(selected.Concat(remaining), candidate => ReferenceEquals(candidate, credential));
		}
	}

	[Theory]
	[InlineData(new long[] { 100 }, new long[] { 0 })]
	[InlineData(new long[] { 100, 0 }, new long[] { 100, 100 })]
	[InlineData(new long[] { 100, 0 }, new long[] { 0, 0 })]
	public void MissingReissuedCredentialFailsInsteadOfLeavingAnUnresolvedDependency(long[] issuedValues, long[] requiredValues)
	{
		var issued = issuedValues.Select(value => new Credential(value, default, null!)).ToArray();
		var node = new SmartRequestNode([], [], [], []);
		Assert.Throws<InvalidOperationException>(() => node.SeparateExtraCredentials(issued, requiredValues));
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task MismatchedOutputDependenciesFailBeforeCallingTheIssuer(bool vsizeMismatch)
	{
		var dependency = new TaskCompletionSource<Credential>(TaskCreationOptions.RunContinuationsAsynchronously);
		var node = vsizeMismatch ? new SmartRequestNode([], [], [], [dependency]) : new SmartRequestNode([], [], [dependency], []);
		await Assert.ThrowsAsync<InvalidOperationException>(() => node.StartReissuanceAsync(null!, [], [], CancellationToken.None));
		Assert.False(dependency.Task.IsCompleted);
	}

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
