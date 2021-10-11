using NBitcoin;
using Newtonsoft.Json;
using System.Collections.Immutable;
using System.Linq;

namespace WalletWasabi.WabiSabi.Models.MultipartyTransaction
{
	public abstract record MultipartyTransactionState
	{
		protected MultipartyTransactionState(MultipartyTransactionParameters parameters)
		{
			Parameters = parameters;
			Order = 0;
		}

		[JsonIgnore]
		public MultipartyTransactionState? PreviousState { get; init; }

		public long Order { get; init; }

		public MultipartyTransactionParameters Parameters { get; }

		public ImmutableList<Coin> Inputs { get; init; } = ImmutableList<Coin>.Empty;
		public ImmutableList<TxOut> Outputs { get; init; } = ImmutableList<TxOut>.Empty;

		public Money Balance => Inputs.Sum(x => x.Amount) - Outputs.Sum(x => x.Value);
		public int EstimatedInputsVsize => Inputs.Sum(x => x.TxOut.ScriptPubKey.EstimateInputVsize());
		public int OutputsVsize => Outputs.Sum(x => x.ScriptPubKey.EstimateOutputVsize());

		public int EstimatedVsize => MultipartyTransactionParameters.SharedOverhead + EstimatedInputsVsize + OutputsVsize;
		public int MaxTransactionSize => Parameters.MaxTransactionSize;

		// With no coordinator fees we can't ensure that the shared overhead
		// of the transaction also pays at the nominal feerate so this will have
		// to do for now, but in the future EstimatedVsize should be used
		// including the shared overhead
		public FeeRate EffectiveFeeRate => new(Balance, EstimatedInputsVsize + OutputsVsize);

		public MultipartyTransactionState GetConstructionStateSince(long order)
		{
			var visitedState = this;
			while (visitedState is not null && visitedState.Order != order)
			{
				visitedState = visitedState.PreviousState;
			}

			// state was not found
			if (visitedState is null)
			{
				return this;
			}

			return this with {
				Inputs = Inputs.GetRange(visitedState.Inputs.Count, Inputs.Count - visitedState.Inputs.Count),
				Outputs = Outputs.GetRange(visitedState.Outputs.Count, Outputs.Count - visitedState.Outputs.Count)
			};
		}

		public MultipartyTransactionState Merge(MultipartyTransactionState diff)
		{
			return this with {
				Inputs = Inputs.AddRange(diff.Inputs),
				Outputs = Outputs.AddRange(diff.Outputs),
				PreviousState = diff.PreviousState,
				Order = diff.Order
			};
		}
	}
}
