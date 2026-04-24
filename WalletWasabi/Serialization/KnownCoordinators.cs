using WalletWasabi.Discoverability;

namespace WalletWasabi.Serialization;

public static partial class Decode
{
	public static Decoder<KnownCoordinator> KnownCoordinator =>
		Object(get => new KnownCoordinator(
			get.Required("Name", String),
			get.Required("CoordinatorUri", Uri),
			get.Required("Description", String),
			get.Optional("ReadMoreUri", Uri),
			get.Required("Network", Network)));
}
