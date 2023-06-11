namespace WalletWasabi.Affiliation.Models.CoinJoinNotification;

public record Header(string Title, string AffiliationId, int Version)
{
	public static Header Create(string affiliationId) => new(Title: "coinjoin notification", AffiliationId: affiliationId, Version: 1);
}
