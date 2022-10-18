using System.ComponentModel;
using System.Linq;
using WalletWasabi.Affiliation.Serialization;

namespace WalletWasabi.Affiliation;

[TypeConverter(typeof(AffiliationFlagConverter))]
public record AffiliationFlag
{
	public static readonly AffiliationFlag Default = new("WalletWasabi");
	public static readonly AffiliationFlag Trezor = new("trezor");

	private const int MinimumNameLength = 1;
	private const int MaximumNameLength = 20;

	public AffiliationFlag(string name)
	{
		if (!IsValidName(name))
		{
			throw new ArgumentException("The name is too long, too short or contains non-alphanumeric characters.", nameof(name));
		}
		Name = name;
	}

	public string Name { get; }

	public override string ToString()
	{
		return Name;
	}

	private static bool IsAlphanumeric(string text)
	{
		return text.All(x => char.IsAscii(x) && char.IsLetterOrDigit(x));
	}

	private static bool IsValidName(string name)
	{
		if (!IsAlphanumeric(name))
		{
			return false;
		}

		if (name.Length < MinimumNameLength || name.Length > MaximumNameLength)
		{
			return false;
		}

		return true;
	}
}
