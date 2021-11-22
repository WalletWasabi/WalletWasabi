using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Crypto;
using WalletWasabi.Crypto.Groups;
using WalletWasabi.Crypto.ZeroKnowledge;

namespace WalletWasabi.WabiSabi.Crypto.CredentialRequesting
{
	/// <summary>
	/// Represents a request message for the WabiSabi unified registration protocol.
	/// </summary>
	/// <remarks>
	/// RegistrationRequestMessage message is the unified protocol message used in both phases,
	/// inputs registration and outputs registration and it is designed to support
	/// credentials reissuance.
	/// </remarks>
	public abstract class CredentialsRequest : IEquatable<CredentialsRequest>
	{
		[JsonConstructor]
		internal CredentialsRequest(
			long delta,
			IEnumerable<CredentialPresentation> presented,
			IEnumerable<IssuanceRequest> requested,
			IEnumerable<Proof> proofs)
		{
			Delta = delta;
			Presented = presented;
			Requested = requested;
			Proofs = proofs;
		}

		/// <summary>
		/// The difference between the sum of the requested credentials and the presented credentials.
		/// </summary>
		/// <remarks>
		/// A positive value of this property indicates that the request is an inputs registration request,
		/// a negative value indicates it is an outputs registration request, while finally a zero value
		/// indicates it is a reissuance request or a request for zero-value credentials.
		/// </remarks>
		public long Delta { get; }

		/// <summary>
		/// Randomized credentials presented for output registration or reissuance.
		/// </summary>
		public IEnumerable<CredentialPresentation> Presented { get; }

		/// <summary>
		/// Credential isssuance requests.
		/// </summary>
		public IEnumerable<IssuanceRequest> Requested { get; }

		/// <summary>
		/// Accompanying range and sum proofs to the coordinator.
		/// </summary>
		public IEnumerable<Proof> Proofs { get; }

		/// <summary>
		/// Is request for zero-value credentials only.
		/// </summary>
		[JsonIgnore]
		internal bool IsNullRequest => Delta == 0 && !Presented.Any();

		/// <summary>
		/// Is request for credential presentation only.
		/// </summary>
		[JsonIgnore]
		internal bool IsPresentationOnlyRequest => Delta < 0 && !Requested.Any();

		/// <summary>
		/// Serial numbers used in the credential presentations.
		/// </summary>
		[JsonIgnore]
		internal IEnumerable<GroupElement> SerialNumbers => Presented.Select(x => x.S);

		/// <summary>
		/// Indicates whether the message contains duplicated serial numbers or not.
		/// </summary>
		[JsonIgnore]
		internal bool AreThereDuplicatedSerialNumbers => SerialNumbers.Distinct().Count() < SerialNumbers.Count();

		public override int GetHashCode()
		{
			int hc = 0;

			foreach (var element in Presented)
			{
				hc ^= element.GetHashCode();
				hc = (hc << 7) | (hc >> (32 - 7));
			}

			foreach (var element in Requested)
			{
				hc ^= element.GetHashCode();
				hc = (hc << 7) | (hc >> (32 - 7));
			}

			foreach (var element in Proofs)
			{
				hc ^= element.GetHashCode();
				hc = (hc << 7) | (hc >> (32 - 7));
			}

			return HashCode.Combine(Delta.GetHashCode(), hc);
		}

		public static bool operator ==(CredentialsRequest? x, CredentialsRequest? y) => x?.Equals(y) ?? false;

		public static bool operator !=(CredentialsRequest? x, CredentialsRequest? y) => !(x == y);

		public override bool Equals(object? other) => Equals(other as CredentialsRequest);

		public bool Equals(CredentialsRequest? other)
		{
			if (other is null)
			{
				return false;
			}

			bool isEqual = Delta == other.Delta 
				&& Presented.SequenceEqual(other.Presented)
				&& Requested.SequenceEqual(other.Requested)
				&& Proofs.SequenceEqual(other.Proofs);

			return isEqual;
		}
	}
}
