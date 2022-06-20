using WalletWasabi.Crypto.Randomness;

namespace WalletWasabi.Tor.Socks5.Pool.Circuits;

/// <summary>
/// Tor circuit for a set of HTTP requests where we don't mind that
/// HTTP requests can be identified as belonging to a single entity (i.e. user).
/// </summary>
/// <remarks>Useful for Alices and Bobs.</remarks>
public class PersonCircuit : ICircuit, IDisposable
{
	private volatile bool _isActive;

	public PersonCircuit(string? purpose = null)
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
			return $"[{nameof(PersonCircuit)}: {Name}]";
		}
		else
		{
			return $"[{nameof(PersonCircuit)}: {Name}|{Purpose}]";
		}
	}

	public void Dispose()
	{
		_isActive = false;
	}
}
