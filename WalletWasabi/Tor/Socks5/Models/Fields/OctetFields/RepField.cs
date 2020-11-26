using System;
using WalletWasabi.Tor.Socks5.Models.Bases;

namespace WalletWasabi.Tor.Socks5.Models.Fields.OctetFields
{
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
		public RepField(byte byteValue)
		{
			ByteValue = byteValue;
		}

		#region Statics

		public static RepField Succeeded => new RepField((byte)ReplyType.Succeeded);

		public static RepField GeneralSocksServerFailure => new RepField((byte)ReplyType.GeneralSocksServerFailure);

		public static RepField CconnectionNotAllowedByRuleset => new RepField((byte)ReplyType.ConnectionNotAllowedByRuleset);

		public static RepField NetworkUnreachable => new RepField((byte)ReplyType.NetworkUnreachable);

		public static RepField HostUnreachable => new RepField((byte)ReplyType.HostUnreachable);

		public static RepField ConnectionRefused => new RepField((byte)ReplyType.ConnectionRefused);

		public static RepField TtlExpired => new RepField((byte)ReplyType.TtlExpired);

		public static RepField CommandNoSupported => new RepField((byte)ReplyType.CommandNotSupported);

		public static RepField AddressTypeNotSupported => new RepField((byte)ReplyType.AddressTypeNotSupported);

		#endregion Statics

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
}
