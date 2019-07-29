namespace WalletWasabi.TorSocks5.Models
{
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
		AddressTypeNotSupported = 0x08
	}
}
