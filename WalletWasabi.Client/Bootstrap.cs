using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.JsonConverters;
using Newtonsoft.Json;
using WalletWasabi.Helpers;
using WalletWasabi.Rpc.JsonConverters;
using WalletWasabi.Scheme;
using WalletWasabi.Wallets;
using static WalletWasabi.Scheme.Interpreter;

namespace WalletWasabi.Client;

using Environment = System.Collections.Immutable.ImmutableDictionary<string, Expression>;

public class Scheme
{
	private Environment _env;
	private bool _initialized;
	private readonly JsonSerializerSettings _defaultJsonSerializerSettings;

	public Scheme(Global global)
	{
		_env = Env
			.NativeFunction("now", () => DateTime.Now)
			.NativeFunction<string, object>("__get", GetterFn)
			.NativeFunction("global", () => global)
			.NativeFunction<object>("native->string", o => o?.ToString() ?? "")
			.NativeFunction<Script>("script->address", s => s.GetDestinationAddress(global.Network)!)
			.NativeFunction<ExtPubKey?>("extpubkey->string", e => e?.ToString(global.Network) ?? "")
			.NativeFunction("wallets", () => global.WalletManager.GetWallets())
			.NativeFunction<Wallet>("wallet-coins", w => w.Coins.AsAllCoinsView())
			.NativeFunction<Wallet>("__start_wallet", w =>
			{
				 global.WalletManager.StartWalletAsync(w).GetAwaiter().GetResult();
				 return w;
			})
			.NativeFunction<string, SpecialExpressionsProcessor>("on", (eventName, func) => SubscribeEvent(global, eventName, func));
		_defaultJsonSerializerSettings = CreateJsonSerializerSettings(global.Network);
	}

	private readonly Dictionary<(Type, string), MemberInfo> _accessors = new();

	private object GetterFn(string method, object instance)
	{
		var typ = instance.GetType();
		var key = (typ, method);
		if (!_accessors.TryGetValue(key, out var info))
		{
			var members = typ.GetMember(method,
				BindingFlags.GetProperty
				//| BindingFlags.InvokeMethod // disable because it could be dangerous
				| BindingFlags.Instance
				| BindingFlags.Public
				| BindingFlags.IgnoreCase);
			if (members is [])
			{
				throw new InvalidOperationException($"Member '{method}' not found");
			}
			info = members[0];
			_accessors.Add(key, info);
		}

		var result = info switch
		{
			MethodInfo mi => mi.Invoke(instance, []),
			PropertyInfo pi => pi.GetValue(instance),
			_ => throw new ArgumentOutOfRangeException()
		};
		return result!;
	}

	private object SubscribeEvent(Global global, string eventName, SpecialExpressionsProcessor func)
	{
		var eventType = Type.GetType($"WalletWasabi.Services.{eventName}, WalletWasabi", throwOnError: false);
		if (eventType is null)
		{
			throw new ArgumentException($"event {eventName} does not exist");
		}
		global.EventBus.Subscribe(eventType, arg => func(_env, new NativeObject(arg)));
		return "Subscribed";
	}

	public async Task<Expression> ExecuteAsync(string prg)
	{
		await InitializeAsync();
		var parsingResult = Parse(Tokenizer.Tokenize(prg).ToArray());
		var (penv, expressionResult) = Eval(_env, parsingResult[0]);
		_env = penv;
		return expressionResult;
	}

	private async Task InitializeAsync()
	{
		if (!_initialized)
		{
			var (env, _) = await Task.Run(() => _env.Load("Scheme/Wasabilib.scm"));
			_env = env;
			_initialized = true;
		}
	}

	private JsonSerializerSettings CreateJsonSerializerSettings(Network network)
	{
		var defaultSettings = new JsonSerializerSettings
		{
			NullValueHandling = NullValueHandling.Ignore,
			ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
			Formatting = Formatting.Indented,
			MaxDepth = 3,
			Converters = new List<JsonConverter>
			{
				new Uint256JsonConverter(),
				new OutPointAsTxoRefJsonConverter(),
				new BitcoinAddressJsonConverter(),
				new DestinationJsonConverter(network),
				new SmartTransactionJsonConverter(),
			}
		};
		Serializer.RegisterFrontConverters(defaultSettings, network);
		return defaultSettings;
	}

	public static object ToObject(object obj)
	{
		if (obj is not IEnumerable<object> e)
		{
			return obj is decimal d && Math.Truncate(d) == d ? (int)d : obj;
		}

		var arr = e.ToArray();
		var dict = new Dictionary<string, object>(arr.Length);

		foreach (var item in arr)
		{
			if (item is IEnumerable<object> pair && pair.ToArray() is [string key, var value])
			{
				dict[key] = ToObject(value);
			}
			else
			{
				return arr.Select(ToObject).ToArray();
			}
		}

		return dict;
	}

	public string ToJson(Expression e) =>
		JsonConvert.SerializeObject(ToObject(ToNativeObject(e)), _defaultJsonSerializerSettings);

	public void RegisterWriter(Action<string> write)
	{
		_env = _env.NativeFunction<object>("display", s =>
		{
			write(s.ToString() ?? "");
			return Unit.Instance;
		});
	}
}
