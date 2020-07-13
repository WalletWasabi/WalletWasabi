namespace WalletWasabi.Stores
{
	/// <summary>
	/// This class provides a Mock version of <see cref="BitcoinStore"/> for
	/// Unit tests that only need a dummy version of the class.
	/// </summary>
	public class BitcoinStoreMock : BitcoinStore
	{
		public BitcoinStoreMock() : base()
		{
		}
	}
}
