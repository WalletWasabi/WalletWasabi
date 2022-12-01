using WalletWasabi.WabiSabi.Models.MultipartyTransaction;

namespace WalletWasabi.WabiSabi.Models.Serialization;

public class CoinJoinEventJsonConverter : GenericInterfaceJsonConverter<IEvent>
{
	public CoinJoinEventJsonConverter() : base(new[] { typeof(InputRegistered), typeof(InputRemoved), typeof(InputAdded), typeof(OutputAdded), typeof(RoundCreated) })
	{
	}
}
