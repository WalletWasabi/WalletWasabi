using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.JsonConverters;
using Newtonsoft.Json;
using NScheme;
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
		RegisterNativeFunction("fee-rate-estimations", () => global.Status.FeeRates?.Estimations ?? new Dictionary<int,FeeRate>());
		RegisterNativeFunction("exchange-rate-usd", () => global.Status.UsdExchangeRate);
		RegisterNativeFunction("tor-running?", () => global.Status.IsTorRunning);
		RegisterNativeFunction("tor-settings", () => global.TorSettings);
		RegisterNativeFunction("onion-service-uri", () => global.OnionServiceUri?.ToString() ?? "");
		RegisterNativeFunction<SmartTransaction>("broadcast-tx", tx =>
			global.TransactionBroadcaster.SendTransactionAsync(tx));
		RegisterNativeFunction("connected-nodes", () => global.GetNodes());
		RegisterNativeFunction<Wallet>("__start_wallet", w =>
		{
			global.WalletManager.StartWalletAsync(w).GetAwaiter().GetResult();
			return w;
		});

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

	private void RegisterNativeFunction<T0, T1>(string name, Func<T0, T1, object> fn)
	{
		_env.Define(name, new Primitive(name, args =>
		{
			var param0 = ConvertSchemeToNative(args[0]);
			var param1 = ConvertSchemeToNative(args[1]);
			return ConvertNativeToScheme(fn((T0)param0, (T1)param1), 0);
		}, 2));
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

		if (e.All(i => i is IEnumerable<object> ie && ie.Count() == 2 && ie.First() is string))
		{
			return e.ToDictionary(x => ((IEnumerable<object>) x).First(),
				x => ToObject(((IEnumerable<object>) x).ElementAt(1)));
		}

		return e.Select(ToObject);
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
			_ => throw new Exception($"Cannot convert {e.GetType().Name} to native object")
		};
}

// NativeObject value type to hold arbitrary .NET objects
public sealed record NativeObject(object Value) : Value;
