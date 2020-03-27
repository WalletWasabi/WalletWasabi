namespace NBitcoin.RPC
{
	public class VerboseInputInfo
	{
		public VerboseInputInfo(OutPoint outPoint, VerboseOutputInfo prevOutput)
		{
			OutPoint = outPoint;
			PrevOutput = prevOutput;
		}

		public OutPoint OutPoint { get; }

		public VerboseOutputInfo PrevOutput { get; }
	}
}
