namespace HiddenWallet.DataClasses
{
    internal class AddressInfo
    {
        internal readonly string Address;
        internal decimal Balance;
        internal decimal BalanceMultisig;
        internal decimal TotalReceived;
        internal uint TransactionCount;

        internal AddressInfo(string address)
        {
            Address = address;
        }

        internal decimal TotalBalance => Balance + BalanceMultisig;
    }
}