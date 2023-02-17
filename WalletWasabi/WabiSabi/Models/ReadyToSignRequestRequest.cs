using System.Linq;
using NBitcoin;
using Newtonsoft.Json;
using WalletWasabi.Affiliation;

namespace WalletWasabi.WabiSabi.Models;

public record ReadyToSignRequestRequest
{
	[JsonConstructor]
	public ReadyToSignRequestRequest(
		uint256 roundId,
		Guid aliceId,
		string? affiliationId = null)
	{
		RoundId = roundId;
		AliceId = aliceId;
		AffiliationId = affiliationId is { } nonNullAffilitiationFlag && IsValidAffiliationName(nonNullAffilitiationFlag)
				? nonNullAffilitiationFlag 
				: AffiliationConstants.DefaultAffiliationId;
	}
	public uint256 RoundId { get; }
	public Guid AliceId { get; }
	public string AffiliationId { get; }
	
	private static bool IsValidAffiliationName(string name)
	{
		const int MinimumNameLength = 1;
		const int MaximumNameLength = 20;
		static bool IsValidLength(string text) => text.Length is >= MinimumNameLength and <= MaximumNameLength;
		static bool IsAlphanumeric(string text) => text.All(x => char.IsAscii(x) && char.IsLetterOrDigit(x));
		return IsValidLength(name) && IsAlphanumeric(name);
	}
}
