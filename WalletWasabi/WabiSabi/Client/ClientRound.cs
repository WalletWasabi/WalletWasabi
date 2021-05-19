using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Crypto;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;

namespace WalletWasabi.WabiSabi.Client
{
	public record ClientRound
	{
		public ClientRound(Round round)
		{
			AmountCredentialIssuerParameters = round.AmountCredentialIssuerParameters;
			VsizeCredentialIssuerParameters = round.VsizeCredentialIssuerParameters;
			Id = round.Id;
			FeeRate = round.FeeRate;
		}

		public uint256 Id { get; }
		public FeeRate FeeRate { get; }
		public CredentialIssuerParameters AmountCredentialIssuerParameters { get; }
		public CredentialIssuerParameters VsizeCredentialIssuerParameters { get; }
		public MultipartyTransactionState CoinjoinState { get; set; }
		public TState Assert<TState>() where TState : MultipartyTransactionState =>
			CoinjoinState switch
			{
				TState s => s,
				_ => throw new InvalidOperationException($"{typeof(TState).Name} state was expected but {CoinjoinState.GetType().Name} state was received.")
			};
	}
}
