using NBitcoin;
using System.Linq;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;

namespace WalletWasabi.Payjoin;

/// <summary>
/// Implementations of payjoin-ffi's receiver wallet callbacks against Wasabi's wallet
/// primitives. The ffi calls back into these while walking the receiver typestate chain;
/// they must not do I/O beyond the wallet stores they wrap.
/// </summary>
internal static class PayjoinWalletCallbacks
{
	/// <summary>Answers "is this scriptPubKey ours?" from the wallet's key manager.</summary>
	internal class ScriptOwnershipChecker : global::Payjoin.IsScriptOwned
	{
		private readonly KeyManager _keyManager;

		public ScriptOwnershipChecker(KeyManager keyManager)
		{
			_keyManager = keyManager;
		}

		public bool Callback(byte[] script) =>
			_keyManager.TryGetKeyForScriptPubKey(Script.FromBytesUnsafe(script), out _);
	}

	/// <summary>
	/// Probing-attack defense: records every outpoint offered to us and reports whether it
	/// was offered before. Backed by the persistent inputs-seen table, so a sender cannot
	/// probe wallet ownership by re-sending the same original proposal across restarts.
	/// </summary>
	internal class InputsSeenChecker : global::Payjoin.IsOutputKnown
	{
		private readonly PayjoinSessionStore _store;

		public InputsSeenChecker(PayjoinSessionStore store)
		{
			_store = store;
		}

		public bool Callback(global::Payjoin.OutPoint outpoint) =>
			!_store.TryInsertInputSeen(new OutPoint(uint256.Parse(outpoint.Txid), outpoint.Vout));
	}

	/// <summary>
	/// Light-client stand-in for bitcoind's <c>testmempoolaccept</c>: the original
	/// transaction must at least parse and pass consensus sanity checks. Fee-rate policy is
	/// enforced separately by the ffi via the min-fee-rate parameter of
	/// <c>CheckBroadcastSuitability</c>.
	/// </summary>
	internal class TransactionSanityChecker : global::Payjoin.CanBroadcast
	{
		private readonly Network _network;

		public TransactionSanityChecker(Network network)
		{
			_network = network;
		}

		public bool Callback(byte[] tx)
		{
			try
			{
				return Transaction.Load(tx, _network).Check() == TransactionCheckResult.Success;
			}
			catch (Exception)
			{
				return false;
			}
		}
	}

	/// <summary>
	/// Signs and finalizes the receiver's contributed inputs in the proposal PSBT.
	/// Only the given coins are touched; the sender's inputs stay as they are.
	/// </summary>
	internal class ContributedInputSigner : global::Payjoin.ProcessPsbt
	{
		private readonly Network _network;
		private readonly KeyManager _keyManager;
		private readonly string _password;
		private readonly SmartCoin[] _contributedCoins;

		public ContributedInputSigner(Network network, KeyManager keyManager, string password, SmartCoin[] contributedCoins)
		{
			_network = network;
			_keyManager = keyManager;
			_password = password;
			_contributedCoins = contributedCoins;
		}

		public string Callback(string psbt)
		{
			PSBT parsed = PSBT.Parse(psbt, _network);

			Key[] signingKeys = _keyManager.GetSecrets(_password, _contributedCoins.Select(x => x.ScriptPubKey).ToArray()).ToArray();
			TransactionBuilder builder = _network.CreateTransactionBuilder();
			builder.AddKeys(signingKeys);
			builder.AddCoins(_contributedCoins.Select(x => x.Coin));
			builder.SignPSBT(parsed);

			// The receiver must return its own inputs finalized: the sender cannot finalize them.
			foreach (PSBTInput input in parsed.Inputs.Where(i => _contributedCoins.Any(c => c.Outpoint == i.PrevOut)))
			{
				if (!input.TryFinalizeInput(out var errors))
				{
					throw new InvalidOperationException($"Could not finalize contributed input {input.PrevOut}: {string.Join("; ", errors)}");
				}
			}

			return parsed.ToBase64();
		}
	}

	/// <summary>Settlement detection: looks the payjoin transaction up in the wallet's transaction store.</summary>
	internal class WalletTransactionFinder : global::Payjoin.TransactionFinder
	{
		private readonly AllTransactionStore _transactionStore;

		public WalletTransactionFinder(AllTransactionStore transactionStore)
		{
			_transactionStore = transactionStore;
		}

		public byte[]? Callback(string txid) =>
			_transactionStore.TryGetTransaction(uint256.Parse(txid), out SmartTransaction? tx)
				? tx.Transaction.ToBytes()
				: null;
	}

	/// <summary>Converts a wallet coin to the ffi's input-pair shape (segwit-only wallet: witness UTXO, no redeem/witness script).</summary>
	public static global::Payjoin.InputPair ToInputPair(SmartCoin coin)
	{
		var txin = new global::Payjoin.TxIn(
			PreviousOutput: new global::Payjoin.OutPoint(coin.TransactionId.ToString(), coin.Index),
			ScriptSig: [],
			Sequence: uint.MaxValue,
			Witness: []);
		var witnessUtxo = new global::Payjoin.TxOut(
			ValueSat: (ulong)coin.Amount.Satoshi,
			ScriptPubkey: coin.ScriptPubKey.ToBytes());
		var psbtInput = new global::Payjoin.PsbtInput(WitnessUtxo: witnessUtxo, RedeemScript: null, WitnessScript: null);

		// Weight: null lets the library derive the satisfaction weight from the script type.
		return new global::Payjoin.InputPair(txin, psbtInput, null);
	}
}
