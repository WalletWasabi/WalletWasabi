using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using WalletWasabi.Extensions;

namespace WalletWasabi.WabiSabi.Coordinator.DoSPrevention;

public enum RoundDisruptionMethod
{
	DidNotConfirm,
	DidNotSignalReadyToSign,
	DidNotSign,
	DoubleSpent,
	IndexerStabilitySafety
}

public abstract record Offense();

public record RoundDisruption(IEnumerable<uint256> DisruptedRoundIds, Money Value, RoundDisruptionMethod Method) : Offense
{
	public	RoundDisruption(uint256 disruptedRoundId, Money value, RoundDisruptionMethod method)
		: this(disruptedRoundId.Singleton(), value, method) {}
}
public record CoordinatorStabilitySafety(uint256 RoundId) : Offense;
public record FailedToVerify(uint256 VerifiedInRoundId) : Offense;
public record Inherited(OutPoint[] Ancestors) : Offense;
public record Cheating(uint256 RoundId) : Offense;

public record Offender(OutPoint OutPoint, DateTimeOffset StartedTime, Offense Offense)
{
	private const string Separator = ",";
	public string ToStringLine()
	{
		IEnumerable<string> SerializedElements()
		{
			yield return StartedTime.ToUnixTimeSeconds().ToString();
			yield return OutPoint.ToString();
			switch (Offense)
			{
				case RoundDisruption rd:
					yield return nameof(RoundDisruption);
					yield return rd.Value.Satoshi.ToString();
					yield return rd.DisruptedRoundIds.First().ToString();
					yield return rd.Method switch
					{
						RoundDisruptionMethod.DidNotConfirm => "didn't confirm",
						RoundDisruptionMethod.DidNotSignalReadyToSign => "didn't signal ready to sign",
						RoundDisruptionMethod.DidNotSign => "didn't sign",
						RoundDisruptionMethod.DoubleSpent => "double spent",
						_ => throw new NotImplementedException("Unknown round disruption method.")
					};
					foreach (var disruptedRoundId in rd.DisruptedRoundIds.Skip(1))
					{
						yield return disruptedRoundId.ToString();
					}
					break;
				case CoordinatorStabilitySafety indexerStabilitySafety:
					yield return nameof(CoordinatorStabilitySafety);
					yield return indexerStabilitySafety.RoundId.ToString();
					break;
				case FailedToVerify fv:
					yield return nameof(FailedToVerify);
					yield return fv.VerifiedInRoundId.ToString();
					break;
				case Inherited inherited:
					yield return nameof(Inherited);
					foreach (var ancestor in inherited.Ancestors)
					{
						yield return ancestor.ToString();
					}
					break;
				case Cheating cheating:
					yield return nameof(Cheating);
					yield return cheating.RoundId.ToString();
					break;
				default:
					throw new NotImplementedException("Cannot serialize an unknown offense type.");
			}
		}

		return string.Join(Separator, SerializedElements());
	}

	public static Offender FromStringLine(string str)
	{
		var parts = str.Split(Separator);

		var startedTime = DateTimeOffset.FromUnixTimeSeconds(long.Parse(parts[0]));
		var outpoint = OutPoint.Parse(parts[1]);

		Offense offense = parts[2] switch
		{
			nameof(RoundDisruption) =>
				new RoundDisruption(
					parts.Skip(6).Select(x => uint256.Parse(x)).Prepend(uint256.Parse(parts[4])),
					Money.Satoshis(long.Parse(parts[3])),

					parts[5] switch
					{
						"didn't confirm" => RoundDisruptionMethod.DidNotConfirm,
						"didn't signal ready to sign" => RoundDisruptionMethod.DidNotSignalReadyToSign,
						"didn't sign" => RoundDisruptionMethod.DidNotSign,
						"double spent" => RoundDisruptionMethod.DoubleSpent,
						_ => throw new NotImplementedException("Unknown round disruption method.")
					}),
			nameof(CoordinatorStabilitySafety) =>
				new CoordinatorStabilitySafety(uint256.Parse(parts[3])),
			nameof(FailedToVerify) =>
				new FailedToVerify(uint256.Parse(parts[3])),
			nameof(Inherited) =>
				ParseInheritedOffense(),
			nameof(Cheating) =>
				new Cheating(uint256.Parse(parts[3])),
		_ => throw new NotImplementedException("Cannot deserialize an unknown offense type.")
		};

		return new Offender(outpoint, startedTime, offense);

		Offense ParseInheritedOffense()
		{
			var ancestors = parts.Skip(3).Select(OutPoint.Parse).ToArray();
			return new Inherited(ancestors);
		}
	}
}
