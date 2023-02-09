using WalletWasabi.Affiliation.Models.CoinjoinRequest;

namespace WalletWasabi.Affiliation.Models;

public record GetCoinjoinRequestRequest(Body Body, byte[] Signature);
