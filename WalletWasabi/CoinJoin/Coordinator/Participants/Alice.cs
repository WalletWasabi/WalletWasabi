using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Helpers;

namespace WalletWasabi.CoinJoin.Coordinator.Participants;

public class Alice
{
	public Alice(IEnumerable<Coin> inputs, Money networkFeeToPayAfterBaseDenomination, BitcoinAddress changeOutputAddress, IEnumerable<uint256> blindedOutputScripts)
	{
		Inputs = Guard.NotNullOrEmpty(nameof(inputs), inputs);
		NetworkFeeToPayAfterBaseDenomination = Guard.NotNull(nameof(networkFeeToPayAfterBaseDenomination), networkFeeToPayAfterBaseDenomination);

		BlindedOutputScripts = blindedOutputScripts?.ToArray() ?? Array.Empty<uint256>();

		ChangeOutputAddress = Guard.NotNull(nameof(changeOutputAddress), changeOutputAddress);
		LastSeen = DateTimeOffset.UtcNow;

		UniqueId = Guid.NewGuid();

		InputSum = inputs.Sum(x => x.Amount);

		State = AliceState.InputsRegistered;

		BlindedOutputSignatures = Array.Empty<uint256>();
	}

	public DateTimeOffset LastSeen { get; set; }

	public Guid UniqueId { get; }

	public Money InputSum { get; }

	public Money NetworkFeeToPayAfterBaseDenomination { get; }

	public IEnumerable<Coin> Inputs { get; }

	public BitcoinAddress ChangeOutputAddress { get; }

	public AliceState State { get; set; }

	public uint256[] BlindedOutputScripts { get; set; }

	public uint256[] BlindedOutputSignatures { get; set; }
}
