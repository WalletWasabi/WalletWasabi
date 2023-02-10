namespace WalletWasabi.Affiliation.Models.CoinjoinRequest;

public record Header(string Title, int Version)
{
	public static readonly Header Instance = new(Title: "payment request", Version: 1);
}
