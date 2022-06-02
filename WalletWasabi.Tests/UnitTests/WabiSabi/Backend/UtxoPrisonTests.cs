using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Backend.Banning;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Backend;

public class UtxoPrisonTests
{
	[Fact]
	public void EmptyPrison()
	{
		var p = new Prison();
		Assert.Empty(p.GetInmates());
		Assert.Equal(0, p.CountInmates().noted);
		Assert.Equal(0, p.CountInmates().banned);
		Assert.False(p.TryGet(BitcoinFactory.CreateOutPoint(), out _));
	}

	[Fact]
	public void PrisonChangeTracking()
	{
		var p = new Prison();
		var currentChangeId = p.ChangeId;

		// Make sure we set them to the past so the release method that looks at the time evaluates to true.
		var past = DateTimeOffset.UtcNow - TimeSpan.FromMilliseconds(2);

		var id1 = BitcoinFactory.CreateUint256();
		p.Punish(new Inmate(BitcoinFactory.CreateOutPoint(), Punishment.Banned, past, id1));
		Assert.NotEqual(currentChangeId, p.ChangeId);
		currentChangeId = p.ChangeId;

		p.Punish(new Inmate(BitcoinFactory.CreateOutPoint(), Punishment.Noted, past, id1));
		Assert.NotEqual(currentChangeId, p.ChangeId);
		currentChangeId = p.ChangeId;

		var op = BitcoinFactory.CreateOutPoint();
		var id2 = BitcoinFactory.CreateUint256();
		p.Punish(new Inmate(op, Punishment.Noted, past, id2));
		Assert.NotEqual(currentChangeId, p.ChangeId);
		currentChangeId = p.ChangeId;

		p.Punish(new Inmate(op, Punishment.Noted, past, id1));
		Assert.NotEqual(currentChangeId, p.ChangeId);
		currentChangeId = p.ChangeId;

		Assert.True(p.TryRelease(op, out _));
		Assert.NotEqual(currentChangeId, p.ChangeId);
		currentChangeId = p.ChangeId;

		p.ReleaseEligibleInmates(normalBanPeriod: TimeSpan.FromMilliseconds(1), longBanPeriod: TimeSpan.FromSeconds(1));
		Assert.NotEqual(currentChangeId, p.ChangeId);
	}

	[Fact]
	public void PrisonOperations()
	{
		var p = new Prison();

		var id1 = BitcoinFactory.CreateUint256();

		var utxo = BitcoinFactory.CreateOutPoint();
		p.Punish(utxo, Punishment.Noted, id1);
		Assert.Single(p.GetInmates());
		Assert.Equal(1, p.CountInmates().noted);
		Assert.Equal(0, p.CountInmates().banned);
		Assert.True(p.TryGet(utxo, out _));

		// Updates to banned.
		p.Punish(utxo, Punishment.Banned, id1);
		Assert.Single(p.GetInmates());
		Assert.Equal(0, p.CountInmates().noted);
		Assert.Equal(1, p.CountInmates().banned);
		Assert.True(p.TryGet(utxo, out _));

		// Removes.
		Assert.True(p.TryRelease(utxo, out _));
		Assert.Empty(p.GetInmates());

		// Noting twice flips to banned.
		p.Punish(utxo, Punishment.Noted, id1);
		p.Punish(utxo, Punishment.Noted, id1);
		Assert.Single(p.GetInmates());
		Assert.Equal(0, p.CountInmates().noted);
		Assert.Equal(1, p.CountInmates().banned);
		Assert.True(p.TryGet(utxo, out _));
		Assert.True(p.TryRelease(utxo, out _));

		// Updates round.
		var id2 = BitcoinFactory.CreateUint256();
		p.Punish(utxo, Punishment.Banned, id1);
		p.Punish(utxo, Punishment.Banned, id2);
		Assert.Single(p.GetInmates());
		Assert.True(p.TryGet(utxo, out var inmate));
		Assert.Equal(id2, inmate!.LastDisruptedRoundId);
		Assert.True(p.TryRelease(utxo, out _));
	}

	[Fact]
	public void CanReleaseAfterLongBan()
	{
		var p = new Prison();
		var id1 = BitcoinFactory.CreateUint256();
		var utxo = BitcoinFactory.CreateOutPoint();
		var past = DateTimeOffset.UtcNow - TimeSpan.FromDays(40);

		p.Punish(new Inmate(utxo, Punishment.LongBanned, past, id1));

		Assert.Single(p.GetInmates());

		p.ReleaseEligibleInmates(normalBanPeriod: TimeSpan.FromSeconds(1), longBanPeriod: TimeSpan.FromDays(31));

		Assert.Empty(p.GetInmates());
	}
}
