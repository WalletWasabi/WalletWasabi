using WalletWasabi.Helpers;

namespace WalletWasabi.Crypto.Randomness;

public static class RandomString
{
	public static string FromCharacters(int length, string characters, bool secureRandom = false)
	{
		using WasabiRandom random = secureRandom ? new SecureRandom() : new InsecureRandom();

		var res = random.GetString(length, characters);
		return res;
	}

	public static string AlphaNumeric(int length, bool secureRandom = false) => FromCharacters(length, Constants.AlphaNumericCharacters, secureRandom);

	public static string CapitalAlphaNumeric(int length, bool secureRandom = false) => FromCharacters(length, Constants.CapitalAlphaNumericCharacters, secureRandom);
}
