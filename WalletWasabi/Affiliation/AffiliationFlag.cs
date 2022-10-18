using System.ComponentModel;
using System.Linq;
using WalletWasabi.Affiliation.Serialization;

namespace WalletWasabi.Affiliation;

[TypeConverter(typeof(AffiliationFlagConverter))]
public class AffiliationFlag : IEquatable<AffiliationFlag>
{
	public static AffiliationFlag Default = new AffiliationFlag("WalletWasabi");
	public static AffiliationFlag Trezor = new AffiliationFlag("trezor");

	private const int MinimumNameLength = 1;
	private const int MaximumNameLength = 20;

	private string _name = "";

	public AffiliationFlag(string name)
	{
		Name = name;
	}

	public string Name
	{
		get
		{
			return _name;
		}
		private set
		{
			if (!IsAlphanumeric(value))
			{
				throw new Exception("Name of the affiliation flag contains nonalphanumeric character.");
			}

			if (value.Length < MinimumNameLength)
			{
				throw new Exception("Name of the affiliation flag is too short.");
			}

			if (value.Length > MaximumNameLength)
			{
				throw new Exception("Name of the affiliation flag is too long.");
			}

			_name = value;
		}
	}

	public bool Equals(AffiliationFlag? other)
	{
		return Name == other?.Name;
	}

	public override string ToString()
	{
		return Name;
	}

	private static bool IsAlphanumeric(string text)
	{
		return text.All(char.IsLetterOrDigit);
	}
}
