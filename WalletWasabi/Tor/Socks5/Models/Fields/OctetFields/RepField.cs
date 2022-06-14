using WalletWasabi.Tor.Socks5.Models.Bases;

namespace WalletWasabi.Tor.Socks5.Models.Fields.OctetFields;

/// <summary>
/// Possible values of "reply field" are:
/// <list type="bullet">
///   <item>X'00' succeeded</item>
///   <item>X'01' general SOCKS server failure</item>
///   <item>X'02' connection not allowed by ruleset</item>
///   <item>X'03' Network unreachable</item>
///   <item>X'04' Host unreachable</item>
///   <item>X'05' Connection refused</item>
///   <item>X'06' TTL expired</item>
///   <item>X'07' Command not supported</item>
///   <item>X'08' Address type not supported</item>
///   <item>X'09' to X'FF' unassigned</item>
/// </list>
/// </summary>
/// <seealso href="https://www.ietf.org/rfc/rfc1928.txt"/>
public class RepField : OctetSerializableBase
{
	public static readonly RepField Succeeded = new((byte)ReplyType.Succeeded);

	public static readonly RepField GeneralSocksServerFailure = new((byte)ReplyType.GeneralSocksServerFailure);

	public static readonly RepField ConnectionNotAllowedByRuleset = new((byte)ReplyType.ConnectionNotAllowedByRuleset);

	public static readonly RepField NetworkUnreachable = new((byte)ReplyType.NetworkUnreachable);

	public static readonly RepField HostUnreachable = new((byte)ReplyType.HostUnreachable);

	public static readonly RepField ConnectionRefused = new((byte)ReplyType.ConnectionRefused);

	public static readonly RepField TtlExpired = new((byte)ReplyType.TtlExpired);

	public static readonly RepField CommandNoSupported = new((byte)ReplyType.CommandNotSupported);

	public static readonly RepField AddressTypeNotSupported = new((byte)ReplyType.AddressTypeNotSupported);

	public static readonly RepField OnionServiceIntroFailed = new((byte)ReplyType.OnionServiceIntroFailed);

	public RepField(byte byteValue)
	{
		ByteValue = byteValue;
	}

	public override string ToString()
	{
		foreach (ReplyType rt in (ReplyType[])Enum.GetValues(typeof(ReplyType)))
		{
			if (ByteValue == (byte)rt)
			{
				return rt.ToString();
			}
		}
		return $"Unassigned ({ToHex()})";
	}
}
