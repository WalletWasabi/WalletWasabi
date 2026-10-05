using NBitcoin;
using System.Text.Json;
using WalletWasabi.Mobile;
using WalletWasabi.WabiSabi.Coordinator.Rounds;
using Xunit;

namespace WalletWasabi.Mobile.Tests;

public class ReleaseSafetyTests
{
  [Theory]
  [InlineData(EndRoundState.AbortedWithError)]
  [InlineData(EndRoundState.AbortedNotEnoughAlices)]
  [InlineData(EndRoundState.NotAllAlicesSign)]
  [InlineData(EndRoundState.AbortedNotEnoughAlicesSigned)]
  [InlineData(EndRoundState.AbortedNotAllAlicesConfirmed)]
  [InlineData(EndRoundState.AbortedLoadBalancing)]
  public void CoordinatorFailureCannotReleasePotentiallyExposedSignatures(EndRoundState reportedOutcome)
  {
    var directory = Path.Combine(Path.GetTempPath(), "wasabi-mobile-tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);
    var input = new OutPoint(uint256.One, 4);
    var journal = new CoinJoinJournal(directory, Network.RegTest);
    var checkpoints = journal.ForWallet("account", () => { }, _ => false);
    checkpoints.BeginRound(uint256.One, [input]);
    checkpoints.BeforeSigning(uint256.One, new uint256(2));
    // The coordinator can retain witnesses even when another participant
    // drops out, and its status response cannot prove a transaction is gone.
    checkpoints.EndRound(uint256.One, reportedOutcome);
    var reopened = new CoinJoinJournal(directory, Network.RegTest);
    Assert.Contains(input, reopened.Reservations());
    Assert.True(reopened.ForWallet("account", () => { }, _ => false).IsReserved(input));
    Assert.Equal(new uint256(2).ToString(), reopened.Entries.Single().TransactionId);
  }

  [Fact]
  public void CoinJoinCheckpointSurvivesLostSigningOutcome()
  {
    var directory = Path.Combine(Path.GetTempPath(), "wasabi-mobile-tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);
    var input = new OutPoint(uint256.One, 4);
    var journal = new CoinJoinJournal(directory, Network.RegTest);
    var persistedAddresses = 0;
    var checkpoints = journal.ForWallet("account", () => persistedAddresses++, _ => false);
    checkpoints.BeginRound(uint256.One, [input]);
    checkpoints.BeforeSigning(uint256.One, new uint256(2));
    checkpoints.EndRound(uint256.One, EndRoundState.None);
    var reopened = new CoinJoinJournal(directory, Network.RegTest);
    Assert.Equal(2, persistedAddresses);
    Assert.Contains(input, reopened.Reservations());
    Assert.Equal(new uint256(2).ToString(), reopened.Entries.Single().TransactionId);
    checkpoints.EndRound(uint256.One, EndRoundState.TransactionBroadcastFailed);
    Assert.Contains(input, new CoinJoinJournal(directory, Network.RegTest).Reservations());
    reopened.Remove(reopened.Entries.Single());
    Assert.Empty(new CoinJoinJournal(directory, Network.RegTest).Reservations());
  }

  [Fact]
  public void DevelopmentRejectsMainnetBelowTheInterface()
  {
    var directory = Path.Combine(Path.GetTempPath(), "wasabi-mobile-tests", Guid.NewGuid().ToString("N"));
    Assert.Throws<InvalidOperationException>(() => new WalletSession(directory, new MobileSettings { Network = "main" }, directory));
    Assert.Equal(Network.TestNet4, new MobileSettings().GetNetwork());
  }

  [Fact]
  public void JournalSurvivesRestartAndReservesExactSignedInputs()
  {
    var directory = Path.Combine(Path.GetTempPath(), "wasabi-mobile-tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);
    var transaction = Network.RegTest.CreateTransaction();
    var input = new OutPoint(uint256.One, 3);
    transaction.Inputs.Add(new TxIn(input));
    using var key = new Key();
    transaction.Outputs.Add(Money.Satoshis(10000), key.PubKey.WitHash.ScriptPubKey);
    var entry = new JournalEntry("opaque-review", "public-account", Network.RegTest.Name, PaymentOperation.Payment, null,
      transaction.GetHash().ToString(), transaction.ToHex(), SubmissionState.Uncertain, DateTimeOffset.UtcNow);
    var journal = new TransactionJournal(directory, Network.RegTest);
    journal.Put(entry);
    var reopened = new TransactionJournal(directory, Network.RegTest);
    Assert.Equal(entry, reopened.Entries.Single());
    Assert.Contains(input, reopened.Reservations());
    reopened.Put(entry with { State = SubmissionState.Confirmed });
    Assert.Empty(new TransactionJournal(directory, Network.RegTest).Reservations());
  }

  [Fact]
  public void FailedJournalWriteKeepsInputsReservedUntilExactBytesAreSaved()
  {
    var directory = Path.Combine(Path.GetTempPath(), "wasabi-mobile-tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);
    var transaction = Network.RegTest.CreateTransaction();
    var input = new OutPoint(uint256.One, 3);
    transaction.Inputs.Add(new TxIn(input));
    using var key = new Key();
    transaction.Outputs.Add(Money.Satoshis(10000), key.PubKey.WitHash.ScriptPubKey);
    var entry = new JournalEntry("review", "account", Network.RegTest.Name, PaymentOperation.Payment, null,
      transaction.GetHash().ToString(), transaction.ToHex(), SubmissionState.Uncertain, DateTimeOffset.UtcNow);
    var journal = new TransactionJournal(directory, Network.RegTest);
    var blocked = Path.Combine(directory, "submissions-" + Network.RegTest.Name + ".json.new");
    Directory.CreateDirectory(blocked);
    var error = Record.Exception(() => journal.Put(entry));
    Assert.True(error is IOException or UnauthorizedAccessException);
    Assert.Contains(input, journal.Reservations());
    Assert.Equal(entry.Hex, journal.Entries.Single().Hex);
    Directory.Delete(blocked);
    journal.Put(entry);
    Assert.Equal(entry.Hex, new TransactionJournal(directory, Network.RegTest).Entries.Single().Hex);
  }

  [Fact]
  public void CorruptJournalBlocksSendingInsteadOfDiscardingReservations()
  {
    var directory = Path.Combine(Path.GetTempPath(), "wasabi-mobile-tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);
    var transaction = Network.RegTest.CreateTransaction();
    transaction.Inputs.Add(new TxIn(new OutPoint(uint256.One, 0)));
    using var key = new Key();
    transaction.Outputs.Add(Money.Satoshis(10000), key.PubKey.WitHash.ScriptPubKey);
    var entry = new JournalEntry("review", "account", Network.RegTest.Name, PaymentOperation.Payment, null, uint256.One.ToString(), transaction.ToHex(), SubmissionState.Uncertain, DateTimeOffset.UtcNow);
    File.WriteAllText(Path.Combine(directory, "submissions-" + Network.RegTest.Name + ".json"), JsonSerializer.Serialize(new[] { entry }));
    Assert.Throws<IOException>(() => new TransactionJournal(directory, Network.RegTest));
  }

  [Fact]
  public void CameraHonorsRowPaddingPixelStrideAndSensorRotation()
  {
    Assert.Equal(new byte[] { 1, 2, 3, 4 }, CameraFrame.Luminance(new byte[] { 1, 99, 2, 99, 99, 3, 99, 4 }, 2, 2, 5, 2));
    Assert.Equal(180, CameraFrame.RelativeRotation(270, 90));
    Assert.Equal(270, CameraFrame.RelativeRotation(0, 90));
    Assert.Throws<ArgumentException>(() => CameraFrame.Luminance(new byte[] { 1 }, 1280, 1280, 1280, 1));
  }
}
