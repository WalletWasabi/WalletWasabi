namespace HiddenWallet.DataClasses
{
    class TransactionInfo
    {
        internal readonly string Hash;
        internal readonly decimal Amount;
        internal readonly uint Confirmations;
        internal readonly string Address;

        internal TransactionInfo(string hash, decimal amount, uint confirmations, string address)
        {
            Hash = hash;
            Amount = amount;
            Confirmations = confirmations;
            Address = address;
        }
    }
}
