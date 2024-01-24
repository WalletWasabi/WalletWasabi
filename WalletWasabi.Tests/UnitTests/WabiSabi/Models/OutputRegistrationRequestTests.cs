using NBitcoin;
using NBitcoin.Secp256k1;
using WabiSabi;
using WabiSabi.CredentialRequesting;
using WabiSabi.Crypto;
using WabiSabi.Crypto.Groups;
using WabiSabi.Crypto.ZeroKnowledge;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Models;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Models;

/// <summary>
/// Tests for <see cref="OutputRegistrationRequest"/> class.
/// </summary>
public class OutputRegistrationRequestTests
{
	[Fact]
	public void EqualityTest()
	{
		uint256 roundId = BitcoinFactory.CreateUint256();
		Script script = Script.FromHex(
			"0200000000010111b6e0460bb810b05744f8d38262f95fbab02b168b070598a6f31fad438fced40" +
			"00000001716001427c106013c0042da165c082b3870c31fb3ab4683feffffff0200ca9a3b000000" +
			"0017a914d8b6fcc85a383261df05423ddf068a8987bf0287873067a3fa0100000017a914d5df0b9" +
			"ca6c0e1ba60a9ff29359d2600d9c6659d870247304402203b85cb05b43cc68df72e2e54c6cb508a" +
			"a324a5de0c53f1bbfe997cbd7509774d022041e1b1823bdaddcd6581d7cde6e6a4c4dbef483e42e" +
			"59e04dbacbaf537c3e3e8012103fbbdb3b3fc3abbbd983b20a557445fb041d6f21cc5977d212197" +
			"1cb1ce5298978c000000");

		// Request #1.
		OutputRegistrationRequest request1 = new(
			RoundId: roundId,
			Script: script,
			AmountCredentialRequests: NewRealCredentialsRequest(modifier: 1),
			VsizeCredentialRequests: NewRealCredentialsRequest(modifier: 2)
		);

		// Request #2.
		OutputRegistrationRequest request2 = new(
			RoundId: roundId,
			Script: script,
			AmountCredentialRequests: NewRealCredentialsRequest(modifier: 1),
			VsizeCredentialRequests: NewRealCredentialsRequest(modifier: 2)
		);

		Assert.Equal(request1, request2);

		// Request #3.
		OutputRegistrationRequest request3 = new(
			RoundId: BitcoinFactory.CreateUint256(), // Intentionally changed.
			Script: script,
			AmountCredentialRequests: NewRealCredentialsRequest(modifier: 1),
			VsizeCredentialRequests: NewRealCredentialsRequest(modifier: 2)
		);

		Assert.NotEqual(request1, request3);

		// Request #4.
		OutputRegistrationRequest request4 = new(
			RoundId: roundId,
			Script: Script.FromHex($"{script.ToHex()[..^8]}8d000000"), // Intentionally changed; lock time: 140 -> 141.
			AmountCredentialRequests: NewRealCredentialsRequest(modifier: 1),
			VsizeCredentialRequests: NewRealCredentialsRequest(modifier: 2)
		);

		Assert.NotEqual(request1, request4);

		// Request #5.
		OutputRegistrationRequest request5 = new(
			RoundId: roundId,
			Script: script,
			AmountCredentialRequests: NewRealCredentialsRequest(modifier: 999), // Intentionally changed.
			VsizeCredentialRequests: NewRealCredentialsRequest(modifier: 2)
		);

		Assert.NotEqual(request1, request5);

		// Request #6.
		OutputRegistrationRequest request6 = new(
			RoundId: roundId,
			Script: script,
			AmountCredentialRequests: NewRealCredentialsRequest(modifier: 1),
			VsizeCredentialRequests: NewRealCredentialsRequest(modifier: 999) // Intentionally changed.
		);

		Assert.NotEqual(request1, request6);
	}

	private static GroupElement NewGroupElement(int i) => Generators.FromText($"T{i}");

	/// <summary>Dummy method to create a new real credentials request for testing purposes only.</summary>
	/// <param name="modifier">Modifier so that we generate a different request if we want.</param>
	private static RealCredentialsRequest NewRealCredentialsRequest(int modifier) =>
		new(
			123456L,
			new[] { new CredentialPresentation(NewGroupElement(7), NewGroupElement(7), NewGroupElement(7), NewGroupElement(7), NewGroupElement(7)) },
			new[] { new IssuanceRequest(ma: NewGroupElement(modifier * 1), bitCommitments: new GroupElement[] { NewGroupElement(2), NewGroupElement(3) }) },
			new[] { new Proof(new GroupElementVector(NewGroupElement(13)), new ScalarVector(new Scalar(5))) });
}
