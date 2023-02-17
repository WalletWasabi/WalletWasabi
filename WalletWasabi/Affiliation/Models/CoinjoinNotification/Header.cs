namespace WalletWasabi.Affiliation.Models.CoinJoinNotification;

public record Header(string Title, int Version)
{
	public static readonly Header Instance = new(Title: "coinjoin notification", Version: 1);
}
