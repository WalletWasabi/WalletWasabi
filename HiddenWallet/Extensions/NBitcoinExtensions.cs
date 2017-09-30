using HiddenWallet.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace NBitcoin
{
    public static class NBitcoinExtensions
    {
        public static ChainedBlock GetBlock(this ConcurrentChain me, Height height)
            => me.GetBlock(height.Value);
    }
}
