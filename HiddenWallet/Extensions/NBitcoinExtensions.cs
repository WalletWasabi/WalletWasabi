using HiddenWallet.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace NBitcoin
{
    public static class NBitcoinExtensions
    {
        public static ChainedBlock GetBlock(this ConcurrentChain concurrentChain, Height height)
            => concurrentChain.GetBlock(height.Value);
    }
}
