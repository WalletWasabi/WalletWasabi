using NBitcoin;

namespace WalletWasabi.Blockchain.Transactions
{
	public class AllTransactionStoreMock : AllTransactionStore
	{
		public AllTransactionStoreMock(string workFolderPath, Network network) : base(workFolderPath, network)
		{
		}

		public override bool TryGetTransaction(uint256 hash, out SmartTransaction sameStx)
		{
			sameStx = null;
			return false;
		}
	}
}
