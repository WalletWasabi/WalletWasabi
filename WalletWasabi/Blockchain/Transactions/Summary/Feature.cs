namespace WalletWasabi.Blockchain.Transactions.Summary;

public record Feature(string Name)
{
	public static Feature Taproot = new("Taproot");
	public static Feature RBF = new("RBF");
	public static Feature SegWit = new("SegWit");
}
