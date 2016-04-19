namespace HiddenWallet.DataClasses
{
    internal class TransactionInfo
    {
        internal readonly string Address;
        internal readonly decimal Amount;
        internal readonly uint Confirmations;
        internal readonly string Hash;

        internal TransactionInfo(string hash, decimal amount, uint confirmations, string address)
        {
            Hash = hash;
            Amount = amount;
            Confirmations = confirmations;
            Address = address;
        }
    }
}