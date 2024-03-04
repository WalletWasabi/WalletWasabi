namespace WalletWasabi.Tor.Control.Messages.CircuitStatus;

/// <remarks>
/// <see cref="FingerPrint"/> is a 40*HEXDIG string.
/// <para><see cref="Nickname"/> matches <c>^[a-zA-Z0-9]{1, 19}$</c>.</para>
/// </remarks>
public record CircuitPath(string FingerPrint, string? Nickname)
{
}
