namespace WalletWasabi.Blockchain.TransactionBuilding.BnB;

/// <summary>Denotes whether we are looking for a payment suggestion with the sum that is larger (or equal) or less (or equal) than the the user payment amount.</summary>
/// <remarks>In payments for goods where one buys a certain one item, the "less suggestion" is rarely useful as the merchant really does not want to accept less bitcoin than the declared price. However, there are many scenarios where the "less suggestions" are useful like buying N items, settlements between friends, donations, etc.</remarks>
public enum SuggestionType
{
	More,
	Less
}
