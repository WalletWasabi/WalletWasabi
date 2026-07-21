using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Helpers;

namespace WalletWasabi.Tests.Helpers;

public static class RandomString
{
	public static string AlphaNumeric(int length, bool secureRandom = false)
	{
		var generator = secureRandom ? RandomnessProviders.Secure : RandomnessProviders.Insecure;

		var result = new char[length];
		for (int i = 0; i < length; i++)
		{
			result[i] = Constants.AlphaNumericCharacters[generator.GetInt(Constants.AlphaNumericCharacters.Length)];
		}
		return new string(result);
	}
}