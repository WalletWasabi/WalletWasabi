namespace WalletWasabi.WabiSabi.Client.CoinJoin;

public record AutoStarting(bool IsSending = false, bool IsPlebStop = false, bool IsDelay = false, bool IsPaused = false) : IWalletCoinJoinState;
public record Playing(bool IsInRound = false, bool InCriticalPhase = false) : IWalletCoinJoinState;
public record Paused() : IWalletCoinJoinState;
public record Finished() : IWalletCoinJoinState;
public record LoadingTrack() : IWalletCoinJoinState;
public record Stopped() : IWalletCoinJoinState;
