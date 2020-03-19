namespace NBitcoin.RPC
{
	public class VerboseInputInfo
	{
		public OutPoint OutPoint { get; set; }
		public VerboseOutputInfo PrevOutput { get; set; }
	}
}