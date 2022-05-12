using Microsoft.VisualBasic;

namespace WalletWasabi.Tor.Socks5.Models;

/// <seealso href="https://github.com/torproject/torspec/blob/main/proposals/304-socks5-extending-hs-error-codes.txt"/>
/// <seealso href="https://github.com/torproject/tor/blob/ea2ada6d1459f829446b6b1e66c557d1b084e78b/src/lib/net/socks5_status.h"/>
public enum ReplyType : byte
{
	Succeeded = 0x00,
	GeneralSocksServerFailure = 0x01,
	ConnectionNotAllowedByRuleset = 0x02,
	NetworkUnreachable = 0x03,
	HostUnreachable = 0x04,
	ConnectionRefused = 0x05,
	TtlExpired = 0x06,
	CommandNotSupported = 0x07,
	AddressTypeNotSupported = 0x08,

	// Extended errors follow. "ExtendedErrors" must be provided in "SocksPort"  for the
	// error codes to be returned.

	/// <summary>Onion service descriptor can not be found</summary>
	/// <remarks>
	/// The requested onion service descriptor can't be found on the hashring
	/// and thus not reachable by the client.
	/// </remarks>
	OnionServiceNotFound = 0xF0,

	/// <summary>Onion service descriptor is invalid</summary>
	/// <remarks>
	/// The requested onion service descriptor can't be parsed or signature validation failed.
	/// </remarks>
	OnionServiceIsInvalid = 0xF1,

	/// <summary>Onion service introduction failed</summary>
	/// <remarks>
	/// Client failed to introduce to the service meaning the descriptor was
	/// found but the service is not anymore at the introduction points. The
	/// service has likely changed its descriptor or is not running.
	/// </remarks>
	OnionServiceIntroFailed = 0xF2,

	/// <summary>Onion service rendezvous failed</summary>
	/// <remarks>
	/// Client failed to rendezvous with the service which means that the client is
	/// unable to finalize the connection.
	/// </remarks>
	OnionServiceRendFailed = 0xF3,

	/// <summary>Onion service missing client authorization</summary>
	/// <remarks>
	/// Tor was able to download the requested onion service descriptor but is unable
	/// to decrypt its content because it is missing client authorization information for it.
	/// </remarks>
	OnionServiceMissingClient_Auth = 0xF4,

	/// <summary>Onion service wrong client authorization</summary>
	/// <remarks>
	/// Tor was able to download the requested onion service descriptor but is
	/// unable to decrypt its content using the client authorization information
	/// it has.This means the client access were revoked.
    /// </remarks>
	OnionServiceBadClientAuth = 0xF5,

	/// <summary>Onion service invalid address</summary>
	/// <remarks>
	/// The given .onion address is invalid. In one of these cases this
	/// error is returned: address checksum doesn't match, ed25519 public
	/// key is invalid or the encoding is invalid. (v3 only)
	/// </remarks>
	OnionServiceBadAddress = 0xF6,

	/// <summary> Onion service introduction timed out</summary>
	/// <remarks>
	/// Similar to X'F2' code but in this case, all introduction attempts
	/// have failed due to a time out. (v3 only)
	/// </remarks>
	OnionServiceIntroTimedOut = 0xF7,
}
