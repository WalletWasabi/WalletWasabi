using WalletWasabi.BitcoinRpc;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Extensions;
using WalletWasabi.WabiSabi.Client.StatusChangedEvents;

namespace WalletWasabi.WabiSabi.Client.CoinJoin.Client;

public delegate Task InputVerifier(Coin[] coins, CancellationToken cancellationToken);

public static class InputVerifiers
{
	public static InputVerifier NoVerification()  =>
		(_, _) => Task.CompletedTask;

	public static InputVerifier CreateRpcVerifier(IRPCClient rpcClient, double samplePercentage = 0.10) =>
		async (coins, cancellationToken) =>
		{
			if (coins.Length == 0)
			{
				return;
			}

			var sampleSize = Math.Max(1, (int)(coins.Length * samplePercentage));
			var sample = coins
				.ToShuffled(RandomnessProviders.Secure)
				.Take(sampleSize)
				.ToList();

			Logger.LogDebug($"Verifying {sample.Count} of {coins.Length} other participants' inputs via RPC.");

			var batchedRpc = rpcClient.PrepareBatch();
			var verifyTasks = sample.Select(coin => (
				Coin: coin,
				Task: batchedRpc.GetTxOutAsync(coin.Outpoint.Hash, (int)coin.Outpoint.N, includeMempool: true, cancellationToken)
			)).ToList();

			await batchedRpc.SendBatchAsync(cancellationToken).ConfigureAwait(false);

			foreach (var (coinToVerify, getTxOutTask) in verifyTasks)
			{
				var getTxOutResponse = await getTxOutTask.ConfigureAwait(false);

				if (getTxOutResponse is null)
				{
					throw new CoinJoinClientException(
						CoinjoinError.CoordinatorLiedAboutInputs,
						$"UTXO {coinToVerify.Outpoint} does not exist or is already spent. The coordinator may be malicious.");
				}

				if (getTxOutResponse.TxOut.ScriptPubKey != coinToVerify.TxOut.ScriptPubKey)
				{
					throw new CoinJoinClientException(
						CoinjoinError.CoordinatorLiedAboutInputs,
						$"ScriptPubKey mismatch for {coinToVerify.Outpoint}. Expected {coinToVerify.TxOut.ScriptPubKey}, got {getTxOutResponse.TxOut.ScriptPubKey}. The coordinator may be malicious.");
				}

				if (getTxOutResponse.TxOut.Value != coinToVerify.Amount)
				{
					throw new CoinJoinClientException(
						CoinjoinError.CoordinatorLiedAboutInputs,
						$"Amount mismatch for {coinToVerify.Outpoint}. Expected {coinToVerify.Amount}, got {getTxOutResponse.TxOut.Value}. The coordinator may be malicious.");
				}
			}

			Logger.LogDebug($"Successfully verified {sample.Count} inputs via RPC.");
		};
}
