using System;

namespace HiddenWallet.DataClasses
{
    internal class AddressInfo
    {
        internal readonly string Address;

        internal decimal Balance;
        internal decimal BalanceMultisig;
        internal decimal TotalBalance => Balance + BalanceMultisig;
        internal uint TransactionCount;

        internal decimal TotalReceived;

        internal AddressInfo(string address)
        {
            Address = address;
        }
    }
}
