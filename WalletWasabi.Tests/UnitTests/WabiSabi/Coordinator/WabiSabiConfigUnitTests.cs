using System.IO;
using System.Threading.Tasks;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Coordinator;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Coordinator;

public class WabiSabiConfigUnitTests
{
	[Fact]
	public async Task LoadFileTestsAsync()
	{
		var dir = await Common.GetEmptyWorkDirAsync();

		var config = new WabiSabiConfig(Path.Combine(dir, "config.json"));
		var json = config.EncodeAsJson().ReplaceLineEndings("\n");

		var expectedJson = $$"""
			{
			  "Network": "Main",
			  "MainNetBitcoinRpcUri": "http://localhost:8332",
			  "TestNetBitcoinRpcUri": "http://localhost:48332",
			  "RegTestBitcoinRpcUri": "http://localhost:18443",
			  "BitcoinRpcConnectionString": "user:password",
			  "ConfirmationTarget": 108,
			  "DoSSeverity": "0.10",
			  "DoSMinTimeForFailedToVerify": "31d 0h 0m 0s",
			  "DoSMinTimeForCheating": "1d 0h 0m 0s",
			  "DoSPenaltyFactorForDisruptingConfirmation": 0.2,
			  "DoSPenaltyFactorForDisruptingSignalReadyToSign": 1,
			  "DoSPenaltyFactorForDisruptingSigning": 1,
			  "DoSPenaltyFactorForDisruptingByDoubleSpending": 3,
			  "DoSMinTimeInPrison": "0d 0h 20m 0s",
			  "MinRegistrableAmount": "0.00005",
			  "MaxRegistrableAmount": "43000.00",
			  "MinimumAcceptableFeeRate": 500,
			  "AllowNotedInputRegistration": true,
			  "StandardInputRegistrationTimeout": "0d 1h 0m 0s",
			  "BlameInputRegistrationTimeout": "0d 0h 3m 0s",
			  "ConnectionConfirmationTimeout": "0d 0h 1m 0s",
			  "OutputRegistrationTimeout": "0d 0h 1m 0s",
			  "TransactionSigningTimeout": "0d 0h 1m 0s",
			  "FailFastOutputRegistrationTimeout": "0d 0h 3m 0s",
			  "FailFastTransactionSigningTimeout": "0d 0h 1m 0s",
			  "RoundExpiryTimeout": "0d 0h 5m 0s",
			  "MaxInputCountByRound": 100,
			  "MinInputCountByRoundMultiplier": 0.5,
			  "MinInputCountByBlameRoundMultiplier": 0.4,
			  "RoundDestroyerThreshold": 375,
			  "CoordinatorExtPubKey": "xpub6C13JhXzjAhVRgeTcRSWqKEPe1vHi3Tmh2K9PN1cZaZFVjjSaj76y5NNyqYjc2bugj64LVDFYu8NZWtJsXNYKFb9J94nehLAPAKqKiXcebC",
			  "CoordinatorExtPubKeyCurrentDepth": 1,
			  "MaxSuggestedAmountBase": "0.10",
			  "RoundParallelization": 1,
			  "CoordinatorIdentifier": "CoinJoinCoordinatorIdentifier",
			  "AllowP2wpkhInputs": true,
			  "AllowP2trInputs": true,
			  "AllowP2wpkhOutputs": true,
			  "AllowP2trOutputs": true,
			  "AllowP2pkhOutputs": false,
			  "AllowP2shOutputs": false,
			  "AllowP2wshOutputs": false,
			  "DelayTransactionSigning": false,
			  "AnnouncerConfig": {
			    "CoordinatorName": "Coordinator",
			    "IsEnabled": false,
			    "CoordinatorDescription": "WabiSabi Coinjoin Coordinator",
			    "CoordinatorUri": "https://api.example.com/",
			    "AbsoluteMinInputCount": 21,
			    "ReadMoreUri": "https://api.example.com/",
			    "RelayUris": [
			      "wss://relay.primal.net"
			    ],
			    "Key": "{{config.AnnouncerConfig.Key}}"
			  },
			  "PublishAsOnionService": false,
			  "OnionServicePrivateKey": null
			}
			""";

		Assert.Equal(expectedJson, json);
	}
}
