using WabiSabi.Crypto.Randomness;
using WalletWasabi.Helpers;

namespace WalletWasabi.Crypto.Randomness;

public static class RandomString
{
	public static string FromCharacters(int length, string characters, bool secureRandom = false)
	{
		WasabiRandom random = secureRandom ? SecureRandom.Instance : InsecureRandom.Instance;

		var res = random.GetString(length, characters);
		return res;
	}

	public static string AlphaNumeric(int length, bool secureRandom = false) => FromCharacters(length, Constants.AlphaNumericCharacters, secureRandom);
}
