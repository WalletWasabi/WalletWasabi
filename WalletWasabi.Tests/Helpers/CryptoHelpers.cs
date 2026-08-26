namespace WalletWasabi.Tests.Helpers;

public static class CryptoHelpers
{
	public static int RandomInt(int minInclusive, int maxInclusive)
		=> Random.Shared.Next(minInclusive, maxInclusive + 1);
}
