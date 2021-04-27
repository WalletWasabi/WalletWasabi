namespace WalletWasabi.WabiSabi.Models.MultipartyTransaction
{
	public static class IStateExtensions
	{
		public static ConstructionState AssertConstruction(this IState state) => (ConstructionState)state;
		public static SigningState AssertSigning(this IState state) => (SigningState)state;
	}
}
