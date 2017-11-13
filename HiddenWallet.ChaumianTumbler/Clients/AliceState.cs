using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HiddenWallet.ChaumianTumbler.Clients
{
    public enum AliceState
    {
		InputsRegistered,
		ConnectionConfirmed,
		AskedForCoinJoin,
		SignedCoinJoin
    }
}
