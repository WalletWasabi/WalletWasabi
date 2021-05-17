namespace WalletWasabi.WabiSabi.Models.MultipartyTransaction
{
	public static class MultipartyTransactionStateExtensions
	{
		public static ConstructionState AssertConstruction(this MultipartyTransactionState state) => (ConstructionState)state;
		public static SigningState AssertSigning(this MultipartyTransactionState state) => (SigningState)state;
	}
}