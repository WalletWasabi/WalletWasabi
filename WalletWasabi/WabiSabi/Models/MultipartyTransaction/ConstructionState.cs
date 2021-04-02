using NBitcoin;
using System;
using System.Collections.Immutable;
using System.Linq;
using WalletWasabi.WabiSabi.Backend.Models;

namespace WalletWasabi.WabiSabi.Models.MultipartyTransaction
{
	// This class represents actions of the BIP 370 creator and constructor roles
	public record ConstructionState(MultipartyTransactionParameters Parameters) : IState
	{
		public ImmutableList<Coin> Inputs { get; init; } = ImmutableList<Coin>.Empty;
		public ImmutableList<TxOut> Outputs { get; init; } = ImmutableList<TxOut>.Empty;

		public Money Balance => Inputs.Sum(x => x.Amount) - Outputs.Sum(x => x.Value);
		public int EstimatedInputsVsize => Inputs.Sum(x => x.TxOut.ScriptPubKey.EstimateInputVsize());
		public int OutputsVsize => Outputs.Sum(x => x.ScriptPubKey.EstimateOutputVsize());

		public int EstimatedVsize => MultipartyTransactionParameters.SharedOverhead + EstimatedInputsVsize + OutputsVsize;
		// With no coordinator fees we can't ensure that the shared overhead
		// of the transaction also pays at the nominal feerate so this will have
		// to do for now, but in the future EstimatedVsize should be used
		// including the shared overhead
		public FeeRate EffectiveFeeRate => new FeeRate(Balance, EstimatedInputsVsize + OutputsVsize);

		// TODO ownership proofs and spend status also in scope
		public ConstructionState AddInput(Coin coin)
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

			if (!prevout.ScriptPubKey.IsScriptType(ScriptType.P2WPKH))
			{
				throw new NotImplementedException(); // See #5440
			}

			if (prevout.Value < Parameters.AllowedInputAmounts.Min)
			{
				throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.NotEnoughFunds);
			}

			if (prevout.Value > Parameters.AllowedInputAmounts.Max)
			{
				throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.TooMuchFunds);
			}

			if (prevout.Value <= Parameters.FeeRate.GetFee(prevout.ScriptPubKey.EstimateInputVsize()))
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

			return this with { Inputs = Inputs.Add(coin) };
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

			if (output.IsDust(Parameters.MinRelayTxFee))
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

			return this with { Outputs = Outputs.Add(output) };
		}

		public SigningState Finalize()
		{
			if (EstimatedVsize > Parameters.MaxTransactionSize)
			{
				throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.SizeLimitExceeded);
			}

			if (EffectiveFeeRate < Parameters.FeeRate)
			{
				throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.InsufficientFees);
			}

			return new SigningState(Parameters, Inputs.ToImmutableArray(), Outputs.ToImmutableArray());
		}
	}
}
