using NBitcoin;

namespace WalletWasabi.BitcoinCore.RpcModels
{
	public class VerboseInputInfo
	{
		public VerboseInputInfo(string coinbase)
		{
			Coinbase = coinbase;
		}

		public VerboseInputInfo(OutPoint outPoint, VerboseOutputInfo prevOutput)
		{
			OutPoint = outPoint;
			PrevOutput = prevOutput;
		}

		public OutPoint OutPoint { get; }

		public VerboseOutputInfo PrevOutput { get; }

		public string Coinbase { get; }
	}
}
