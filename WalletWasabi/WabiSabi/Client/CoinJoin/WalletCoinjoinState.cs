using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WalletWasabi.WabiSabi.Client.CoinJoin;

public class WalletCoinjoinState : IEquatable<WalletCoinjoinState>
{
	private WalletCoinjoinState(State status, bool isSending, bool isPlebStop, bool isDelay, bool isPaused, bool inRound, bool inCriticalPhase)
	{
		Status = status;
		IsSending = isSending;
		IsPlebStop = isPlebStop;
		IsDelay = isDelay;
		IsPaused = isPaused;
		InRound = inRound;
		InCriticalPhase = inCriticalPhase;
	}

	public static WalletCoinjoinState AutoStarting(bool isSending = false, bool isPlebStop = false, bool isDelay = false, bool isPaused = false)
		=> new(State.AutoStarting, isSending, isPlebStop, isDelay, isPaused, false, false);

	public static WalletCoinjoinState Playing(bool inRound = false, bool inCriticalPhase = false)
		=> new(State.Playing, false, false, false, false, inRound, inCriticalPhase);

	public static WalletCoinjoinState Paused()
		=> new(State.Paused, false, false, false, false, false, false);

	public static WalletCoinjoinState Finished()
		=> new(State.Finished, false, false, false, false, false, false);

	public static WalletCoinjoinState LoadingTrack()
		=> new(State.LoadingTrack, false, false, false, false, false, false);

	public static WalletCoinjoinState Stopped()
		=> new(State.Stopped, false, false, false, false, false, false);

	public State Status { get; }
	public bool IsSending { get; }
	public bool IsPlebStop { get; }
	public bool IsDelay { get; }
	public bool IsPaused { get; }
	public bool InRound { get; }
	public bool InCriticalPhase { get; }

	#region EqualityAndComparison

	public override bool Equals(object? obj) => Equals(obj as WalletCoinjoinState);

	public bool Equals(WalletCoinjoinState? other) => this == other;

	public override int GetHashCode() => HashCode.Combine(Status, IsSending, IsPlebStop, IsDelay, IsPaused, InRound, InCriticalPhase);

	public static bool operator ==(WalletCoinjoinState? x, WalletCoinjoinState? y) =>
		y?.Status == x?.Status
		&& y?.IsSending == x?.IsSending
		&& y?.IsPlebStop == x?.IsPlebStop
		&& y?.IsDelay == x?.IsDelay
		&& y?.IsPaused == x?.IsPaused
		&& y?.InRound == x?.InRound
		&& y?.InCriticalPhase == x?.InCriticalPhase;

	public static bool operator !=(WalletCoinjoinState? x, WalletCoinjoinState? y) => !(x == y);

	#endregion EqualityAndComparison

	public override string ToString()
	{
		return $"{Status.ToFriendlyString()}, {nameof(IsSending)}: {IsSending}, {nameof(IsPlebStop)}: {IsPlebStop}, {nameof(IsDelay)}: {IsDelay}, {nameof(IsPaused)}: {IsPaused}, {nameof(InRound)}: {InRound}, {nameof(InCriticalPhase)}: {InCriticalPhase}";
	}

	public enum State
	{
		AutoStarting,
		Playing,
		Paused,
		Finished,
		LoadingTrack,
		Stopped
	}
}
