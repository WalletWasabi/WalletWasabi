using NBitcoin;
using System.Linq;
using WalletWasabi.Crypto;
using WalletWasabi.Extensions;
using WalletWasabi.WabiSabi.Backend.Models;
using WalletWasabi.WabiSabi.Backend.Rounds;

namespace WalletWasabi.WabiSabi.Models.MultipartyTransaction;

// This class represents actions of the BIP 370 creator and constructor roles
public record ConstructionState : MultipartyTransactionState
{
	public ConstructionState(RoundParameters parameters)
		: base(parameters)
	{
	}

	public ConstructionState AddInput(Coin coin, OwnershipProof ownershipProof, CoinJoinInputCommitmentData coinJoinInputCommitmentData)
	{
		var prevout = coin.TxOut;

		if (!StandardScripts.IsStandardScriptPubKey(prevout.ScriptPubKey))
		{
			throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.NonStandardInput);
		}

		if (!Parameters.AllowedInputTypes.Any(x => prevout.ScriptPubKey.IsScriptType(x)))
		{
			throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.ScriptNotAllowed);
		}

		if (prevout.Value < Parameters.AllowedInputAmounts.Min)
		{
			throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.NotEnoughFunds);
		}

		if (prevout.Value > Parameters.AllowedInputAmounts.Max)
		{
			throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.TooMuchFunds);
		}

		if (prevout.Value <= Parameters.MiningFeeRate.GetFee(prevout.ScriptPubKey.EstimateInputVsize()))
		{
			// Inputs must contribute more than they cost to spend because:
			// - Such inputs contribute nothing to privacy and may degrade it
			//   because they must be paid for, when constraining sub-transactions
			//   this may be useful for disambiguating between different input
			//   clusters in the same way that the existence of a dust output
			//   might.
			// - Space in standard transaction is limited and must be shared
			//   between participants.
			throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.UneconomicalInput);
		}

		if (Inputs.Any(x => x.Outpoint == coin.Outpoint))
		{
			throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.NonUniqueInputs);
		}

		if (!OwnershipProof.VerifyCoinJoinInputProof(ownershipProof, coin.TxOut.ScriptPubKey, coinJoinInputCommitmentData))
		{
			throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.WrongOwnershipProof);
		}

		return this with { Events = Events.Add(new InputAdded(coin, ownershipProof)) };
	}

	public ConstructionState AddOutput(TxOut output)
	{
		if (output.Value < Parameters.AllowedOutputAmounts.Min)
		{
			throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.NotEnoughFunds);
		}

		if (output.Value > Parameters.AllowedOutputAmounts.Max)
		{
			throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.TooMuchFunds);
		}

		if (output.IsDust())
		{
			throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.DustOutput);
		}

		if (!StandardScripts.IsStandardScriptPubKey(output.ScriptPubKey))
		{
			throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.NonStandardOutput);
		}

		// Only one OP_RETURN is allowed per standard transaction, but this
		// check is not implemented since there is no OP_RETURN ScriptType,
		// which means no OP_RETURN outputs can be registered at all.
		if (!Parameters.AllowedOutputTypes.Any(x => output.ScriptPubKey.IsScriptType(x)))
		{
			throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.ScriptNotAllowed);
		}

		return this with { Events = Events.Add(new OutputAdded(output)) };
	}

	public SigningState Finalize()
	{
		if (EstimatedVsize > Parameters.MaxTransactionSize)
		{
			throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.SizeLimitExceeded, $"Transaction size is {EstimatedVsize} bytes, which exceeds the limit of {Parameters.MaxTransactionSize} bytes.");
		}

		var minToleratedMiningFeeRate = new FeeRate(0.95m * Parameters.MiningFeeRate.SatoshiPerByte);
		if (EffectiveFeeRate < minToleratedMiningFeeRate)
		{
			var state = new SigningState(Parameters, Events);
			var tx = state.CreateUnsignedTransaction();
			var txHex = tx.ToHex();

			throw new WabiSabiProtocolException(
				WabiSabiProtocolErrorCode.InsufficientFees,
				$"Effective fee rate {EffectiveFeeRate} is less than required {minToleratedMiningFeeRate}. RawTx: {txHex}");
		}

		return new SigningState(Parameters, Events);
	}

	public ConstructionState AsPayingForSharedOverhead() =>
		this with
		{
			UnpaidSharedOverhead = 0
		};
}
