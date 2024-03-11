using NBitcoin;
using System.IO;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Backend;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.WabiSabi.Models.Serialization;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Bases;

/// <summary>
/// Tests for <see cref="ConfigManager"/>
/// </summary>
public class ConfigManagerTests
{
	/// <summary>
	/// Tests <see cref="ConfigManager.CheckFileChange{T}(string, T)"/>.
	/// </summary>
	[Fact]
	public async Task CheckFileChangeTestAsync()
	{
		string workDirectory = await Common.GetEmptyWorkDirAsync();
		string configPath = Path.Combine(workDirectory, $"{nameof(CheckFileChangeTestAsync)}.json");

		// Create config and store it.
		WabiSabiConfig config = new();
		config.SetFilePath(configPath);
		config.ToFile();

		// Check that the stored config corresponds to the expected "vanilla" config.
		{
			string expectedFileContents = GetVanillaConfigString();
			string actualFileContents = ReadAllTextAndNormalize(configPath);

			Assert.Equal(expectedFileContents, actualFileContents);

			// No change was done.
			Assert.False(ConfigManager.CheckFileChange(configPath, config));
		}

		// Change coordination fee rate.
		{
			// Double coordination fee rate.
			config.CoordinationFeeRate = new CoordinationFeeRate(rate: 0.006m, plebsDontPayThreshold: Money.Coins(0.01m));

			// Change should be detected.
			Assert.True(ConfigManager.CheckFileChange(configPath, config));

			// Now store and check that JSON is as expected.
			config.ToFile();

			string expectedFileContents = GetVanillaConfigString(coordinationFeeRate: 0.006m);
			string actualFileContents = ReadAllTextAndNormalize(configPath);

			Assert.Equal(expectedFileContents, actualFileContents);
		}

		static string GetVanillaConfigString(decimal coordinationFeeRate = 0.003m)
				=> $$"""
			{
			  "ConfirmationTarget": 108,
			  "DoSSeverity": "0.10",
			  "DoSMinTimeForFailedToVerify": "31d 0h 0m 0s",
			  "DoSMinTimeForCheating": "1d 0h 0m 0s",
			  "DoSPenaltyFactorForDisruptingConfirmation": 0.2,
			  "DoSPenaltyFactorForDisruptingSignalReadyToSign": 1.0,
			  "DoSPenaltyFactorForDisruptingSigning": 1.0,
			  "DoSPenaltyFactorForDisruptingByDoubleSpending": 3.0,
			  "DoSMinTimeInPrison": "0d 0h 20m 0s",
			  "MinRegistrableAmount": "0.00005",
			  "MaxRegistrableAmount": "43000.00",
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
			  "CoordinationFeeRate": {
			    "Rate": {{coordinationFeeRate}},
			    "PlebsDontPayThreshold": 1000000
			  },
			  "CoordinatorExtPubKey": "xpub6C13JhXzjAhVRgeTcRSWqKEPe1vHi3Tmh2K9PN1cZaZFVjjSaj76y5NNyqYjc2bugj64LVDFYu8NZWtJsXNYKFb9J94nehLAPAKqKiXcebC",
			  "CoordinatorExtPubKeyCurrentDepth": 1,
			  "MaxSuggestedAmountBase": "0.10",
			  "IsCoinVerifierEnabled": false,
			  "RiskFlags": "",
			  "CoinVerifierApiUrl": "",
			  "CoinVerifierApiAuthToken": "",
			  "CoinVerifierStartBefore": "0d 0h 2m 0s",
			  "CoinVerifierRequiredConfirmations": 3,
			  "CoinVerifierRequiredConfirmationAmount": "1.00",
			  "ReleaseFromWhitelistAfter": "31d 0h 0m 0s",
			  "RoundParallelization": 1,
			  "WW200CompatibleLoadBalancing": false,
			  "WW200CompatibleLoadBalancingInputSplit": 0.75,
			  "CoordinatorIdentifier": "CoinJoinCoordinatorIdentifier",
			  "AllowP2wpkhInputs": true,
			  "AllowP2trInputs": true,
			  "AllowP2wpkhOutputs": true,
			  "AllowP2trOutputs": true,
			  "AllowP2pkhOutputs": false,
			  "AllowP2shOutputs": false,
			  "AllowP2wshOutputs": false,
			  "AffiliationMessageSignerKey": "30770201010420686710a86f0cdf425e3bc9781f51e45b9440aec1215002402d5cdee713066623a00a06082a8648ce3d030107a14403420004f267804052bd863a1644233b8bfb5b8652ab99bcbfa0fb9c36113a571eb5c0cb7c733dbcf1777c2745c782f96e218bb71d67d15da1a77d37fa3cb96f423e53ba",
			  "AffiliateServers": {},
			  "DelayTransactionSigning": false
			}
			""".ReplaceLineEndings("\n");
	}

	private static string ReadAllTextAndNormalize(string configPath)
		=> File.ReadAllText(configPath).ReplaceLineEndings("\n");
}
