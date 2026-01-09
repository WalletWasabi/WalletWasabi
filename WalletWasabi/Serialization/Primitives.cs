using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Userfacing;
using JsonException = System.Text.Json.JsonException;

namespace WalletWasabi.Serialization;

public delegate JsonNode Encoder<in T>(T value);
public delegate Result<T, string> Decoder<T>(JsonElement value);

// Define encoders for primitive types.
// An encoder is a function that takes a value and returns a JsonNode representation of that value.
public static partial class Encode
{
	[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static JsonNode String(string value) => JsonValue.Create(value);

	[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static JsonNode Decimal(decimal value) => JsonValue.Create(value);

	[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static JsonNode Double(double value) => JsonValue.Create(value);

	[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static JsonNode Bool(bool value) => JsonValue.Create(value);

	[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static JsonNode Guid(Guid value) => JsonValue.Create(value);

	[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static JsonNode Dictionary(Dictionary<string, JsonNode> values) => new JsonObject(values);

	[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static JsonNode Object(IEnumerable<(string, JsonNode?)> values) => new JsonObject(values.ToDictionary(x => x.Item1, x => x.Item2));

	[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static JsonNode Array(IEnumerable<JsonNode> values) => new JsonArray(values.ToArray());

	[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static JsonNode DatetimeOffset(DateTimeOffset value) =>  JsonValue.Create(value);

	[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static JsonNode Int(int value) => JsonValue.Create(value);

	[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static JsonNode UInt(uint value) => JsonValue.Create(value);

	[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static JsonNode Int64(long value) => JsonValue.Create(value);

	[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static JsonNode? Optional<T>(T? value, Encoder<T> encoder) =>
		value is { } nonNullValue ? encoder(nonNullValue) : null;

	[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static JsonNode EndPoint(EndPoint ep, int defaultPort) =>
		String(ep.ToString(defaultPort));
}


// Define decoders for primitive types.
// A Decoder is a function that takes a JsonElement and returns the value represented by it.
public static partial class Decode
{
	private static Result<string, string> GetString(JsonElement value)
	{
		if (value.ValueKind == JsonValueKind.String)
		{
			if (value.GetString() is { } str)
			{
				return Result<string, string>.Ok(str);
			}

			return Result<string, string>.Fail("It is empty");
		}

		return Result<string, string>.Fail("It is not a string");
	}

	public static Decoder<string> String =>
		value =>
			value.ValueKind == JsonValueKind.String
				? Result<string, string>.Ok(value.GetString()!)
				: Result<string, string>.Fail("It is not a string");

	public static Decoder<Guid> Guid =>
		String.AndThen(str => System.Guid.TryParse(str, out var guid)
			? Succeed(guid)
			: Fail<Guid>("The string is empty"));

	private delegate bool TryParse<T>(string input, [NotNullWhen(true)] out T? result);

	public static Decoder<int> Int =>
		value => Integral("a integer", int.TryParse, int.MinValue, int.MaxValue, Convert.ToInt32, value);

	public static Decoder<uint> UInt =>
		value => Integral("an unsigned integer", uint.TryParse, uint.MinValue, uint.MaxValue, Convert.ToUInt32, value);

	public static Decoder<long> Int64 =>
		value => Integral("a long integer", long.TryParse, long.MinValue, long.MaxValue, Convert.ToInt64, value);

	public static Decoder<bool> Bool =>
		value =>
			value.ValueKind is JsonValueKind.True or JsonValueKind.False
				? Result<bool, string>.Ok(value.GetBoolean())
				: Result<bool, string>.Fail("It is not a boolean");

	public static Decoder<decimal> Decimal =>
		value =>
			value.ValueKind == JsonValueKind.Number
				? Result<decimal, string>.Ok(value.GetDecimal())
				: Result<decimal, string>.Fail("It is not a number");

	public static Decoder<double> Double =>
		Decimal.Map(d => (double) d);

	public static Decoder<DateTimeOffset> DateTimeOffset =
		String.Map(System.DateTimeOffset.Parse);

	public static readonly Decoder<EndPoint> EndPoint =
		String.AndThen(s =>
		{
			if (EndPointParser.TryParse(s, out EndPoint? endPoint))
			{
				return Succeed(endPoint);
			}

			return Fail<EndPoint>($"Invalid endpoint format: '{s}'");
		});

	public static Decoder<T> Succeed<T>(T output) =>
		_ => output;

	public static Decoder<T> Fail<T>(string message) =>
		_ => Result<T, string>.Fail(message);

	public static Decoder<T> Map<T, R>(this Decoder<R> decoder, Func<R, T> f) =>
		value =>
		{
			var m = decoder(value);
			return m.IsOk ? f(m.Value) : m.Error;
		};

	public static Decoder<T> Map2<T, R, U>(Func<R, U, T> ctor, Decoder<R> d1, Decoder<U> d2) =>
		value =>
		{
			var (m1, m2) = (d1(value), d2(value));
			return (m1.IsOk, m2.IsOk) switch
			{
				(true, true) => ctor(m1.Value, m2.Value),
				(false, _) => Result<T, string>.Fail(m1.Error),
				(_, false) => Result<T, string>.Fail(m2.Error),
			};
		};

	public static Decoder<T> Index<T>(int index, Decoder<T> decoder) =>
		value =>
		{
			if (value.ValueKind == JsonValueKind.Array)
			{
				var len = value.GetArrayLength();
				if (index < value.GetArrayLength())
				{
					return decoder(value[index]);
				}

				return Result<T, string>.Fail($"Index {index} requested for an array of length {len}");
			}

			return Result<T, string>.Fail("Can't get the index of a non-array element");
		};

	public static Decoder<T[]> Array<T>(Decoder<T> decoder) =>
		value =>
		{
			if (value.ValueKind == JsonValueKind.Array)
			{
				List<T> list = [];
				foreach (var t in value.EnumerateArray().Select(
					         elem => decoder(elem)
					         ))
				{
					if (!t.IsOk)
					{
						return Result<T[], string>.Fail(t.Error);
					}

					list.Add(t.Value);
				}

				return list.ToArray();
			}

			return Result<T[], string>.Fail("It is not an array");
		};

	public static Decoder<Dictionary<string, T>> Dictionary<T>(Decoder<T> decoder) =>
		value =>
		{
			if (value.ValueKind != JsonValueKind.Object)
			{
				return Result<Dictionary<string, T>, string>.Fail("It is not a dictionary");
			}

			return Result<Dictionary<string, T>, string>.Ok(value.EnumerateObject()
				.ToDictionary(x => x.Name, x => decoder(x.Value).Value));
		};

	public static Decoder<(D0 d0,D1 d1,D2 d2, D3 d3)> Tuple4<D0, D1, D2, D3>(
		Decoder<D0> decoder0,
		Decoder<D1> decoder1,
		Decoder<D2> decoder2,
		Decoder<D3> decoder3) =>
		Index(0, decoder0)
			.AndThen(v0 => Index(1, decoder1)
				.AndThen(v1 => Index(2, decoder2)
					.AndThen(v2 => Index(3, decoder3)
						.AndThen(v3 => Succeed((v0, v1, v2, v3))))));

	public static Decoder<T> Field<T>(string fieldName, Decoder<T> decoder) =>
		value =>
		{
			if (value.ValueKind != JsonValueKind.Object)
			{
				return Result<T, string>.Fail($"It is not an object. Try to access field '{fieldName}'");
			}

			// this is because some coordinators serialize the message in pascal case
			var pascalCasedFieldName = string.Join("", fieldName[..1].ToUpperInvariant().Concat(fieldName[1..]));
			if (!value.TryGetProperty(fieldName, out var p) && !value.TryGetProperty(pascalCasedFieldName, out p) )
			{
				return Result<T, string>.Fail($"Object does not contain a property called '{fieldName}'");
			}

			return decoder(p);
		};

	public static Decoder<T?> Optional<T>(Decoder<T> decoder) =>
		value => decoder(value).Match(v => v, e => default(T?));

	public static Decoder<T> OneOf<T>(Decoder<T>[] decoders) =>
		value =>
		{
			var errors = new List<string>();
			foreach (var decoder in decoders)
			{
				var result = decoder(value);
				if (result.IsOk)
				{
					return result;
				}

				errors.Add(result.Error);
			}

			return Result<T, string>.Fail(string.Join("; ", errors));
		};

	public class Getters(JsonElement value)
	{
		public List<string> Errors { get; } = [];
		public JsonElement Value => value;

		public T Required<T>(string fileName, Decoder<T> decoder) =>
			Field(fileName, decoder)(value)
				.Match(v => v, e =>
				{
					Errors.Add(e);
					return default!;
				});

		public T? Optional<T>(string fileName, Decoder<T> decoder) =>
			Field(fileName, decoder)(value).Match(
				v => v,
				_ => (T?) (object?) null);

		public T Optional<T>(string fileName, Decoder<T> decoder, T def) where T : struct =>
			Field(fileName, decoder)(value).Match(v => v, _ => def);
	}

	public static Decoder<T> Object<T>(Func<Getters, T> builder) =>
		value =>
		{
			var getters = new Getters(value);
			var result = builder(getters);
			return getters.Errors is [] ? result : Result<T, string>.Fail(string.Join("; ", getters.Errors));
		};

	public static Decoder<T> AndThen<T, R>(Func<R, Decoder<T>> cb, Decoder<R> decoder) =>
		value => decoder(value).Match(r => cb(r)(value), s => s);

	public static Decoder<T> AndMap<T, R>(Decoder<R> decoder1, Decoder<R> decoder2, Func<R, R, T> ctor) =>
		Map2(ctor, decoder1, decoder2);

	public static Decoder<T> AndThen<T, R>(this Decoder<R> decoder, Func<R, Decoder<T>> cb) =>
		AndThen(cb, decoder);

	public static Decoder<T> AndMap<T, R>(this Decoder<Func<R, T>> decoderFun, Decoder<R> decoder) =>
		Map2((x, f) => f(x), decoder, decoderFun);

	private static Result<T, string> Integral<T>(
		string name,
		TryParse<T> tryParse,
		long min,
		long max,
		Func<double, T> conv,
		JsonElement value)
	{
		if (value.ValueKind == JsonValueKind.Number)
		{
			var rawText = value.GetRawText();
			if (!rawText.Contains('.'))
			{
				var doubleValue = value.GetDouble();
				return doubleValue >= min && doubleValue <= max
					? conv(doubleValue)
					: Result<T, string>.Fail($"'{name}' is out of range for {typeof(T).Name}");
			}
		}
		else if (value.ValueKind == JsonValueKind.String)
		{
			return GetString(value).Then(str => tryParse(str, out T? parsedValue)
				? parsedValue
				: Result<T, string>.Fail($"The string is not a valid integral number of '{name}' type '{typeof(T).Name}'"));
		}
		return Result<T, string>.Fail($"It is not '{name}'");
	}
}

public static class JsonEncoder
{
	private static readonly JsonSerializerOptions Indented = new()
	{
		WriteIndented = true,
		Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
	};

	public static string ToString<T>(T obj, Encoder<T> encoder) =>
		encoder(obj).ToJsonString();

	public static string ToReadableString<T>(T obj, Encoder<T> encoder) =>
		encoder(obj).ToJsonString(Indented);
}

public static class JsonDecoder
{
	public static Func<string, Result<T, string>> FromString<T>(Decoder<T> decoder) =>
		value =>
		{
			try
			{
				var jsonDocument = JsonDocument.Parse(value);
				return decoder(jsonDocument.RootElement);
			}
			catch (JsonException e)
			{
				return Result<T, string>.Fail(e.Message);
			}
		};

	internal static T? FromString<T>(string json, Decoder<T> decoder) =>
		FromString(decoder)(json).AsNullable();

	public static Func<Stream, Task<Result<T, string>>> FromStreamAsync<T>(Decoder<T> decoder) =>
		async value =>
		{
			try
			{
				var jsonDocument = await JsonDocument.ParseAsync(value).ConfigureAwait(false);
				return decoder(jsonDocument.RootElement);
			}
			catch (JsonException e)
			{
				return Result<T, string>.Fail(e.Message);
			}
		};

	public static Func<Stream, Result<T, string>> FromStream<T>(Decoder<T> decoder) =>
		value =>
		{
			try
			{
				var jsonDocument = JsonDocument.Parse(value);
				return decoder(jsonDocument.RootElement);
			}
			catch (JsonException e)
			{
				return Result<T, string>.Fail(e.Message);
			}
		};
}
