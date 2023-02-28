using WalletWasabi.Affiliation.Models.CoinJoinNotification;

namespace WalletWasabi.Affiliation.Models;

public record CoinJoinNotificationRequest(Body Body, byte[] Signature);
