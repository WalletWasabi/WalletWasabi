using WalletWasabi.Crypto.Randomness;

namespace WalletWasabi.Tor.Socks5.Pool.Circuits
{
	/// <summary>
	/// Random Tor circuit for an HTTP request which should not be linked with any other HTTP request sent previously.
	/// </summary>
	public class OneOffCircuit : ICircuit
	{
		public OneOffCircuit(bool isPreEstablished)
		{
			Name = RandomString.CapitalAlphaNumeric(21);
			IsPreEstablished = isPreEstablished;
		}

		public string Name { get; }

		/// <summary>Specifies whether the circuit was pre-established to be used later or not.</summary>
		/// <remarks>
		/// Pre-established connections are useful to avoid latency when we know we will
		/// need a set of connections later.
		/// </remarks>
		public bool IsPreEstablished { get; }

		/// <inheritdoc/>
		public override string ToString()
		{
			return $"[{nameof(OneOffCircuit)}: {Name}|{IsPreEstablished}]";
		}
	}
}
