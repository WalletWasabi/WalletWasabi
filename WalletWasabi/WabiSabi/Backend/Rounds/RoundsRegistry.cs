using Microsoft.VisualStudio.Threading;
using NBitcoin;
using Nito.AsyncEx;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.WabiSabi.Backend.Models;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.WabiSabi.Backend.Rounds;

internal class RoundsRegistry
{
	public ImmutableList<ArenaRoundState> RoundStates { get; private set; } = ImmutableList.Create<ArenaRoundState>();

	private ConcurrentDictionary<uint256, RoundRegistryItem> RoundRegistryItems { get; } = new();

	internal IEnumerable<Round> Rounds => RoundRegistryItems.Values.Select(x => x.Round);

	public void AddRound(Round round)
	{
		var asyncLock = new AsyncReaderWriterLock();
		asyncLock.OnBeforeWriteLockReleased(() =>
			{
				RefreshRoundStates(round.Id);
				return Task.CompletedTask;
			});
		RoundRegistryItems.TryAdd(round.Id, new(round, asyncLock));
		RefreshRoundStates();
		round.LogInfo($"Created round with params: {nameof(RoundParameters.MaxRegistrableAmount)}:'{round.MaxAmountCredentialValue}'.");
	}

	public void RemoveRound(uint256 roundId)
	{
		if (RoundRegistryItems.TryRemove(roundId, out var roundRegistryItem))
		{
			roundRegistryItem.AsyncReaderWriterLock.Dispose();
			RefreshRoundStates();
		}
	}

	private void RefreshRoundStates(uint256? roundId = null)
	{
		RoundStates = RoundRegistryItems.Select(s => ArenaRoundState.FromRound(s.Value.Round)).ToImmutableList();
	}

	public async Task<RoundLockReleaser> LockRoundAsync(uint256 roundId)
	{
		if (RoundRegistryItems.TryGetValue(roundId, out var roundRegistryItem))
		{
			var releaser = await roundRegistryItem.AsyncReaderWriterLock.WriteLockAsync();
			if (RoundRegistryItems.TryGetValue(roundId, out roundRegistryItem))
			{
				return new RoundLockReleaser(releaser, roundRegistryItem.Round);
			}
		}
		throw new InvalidOperationException();
	}

	public ArenaRoundState GetRoundState(uint256 roundId) =>
		RoundStates.FirstOrDefault(x => x.Id == roundId)
			?? throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.RoundNotFound, $"Round ({roundId}) not found.");

	private ArenaRoundState InPhase(ArenaRoundState round, Phase[] phases) =>
		phases.Contains(round.Phase)
		? round
		: throw new WrongPhaseException(RoundRegistryItems[round.Id].Round, phases);

	public ArenaRoundState GetRoundState(uint256 roundId, params Phase[] phases) =>
		InPhase(GetRoundState(roundId), phases);
}

public record RoundRegistryItem(Round Round, AsyncReaderWriterLock AsyncReaderWriterLock);

public record RoundLockReleaser(AsyncReaderWriterLock.Releaser Releaser, Round Round) : IDisposable
{
	public void Dispose()
	{
		Releaser.Dispose();
	}
}
