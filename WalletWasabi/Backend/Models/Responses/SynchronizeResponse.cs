using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Models;

namespace WalletWasabi.Backend.Models.Responses
{
	public class SynchronizeResponse
	{
		public FiltersResponseState FiltersResponseState { get; set; }
		public IEnumerable<string> Filters { get; set; }

		public int BestHeight { get; set; }

		public IEnumerable<CcjRunningRoundState> CcjRoundStates { get; set; }

		public AllFeeEstimate AllFeeEstimate { get; set; }

		public IEnumerable<ExchangeRate> ExchangeRates { get; set; }
	}
}
