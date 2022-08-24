using WalletWasabi.Crypto.Randomness;

namespace WalletWasabi.Tor.Socks5.Pool.Circuits;

/// <summary>Tor circuit that exists for the entire Wasabi Wallet application life.</summary>
/// <remarks>
/// Use this class for HTTP requests where privacy is not important really.
/// This includes downloading fee rates from a 3rd party service, etc.
/// </remarks>
public class DefaultCircuit : ICircuit
{
	public static readonly DefaultCircuit Instance = new();

	private static string RandomName = RandomString.CapitalAlphaNumeric(21);

	private DefaultCircuit(string? purpose = null)
	{
		Purpose = purpose;
	}

	/// <inheritdoc/>
	public string Name => RandomName;

	/// <inheritdoc/>
	public bool IsActive => true;

	/// <inheritdoc/>
	public string? Purpose { get; }

	/// <inheritdoc/>
	public override string ToString()
	{
		if (Purpose is null)
		{
			return $"[{nameof(DefaultCircuit)}:{Name}]";
		}
		else
		{
			return $"[{nameof(PersonCircuit)}:{Name}|{Purpose}]";
		}
	}
}
