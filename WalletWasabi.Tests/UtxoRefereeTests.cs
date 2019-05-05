using System;
using System.IO;
using System.Linq;
using FakeItEasy;
using NBitcoin;
using WalletWasabi.Models;
using WalletWasabi.Models.ChaumianCoinJoin;
using WalletWasabi.Services;
using Xunit;

namespace WalletWasabi.Tests
{
	public class BannedUtxoRepositoryTests
	{
		[Fact]
		public async void TestAsync()
		{
			string repoFile = Path.Combine("./test", Path.GetRandomFileName());

			var repo = new BannedUtxoRepository(repoFile);
			Assert.Empty(repo.Enumerate());

			var outpoint1 = new OutPoint(uint256.One, 13);
			var bannedUtxo1 = new BannedUtxoRecord(outpoint1, 0, DateTimeOffset.UtcNow, false, 0);
			Assert.True(repo.TryAddOrUpdate(bannedUtxo1));

			Assert.True(repo.TryGet(outpoint1, out var retrieved1));
			Assert.Equal(retrieved1, bannedUtxo1);
			Assert.Single(repo.Enumerate());

			var outpoint2 = new OutPoint(uint256.One, 67);
			var bannedUtxo2 = new BannedUtxoRecord(outpoint2, 0, DateTimeOffset.UtcNow, false, 0);
			Assert.True(repo.TryAddOrUpdate(bannedUtxo2));

			Assert.True(repo.TryGet(outpoint2, out var retrieved2));
			Assert.Equal(retrieved2, bannedUtxo2);
			Assert.Equal(2, (int)repo.Enumerate().Count());

			await repo.SaveChangesAsync();

			var lines = File.ReadAllLines(repoFile);
			Assert.Equal(bannedUtxo1.ToString(), lines[0]);
			Assert.Equal(bannedUtxo2.ToString(), lines[1]);

			Assert.True(repo.TryRemove(outpoint1));
			Assert.False(repo.TryRemove(outpoint1));
			Assert.Single(repo.Enumerate());

			Assert.False( repo.TryGet(outpoint1, out var _));
			Assert.True( repo.TryGet(outpoint2, out var _));

			await repo.SaveChangesAsync();

			lines = File.ReadAllLines(repoFile);
			Assert.Single(lines);
			Assert.Equal(bannedUtxo2.ToString(), lines[0]);

			repo.Clear();
			Assert.False(repo.TryRemove(outpoint2));
		}
	}

	public class UtxoRefereeTests
	{
		public static UtxoReferee CreateUtxoReferee(int severity, long durationHours, bool noteBeforeBan, out BannedUtxoRepository repo, out IUtxoProvider utxoProvider)
		{
			string repoFile = Path.Combine("./test", Path.GetRandomFileName());
			repo = new BannedUtxoRepository(repoFile);
			utxoProvider = A.Fake<IUtxoProvider>();
			var dosConfig = new CcjDenialOfServiceConfig(severity, durationHours, noteBeforeBan);

			var referee = new UtxoReferee(repo, utxoProvider, dosConfig);
			return referee;
		}

		[Fact]
		public async void TestBanNotedAsync()
		{
			var referee = CreateUtxoReferee(1, 24, true, out var repo, out var _);

			var outpoint = new OutPoint(uint256.One, 67);
			Assert.Null(await referee.TryGetBannedAsync(outpoint, true));

			var now = DateTimeOffset.UtcNow;
			// Ban coin for round 1 (noted first)
			await referee.BanUtxosAsync(1, now, false, 1, outpoint);
			var bannedUtxo = await referee.TryGetBannedAsync(outpoint, notedToo: false);
			Assert.Null(bannedUtxo);

			bannedUtxo = await referee.TryGetBannedAsync(outpoint, notedToo: true);
			Assert.NotNull(bannedUtxo);
			Assert.Equal(1, bannedUtxo.Severity);
			Assert.Equal(now, bannedUtxo.TimeOfBan);
			Assert.Equal(true, bannedUtxo.IsNoted);
			Assert.Equal(1, bannedUtxo.BannedForRound);
			Assert.Equal(outpoint, bannedUtxo.Utxo);

			// Ban same coin for round 2
			await referee.BanUtxosAsync(1, now, false, 2, outpoint);
			bannedUtxo = await referee.TryGetBannedAsync(outpoint, notedToo: false);
			Assert.NotNull(bannedUtxo);
			Assert.Equal(1, bannedUtxo.Severity);
			Assert.Equal(now, bannedUtxo.TimeOfBan);
			Assert.False(bannedUtxo.IsNoted);
			Assert.Equal(2, bannedUtxo.BannedForRound);
			Assert.Equal(outpoint, bannedUtxo.Utxo);

			// Ban coin and test expiration
			var coin = new OutPoint(uint256.Zero, 331);
			await referee.BanUtxosAsync(1, now.AddHours(-25), false, 2, coin);
			bannedUtxo = await referee.TryGetBannedAsync(coin, notedToo: true);
			Assert.Null(bannedUtxo);
		}

		[Fact]
		public async void TestBanAsync()
		{
			var referee = CreateUtxoReferee(1, 24, false, out var repo, out var _);

			var outpoint = new OutPoint(uint256.One, 67);
			Assert.Null(await referee.TryGetBannedAsync(outpoint, true));

			var now = DateTimeOffset.UtcNow;
			// Ban coin for round 1
			await referee.BanUtxosAsync(1, now, false, 1, outpoint);
			var bannedUtxo = await referee.TryGetBannedAsync(outpoint, notedToo: false);
			Assert.NotNull(bannedUtxo);
			Assert.Equal(1, bannedUtxo.Severity);
			Assert.Equal(now, bannedUtxo.TimeOfBan);
			Assert.False(bannedUtxo.IsNoted);
			Assert.Equal(1, bannedUtxo.BannedForRound);
			Assert.Equal(outpoint, bannedUtxo.Utxo);

			// Ban same coin for round 2
			await referee.BanUtxosAsync(1, now, false, 2, outpoint);
			bannedUtxo = await referee.TryGetBannedAsync(outpoint, notedToo: false);
			Assert.NotNull(bannedUtxo);
			Assert.Equal(1, bannedUtxo.Severity);
			Assert.Equal(now, bannedUtxo.TimeOfBan);
			Assert.False(bannedUtxo.IsNoted);
			Assert.Equal(2, bannedUtxo.BannedForRound);
			Assert.Equal(outpoint, bannedUtxo.Utxo);

			// Ban a coin and force noted
			var outpoint1 = new OutPoint(uint256.Parse("bb36dd56f2818641dcd94fad34845745cf3f3b4e6189f5fda174a1d2a72a42c3"), 67);
			await referee.BanUtxosAsync(1, now, true, 2, outpoint1);
			bannedUtxo = await referee.TryGetBannedAsync(outpoint1, notedToo: true);
			Assert.NotNull(bannedUtxo);
			Assert.Equal(1, bannedUtxo.Severity);
			Assert.Equal(now, bannedUtxo.TimeOfBan);
			Assert.True(bannedUtxo.IsNoted);
			Assert.Equal(2, bannedUtxo.BannedForRound);
			Assert.Equal(outpoint1, bannedUtxo.Utxo);

		}

		[Fact]
		public async void TestBanDosProtectionDisabledAsync()
		{
			var referee = CreateUtxoReferee(0, 24, false, out var repo, out var _);

			var outpoint = new OutPoint(uint256.One, 67);
			await referee.BanUtxosAsync(1, DateTimeOffset.UtcNow, false, 1, outpoint);
			var bannedUtxo = await referee.TryGetBannedAsync(outpoint, notedToo: true);
			Assert.Null(bannedUtxo);
		}

		[Fact]
		public async void TestInitialLoadingAsync()
		{
			var referee = CreateUtxoReferee(1, 24, false, out var repo, out var _);

			var now = DateTimeOffset.UtcNow;
			var yesterday = now.AddHours(-25);

			var outpoint1 = new OutPoint(uint256.Parse("1d4d1670361fdd39695b04f98e8f5f761eee7def54e89378f67fa989291a9e92"), 67);
			var outpoint2 = new OutPoint(uint256.Parse("c185e0bfb10ee84a7cb413cd2713497251cf1403c1a487420e51c842aa7ecbfe"), 12);
			var outpoint3 = new OutPoint(uint256.Parse("5b04f98e8f5f761eee7def54e89371d4d1670361fdd39695b04f98e8f5f761ee"), 55);
			var outpoint4 = new OutPoint(uint256.Parse("7fa989291a9e92fdd39695b04f98e8f5f761eee7def54e89378f67fa989291a9"), 18);

			repo.TryAddOrUpdate(new BannedUtxoRecord(outpoint1, 2, yesterday, true, 10));
			repo.TryAddOrUpdate(new BannedUtxoRecord(outpoint2, 2, yesterday, false, 10));
			repo.TryAddOrUpdate(new BannedUtxoRecord(outpoint3, 1, now, false, 10));
			repo.TryAddOrUpdate(new BannedUtxoRecord(outpoint4, 3, now, true, 10));

			Assert.Null(await referee.TryGetBannedAsync(outpoint1, true));
			Assert.Null(await referee.TryGetBannedAsync(outpoint2, true));
			Assert.NotNull(await referee.TryGetBannedAsync(outpoint3, true));
			Assert.NotNull(await referee.TryGetBannedAsync(outpoint4, true));
		}

		[Fact]
		public async void TestInitialLoading2Async()
		{
			var referee = CreateUtxoReferee(1, 24, false, out var repo, out var utxoProvider);

			var now = DateTimeOffset.UtcNow;
			var yesterday = now.AddHours(-25);

			var outpoint1 = new OutPoint(uint256.Parse("1d4d1670361fdd39695b04f98e8f5f761eee7def54e89378f67fa989291a9e92"), 67);
			var outpoint2 = new OutPoint(uint256.Parse("c185e0bfb10ee84a7cb413cd2713497251cf1403c1a487420e51c842aa7ecbfe"), 12);
			var outpoint3 = new OutPoint(uint256.Parse("5b04f98e8f5f761eee7def54e89371d4d1670361fdd39695b04f98e8f5f761ee"), 55);
			var outpoint4 = new OutPoint(uint256.Parse("7fa989291a9e92fdd39695b04f98e8f5f761eee7def54e89378f67fa989291a9"), 18);
			A.CallTo(()=> utxoProvider.GetUtxoAsync(outpoint1.Hash, (int)outpoint1.N)).Returns((TxOut)null); // txout not found
			A.CallTo(()=> utxoProvider.GetUtxoAsync(outpoint3.Hash, (int)outpoint3.N)).Returns((TxOut)null); // txout not found

			repo.TryAddOrUpdate(new BannedUtxoRecord(outpoint1, 2, now, true, 10));
			repo.TryAddOrUpdate(new BannedUtxoRecord(outpoint2, 2, now, false, 10));
			repo.TryAddOrUpdate(new BannedUtxoRecord(outpoint3, 1, now, false, 10));
			repo.TryAddOrUpdate(new BannedUtxoRecord(outpoint4, 3, now, true, 10));

			Assert.Null(await referee.TryGetBannedAsync(outpoint1, true));
			Assert.NotNull(await referee.TryGetBannedAsync(outpoint2, true));
			Assert.Null(await referee.TryGetBannedAsync(outpoint3, true));
			Assert.NotNull(await referee.TryGetBannedAsync(outpoint4, true));
		}
	}
}
