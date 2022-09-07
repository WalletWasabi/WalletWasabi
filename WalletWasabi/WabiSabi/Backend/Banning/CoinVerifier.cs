using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using WalletWasabi.Logging;
using WalletWasabi.WabiSabi.Backend.Rounds.CoinJoinStorage;
using WalletWasabi.WabiSabi.Backend.Statistics;

namespace WalletWasabi.WabiSabi.Backend.Banning;

public record CoinVerifyInfo(bool ShouldBan, Coin Coin);

public class CoinVerifier
{
	/// <summary>
	/// A list of indicator Id's that we use to filter out utxos from CoinJoin rounds based on API response.
	/// </summary>
	private readonly HashSet<int> _flagIds = new()
	{
		11,
		22,
		34,
		57,
		58,
		59,
		60,
		61,
		62,
		63,
		150,
		165,
		174,
		177,
		267,
		272,
		340
	};

	public CoinVerifier(CoinJoinIdStore coinJoinIdStore, CoinVerifierApiClient apiClient, Whitelist whitelist, WabiSabiConfig wabiSabiConfig)
	{
		CoinJoinIdStore = coinJoinIdStore;
		CoinVerifierApiClient = apiClient;
		Whitelist = whitelist;
		WabiSabiConfig = wabiSabiConfig;
	}

	// Blank constructor used for testing
	internal CoinVerifier(CoinJoinIdStore coinJoinIdStore, CoinVerifierApiClient apiClient)
	{
		CoinJoinIdStore = coinJoinIdStore;
		CoinVerifierApiClient = apiClient;
		Whitelist = new();
		WabiSabiConfig = new();
	}

	public Whitelist Whitelist { get; }
	public WabiSabiConfig WabiSabiConfig { get; }
	private CoinJoinIdStore CoinJoinIdStore { get; }
	private CoinVerifierApiClient CoinVerifierApiClient { get; }

	private bool CheckIfAlreadyVerified(Coin coin)
	{
		// Step 1: Check if address is whitelisted.
		if (Whitelist.TryGet(coin.Outpoint, out _))
		{
			return true;
		}

		// Step 2: Check if address is from a coinjoin.
		if (CoinJoinIdStore.Contains(coin.Outpoint.Hash))
		{
			return true;
		}

		return false;
	}

	public async IAsyncEnumerable<CoinVerifyInfo> VerifyCoinsAsync(IEnumerable<Coin> coinsToCheck, [EnumeratorCancellation] CancellationToken cancellationToken, string roundId = "")
	{
		var before = DateTimeOffset.UtcNow;

		using CancellationTokenSource cancellationTokenSource = new(TimeSpan.FromSeconds(30));
		using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationTokenSource.Token, cancellationToken);

		var lastChangeId = Whitelist.ChangeId;
		Whitelist.RemoveAllExpired(WabiSabiConfig.ReleaseFromWhitelistAfter);

		var scriptsToCheck = new HashSet<Script>();
		var innocentsCounter = 0;

		foreach (var coin in coinsToCheck)
		{
			if (CheckIfAlreadyVerified(coin))
			{
				innocentsCounter++;
				yield return new CoinVerifyInfo(false, coin);
			}
			else
			{
				scriptsToCheck.Add(coin.ScriptPubKey);			
			}
		}

		Logger.LogInfo($"{innocentsCounter} out of {coinsToCheck.Count()} utxo is already verified in Round({roundId}).");
		await foreach (var response in CoinVerifierApiClient.VerifyScriptsAsync(scriptsToCheck, linkedCts.Token))
		{
			bool shouldBanUtxo = CheckForFlags(response.ApiResponseItem);

			// Find all coins with the same script (address reuse). 
			foreach (var coin in coinsToCheck.Where(c => c.ScriptPubKey == response.ScriptPubKey))
			{
				if (!shouldBanUtxo)
				{
					Whitelist.Add(coin.Outpoint);
				}

				yield return new CoinVerifyInfo(shouldBanUtxo, coin);
			}
		}

		if (Whitelist.ChangeId != lastChangeId)
		{
			Whitelist.WriteToFile();
		}

		var duration = DateTimeOffset.UtcNow - before;
		RequestTimeStatista.Instance.Add("verifier-period", duration);
	}

	private bool CheckForFlags(ApiResponseItem response)
	{
		bool shouldBan = false;

		var coinflaglist = response.Cscore_section.Cscore_info;
		shouldBan = coinflaglist.Any(cscoreInfo => _flagIds.Contains(cscoreInfo.Id));

		return shouldBan;
	}
}
