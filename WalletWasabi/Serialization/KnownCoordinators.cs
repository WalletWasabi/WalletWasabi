using WalletWasabi.Discoverability;

namespace WalletWasabi.Serialization;

public static partial class Decode
{
	public static Decoder<KnownCoordinator> KnownCoordinator =>
		Object(get => new KnownCoordinator(
			get.Required("Name", String),
			get.Required("CoordinatorUri", HttpUri),
			get.Required("Description", String),
			get.Optional("ReadMoreUri", HttpUri),
			get.Required("Network", Network),
			get.Required("FirstSeen", DateOnly)));
}
