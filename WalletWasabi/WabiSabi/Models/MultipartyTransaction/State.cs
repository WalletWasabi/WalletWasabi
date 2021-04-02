namespace WalletWasabi.WabiSabi.Models.MultipartyTransaction
{
	public abstract record State
	{
		public Construction AssertConstruction() => (Construction)this;
		public Signing AssertSigning() => (Signing)this;
	}
}
