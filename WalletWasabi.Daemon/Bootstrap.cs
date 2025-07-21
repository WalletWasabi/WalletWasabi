using System;
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
		env = DefineNativeFunction<string, object>("__get", (method, instance) =>
			instance.GetType()
				.InvokeMember(method,
					BindingFlags.GetProperty | BindingFlags.InvokeMethod | BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase, null, instance, [])!, env);
		env = DefineNativeFunction("global", () => global, env);
		env = DefineNativeFunction("wallets",
			() => global.WalletManager.GetWallets(), env);
		_env = env;
	}

	public Expression Execute(string prg)
	{
		var parsingResult = Parse(Tokenizer.Tokenize(prg).ToArray());
		var (penv, expressionResult) = Eval(_env, parsingResult[0]);
		_env = penv;
		return expressionResult;
	}
}
