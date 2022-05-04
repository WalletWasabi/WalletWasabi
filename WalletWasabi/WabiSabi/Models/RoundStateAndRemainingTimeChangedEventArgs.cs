using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.Wabisabi.Models;

public record RoundStateAndRemainingTimeChangedEventArgs(RoundState RoundState, DateTimeOffset PhaseEndTime);
