using System;
using NBitcoin;

namespace HBitcoin.KeyManagement
{
	public enum HdPathType
	{
		Stealth,
		Receive,
		Change,
		NonHardened,
		Account // special
	}

	public static class Hierarchy
	{
		public static string GetPathString(HdPathType type)
		{
			switch(type)
			{
				case HdPathType.Stealth:
					return "0'";
				case HdPathType.Receive:
					return "1'";
				case HdPathType.Change:
					return "2'";
				case HdPathType.NonHardened:
					return "3";
				case HdPathType.Account:
					return "4'"; // special
				default:
					throw new ArgumentOutOfRangeException(nameof(type), type, null);
			}
		}

		public static string GetPathString(SafeAccount account) => account.PathString;
	}

	public class SafeAccount: IEquatable<SafeAccount>
	{
		public readonly uint Id;
		public readonly string PathString;

		public SafeAccount(uint id)
		{
			try
			{
				string firstPart = Hierarchy.GetPathString(HdPathType.Account);

				string lastPart = $"/{id}'";
				PathString = firstPart + lastPart;

				KeyPath.Parse(PathString);
			}
			catch(Exception ex)
			{
				throw new ArgumentOutOfRangeException($"{nameof(id)} : {id}", ex);
			}

			Id = id;
		}

		#region Equality

		public bool Equals(SafeAccount other) => PathString.Equals(other.PathString, StringComparison.Ordinal);

		public override bool Equals(object obj)
		{
			bool rc = false;

            var transaction = obj as SafeAccount;
            if(obj != null) rc = PathString.Equals(transaction.PathString, StringComparison.Ordinal);
			
			return rc;
		}

		public override int GetHashCode()
		{
			return PathString.GetHashCode();
		}

		public static bool operator !=(SafeAccount sa1, SafeAccount sa2)
		{
			return !(sa1 == sa2);
		}

		public static bool operator ==(SafeAccount sa1, SafeAccount sa2)
		{
			bool rc;

			if(ReferenceEquals(sa1, sa2)) rc = true;

			else if((object) sa1 == null || (object) sa2 == null)
			{
				rc = false;
			}
			else
			{
				rc = sa1.PathString.Equals(sa2.PathString, StringComparison.Ordinal);
			}

			return rc;
		}

		#endregion
	}
}
