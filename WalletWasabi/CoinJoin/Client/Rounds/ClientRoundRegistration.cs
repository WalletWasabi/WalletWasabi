using NBitcoin;
using NBitcoin.Crypto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WalletWasabi.Backend.Models.Responses;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.CoinJoin.Client.Clients;
using WalletWasabi.CoinJoin.Common.Models;
using WalletWasabi.Helpers;

namespace WalletWasabi.CoinJoin.Client.Rounds
{
	public class ClientRoundRegistration : IDisposable
	{
		/// <summary>
		/// Completed all the necessary actions in the phase.
		/// </summary>
		public RoundPhase CompletedPhase { get; set; }

		public BitcoinAddress ChangeAddress { get; }

		public IEnumerable<ActiveOutput> ActiveOutputs { get; set; }

		public IEnumerable<SmartCoin> CoinsRegistered { get; }

		public AliceClient AliceClient { get; }

		public ClientRoundRegistration(AliceClient aliceClient, IEnumerable<SmartCoin> coinsRegistereds, BitcoinAddress changeAddress)
		{
			AliceClient = Guard.NotNull(nameof(aliceClient), aliceClient);
			CoinsRegistered = Guard.NotNullOrEmpty(nameof(coinsRegistereds), coinsRegistereds);
			ChangeAddress = Guard.NotNull(nameof(changeAddress), changeAddress);

			ActiveOutputs = Enumerable.Empty<ActiveOutput>();
		}

		public bool IsPhaseActionsComleted(RoundPhase phase) => CompletedPhase >= phase;

		public void SetPhaseCompleted(RoundPhase phase)
		{
			if (!IsPhaseActionsComleted(phase))
			{
				CompletedPhase = phase;
			}
		}

		#region IDisposable Support

		private volatile bool _disposedValue = false; // To detect redundant calls

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					AliceClient?.Dispose();
				}

				_disposedValue = true;
			}
		}

		// This code added to correctly implement the disposable pattern.
		public void Dispose()
		{
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose(true);
		}

		#endregion IDisposable Support
	}
}
