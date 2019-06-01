using System;
using WalletWasabi.Bases;

namespace WalletWasabi.TorSocks5.Models.Fields.OctetFields
{
	public class RepField : OctetSerializableBase
	{
		#region Statics

		// https://www.ietf.org/rfc/rfc1928.txt
		// REP    Reply field:
		// o X'00' succeeded
		// o X'01' general SOCKS server failure
		// o X'02' connection not allowed by ruleset
		// o X'03' Network unreachable
		// o X'04' Host unreachable
		// o X'05' Connection refused
		// o X'06' TTL expired
		// o X'07' Command not supported
		// o X'08' Address type not supported
		// o X'09' to X'FF' unassigned

		public static RepField Succeeded
		{
			get
			{
				var cmd = new RepField();
				cmd.FromByte((byte)ReplyType.Succeeded);
				return cmd;
			}
		}

		public static RepField GeneralSocksServerFailure
		{
			get
			{
				var cmd = new RepField();
				cmd.FromByte((byte)ReplyType.GeneralSocksServerFailure);
				return cmd;
			}
		}

		public static RepField CconnectionNotAllowedByRuleset
		{
			get
			{
				var cmd = new RepField();
				cmd.FromByte((byte)ReplyType.ConnectionNotAllowedByRuleset);
				return cmd;
			}
		}

		public static RepField NetworkUnreachable
		{
			get
			{
				var cmd = new RepField();
				cmd.FromByte((byte)ReplyType.NetworkUnreachable);
				return cmd;
			}
		}

		public static RepField HostUnreachable
		{
			get
			{
				var cmd = new RepField();
				cmd.FromByte((byte)ReplyType.HostUnreachable);
				return cmd;
			}
		}

		public static RepField ConnectionRefused
		{
			get
			{
				var cmd = new RepField();
				cmd.FromByte((byte)ReplyType.ConnectionRefused);
				return cmd;
			}
		}

		public static RepField TtlExpired
		{
			get
			{
				var cmd = new RepField();
				cmd.FromByte((byte)ReplyType.TtlExpired);
				return cmd;
			}
		}

		public static RepField CommandNoSupported
		{
			get
			{
				var cmd = new RepField();
				cmd.FromByte((byte)ReplyType.CommandNotSupported);
				return cmd;
			}
		}

		public static RepField AddressTypeNotSupported
		{
			get
			{
				var cmd = new RepField();
				cmd.FromByte((byte)ReplyType.AddressTypeNotSupported);
				return cmd;
			}
		}

		#endregion Statics

		#region ConstructorsAndInitializers

		public RepField()
		{
		}

		#endregion ConstructorsAndInitializers

		#region Serialization

		public override string ToString()
		{
			foreach (ReplyType rt in Enum.GetValues(typeof(ReplyType)))
			{
				if (ByteValue == (byte)rt)
				{
					return rt.ToString();
				}
			}
			return $"Unassigned ({ToHex()})";
		}

		#endregion Serialization
	}
}
