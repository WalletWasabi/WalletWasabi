using System.Linq;
using System.Collections.Generic;
using WalletWasabi.Crypto.Groups;

namespace WalletWasabi.WabiSabi.Crypto.CredentialRequesting;

public static class CredentialsRequestExtensions
{
	/// <summary>
	/// Is request for zero-value credentials only.
	/// </summary>
	public static bool IsNullRequest(this ICredentialsRequest request) => request.Delta == 0 && !request.Presented.Any();

	/// <summary>
	/// Is request for credential presentation only.
	/// </summary>
	public static bool IsPresentationOnlyRequest(this ICredentialsRequest request) => request.Delta < 0 && !request.Requested.Any();

	/// <summary>
	/// Serial numbers used in the credential presentations.
	/// </summary>
	public static IEnumerable<GroupElement> SerialNumbers(this ICredentialsRequest request) => request.Presented.Select(x => x.S);

	/// <summary>
	/// Indicates whether the message contains duplicated serial numbers or not.
	/// </summary>
	public static bool AreThereDuplicatedSerialNumbers(this ICredentialsRequest request) => request.SerialNumbers().Distinct().Count() < request.SerialNumbers().Count();
}
