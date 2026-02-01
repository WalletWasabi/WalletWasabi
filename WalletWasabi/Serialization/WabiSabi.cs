using System.Linq;
using System.Text.Json.Nodes;
using NBitcoin.Secp256k1;
using WabiSabi;
using WabiSabi.Crypto;
using WabiSabi.Crypto.Groups;
using WabiSabi.Crypto.ZeroKnowledge;
using static WalletWasabi.JsonConverters.ReflectionUtils;

namespace WalletWasabi.Serialization;

public static partial class Encode
{
	private static JsonNode Scalar(Scalar ge) =>
		Hexadecimal(ge.ToBytes());

	private static JsonNode GroupElement(GroupElement ge) =>
		Hexadecimal(ge.ToBytes());

	public static JsonNode CredentialPresentation(CredentialPresentation cp) =>
		Object([
			("Ca", GroupElement(cp.Ca)),
			("Cx0", GroupElement(cp.Cx0)),
			("Cx1", GroupElement(cp.Cx1)),
			("CV", GroupElement(cp.CV)),
			("S", GroupElement(cp.S)),
		]);

	public static JsonNode IssuanceRequest(IssuanceRequest ir) =>
		Object([
			("Ma", GroupElement(ir.Ma)),
			("BitCommitments", Array(ir.BitCommitments.Select(GroupElement)))
		]);

	public static JsonNode MAC(MAC mac) =>
		Object([
			("T", Scalar(mac.T)),
			("V", GroupElement(mac.V))
		]);

	public static JsonNode Proof(Proof p) =>
		Object([
			("PublicNonces", Array(p.PublicNonces.Select(GroupElement))),
			("Responses", Array(p.Responses.Select(Scalar)))
		]);
}

public static partial class Decode
{
	private static Decoder<Scalar> Scalar =>
		Hexadecimal.Map(bytes => new Scalar(bytes)).Catch();

	private static Decoder<GroupElement> GroupElement =>
		Hexadecimal.Map(bytes => global::WabiSabi.Crypto.Groups.GroupElement.FromBytes(bytes)).Catch();

	private static Decoder<CredentialPresentation> CredentialPresentation =>
		Object(get => CreateInstance<CredentialPresentation>([
			get.Required("Ca", GroupElement),
			get.Required("Cx0", GroupElement),
			get.Required("Cx1", GroupElement),
			get.Required("CV", GroupElement),
			get.Required("S", GroupElement)]
		)).Catch();

	public static Decoder<IssuanceRequest> IssuanceRequest =>
		Object(get => CreateInstance<IssuanceRequest>([
			get.Required("Ma", GroupElement),
			get.Required("BitCommitments", Array(GroupElement))
		])).Catch();

	private static Decoder<MAC> MAC =>
		Object(get => CreateInstance<MAC>([
			get.Required("T", Scalar),
			get.Required("V", GroupElement)
		])).Catch();

	private static Decoder<GroupElementVector> GroupElementVector =>
		Array(GroupElement).Map(CreateInstance<GroupElementVector>).Catch();

	private static Decoder<ScalarVector> ScalarVector =>
		Array(Scalar).Map(a => CreateInstance<ScalarVector>(a.Cast<object>().ToArray())).Catch();

	private static Decoder<Proof> Proof =>
		Object(get => CreateInstance<Proof>([
			get.Required("PublicNonces", GroupElementVector),
			get.Required("Responses", ScalarVector)
		])).Catch();
}
