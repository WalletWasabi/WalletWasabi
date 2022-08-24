using WalletWasabi.Crypto.Randomness;

namespace WalletWasabi.Tor.Socks5.Pool.Circuits;

/// <summary>
/// Random Tor circuit for an HTTP request which should not be linked with any other HTTP request sent previously.
/// </summary>
/// <remarks>The idea is that the Tor circuit is used for just one HTTP request and not more.</remarks>
public class OneOffCircuit : ICircuit, IDisposable
{
	private volatile bool _isActive;

	public OneOffCircuit(string? purpose = null)
	{
		Name = RandomString.CapitalAlphaNumeric(21);
		_isActive = true;
		Purpose = purpose;
	}

	/// <inheritdoc/>
	public string Name { get; }

	/// <inheritdoc/>
	public bool IsActive => _isActive;

	/// <inheritdoc/>
	public string? Purpose { get; }

	/// <inheritdoc/>
	public override string ToString()
	{
		if (Purpose is null)
		{
			return $"[{nameof(OneOffCircuit)}: {Name}]";
		}
		else
		{
			return $"[{nameof(OneOffCircuit)}: {Name}|{Purpose}]";
		}
	}

	public void Dispose()
	{
		_isActive = false;
	}
}
