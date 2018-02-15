using System;
using System.Collections.Generic;
using System.Text;

namespace MagicalCryptoWallet.Backend.Models.Responses
{
    public class FeesResponse
    {
		/// <summary>
		/// int: confirmation target
		/// </summary>
		public SortedDictionary<int, FeeEstimationPair> Fees { get; set; }
    }
}
