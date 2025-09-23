using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Scheme;
using WalletWasabi.Wallets;
using static WalletWasabi.Scheme.Interpreter;

namespace WalletWasabi.Daemon;
using Environment = System.Collections.Immutable.ImmutableDictionary<string, Expression>;

public class Scheme
{
	private Environment _env;
	private readonly Global _global;
	private bool _initialized = false;

	public Scheme(Global global)
	{
		_global = global;
		_env = Env
			.NativeFunction("now", () => DateTime.Now)
			.NativeFunction<string, object>("__get", GetterFn)
			.NativeFunction("global", () => _global)
			.NativeFunction<object>("native->string", o => o?.ToString() ?? "")
			.NativeFunction<Script>("script->address", s => s.GetDestinationAddress(_global.Network)!)
			.NativeFunction<ExtPubKey?>("extpubkey->string", e => e?.ToString(_global.Network) ?? "")
			.NativeFunction("wallets", () => _global.WalletManager.GetWallets())
			.NativeFunction<Wallet>("wallet-coins", w => w.Coins.AsAllCoinsView())
			.NativeFunction<Wallet>("__start_wallet", w =>
			{
				 _global.WalletManager.StartWalletAsync(w).GetAwaiter().GetResult();
				 return w;
			});
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

	public async Task<Expression> Execute(string prg)
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
}
