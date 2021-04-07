namespace WalletWasabi.WabiSabi.Models.MultipartyTransaction
{
	public abstract record State
	{
		public ConstructionState AssertConstruction() => (ConstructionState)this;
		public SigningState AssertSigning() => (SigningState)this;
	}
}
