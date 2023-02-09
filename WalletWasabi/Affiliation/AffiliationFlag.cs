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
		Name = IsValidName(name) 
			? name
			: throw new ArgumentException("The name is too long, too short or contains non-alphanumeric characters.", nameof(name)); 
	}

	public string Name { get; }

	public override string ToString() => Name;

	private static bool IsValidName(string name)
	{
		static bool IsValidLength(string text) => text.Length is >= MinimumNameLength and <= MaximumNameLength;
		static bool IsAlphanumeric(string text) => text.All(x => char.IsAscii(x) && char.IsLetterOrDigit(x));
		return IsValidLength(name) && IsAlphanumeric(name);
	}
}
