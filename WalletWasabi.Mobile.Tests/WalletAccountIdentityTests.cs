using NBitcoin;
using WalletWasabi.Mobile;
using WalletWasabi.WabiSabi.Coordinator.Rounds;
using Xunit;

namespace WalletWasabi.Mobile.Tests;

public class WalletAccountIdentityTests
{
    [Fact]
    public void LegacyOwnershipRequiresEveryInputAndAUniqueAccount()
    {
        var first = new OutPoint(uint256.One, 0);
        var second = new OutPoint(uint256.One, 1);
        var unrelated = new OutPoint(uint256.One, 2);
        var partial = new WalletAccountIdentity.Ownership("partial", "legacy", new HashSet<OutPoint> { first });
        var owner = new WalletAccountIdentity.Ownership("owner", "legacy", new HashSet<OutPoint> { first, second });
        Assert.Null(WalletAccountIdentity.ResolveLegacyOwner("legacy", [first, second], [partial]));
        Assert.Null(WalletAccountIdentity.ResolveLegacyOwner("legacy", [first, unrelated], [owner]));
        Assert.Null(WalletAccountIdentity.ResolveLegacyOwner("legacy", [], [owner]));
        Assert.Null(WalletAccountIdentity.ResolveLegacyOwner("unknown", [first], [owner]));
        Assert.Null(WalletAccountIdentity.ResolveLegacyOwner("legacy", [first, second], [owner, owner with { Reference = "ambiguous" }]));
        Assert.Equal("owner", WalletAccountIdentity.ResolveLegacyOwner("legacy", [first, second], [partial, owner]));
        Assert.Equal("owner", WalletAccountIdentity.ResolveLegacyOwner("legacy", [first, second], [owner, owner]));
    }

    [Fact]
    public void MigrationPreservesSignedBytesAndReservationsAfterRestart()
    {
        var directory = Path.Combine(Path.GetTempPath(), "wasabi-mobile-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var input = new OutPoint(uint256.One, 4);
        var transaction = Network.RegTest.CreateTransaction();
        transaction.Inputs.Add(new TxIn(input));
        using var key = new Key();
        transaction.Outputs.Add(Money.Satoshis(10000), key.PubKey.WitHash.ScriptPubKey);
        var entry = new JournalEntry("approved", "legacy", Network.RegTest.Name, PaymentOperation.Payment, null,
            transaction.GetHash().ToString(), transaction.ToHex(), SubmissionState.Uncertain, DateTimeOffset.UtcNow,
            10000, 500, [new(transaction.Outputs[0].ScriptPubKey.ToHex(), null, 10000, false, true)]);
        var payments = new TransactionJournal(directory, Network.RegTest);
        payments.Put(entry);
        payments.Put(entry with { WalletId = "owner" });
        var restoredPayments = new TransactionJournal(directory, Network.RegTest);
        var restoredPayment = restoredPayments.Entries.Single();
        Assert.Equal(entry with { WalletId = "owner" }, restoredPayment with { Outputs = entry.Outputs });
        Assert.True(entry.Outputs.SequenceEqual(restoredPayment.Outputs));
        Assert.Contains(input, restoredPayments.Reservations());

        var rounds = new CoinJoinJournal(directory, Network.RegTest);
        var checkpoint = rounds.ForWallet("legacy", () => { }, _ => false);
        checkpoint.BeginRound(uint256.One, [input]);
        checkpoint.BeforeSigning(uint256.One, transaction.GetHash());
        var before = rounds.Entries.Single();
        rounds.ReassignWallet(before, "owner");
        var restoredRounds = new CoinJoinJournal(directory, Network.RegTest);
        var restoredRound = restoredRounds.Entries.Single();
        Assert.Equal(before with { WalletId = "owner" }, restoredRound with { Inputs = before.Inputs });
        Assert.True(before.Inputs.SequenceEqual(restoredRound.Inputs));
        Assert.Contains(input, restoredRounds.Reservations());
        restoredRounds.ForWallet("owner", () => { }, _ => false).EndRound(uint256.One, EndRoundState.AbortedWithError);
        Assert.Contains(input, new CoinJoinJournal(directory, Network.RegTest).Reservations());
    }

    [Fact]
    public void AmbiguousRoundMigrationPreservesBothCheckpoints()
    {
        var directory = Path.Combine(Path.GetTempPath(), "wasabi-mobile-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var journal = new CoinJoinJournal(directory, Network.RegTest);
        var first = new OutPoint(uint256.One, 1);
        var second = new OutPoint(uint256.One, 2);
        journal.ForWallet("legacy", () => { }, _ => false).BeginRound(uint256.One, [first]);
        journal.ForWallet("owner", () => { }, _ => false).BeginRound(uint256.One, [second]);
        Assert.Throws<IOException>(() => journal.ReassignWallet(journal.Entries.Single(e => e.WalletId == "legacy"), "owner"));
        var reopened = new CoinJoinJournal(directory, Network.RegTest);
        Assert.Equal(2, reopened.Entries.Length);
        Assert.Contains(first, reopened.Reservations());
        Assert.Contains(second, reopened.Reservations());
    }
}
