using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using NBitcoin;
using WabiSabi.CredentialRequesting;
using WabiSabi.Crypto;
using WalletWasabi.Crypto;
using WalletWasabi.Helpers;
using WalletWasabi.WabiSabi.Coordinator.Models;
using WalletWasabi.WabiSabi.Coordinator.Rounds;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;
using static WalletWasabi.JsonConverters.ReflectionUtils;

namespace WalletWasabi.Serialization;

public static partial class Encode
{
	private static JsonNode MoneyRange(MoneyRange range) =>
		Object([
			("Min", MoneySatoshis(range.Min)),
			("Max", MoneySatoshis(range.Max)),
		]);

	public static JsonNode TimeSpan(TimeSpan ts) =>
		String($"{ts.Days}d {ts.Hours}h {ts.Minutes}m {ts.Seconds}s");

	private static JsonNode OwnershipProof(OwnershipProof proof) =>
		Hexadecimal(proof.ToBytes());

	public static JsonNode InputAdded(InputAdded input) =>
		Object([
			("Type", String("InputAdded")),
			("Coin", Coin(input.Coin)),
			("OwnershipProof", OwnershipProof(input.OwnershipProof))
		]);

	public static JsonNode OutputAdded(OutputAdded output) =>
		Object([
			("Type", String("OutputAdded")),
			("Output", TxOut(output.Output)),
		]);

	private static JsonNode CoordinationFeeRate() =>
		Object([
			("Rate", Decimal(0.0m)),
			("PlebsDontPayThreshold", Decimal(0))
		]);

	private static JsonNode RoundParameters(RoundParameters p) =>
		Object([
			("Network", String(p.Network.Name)),
			("MiningFeeRate", FeeRate(p.MiningFeeRate)),
			("CoordinationFeeRate", CoordinationFeeRate() ),
			("MaxSuggestedAmount",  MoneySatoshis(p.MaxSuggestedAmount)),
			("MinInputCountByRound", Int(p.MinInputCountByRound)),
			("MaxInputCountByRound", Int(p.MaxInputCountByRound) ),
			("AllowedInputAmounts", MoneyRange(p.AllowedInputAmounts)),
			("AllowedOutputAmounts", MoneyRange(p.AllowedOutputAmounts)),
			("AllowedInputTypes", Array( p.AllowedInputTypes.Select(x => Int((int)x)) )),
			("AllowedOutputTypes", Array( p.AllowedOutputTypes.Select(x => Int((int)x)) )),
			("StandardInputRegistrationTimeout", TimeSpan(p.StandardInputRegistrationTimeout) ),
			("ConnectionConfirmationTimeout", TimeSpan(p.ConnectionConfirmationTimeout) ),
			("OutputRegistrationTimeout", TimeSpan(p.OutputRegistrationTimeout) ),
			("TransactionSigningTimeout", TimeSpan(p.TransactionSigningTimeout) ),
			("BlameInputRegistrationTimeout", TimeSpan(p.BlameInputRegistrationTimeout) ),
			("MinAmountCredentialValue", MoneySatoshis(p.MinAmountCredentialValue) ),
			("MaxAmountCredentialValue", MoneySatoshis(p.MaxAmountCredentialValue) ),
			("MaxVsizeAllocationPerAlice", Int(p.MaxVsizeAllocationPerAlice) ),
			("CoordinationIdentifier", String(p.CoordinationIdentifier) ),
			("DelayTransactionSigning", Bool(p.DelayTransactionSigning) ),
			("MaxTransactionSize", Int(p.MaxTransactionSize) ),
			("MinRelayTxFee", FeeRate(p.MinRelayTxFee) ),
		]);

	public static JsonNode RoundCreated(RoundCreated round) =>
		Object([
			("Type", String("RoundCreated")),
			("RoundParameters", RoundParameters(round.RoundParameters))
		]);

	public static JsonNode RoundEvent(IEvent e) =>
		e switch
		{
			RoundCreated x => RoundCreated(x),
			InputAdded x => InputAdded(x),
			OutputAdded x => OutputAdded(x),
			_ => throw new ArgumentException("Unknown event type")
		};

	private static JsonNode InputBannedExceptionData(InputBannedExceptionData e) =>
		Object([
			("Type", String("InputBannedExceptionData")),
			("BannedUntil", DatetimeOffset(e.BannedUntil))
		]);

	private static JsonNode WrongPhaseExceptionData(WrongPhaseExceptionData e) =>
		Object([
			("Type", String("WrongPhaseExceptionData")),
			("CurrentPhase", Int((int)e.CurrentPhase))
		]);

	private static JsonNode EmptyExceptionData() =>
		Object([
			("Type", String("EmptyExceptionData")),
		]);

	public static JsonNode ExceptionData(ExceptionData? e) =>
		e switch
		{
			InputBannedExceptionData x => InputBannedExceptionData(x),
			WrongPhaseExceptionData x => WrongPhaseExceptionData(x),
			null or EmptyExceptionData _ => EmptyExceptionData(),
			_ => throw new ArgumentException("Unknown ExceptionData type")
		};

	public static JsonNode RealCredentialsRequest(RealCredentialsRequest cr) =>
		Object([
			("Delta", Int64(cr.Delta)),
			("Presented", Array(cr.Presented.Select(CredentialPresentation))),
			("Requested", Array(cr.Requested.Select(IssuanceRequest))),
			("Proofs", Array(cr.Proofs.Select(Proof)))
		]);

	public static JsonNode ZeroCredentialsRequest(ZeroCredentialsRequest cr) =>
		Object([
			("Delta", Int64(cr.Delta)),
			("Presented", Array(cr.Presented.Select(CredentialPresentation))),
			("Requested", Array(cr.Requested.Select(IssuanceRequest))),
			("Proofs", Array(cr.Proofs.Select(Proof)))
		]);


	public static JsonNode CredentialsResponse(CredentialsResponse cr) =>
		Object([
			("issuedCredentials", Array(cr.IssuedCredentials.Select(MAC))),
			("proofs", Array(cr.Proofs.Select(Proof)))
		]);

	private static JsonNode CredentialIssuerParameters(CredentialIssuerParameters ip) =>
		Object([
			("cw", GroupElement(ip.Cw)),
			("i", GroupElement(ip.I))
		]);

	private static JsonNode ConstructionState(ConstructionState cs) =>
		Object([
			("Type", String("ConstructionState")),
			("Events", Array(cs.Events.Select(RoundEvent)))
		]);

	private static JsonNode SigningState(SigningState ss) =>
		Object([
			("Type", String("SigningState")),
		    ("Witnesses", Dictionary(ss.Witnesses.ToDictionary(x => x.Key.ToString(), x => WitScript(x.Value)))),
            ("IsFullySigned", Bool(ss.IsFullySigned)),
			("Events", Array(ss.Events.Select(RoundEvent)))
		]);

	private static JsonNode MultipartyTransactionState(MultipartyTransactionState ts) =>
		ts switch
		{
			ConstructionState cs => ConstructionState(cs),
			SigningState ss => SigningState(ss),
			_ => throw new ArgumentException("There is not such a transaction state")
		};

	private static JsonNode RoundState(RoundState rs) =>
		Object([
			("id", UInt256(rs.Id)),
			("blameOf", UInt256(rs.BlameOf)),
			("amountCredentialIssuerParameters", CredentialIssuerParameters(rs.AmountCredentialIssuerParameters)),
			("vsizeCredentialIssuerParameters", CredentialIssuerParameters(rs.VsizeCredentialIssuerParameters)),
			("phase", Int((int)rs.Phase)),
			("endRoundState", Int((int)rs.EndRoundState)),
			("inputRegistrationStart", DatetimeOffset(rs.InputRegistrationStart)),
			("inputRegistrationTimeout", TimeSpan(rs.InputRegistrationTimeout)),
			("coinjoinState", MultipartyTransactionState(rs.CoinjoinState)),
			("inputRegistrationEnd", DatetimeOffset(rs.InputRegistrationEnd)),
			("isBlame", Bool(rs.IsBlame)),
		]);

	private static JsonNode RoundStateCheckpoint(RoundStateCheckpoint cp) =>
		Object([
			("RoundId", UInt256(cp.RoundId)),
			("StateId", Int(cp.StateId)),
		]);

	private static JsonNode RoundStateRequest(RoundStateRequest sr) =>
		Object([
			("RoundCheckpoints", Array(sr.RoundCheckpoints.Select(RoundStateCheckpoint))),
		]);

	public static JsonNode RoundStateResponse(RoundStateResponse sr) =>
		Object([
			("roundStates", Array(sr.RoundStates.Select(RoundState))),
			("coinJoinFeeRateMedians", Array([]))
		]);

	public static JsonNode InputRegistrationRequest(InputRegistrationRequest ir) =>
		Object([
			("RoundId", UInt256(ir.RoundId)),
			("Input", Outpoint(ir.Input)),
			("OwnershipProof", OwnershipProof(ir.OwnershipProof)),
			("ZeroAmountCredentialRequests", ZeroCredentialsRequest(ir.ZeroAmountCredentialRequests)),
			("ZeroVsizeCredentialRequests", ZeroCredentialsRequest(ir.ZeroVsizeCredentialRequests)),
		]);

	public static JsonNode InputRegistrationResponse(InputRegistrationResponse ir) =>
		Object([
			("aliceId", Guid(ir.AliceId)),
			("amountCredentials", CredentialsResponse(ir.AmountCredentials)),
			("vsizeCredentials", CredentialsResponse(ir.VsizeCredentials)),
		]);

	public static JsonNode OutputRegistrationRequest(OutputRegistrationRequest rr) =>
		Object([
			("RoundId", UInt256(rr.RoundId)),
			("Script", Script(rr.Script)),
			("AmountCredentialRequests", RealCredentialsRequest(rr.AmountCredentialRequests)),
			("VsizeCredentialRequests", RealCredentialsRequest(rr.VsizeCredentialRequests)),
		]);

	public static JsonNode ReissueCredentialRequest(ReissueCredentialRequest rr) =>
		Object([
			("RoundId", UInt256(rr.RoundId)),
			("RealAmountCredentialRequests", RealCredentialsRequest(rr.RealAmountCredentialRequests)),
			("RealVsizeCredentialRequests", RealCredentialsRequest(rr.RealVsizeCredentialRequests)),
			("ZeroAmountCredentialRequests", ZeroCredentialsRequest(rr.ZeroAmountCredentialRequests)),
			("ZeroVsizeCredentialsRequests", ZeroCredentialsRequest(rr.ZeroVsizeCredentialsRequests)),
		]);

	public static JsonNode ReissueCredentialResponse(ReissueCredentialResponse rr) =>
		Object([
			("realAmountCredentials", CredentialsResponse(rr.RealAmountCredentials)),
			("realVsizeCredentials", CredentialsResponse(rr.RealVsizeCredentials)),
			("zeroAmountCredentials", CredentialsResponse(rr.ZeroAmountCredentials)),
			("zeroVsizeCredentials", CredentialsResponse(rr.ZeroVsizeCredentials)),
		]);

	public static JsonNode InputsRemovalRequest(InputsRemovalRequest rr) =>
		Object([
			("RoundId", UInt256(rr.RoundId)),
			("AliceId", Guid(rr.AliceId))
		]);

	public static JsonNode ConnectionConfirmationRequest(ConnectionConfirmationRequest ccr) =>
		Object([
			("RoundId", UInt256(ccr.RoundId)),
			("AliceId", Guid(ccr.AliceId)),
			("ZeroAmountCredentialRequests", ZeroCredentialsRequest(ccr.ZeroAmountCredentialRequests)),
			("RealAmountCredentialRequests", RealCredentialsRequest(ccr.RealAmountCredentialRequests)),
			("ZeroVsizeCredentialRequests",  ZeroCredentialsRequest(ccr.ZeroVsizeCredentialRequests)),
			("RealVsizeCredentialRequests", RealCredentialsRequest(ccr.RealVsizeCredentialRequests)),
		]);

	public static JsonNode ConnectionConfirmationResponse(ConnectionConfirmationResponse ccr)
	{
		IEnumerable<(string, JsonNode?)> Properties()
		{
			yield return ("zeroAmountCredentials", CredentialsResponse(ccr.ZeroAmountCredentials));
			yield return ("zeroVsizeCredentials", CredentialsResponse(ccr.ZeroVsizeCredentials));
			if (ccr.RealAmountCredentials is not null && ccr.RealVsizeCredentials is not null)
			{
				yield return ("realAmountCredentials", CredentialsResponse(ccr.RealAmountCredentials));
				yield return ("realVsizeCredentials", CredentialsResponse(ccr.RealVsizeCredentials));
			}
		}

		return Object(Properties());
	}

	public static JsonNode TransactionSignaturesRequest(TransactionSignaturesRequest sr) =>
		Object([
			("RoundId", UInt256(sr.RoundId)),
			("InputIndex", UInt(sr.InputIndex)),
			("Witness", WitScript(sr.Witness)),
		]);

	public static JsonNode ReadyToSignRequestRequest(ReadyToSignRequestRequest sr) =>
		Object([
			("RoundId", UInt256(sr.RoundId)),
			("AliceId", Guid(sr.AliceId)),
		]);


	public static JsonNode HummanMonitorRoundResponse(HumanMonitorRoundResponse rs) =>
		Object([
			("RoundId", UInt256(rs.RoundId)),
			("IsBlameRound", Bool(rs.IsBlameRound)),
			("InputCount", Int(rs.InputCount)),
			("MaxSuggestedAmount", Decimal(rs.MaxSuggestedAmount)),
			("InputRegistrationRemaining", TimeSpan(rs.InputRegistrationRemaining)),
			("Phase", String(rs.Phase)),
		]);

	public static JsonNode HummanMonitorResponse(HumanMonitorResponse rs) =>
		Object([
			("RoundStates", Array(rs.RoundStates.Select(HummanMonitorRoundResponse)))
		]);

	public static JsonNode Error(Error e) =>
		Object([
			("type", String(e.Type)),
			("errorCode", String(e.ErrorCode)),
			("description", String(e.Description)),
			("exceptionData", ExceptionData(e.ExceptionData) ),
		]);

	public static JsonNode CoordinatorMessage<T>(T obj) =>
		obj switch
		{
			InputRegistrationRequest irr => InputRegistrationRequest(irr),
			ConnectionConfirmationRequest ccr => ConnectionConfirmationRequest(ccr),
			ReissueCredentialRequest rcr => ReissueCredentialRequest(rcr),
			OutputRegistrationRequest orr => OutputRegistrationRequest(orr),
			ReadyToSignRequestRequest rsr => ReadyToSignRequestRequest(rsr),
			TransactionSignaturesRequest tsr => TransactionSignaturesRequest(tsr),
			RoundStateRequest rsr => RoundStateRequest(rsr),

			InputRegistrationResponse irr => InputRegistrationResponse(irr),
			InputsRemovalRequest irr => InputsRemovalRequest(irr),
			ConnectionConfirmationResponse ccr => ConnectionConfirmationResponse(ccr),
			ReissueCredentialResponse rcr => ReissueCredentialResponse(rcr),
			RoundStateResponse rsr => RoundStateResponse(rsr),
			HumanMonitorResponse hmr => HummanMonitorResponse(hmr),

			Error error => Error(error),

			_ => throw new Exception($"{obj.GetType().FullName} is not known")
		};
}

public static partial class Decode
{
	public static readonly Decoder<MoneyRange> MoneyRange =
		Object(get => new MoneyRange(
			get.Required("Min", MoneySatoshis),
			get.Required("Max", MoneySatoshis)));

	public static readonly Decoder<TimeSpan> TimeSpan =
		String.Map(s =>
		{
			var daysParts = s.Split('d');
			var days = int.Parse(daysParts[0].Trim());
			var hoursParts = daysParts[1].Split('h');
			var hours = int.Parse(hoursParts[0].Trim());
			var minutesParts = hoursParts[1].Split('m');
			var minutes = int.Parse(minutesParts[0].Trim());
			var secondsParts = minutesParts[1].Split('s');
			var seconds = int.Parse(secondsParts[0].Trim());
			return new TimeSpan(days, hours, minutes, seconds);
		}).Catch();

	public static readonly Decoder<ZeroCredentialsRequest> ZeroCredentialsRequest =
		Object(get => CreateInstance<ZeroCredentialsRequest>([
			get.Required("Requested", Array(IssuanceRequest)),
			get.Required("Proofs", Array(Proof))
			]));

	public static readonly Decoder<RealCredentialsRequest> RealCredentialsRequest =
		Object(get => CreateInstance<RealCredentialsRequest>([
			get.Required("Delta", Int64),
			get.Required("Presented", Array(CredentialPresentation)),
			get.Required("Requested", Array(IssuanceRequest)),
			get.Required("Proofs", Array(Proof))
			])).Catch();

	public static readonly Decoder<CredentialsResponse> CredentialsResponse =
		Object(get => CreateInstance<CredentialsResponse>([
			get.Required("issuedCredentials", Array(MAC)),
			get.Required("proofs", Array(Proof))
			])).Catch();

	public static readonly Decoder<CredentialIssuerParameters> CredentialIssuerParameters =
		Object(get => new CredentialIssuerParameters(
			get.Required("cw", GroupElement ),
			get.Required("i", GroupElement)
			)).Catch();

	private static readonly Decoder<OwnershipProof> OwnershipProof =
		Hexadecimal.Map(hex =>
		{
			var proof = new OwnershipProof();
			proof.FromBytes(hex);
			return proof;
		}).Catch();

	public static readonly Decoder<InputAdded> InputAdded =
		Object(get => new InputAdded(
			get.Required("Coin", Coin),
			get.Required("OwnershipProof", OwnershipProof)
		));

	public static readonly Decoder<OutputAdded> OutputAdded =
		Object(get => new OutputAdded(get.Required("Output", TxOut)));

	public static readonly Decoder<RoundCreated> RoundCreated =
		Object(get => new RoundCreated(get.Required("RoundParameters", RoundParameters)));

	public static readonly Decoder<ScriptType> ScriptType =
		Int.AndThen(n => n <= (int)NBitcoin.ScriptType.Taproot
			? Succeed((ScriptType) n)
			: Fail<ScriptType>("Invalid ScriptType, it is greater than ScriptType.Taproot"));

	private static Decoder<T> Cast<T, R>(Decoder<R> decoder) where R : T =>
		decoder.Map(r => (T) r);

	public static readonly Decoder<IEvent> RoundEvent =
		Field("Type", String)
			.AndThen(t => t switch
			{
				"RoundCreated" => Cast<IEvent, RoundCreated>(RoundCreated),
				"InputAdded" => Cast<IEvent, InputAdded>(InputAdded),
				"OutputAdded" => Cast<IEvent, OutputAdded>(OutputAdded),
				_ => Fail<IEvent>($"Unknown event type 't'")
			});

	public static readonly Decoder<RoundParameters> RoundParameters =
		Object(get => new RoundParameters(
			get.Required("Network", Network),
			get.Required("MiningFeeRate", FeeRate),
			get.Required("MaxSuggestedAmount",  MoneySatoshis),
			get.Required("MinInputCountByRound", Int),
			get.Required("MaxInputCountByRound", Int),
			get.Required("AllowedInputAmounts", MoneyRange),
			get.Required("AllowedOutputAmounts", MoneyRange),
			get.Required("AllowedInputTypes", Array(ScriptType)).ToImmutableSortedSet(),
			get.Required("AllowedOutputTypes", Array(ScriptType)).ToImmutableSortedSet(),
			get.Required("StandardInputRegistrationTimeout", TimeSpan),
			get.Required("ConnectionConfirmationTimeout", TimeSpan),
			get.Required("OutputRegistrationTimeout", TimeSpan ),
			get.Required("TransactionSigningTimeout", TimeSpan ),
			get.Required("BlameInputRegistrationTimeout", TimeSpan ),
			get.Required("CoordinationIdentifier", String ),
			get.Required("DelayTransactionSigning", Bool ))
		{
			MaxVsizeAllocationPerAlice = get.Required("MaxVsizeAllocationPerAlice", Int)
		});

	public static readonly Decoder<MultipartyTransactionState> MultipartyTransactionState =
			Field("Type", String).AndThen(t => t switch
			{
				"ConstructionState" => Cast<MultipartyTransactionState, ConstructionState>(ConstructionState),
				"SigningState" => Cast<MultipartyTransactionState, SigningState>(SigningState),
				_ => Fail<MultipartyTransactionState>($"Unknown MultipartyTransactionState '{t}'")
			});

	private static readonly Decoder<IEvent[]> RoundEvents =
		Field("Events", Array(RoundEvent));

	private static readonly Decoder<ConstructionState> ConstructionState =
		RoundEvents.Map(events =>
		{
			var state = new ConstructionState(null!);
			return state with {Events = events.ToImmutableList() };
		});

	private static readonly Decoder<SigningState> SigningState =
		RoundEvents.Map(events => new SigningState(null!, events));

	public static readonly Decoder<InputRegistrationRequest> InputRegistrationRequest =
		Object(get => new InputRegistrationRequest(
			get.Required("RoundId", UInt256),
			get.Required("Input", OutPoint),
			get.Required("OwnershipProof", OwnershipProof),
			get.Required("ZeroAmountCredentialRequests", ZeroCredentialsRequest),
			get.Required("ZeroVsizeCredentialRequests", ZeroCredentialsRequest)
		));

	public static readonly Decoder<InputRegistrationResponse> InputRegistrationResponse =
		Object(get => new InputRegistrationResponse(
			get.Required("aliceId", Guid),
			get.Required("amountCredentials", CredentialsResponse),
			get.Required("vsizeCredentials", CredentialsResponse)
		));

	public static readonly Decoder<OutputRegistrationRequest> OutputRegistrationRequest =
		Object(get => new OutputRegistrationRequest(
			get.Required("RoundId", UInt256),
			get.Required("Script", Script),
			get.Required("AmountCredentialRequests", RealCredentialsRequest),
			get.Required("VsizeCredentialRequests", RealCredentialsRequest)
		));

	public static readonly Decoder<ReissueCredentialRequest> ReissueCredentialRequest =
		Object(get => new ReissueCredentialRequest(
			get.Required("RoundId", UInt256),
			get.Required("RealAmountCredentialRequests", RealCredentialsRequest),
			get.Required("RealVsizeCredentialRequests", RealCredentialsRequest),
			get.Required("ZeroAmountCredentialRequests", ZeroCredentialsRequest),
			get.Required("ZeroVsizeCredentialsRequests", ZeroCredentialsRequest)
		));

	public static readonly Decoder<ReissueCredentialResponse> ReissueCredentialResponse =
		Object(get => new ReissueCredentialResponse(
			get.Required("realAmountCredentials", CredentialsResponse),
			get.Required("realVsizeCredentials", CredentialsResponse),
			get.Required("zeroAmountCredentials", CredentialsResponse),
			get.Required("zeroVsizeCredentials", CredentialsResponse)
		));

	public static readonly Decoder<InputsRemovalRequest> InputsRemovalRequest =
		Object(get => new InputsRemovalRequest(
			get.Required("RoundId", UInt256),
			get.Required("AliceId", Guid)
		));

	public static readonly Decoder<ConnectionConfirmationRequest> ConnectionConfirmationRequest =
		Object(get => new ConnectionConfirmationRequest(
			get.Required("RoundId", UInt256),
			get.Required("AliceId", Guid),
			get.Required("ZeroAmountCredentialRequests", ZeroCredentialsRequest),
			get.Required("RealAmountCredentialRequests", RealCredentialsRequest),
			get.Required("ZeroVsizeCredentialRequests", ZeroCredentialsRequest),
			get.Required("RealVsizeCredentialRequests", RealCredentialsRequest)
		));

	public static readonly Decoder<ConnectionConfirmationResponse> ConnectionConfirmationResponse =
		Object(get => new ConnectionConfirmationResponse(
			get.Required("zeroAmountCredentials", CredentialsResponse),
			get.Required("zeroVsizeCredentials", CredentialsResponse),
			get.Optional("realAmountCredentials", CredentialsResponse),
			get.Optional("realVsizeCredentials", CredentialsResponse)
		));

	public static readonly Decoder<TransactionSignaturesRequest> TransactionSignaturesRequest =
		Object(get => new TransactionSignaturesRequest(
			get.Required("RoundId", UInt256),
			get.Required("InputIndex", UInt),
			get.Required("Witness", WitScript)
		));

	public static readonly Decoder<ReadyToSignRequestRequest> ReadyToSignRequestRequest =
		Object(get => new ReadyToSignRequestRequest(
			get.Required("RoundId", UInt256),
			get.Required("AliceId", Guid)
		));

	private static readonly Decoder<RoundState> RoundState =
		Object(get => new RoundState(
			get.Required("id", UInt256),
			get.Required("blameOf", UInt256),
			get.Required("amountCredentialIssuerParameters", CredentialIssuerParameters),
			get.Required("vsizeCredentialIssuerParameters", CredentialIssuerParameters),
			get.Required("phase", Int.Map(n => (Phase)n)),
			get.Required("endRoundState", Int.Map(n => (EndRoundState)n)),
			get.Required("inputRegistrationStart", DateTimeOffset),
			get.Required("inputRegistrationTimeout", TimeSpan),
			get.Required("coinjoinState", MultipartyTransactionState)
		));

	private static readonly Decoder<RoundStateCheckpoint> RoundStateCheckpoint =
		Object(get => new RoundStateCheckpoint(
			get.Required("RoundId", UInt256),
			get.Required("StateId", Int)
		));

	public static readonly Decoder<RoundStateRequest> RoundStateRequest =
		Object(get => new RoundStateRequest(
			get.Required("RoundCheckpoints", Array(RoundStateCheckpoint)).ToImmutableList()
		));

	public static readonly Decoder<RoundStateResponse> RoundStateResponse =
		Object(get => new RoundStateResponse(
			get.Required("roundStates", Array(RoundState))
		));

	private static readonly Decoder<InputBannedExceptionData> InputBannedExceptionData =
		Object(get => new InputBannedExceptionData(
			get.Required("BannedUntil", DateTimeOffset)
		));

	private static readonly Decoder<WrongPhaseExceptionData> WrongPhaseExceptionData =
		Object(get => new WrongPhaseExceptionData(
			get.Required("CurrentPhase", Int.Map(x => (Phase)x))
		));

	private static readonly Decoder<EmptyExceptionData> EmptyExceptionData =
		Succeed(WabiSabi.Coordinator.Models.EmptyExceptionData.Instance);

	public static readonly Decoder<ExceptionData> ExceptionData =
		Field("Type", String).AndThen(t => t switch
		{
			"InputBannedExceptionData" => Cast<ExceptionData, InputBannedExceptionData>(InputBannedExceptionData),
			"WrongPhaseExceptionData" => Cast<ExceptionData, WrongPhaseExceptionData>(WrongPhaseExceptionData),
			"EmptyExceptionData" => Cast<ExceptionData, EmptyExceptionData>(EmptyExceptionData),
			_ => Fail<ExceptionData>($"Unknown ExceptionData '{t}'")
		});

	public static readonly Decoder<Error> Error =
		Object(get => new Error(
			get.Required("type", String),
			get.Required("errorCode", String),
			get.Required("description", String),
			get.Required("exceptionData", ExceptionData)
		));

	public static async Task<Result<object, string>> CoordinatorMessageFromStreamAsync(Stream json, Type modelType)
	{
		var decoder = GetDecoder(modelType);
		var asyncDeserializer = JsonDecoder.FromStreamAsync(decoder);
		var result = await asyncDeserializer(json).ConfigureAwait(false);
		return result;
	}

	public static T CoordinatorMessage<T>(string json)
	{
		var decoder = GetDecoder(typeof(T));
		return (T)JsonDecoder.FromString(json, decoder)!;
	}

	private static Decoder<object> GetDecoder(Type modelType)
	{
		var decoder = modelType switch
		{
			{ } t when t == typeof(InputRegistrationRequest) => Cast<object, InputRegistrationRequest>(InputRegistrationRequest),
			{ } t when t == typeof(InputsRemovalRequest) => Cast<object, InputsRemovalRequest>(InputsRemovalRequest),
			{ } t when t == typeof(ConnectionConfirmationRequest) => Cast<object, ConnectionConfirmationRequest>(ConnectionConfirmationRequest),
			{ } t when t == typeof(ReissueCredentialRequest) => Cast<object, ReissueCredentialRequest>(ReissueCredentialRequest),
			{ } t when t == typeof(OutputRegistrationRequest) => Cast<object, OutputRegistrationRequest>(OutputRegistrationRequest),
			{ } t when t == typeof(ReadyToSignRequestRequest) => Cast<object, ReadyToSignRequestRequest>(ReadyToSignRequestRequest),
			{ } t when t == typeof(TransactionSignaturesRequest) => Cast<object, TransactionSignaturesRequest>(TransactionSignaturesRequest),
			{ } t when t == typeof(RoundStateRequest) => Cast<object, RoundStateRequest>(RoundStateRequest),
			{ } t when t == typeof(RoundState) => Cast<object, RoundState>(RoundState),

			{ } t when t == typeof(InputRegistrationResponse) => Cast<object, InputRegistrationResponse>(InputRegistrationResponse),
			{ } t when t == typeof(ReissueCredentialResponse) => Cast<object, ReissueCredentialResponse>(ReissueCredentialResponse),
			{ } t when t == typeof(ConnectionConfirmationResponse) => Cast<object, ConnectionConfirmationResponse>(ConnectionConfirmationResponse),
			{ } t when t == typeof(RoundStateResponse) => Cast<object, RoundStateResponse>(RoundStateResponse),
			_ => throw new Exception($"{modelType.FullName} is not known")
		};
		return decoder;
	}
}
