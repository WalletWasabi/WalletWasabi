using WalletWasabi.Crypto.Randomness;

namespace WalletWasabi.Tor.Socks5.Pool.Circuits
{
	/// <summary>
	/// Random Tor circuit for an HTTP request which should not be linked with any other HTTP request sent previously.
	/// </summary>
	public class OneOffCircuit : ICircuit, IDisposable
	{
		private volatile bool _isActive;

		public OneOffCircuit()
		{
			Name = RandomString.CapitalAlphaNumeric(21);
			_isActive = true;
		}

		/// <inheritdoc/>
		public string Name { get; }

		/// <inheritdoc/>
		public bool IsActive => _isActive;

		/// <inheritdoc/>
		public override string ToString()
		{
			return $"[{nameof(OneOffCircuit)}: {Name}]";
		}

		public void Dispose()
		{
			_isActive = false;
		}
	}
}
