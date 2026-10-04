using NBitcoin;
using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using WalletWasabi.Io;

namespace WalletWasabi.Mobile;

internal sealed record JournalEntry(
  string ProposalId, string WalletId, string Network, PaymentOperation Operation,
  string? OriginalTransactionId, string TransactionId, string Hex,
  SubmissionState State, DateTimeOffset CreatedAt,
  long AmountSatoshis = 0, long FeeSatoshis = 0,
  [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] ImmutableArray<ProposalOutput> Outputs = default);

internal sealed class TransactionJournal
{
  private readonly string _path;
  private readonly Network _network;
  private readonly object _gate = new();
  private ImmutableArray<JournalEntry> _entries;

  public TransactionJournal(string directory, Network network)
  {
    _network = network;
    _path = Path.Combine(directory, "submissions-" + network.Name + ".json");
    _entries = File.Exists(_path) || File.Exists(_path + ".old")
      ? JsonSerializer.Deserialize<ImmutableArray<JournalEntry>>(File.SafelyReadAllText(_path, Encoding.UTF8))
      : [];
    if (_entries.IsDefault) { throw new IOException("The transaction journal is invalid. Restore its last usable copy before sending."); }
    foreach (var entry in _entries)
    {
      var transaction = Transaction.Parse(entry.Hex, network);
      if (entry.Network != network.Name || transaction.GetHash().ToString() != entry.TransactionId
        || string.IsNullOrWhiteSpace(entry.WalletId) || string.IsNullOrWhiteSpace(entry.ProposalId)
        || entry.AmountSatoshis < 0 || entry.FeeSatoshis < 0 || !Enum.IsDefined(entry.State) || !Enum.IsDefined(entry.Operation)
        || !entry.Outputs.IsDefault && !entry.Outputs.Select(o => (o.ScriptHex, o.AmountSatoshis)).SequenceEqual(transaction.Outputs.Select(o => (o.ScriptPubKey.ToHex(), o.Value.Satoshi))))
      {
        throw new IOException("The transaction journal is inconsistent. Sending remains locked.");
      }
    }
  }

  public ImmutableArray<JournalEntry> Entries { get { lock (_gate) { return _entries; } } }
  public HashSet<OutPoint> Reservations() => Entries
    .Where(e => e.State is SubmissionState.Uncertain or SubmissionState.Pending)
    .SelectMany(e => Transaction.Parse(e.Hex, _network).Inputs.Select(i => i.PrevOut))
    .ToHashSet();

  public void Put(JournalEntry entry)
  {
    lock (_gate)
    {
    var updated = _entries.Where(e => e.ProposalId != entry.ProposalId).Append(entry).ToImmutableArray();
    try { File.SafelyWriteAllText(_path, JsonSerializer.Serialize(updated), Encoding.UTF8); }
    catch
    {
      // A flush/rename can fail after publishing the file. Conservatively retain
      // the approved bytes and inputs in memory as well; a different payment
      // must not use them while the failed write is being reconciled.
      _entries = _entries.Where(e => e.ProposalId != entry.ProposalId).Append(entry with { State = SubmissionState.Uncertain }).ToImmutableArray();
      throw;
    }
    // Publication in memory follows a successful durable write.
    _entries = updated;
    }
  }
}
