using NBitcoin;
using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using WalletWasabi.Io;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Coordinator.Rounds;

namespace WalletWasabi.Mobile;

internal sealed record RoundInput(string TransactionId, uint Index);
internal sealed record RoundCheckpoint(string WalletId, string RoundId, ImmutableArray<RoundInput> Inputs, string? TransactionId);

internal sealed class CoinJoinJournal
{
	private readonly object _gate = new();
	private readonly string _path;
	private ImmutableArray<RoundCheckpoint> _entries;
	public CoinJoinJournal(string directory, Network network)
	{
		_path = Path.Combine(directory, "coinjoins-" + network.Name + ".json");
		_entries = File.Exists(_path) || File.Exists(_path + ".old")
			? JsonSerializer.Deserialize<ImmutableArray<RoundCheckpoint>>(File.SafelyReadAllText(_path, Encoding.UTF8)) : [];
		if (_entries.IsDefault) { throw new IOException("The CoinJoin journal is invalid. Reconcile it before spending."); }
		foreach (var entry in _entries)
		{
			if (string.IsNullOrWhiteSpace(entry.WalletId) || !uint256.TryParse(entry.RoundId, out _) || entry.Inputs.IsDefaultOrEmpty
				|| entry.TransactionId is { } txid && !uint256.TryParse(txid, out _) || entry.Inputs.Any(i => !uint256.TryParse(i.TransactionId, out _)))
			{ throw new IOException("The CoinJoin journal is inconsistent. Sending remains locked."); }
		}
	}
	public ImmutableArray<RoundCheckpoint> Entries { get { lock (_gate) { return _entries; } } }
	public HashSet<OutPoint> Reservations() => Entries.SelectMany(e => e.Inputs.Select(i => new OutPoint(uint256.Parse(i.TransactionId), i.Index))).ToHashSet();
	public void Remove(RoundCheckpoint entry) { lock (_gate) { Save(_entries.Where(e => e.WalletId != entry.WalletId || e.RoundId != entry.RoundId).ToImmutableArray()); } }
	public void ReassignWallet(RoundCheckpoint entry, string walletId)
	{
		lock (_gate)
		{
			if (string.IsNullOrWhiteSpace(walletId) || !_entries.Contains(entry)
				|| _entries.Any(e => e.WalletId == walletId && e.RoundId == entry.RoundId))
			{ throw new IOException("The interrupted round owner is ambiguous. Keep its reservations until reconciliation."); }
			Save(_entries.Replace(entry, entry with { WalletId = walletId }));
		}
	}
	private void Save(ImmutableArray<RoundCheckpoint> entries)
	{
		File.SafelyWriteAllText(_path, JsonSerializer.Serialize(entries), Encoding.UTF8);
		_entries = entries;
	}
	public ICoinJoinCheckpointStore ForWallet(string walletId, Action persistAddresses, Func<OutPoint, bool> paymentReserved) => new WalletCheckpoints(this, walletId, persistAddresses, paymentReserved);
	private sealed class WalletCheckpoints(CoinJoinJournal journal, string walletId, Action persistAddresses, Func<OutPoint, bool> paymentReserved) : ICoinJoinCheckpointStore
	{
		public bool IsReserved(OutPoint input) => paymentReserved(input) || journal.Reservations().Contains(input);
		public void BeginRound(uint256 roundId, IEnumerable<OutPoint> inputs)
		{
			persistAddresses();
			var entry = new RoundCheckpoint(walletId, roundId.ToString(), inputs.Select(i => new RoundInput(i.Hash.ToString(), i.N)).ToImmutableArray(), null);
			lock (journal._gate)
			{
				if (journal._entries.Any(e => e.WalletId == walletId && e.RoundId == entry.RoundId)) { throw new InvalidOperationException("This round must be reconciled before reuse."); }
				journal.Save(journal._entries.Add(entry));
			}
		}
		public void BeforeSigning(uint256 roundId, uint256 transactionId)
		{
			persistAddresses();
			lock (journal._gate)
			{
				var entry = journal._entries.Single(e => e.WalletId == walletId && e.RoundId == roundId.ToString());
				journal.Save(journal._entries.Replace(entry, entry with { TransactionId = transactionId.ToString() }));
			}
		}
		public void EndRound(uint256 roundId, EndRoundState outcome)
		{
			// Once witnesses might have left this device, a coordinator's failure
			// report cannot prove that the signed transaction cannot be broadcast.
			// Only observed transaction/conflict reconciliation releases these inputs.
			lock (journal._gate)
			{
				var checkpoint = journal._entries.FirstOrDefault(e => e.WalletId == walletId && e.RoundId == roundId.ToString());
				if (checkpoint?.TransactionId is not null) { return; }
				journal.Save(journal._entries.Where(e => e.WalletId != walletId || e.RoundId != roundId.ToString()).ToImmutableArray());
			}
		}
	}
}
