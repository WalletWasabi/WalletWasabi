using WalletWasabi.Crypto.Randomness;

namespace WalletWasabi.Tests.Helpers;

public static class RandomString
{
	private static readonly RandomStringGenerator InsecureGenerator = RandomnessProviders.Insecure.CreateRandomStringGenerator();

	public static string AlphaNumeric(int length) =>
		InsecureGenerator(length);
}
