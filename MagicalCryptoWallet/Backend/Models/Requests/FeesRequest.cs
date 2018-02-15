using System;
using System.Collections.Generic;
using System.Text;

namespace MagicalCryptoWallet.Backend.Models.Requests
{
    public class FeesRequest
    {
		public List<int> ConfirmationTargets { get; set; }
    }
}
