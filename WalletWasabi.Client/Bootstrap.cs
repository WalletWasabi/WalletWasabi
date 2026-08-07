using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.JsonConverters;
using Newtonsoft.Json;
using NScheme;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Helpers;
using WalletWasabi.Rpc.JsonConverters;
using WalletWasabi.Wallets;

namespace WalletWasabi.Client;

public class Scheme
{
	private Env _env;
	private bool _initialized = false;
	private JsonSerializerSettings _defaultJsonSerializerSettings;

	public Action<string>? OnDisplay { get; set; }

	public Scheme(Global global)
	{
		var scriptsDir = Path.Combine(global.DataDir, "scripts");

		EnsureScriptsDirectory(scriptsDir);

		// Create IoCapabilities that only allows reading files by name from scripts directory
		var ioCapabilities = new IoCapabilities(
			OpenInput: name =>
			{
				// Ensure name is just a filename, not a path
				if (Path.GetFileName(name) != name)
				{
					throw new UnauthorizedAccessException(
						$"Access denied: only filenames are allowed, not paths.");
				}

				var fullPath = Path.Combine(scriptsDir, name);
				return new StreamReader(fullPath);
			},
			OpenOutput: null, // No file output allowed
			Console: TextWriter.Null // No console output
		);

		_env = Builtins.Global(ioCapabilities);

		// Register native functions
		RegisterNativeFunction("now", () => DateTime.Now);
		RegisterNativeFunction<string, object>("__get", GetterFn);
		RegisterNativeFunction("global", () => global);
		RegisterNativeFunction<object>("native->string", o => o?.ToString() ?? "");
		RegisterNativeFunction<Script>("script->address", s => s.GetDestinationAddress(global.Network)!);
		RegisterNativeFunction<ExtPubKey?>("extpubkey->string", e => e?.ToString(global.Network) ?? "");
		RegisterNativeFunction("wallets", () => global.WalletManager.GetWallets());
		RegisterNativeFunction<Wallet>("wallet-coins", w => w.Coins.AsAllCoinsView());

		RegisterNativeFunction<Wallet>("wallet-hdpubkeys", w => w.KeyManager.GetKeys());
		RegisterNativeFunction("fee-rate-estimations", () => global.Status.FeeRates?.Estimations ?? ImmutableSortedDictionary<int, FeeRate>.Empty);
		RegisterNativeFunction("exchange-rate-usd", () => global.Status.UsdExchangeRate);
		RegisterNativeFunction("tor-running?", () => global.Status.IsTorRunning);
		RegisterNativeFunction("tor-settings", () => global.TorSettings);
		RegisterNativeFunction("onion-service-uri", () => global.OnionServiceUri?.ToString() ?? "");
		RegisterNativeFunction<SmartTransaction>("broadcast-tx", tx =>
		{
			global.TransactionBroadcaster.SendTransactionAsync(tx).GetAwaiter().GetResult();
			return tx;
		});
		RegisterNativeFunction("connected-nodes", () => global.GetNodes());
		RegisterNativeFunction<Wallet>("__start_wallet", w =>
		{
			global.WalletManager.StartWalletAsync(w).GetAwaiter().GetResult();
			return w;
		});

		RegisterNativeFunction<string, Closure>("on",
			(eventName, func) => SubscribeEvent(global, eventName, func));
		RegisterNativeAction<object>("display", o => OnDisplay?.Invoke(o?.ToString() ?? ""));

		// Address generation functions
		RegisterNativeFunction<Wallet, string, bool, object>("generate-address",
			(wallet, label, taproot) =>
			{
				var scriptType = taproot ? ScriptPubKeyType.TaprootBIP86 : ScriptPubKeyType.Segwit;
				var hdKey = wallet.KeyManager.GetNextReceiveKey(new LabelsArray(label), scriptType);
				return hdKey;
			});

		// Transaction building: wallet, address, amount (BTC), fee-rate (sat/vB), coins (list or empty), subtractFee, password
		RegisterNativeFunction<Wallet, string, decimal, decimal, IEnumerable<object>, bool, string, object>("build-tx",
			(wallet, addressStr, amountBtc, feeRateSatPerVb, coins, subtractFee, password) =>
			{
				var address = BitcoinAddress.Create(addressStr, global.Network);
				var feeRate = new FeeRate(feeRateSatPerVb);

				var coinsList = coins.ToList();
				IEnumerable<OutPoint>? allowedInputs = null;
				if (coinsList.Count > 0)
				{
					allowedInputs = coinsList.Select(c =>
					{
						if (c is SmartCoin smartCoin)
						{
							return smartCoin.Outpoint;
						}
						throw new ArgumentException($"Expected SmartCoin but got {c.GetType().Name}");
					});
				}

				PaymentIntent payment;
				if (amountBtc <= 0)
				{
					// Send all remaining (no change)
					payment = new PaymentIntent(address.ScriptPubKey, MoneyRequest.CreateAllRemaining(subtractFee: true));
				}
				else
				{
					payment = new PaymentIntent(address.ScriptPubKey, Money.Coins(amountBtc), subtractFee);
				}

				var result = wallet.BuildTransaction(
					password,
					payment,
					FeeStrategy.CreateFromFeeRate(feeRate),
					allowUnconfirmed: true,
					allowedInputs: allowedInputs);

				return result.Transaction;
			});

		// Parse an address
		RegisterNativeFunction<string>("parse-address",
			addressStr => BitcoinAddress.Create(addressStr, global.Network));

		// Get address from HdPubKey
		RegisterNativeFunction<HdPubKey>("hdpubkey->address",
			key => key.GetAddress(global.Network));

		_defaultJsonSerializerSettings = CreateJsonSerializerSettings(global.Network);
	}

	private void RegisterNativeFunction(string name, Func<object> fn)
	{
		_env.Define(name, new Primitive(name, _ => ConvertNativeToScheme(fn(), 0), 0));
	}

	private void RegisterNativeFunction<T>(string name, Func<T, object> fn)
	{
		_env.Define(name, new Primitive(name, args =>
		{
			var param = ConvertSchemeToNative(args[0]);
			return ConvertNativeToScheme(fn((T)param), 0);
		}, 1));
	}

	private void RegisterNativeAction<T>(string name, Action<T> fn)
	{
		_env.Define(name, new Primitive(name, args =>
		{
			var param = ConvertSchemeToNative(args[0]);
			fn((T)param);
			return Unspecified.Instance;
		}, 1));
	}

	private void RegisterNativeFunction<T0, T1>(string name, Func<T0, T1, object> fn)
	{
		_env.Define(name, new Primitive(name, args =>
		{
			var param0 = ConvertSchemeToNative(args[0]);
			var param1 = ConvertSchemeToNative(args[1]);
			return ConvertNativeToScheme(fn((T0)param0, (T1)param1), 0);
		}, 2));
	}

	private void RegisterNativeFunction<T0, T1, T2, TResult>(string name, Func<T0, T1, T2, TResult> fn)
	{
		_env.Define(name, new Primitive(name, args =>
		{
			var param0 = ConvertSchemeToNative(args[0]);
			var param1 = ConvertSchemeToNative(args[1]);
			var param2 = ConvertSchemeToNative(args[2]);
			return ConvertNativeToScheme(fn((T0)param0, (T1)param1, (T2)param2)!, 0);
		}, 3));
	}

	private void RegisterNativeFunction<T0, T1, T2, T3, T4, TResult>(string name, Func<T0, T1, T2, T3, T4, TResult> fn)
	{
		_env.Define(name, new Primitive(name, args =>
		{
			var param0 = ConvertSchemeToNative(args[0]);
			var param1 = ConvertSchemeToNative(args[1]);
			var param2 = ConvertSchemeToNative(args[2]);
			var param3 = ConvertSchemeToNative(args[3]);
			var param4 = ConvertSchemeToNative(args[4]);
			return ConvertNativeToScheme(fn((T0)param0, (T1)param1, (T2)param2, (T3)param3, (T4)param4)!, 0);
		}, 5));
	}

	private void RegisterNativeFunction<T0, T1, T2, T3, T4, T5, TResult>(string name, Func<T0, T1, T2, T3, T4, T5, TResult> fn)
	{
		_env.Define(name, new Primitive(name, args =>
		{
			var param0 = ConvertSchemeToNative(args[0]);
			var param1 = ConvertSchemeToNative(args[1]);
			var param2 = ConvertSchemeToNative(args[2]);
			var param3 = ConvertSchemeToNative(args[3]);
			var param4 = ConvertSchemeToNative(args[4]);
			var param5 = ConvertSchemeToNative(args[5]);
			return ConvertNativeToScheme(fn((T0)param0, (T1)param1, (T2)param2, (T3)param3, (T4)param4, (T5)param5)!, 0);
		}, 6));
	}

	private void RegisterNativeFunction<T0, T1, T2, T3, T4, T5, T6, TResult>(string name, Func<T0, T1, T2, T3, T4, T5, T6, TResult> fn)
	{
		_env.Define(name, new Primitive(name, args =>
		{
			var param0 = ConvertSchemeToNative(args[0]);
			var param1 = ConvertSchemeToNative(args[1]);
			var param2 = ConvertSchemeToNative(args[2]);
			var param3 = ConvertSchemeToNative(args[3]);
			var param4 = ConvertSchemeToNative(args[4]);
			var param5 = ConvertSchemeToNative(args[5]);
			var param6 = ConvertSchemeToNative(args[6]);
			return ConvertNativeToScheme(fn((T0)param0, (T1)param1, (T2)param2, (T3)param3, (T4)param4, (T5)param5, (T6)param6)!, 0);
		}, 7));
	}

	private Value ConvertNativeToScheme(object obj, int depth)
	{
		if (depth++ >= 5)
		{
			throw new InvalidOperationException("Too deep data structure. Max depth is 5");
		}
		return obj switch
		{
			null => Nil.Instance,
			int or short or decimal or byte or long or float or double or uint or ulong or ushort =>
				new RealNumber(Convert.ToDouble(obj)),
			Enum e => new RealNumber(Convert.ToDouble(e)),
			string stringValue => new Str(stringValue),
			char characterValue => new Character(characterValue),
			bool booleanValue => NScheme.Boolean.Of(booleanValue),
			System.Collections.IEnumerable e when e is not string =>
				SExpr.FromArray(e.Cast<object>().Select(x => ConvertNativeToScheme(x, depth)).ToArray()),
			var o => new NativeObject(o)
		};
	}

	private object ConvertSchemeToNative(Value e) => ToNativeObject(e);

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

	private object SubscribeEvent(Global global, string eventName, Closure func)
	{
		var eventType = Type.GetType($"WalletWasabi.Services.{eventName}, WalletWasabi", throwOnError: false);
		if (eventType is null)
		{
			throw new ArgumentException($"event {eventName} does not exist");
		}
		global.EventBus.Subscribe(eventType, arg => Interpreter.Apply(func, [ConvertNativeToScheme(arg, 0)]));
		return Unspecified.Instance;
	}

	public async Task<Value> ExecuteAsync(string prg)
	{
		await InitializeAsync().ConfigureAwait(false);
		var result = Interpreter.Run(prg, _env);
		return result;
	}

	private async Task InitializeAsync()
	{
		if (!_initialized)
		{
			await Task.Run(() => Interpreter.Run("(load \"Wasabilib.scm\")", _env)).ConfigureAwait(false);
			_initialized = true;
		}
	}

	private static void EnsureScriptsDirectory(string scriptsDir)
	{
		Directory.CreateDirectory(scriptsDir);

		var appSchemeDir = Path.Combine(EnvironmentHelpers.GetFullBaseDirectory(), "Scheme");
		string[] libraryFiles = ["Stdlib.scm", "Wasabilib.scm"];

		foreach (var fileName in libraryFiles)
		{
			var targetPath = Path.Combine(scriptsDir, fileName);
			if (!File.Exists(targetPath))
			{
				var sourcePath = Path.Combine(appSchemeDir, fileName);
				if (File.Exists(sourcePath))
				{
					File.Copy(sourcePath, targetPath);
				}
			}
		}
	}

	private JsonSerializerSettings CreateJsonSerializerSettings(Network network)
	{
		var defaultSettings = new JsonSerializerSettings
		{
			NullValueHandling = NullValueHandling.Ignore,
			ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
			Formatting = Formatting.Indented,
			MaxDepth = 5,
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

	public string ToJson(Value e) =>
		JsonConvert.SerializeObject(ToObject(ToNativeObject(e)), _defaultJsonSerializerSettings);

	public static object ToNativeObject(Value e) =>
		e switch
		{
			IntegerNumber(var value) => (decimal)value,
			RationalNumber r => (decimal)r.Numerator / (decimal)r.Denominator,
			RealNumber(var value) => (decimal)value,
			Str s => s.Val,
			Character(var c) => c.ToString(),
			NScheme.Boolean b => b.Val,
			NativeObject o => o.Value,
			Symbol(var name) => name,
			Pair p => SExpr.Iterate(p).Select(ToNativeObject),
			Nil _ => false,
			Unspecified _ => "Done",
			Closure c => c,
			_ => throw new Exception($"Cannot convert {e.GetType().Name} to native object")
		};
}

// NativeObject value type to hold arbitrary .NET objects
public sealed record NativeObject(object Value) : Value;
