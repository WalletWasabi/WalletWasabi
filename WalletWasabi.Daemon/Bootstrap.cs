using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using WalletWasabi.Helpers;
using WalletWasabi.Scheme;
using static WalletWasabi.Scheme.Interpreter;

namespace WalletWasabi.Daemon;
using Environment = System.Collections.Immutable.ImmutableDictionary<string, Expression>;

public class Scheme
{
	private Environment _env;

	public Scheme(Global global)
	{
		var (env, _) = Load(Env, "Scheme/Wasabilib.scm");
		env = DefineNativeFunction("now", () => DateTime.Now, env);
		env = DefineNativeFunction<string, object>("__get", GetterFn, env);
		env = DefineNativeFunction("global", () => global, env);
		env = DefineNativeFunction("wallets",
			() => global.WalletManager.GetWallets(), env);
		_env = env;
	}

	private Dictionary<(Type, string), MemberInfo> Accessors = new();
	private object GetterFn(string method, object instance)
	{
		var typ = instance.GetType();
		var key = (typ, method);
		if (!Accessors.TryGetValue(key, out var info))
		{
			var members = typ.GetMember(method,
				BindingFlags.GetProperty
				| BindingFlags.InvokeMethod
				| BindingFlags.Instance
				| BindingFlags.Public
				| BindingFlags.IgnoreCase);
			info = members[0];
			Accessors.Add(key, info);
		}

		var result = info switch
		{
			MethodInfo mi => mi.Invoke(instance, []),
			PropertyInfo pi => pi.GetValue(instance),
			_ => throw new ArgumentOutOfRangeException()
		};
		return result!;
	}

	public Expression Execute(string prg)
	{
		var parsingResult = Parse(Tokenizer.Tokenize(prg).ToArray());
		var (penv, expressionResult) = Eval(_env, parsingResult[0]);
		_env = penv;
		return expressionResult;
	}
}
