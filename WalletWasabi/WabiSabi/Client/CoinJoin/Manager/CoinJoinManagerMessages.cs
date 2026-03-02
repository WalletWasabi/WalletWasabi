using System;
using WalletWasabi.Services;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Client.CoinJoin.Client;
using WalletWasabi.Wallets;

namespace WalletWasabi.WabiSabi.Client.CoinJoin.Manager;

// Base message type for all CoinJoinManager messages
internal abstract record CoinJoinMessage;

// Command messages (user-initiated)
internal record StartCoinJoin(IWallet Wallet, IWallet OutputWallet, bool StopWhenAllMixed, bool OverridePlebStop) : CoinJoinMessage;
internal record StopCoinJoin(IWallet Wallet) : CoinJoinMessage;

// Event messages (system-initiated)
internal record WalletBecameMixable(IWallet Wallet) : CoinJoinMessage;
internal record WalletBecameUnmixable(IWallet Wallet) : CoinJoinMessage;
internal record CoinJoinCompleted(WalletId WalletId) : CoinJoinMessage;

// Periodic update messages (timer-driven)
internal record UpdateWalletStates() : CoinJoinMessage;
internal record CheckFinalization() : CoinJoinMessage;
internal record CheckScheduledRestarts() : CoinJoinMessage;

// UI workflow messages
internal record WalletEnteredSendWorkflowMsg(WalletId WalletId) : CoinJoinMessage;
internal record WalletLeftSendWorkflowMsg(IWallet Wallet) : CoinJoinMessage;
internal record WalletEnteredSendingMsg(IWallet Wallet) : CoinJoinMessage;
internal record SignalStopAllCoinjoins() : CoinJoinMessage;
internal record RestartAbortedCoinjoins() : CoinJoinMessage;

// Query messages (request-reply pattern)
internal record GetCoinJoinState(WalletId WalletId, IReplyChannel<CoinJoinClientState> Reply) : CoinJoinMessage;

// Delayed execution messages (auto-restart)
internal record ScheduleRestart(IWallet Wallet, IWallet OutputWallet, bool StopWhenAllMixed, bool OverridePlebStop, DateTimeOffset ScheduledFor, Guid ScheduleId) : CoinJoinMessage;
internal record CancelScheduledRestart(IWallet Wallet) : CoinJoinMessage;
