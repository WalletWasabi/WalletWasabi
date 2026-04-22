using WalletWasabi.Discoverability;

namespace WalletWasabi.Serialization;

public static partial class Decode
{
	private static Decoder<Uri> Uri =>
		String.AndThen(s => System.Uri.TryCreate(s, UriKind.Absolute, out var uri)
			? Succeed(uri)
			: Fail<Uri>($"'{s}' is not an absolute URI."));

	public static Decoder<KnownCoordinator> KnownCoordinator =>
		Object(get => new KnownCoordinator(
			get.Required("Name", String),
			get.Required("CoordinatorUri", Uri),
			get.Required("Description", String),
			get.Optional("ReadMoreUri", Uri),
			get.Required("Network", Network)));
}
