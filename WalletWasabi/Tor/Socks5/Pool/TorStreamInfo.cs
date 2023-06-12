using WalletWasabi.Tor.Control.Messages.StreamStatus;

namespace WalletWasabi.Tor.Socks5.Pool;

/// <summary>Latest Tor stream state update.</summary>
/// <remarks>
/// Informs that a Tor stream (corresponds to our <see cref="TorTcpConnection"/>) is currently using
/// the <paramref name="CircuitId">Tor circuit</paramref> and has a certain <paramref name="Status"/>.
/// </remarks>
public record TorStreamInfo(string CircuitId, StreamStatusFlag Status);
