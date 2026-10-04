using System.Collections.Immutable;

namespace WalletWasabi.Mobile;

public enum PaymentOperation { Payment, SpeedUp, Cancel }
public enum SubmissionState { Uncertain, Pending, Confirmed, Replaced, Conflicted }
public sealed record ProposalInput(string TransactionId, uint Index, long AmountSatoshis);
public sealed record ProposalOutput(string ScriptHex, string? Address, long AmountSatoshis, bool IsWalletOutput);

// Public review data never exposes the PSBT, signing keys, or mutable Bitcoin objects.
public sealed record PaymentProposal(
  string Id, string WalletId, string Network, PaymentOperation Operation,
  ImmutableArray<ProposalInput> Inputs, ImmutableArray<ProposalOutput> Outputs,
  long AmountSatoshis, long FeeSatoshis, long TotalSatoshis,
  DateTimeOffset ExpiresAt, string? OriginalTransactionId);

public sealed record BroadcastReceipt(string ProposalId, string TransactionId, SubmissionState State);
public sealed record SubmissionDetails(
  string TransactionId, PaymentOperation Operation, SubmissionState State,
  long AmountSatoshis, long FeeSatoshis, DateTimeOffset CreatedAt,
  ImmutableArray<ProposalOutput> Outputs);
