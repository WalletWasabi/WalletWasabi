using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using WalletWasabi.Helpers;
using WalletWasabi.Scheme;
using static WalletWasabi.Scheme.Tokenizer;
using Environment = System.Collections.Immutable.ImmutableDictionary<string, WalletWasabi.Scheme.Expression>;

[assembly: DebuggerDisplay("{Interpreter.Print(this),ng}", Target = typeof(Expression))]

namespace WalletWasabi.Scheme;

public abstract record Expression;

public record EvalContext(Environment Env, Expression Expr);

public static class Interpreter
{
	private static Expression Lookup(string symbol, Environment env) =>
		env.TryGetValue(symbol, out var value)
			? value
			: throw new InvalidOperationException($"No binding for '{symbol}'.");

	private static Environment ExtendEnvironment(IEnumerable<(string s, Expression e)> bindings, Environment env) =>
		env.SetItems(bindings.Select(x => KeyValuePair.Create(x.s, x.e)));

	private delegate Expression ExpressionsProcessor(Expression args);

	private delegate EvalContext SpecialExpressionsProcessor(Environment env, Expression args);

	private record Number(decimal Value) : Expression;

	private record String(string Value) : Expression;

	private record Character(string Value) : Expression;

	private record Boolean(bool Bool) : Expression;

	private record Symbol(string Value) : Expression;

	private abstract record List : Expression;

	private record Pair(Expression Car, Expression Cdr) : List;

	private record VarArgs(Expression Car, Expression Cdr) : Pair(Car, Cdr);

	private record Nil : List;

	private record Function(ExpressionsProcessor Fn) : Expression;

	private record Procedure(SpecialExpressionsProcessor Fn) : Expression;

	private record NativeObject(object Value) : Expression;

	private record DummyExpression(string Val) : Expression;

	private static readonly Symbol QuoteExpr = new("quote");
	private static readonly Symbol QuasiQuoteExpr = new("quasiquote");
	private static readonly Symbol UnquoteExpr = new("unquote");
	private static readonly Symbol UnquoteSplicingExpr = new("unquote-splicing");
	private static readonly Boolean True = new(true);
	private static readonly Boolean False = new(false);
	private static readonly Nil NilExpr = new();

	private static Expression MapTokenToExpression(Token token) =>
		token switch
		{
			NumberToken n => new Number(decimal.Parse(n.number)),
			StringToken s => new String(s.str),
			CharacterToken c => new Character(c.c),
			BooleanToken s => s.b ? True : False,
			SymbolToken t => new Symbol(t.symbol),
			_ => throw new SyntaxException($"token '{token}' is not mappable to an expression.")
		};

	public static ImmutableArray<Expression> Parse(Token[] tokens)
	{
		var expressions = new List<Expression>();
		while (tokens.Length > 0)
		{
			var x = ParseExpression(tokens);
			expressions.Add(x.ParsedExpression);
			tokens = x.UnparsedTokens;
		}

		return [..expressions];

		static (Expression, Token[] rest) ParseList(Token[] tokens)
		{
			(Expression Pair, Token[] UnparsedTokens) ParseListElements(Expression acc, Token[] tokens) =>
				tokens switch
				{
					[CloseToken, .. var t] => (acc, t),
					[DotToken, .. var t] => ParseExpression(t)
						.Then(r => r.UnparsedTokens is [CloseToken, .. var tokensToParse]
							? (r.ParsedExpression, tokensToParse)
							: throw SyntaxError("Incomplete form", NilExpr)),

					_ => ParseExpression(tokens)
						.Then(r => (Car: r.ParsedExpression, Cdr: ParseListElements(acc, r.UnparsedTokens)))
						.Then(p => (Cons(p.Car, p.Cdr.Pair), p.Cdr.UnparsedTokens))
				};

			return ParseListElements(NilExpr, tokens);
		}

		static (Expression ParsedExpression, Token[] UnparsedTokens) ParseExpression(Token[] tokens) =>
			tokens switch
			{
				[OpenToken, .. var t] => ParseList(t),
				[QuoteToken, .. var t] => ParseExpression(t).Then(r =>
					(Cons(QuoteExpr, r.ParsedExpression), rest: r.UnparsedTokens)),
				[QuasiQuoteToken, .. var t] => ParseExpression(t).Then(r =>
					(Cons(QuasiQuoteExpr, r.ParsedExpression), rest: r.UnparsedTokens)),
				[UnquoteToken, .. var t] => ParseExpression(t).Then(r =>
					(Cons(UnquoteExpr, r.ParsedExpression), rest: r.UnparsedTokens)),
				[UnquoteSplicingToken, .. var t] => ParseExpression(t).Then(r =>
					(Cons(UnquoteSplicingExpr, r.ParsedExpression), rest: r.UnparsedTokens)),
				[var h, .. var t] => (MapTokenToExpression(h), t),
			};
	}

	public static EvalContext Eval(Environment env, Expression expr) =>
		expr switch
		{
			Symbol(var sym) => new EvalContext(env, Lookup(sym, env)),
			Pair(var h, var t) => Eval(env, h).Then(ctx => ctx.Expr switch
			{
				Function(var f) => ApplyArgs(ctx.Env, f, t),
				Procedure(var f) => f(ctx.Env, t),
				_ => throw SyntaxError("The first element in a list must be a function", ctx.Expr)
			}),
			_ => new EvalContext(env, expr)
		};

	private static EvalContext Evaluate(Environment env, Expression exprs) =>
		Eval(env, Car(exprs)).Then(ctx =>
			Eval(ctx.Env, ctx.Expr));

	private static EvalContext ApplyArgs(Environment env, ExpressionsProcessor function, Expression args) =>
		Map(e => Eval(env, e).Expr, args)
			.Then(ars => new EvalContext(env, function(
				ars is Pair(var car, Cdr: Nil) ? car : ars)));

	private static Pair Cons(Expression Car, Expression Cdr) => new(Car, Cdr);

	private static Expression Car(Expression es) =>
		es is Pair (var car, _) ? car : throw SyntaxError("'car'", es);

	private static Expression Cdr(Expression es) =>
		es is Pair (_, var cdr) ? cdr : throw SyntaxError("'cdr'", es);

	private static Expression Cons(Expression es) =>
		es is Pair (var car, Pair(var cdr, Nil))
			? new Pair(car, cdr)
			: throw SyntaxError("'cons'", es);

	private static EvalContext If(Environment env, Expression exprs) =>
		exprs is Pair (var condition, Pair (var t, var f))
			? Eval(env, condition).Then(ctx => ctx.Expr switch
			{
				Boolean(false) => Eval(ctx.Env, Car(f)),
				_ => Eval(ctx.Env, t) // everything else is true
			})
			: throw SyntaxError("'if' must have a condition and true expressions", exprs);

	private static EvalContext LetRec(Environment env, Expression exprs)
	{
		if (exprs is not Pair (var bindings, var body))
		{
			throw SyntaxError("'let' must have bindings and a body expression.", exprs);
		}

		return Eval(Fold(env, Bind, bindings), WrapBegin(body)) with {Env = env};

		Environment Bind(Environment ppenv, Expression binding) =>
			binding is Pair (Symbol(var sym), Pair(var e, _))
				? FunctionExtensions.Then(Eval(ppenv, e), r => ExtendEnvironment([(sym, r.Expr)], r.Env))
				: throw SyntaxError("'let' binding.", binding);
	}

	private static EvalContext Lambda(Environment env, Expression expr)
	{
		if (expr is not Pair (var parameters, var body))
		{
			throw SyntaxError("'lambda'", expr);
		}

		return new EvalContext(env, new Procedure(Closure));

		EvalContext Closure(Environment callerEnv, Expression args)
		{
			var penv = env.SetItems(callerEnv);
			var bindings = Zip(parameters, args);
			return Eval(Fold(penv, Bind, bindings), WrapBegin(body)) with {Env = callerEnv};

			Environment Bind(Environment penv, Expression binding) =>
				binding switch
				{
					VarArgs (Symbol(var sym), List lst) =>
						FunctionExtensions.Then(Map(e => Eval(callerEnv, e).Expr, lst),
							r => ExtendEnvironment([(sym, r)], penv)),
					Pair (Symbol(var sym), var e) =>
						ExtendEnvironment([(sym, Eval(callerEnv, e).Expr)], penv),
					_ => throw SyntaxError("'lambda' binding.", binding)
				};
		}
	}

	private static Expression IsFalse(Expression expr) => expr is Boolean(false) ? True : False;

	private static EvalContext Test(Environment env, Expression exprs, Boolean expected) =>
		Find(e => Eval(env, e).Then(ctx => IsFalse(ctx.Expr) == expected), exprs)
			.Then(r => r is Nil ? expected : r)
			.Then(r => Eval(env, r));

	private static EvalContext And(Environment env, Expression exprs) =>
		Test(env, exprs, True);

	private static EvalContext Or(Environment env, Expression exprs) =>
		Test(env, exprs, False);

	private static Expression WrapBegin(Expression exprs) =>
		exprs switch
		{
			Pair (var car, Nil) => car,
			Pair (Pair, _) p => Cons(new Symbol("begin"), p),
			_ => exprs
		};

	private static EvalContext QuasiQuote(Environment env, Expression exprs)
	{
		return new EvalContext(env, Unquote(exprs));

		Expression Unquote(Expression expr) =>
			expr switch
			{
				Pair (Symbol("unquote"), var e) => Eval(env, e).Expr,
				Pair (Symbol("unquote-splicing"), var e) => Eval(env, e).Expr,
				Pair (Pair (Symbol("unquote-splicing"), _) car, var cdr) => Append(Unquote(car), Unquote(cdr)),
				Pair (var car, var cdr) => Cons(Unquote(car), Unquote(cdr)),
				_ => expr
			};

		Expression Append(Expression p1, Expression p2) =>
			p1 switch
			{
				Nil => p2,
				Pair p => Cons(p.Car, Append(p.Cdr, p2))
			};
	}

	private static EvalContext Quote(Environment env, Expression exprs) =>
		new(env, exprs);

	private static EvalContext Begin(Environment env, Expression exprs) =>
		Fold(new EvalContext(env, NilExpr), (ctx, e) => Eval(ctx.Env, e), exprs); // with{ Environment = env};

	private static EvalContext Define(Environment env, Expression exprs) =>
		exprs switch
		{
			Pair (Symbol(var sym), Pair(var e, _)) =>
				FunctionExtensions.Then(Eval(env, e), ctx => ExtendEnvironment([(sym, ctx.Expr)], ctx.Env))
					.Then(penv => new EvalContext(penv, new DummyExpression($"Define {sym}"))),
			Pair (Pair(Symbol(var sym), var ps), var body) =>
				FunctionExtensions.Then(Lambda(env, Cons(ps, body)),
						ctx => ExtendEnvironment([(sym, ctx.Expr)], ctx.Env))
					.Then(penv => new EvalContext(penv, new DummyExpression($"Define {sym}"))),
			_ => throw SyntaxError("'Define'", exprs)
		};

	private static EvalContext Apply(Environment env, Expression exprs)
	{
		if (exprs is not Pair (Symbol(var procName), var args))
		{
			throw SyntaxError("'apply'", exprs);
		}

		var proc = Lookup(procName, env);
		var evaluatedArgs = Map(e => Eval(env, e).Expr, args)
			.Then(ars => ars is Pair (var car, Nil) ? car : ars);

		return proc switch
		{
			Procedure p => p.Fn(env, evaluatedArgs),
			Function f => f.Fn(evaluatedArgs).Then(r => new EvalContext(env, r)),
			_ => throw SyntaxError("", exprs)
		};
	}

	private static EvalContext DefineMacro(Environment env, Expression exprs)
	{
		if (exprs is not Pair (Pair(Symbol(var sym), var parameters), Pair(var body, _)))
		{
			throw SyntaxError("'define-macro'", exprs);
		}

		var penv = ExtendEnvironment([(sym, new Procedure(Closure))], env);
		return new EvalContext(penv, new DummyExpression($"Define Macro {sym}"));

		EvalContext Closure(Environment callerEnv, Expression args)
		{
			var binding = Zip(parameters, args);
			return Eval(Fold(callerEnv, Bind, binding), body)
				.Then(ctx => Eval(callerEnv, ctx.Expr) with {Env = callerEnv});

			Environment Bind(Environment penv, Expression pexpr) =>
				pexpr is Pair (Symbol(var s), var e)
					? ExtendEnvironment([(s, e)], penv)
					: throw SyntaxError("'macro' parameter.", pexpr);
		}
	}

	private static ExpressionsProcessor Math(Func<decimal, decimal, decimal> op) =>
		es => es is Pair(Number(var n1), Pair(Number(var n2), _))
			? new Number(op(n1, n2))
			: throw SyntaxError("Math can only involve number", es);

	private static ExpressionsProcessor Compare(Func<decimal, decimal, bool> op) =>
		es => es is Pair(Number(var a), Pair(Number(var b), _))
			? op(a, b) ? True : False
			: throw SyntaxError("Binary comparison requires two expressions", es);

	private static ExpressionsProcessor NumericEquality =>
		es => es is Pair p
			? SameType<Number>(p, (a, b) => a.Value == b.Value) ? True : False
			: throw SyntaxError("Binary comparison requires two expressions", es);

	private static bool SameType<T>(Pair p, Func<T, T, bool> fn) =>
		p is (T a, Pair(T b, _)) && fn(a, b);

	private static ExpressionsProcessor IdentityEquality =>
		es => es switch
		{
			Pair p when SameType<Number>(p, (a, b) => a.Value == b.Value) => True,
			Pair p when SameType<String>(p, (a, b) => a.Value == b.Value) => True,
			Pair p when SameType<Character>(p, (a, b) => a.Value == b.Value) => True,
			Pair p when SameType<Boolean>(p, (a, b) => a.Bool == b.Bool) => True,
			Pair p when SameType<Symbol>(p, (a, b) => a.Value == b.Value) => True,
			Pair p when SameType<Pair>(p, ReferenceEquals) => True,
			Pair p when SameType<Function>(p, (a, b) => ReferenceEquals(a.Fn, b.Fn)) => True,
			Pair p when SameType<Procedure>(p, (a, b) => ReferenceEquals(a.Fn, b.Fn)) => True,
			Pair p when SameType<Nil>(p, (_, _) => true) => True,
			Pair {Cdr: Pair} => False,
			_ => throw SyntaxError("Binary comparison requires two expressions", es)
		};

	private static ExpressionsProcessor CompareStructuralEquality =>
		es => es switch
		{
			Pair p when SameType<Number>(p, (a, b) => a.Value == b.Value) => True,
			Pair p when SameType<String>(p, (a, b) => a.Value == b.Value) => True,
			Pair p when SameType<Character>(p, (a, b) => a.Value == b.Value) => True,
			Pair p when SameType<Boolean>(p, (a, b) => a.Bool == b.Bool) => True,
			Pair p when SameType<Symbol>(p, (a, b) => a.Value == b.Value) => True,
			Pair p when SameType<Function>(p, (a, b) => a.Fn == b.Fn) => True,
			Pair p when SameType<Procedure>(p, (a, b) => a.Fn == b.Fn) => True,
			Pair p when SameType<Pair>(p, (a, b) => a == b) => True,
			Pair p when SameType<Nil>(p, (_, _) => true) => True,
			_ => False
		};

	private static readonly ExpressionsProcessor Add = Math((a, b) => a + b);
	private static readonly ExpressionsProcessor Subtract = Math((a, b) => a - b);
	private static readonly ExpressionsProcessor Multiply = Math((a, b) => a * b);
	private static readonly ExpressionsProcessor Divide = Math((a, b) => a / b);
	private static readonly ExpressionsProcessor Modulus = Math((a, b) => a % b);

	private static readonly ExpressionsProcessor Greater = Compare((a, b) => a > b);
	private static readonly ExpressionsProcessor Less = Compare((a, b) => a < b);

	private static int _genSymCounter;

	private static Expression GenSym(Expression exprs) =>
		exprs switch
		{
			Symbol(var prefix) => new Symbol($"#:{prefix}{_genSymCounter++}"),
			String(var prefix) => new Symbol($"#:{prefix}{_genSymCounter++}"),
			Nil => new Symbol($"#:g{_genSymCounter++}"),
			_ => throw SyntaxError("gensym", exprs)
		};

	private static ExpressionsProcessor Convert<TSource>(
		Func<TSource, Expression> transformer,
		string operationName)
		where TSource : Expression =>
		expr =>
			expr is TSource source
				? transformer(source)
				: throw SyntaxError(operationName, expr);

	private static ExpressionsProcessor StringToSymbol =
		Convert<String>(str => new Symbol(str.Value), "string->symbol");

	private static ExpressionsProcessor SymbolToString =
		Convert<Symbol>(sym => new String(sym.Value), "symbol->string");

	private static ExpressionsProcessor StringToNumber =
		Convert<String>(str => decimal.TryParse(str.Value, CultureInfo.InvariantCulture, out var n)
			? new Number(n)
			: False, "string->number");

	private static ExpressionsProcessor StringToList =
		Convert<String>(
			str => str.Value.Reverse()
				.Aggregate((Expression) NilExpr, (acc, c) => Cons(new Character(c.ToString()), acc)), "string->list");

	private static ExpressionsProcessor ListToString =
		Convert<List>(lst => new String(lst switch
		{
			Nil => "",
			Pair p => string.Join("", FlatPairChain(p).OfType<Character>().Select(x => x.Value))
		}), "list->string");

	private static ExpressionsProcessor NumberToString =
		Convert<Number>(num => new String(num.Value.ToString(CultureInfo.InvariantCulture)), "number->string");

	private static Expression NullQm(Expression es) =>
		es is Nil or Pair(Nil, Nil) ? True : False;

	private static Expression Display(Expression e)
	{
		Console.Write(Print(e));
		return new DummyExpression("Dummy 'display'");
	}

	private static EvalContext Load(Environment env, Expression exprs)
	{
		if (exprs is not Pair(String(var filename), _))
		{
			throw SyntaxError("'load'", exprs);
		}

		filename = Path.IsPathFullyQualified(filename)
			? filename
			: Path.Combine(EnvironmentHelpers.GetFullBaseDirectory(), filename);
		var tokens = Tokenize(File.OpenText(filename).ReadToEnd());
		var parsingResults = Parse(tokens.ToArray());
		foreach (var result in parsingResults)
		{
			(env, _) = Eval(env, result);
		}

		return new EvalContext(env, new Symbol($"Loaded '{filename}'"));
	}

	public static EvalContext Load(this Environment env, string filename) =>
		Load(env, Cons(new String(filename), NilExpr));

	private static Boolean Is<T>(Expression xs) => xs is T ? True : False;

	public static readonly Environment Env = new Dictionary<string, Expression>
	{
		{"$math_op_*", new Function(Multiply)},
		{"$math_op_/", new Function(Divide)},
		{"$math_op_+", new Function(Add)},
		{"$math_op_-", new Function(Subtract)},
		{"$compare_=", new Function(NumericEquality)},
		{"$compare_>", new Function(Greater)},
		{"$compare_<", new Function(Less)},
		{"%", new Function(Modulus)},
		{"eq?", new Function(IdentityEquality)},
		{"equal?", new Function(CompareStructuralEquality)},
		{"null?", new Function(NullQm)},
		{"if", new Procedure(If)},
		{"letrec", new Procedure(LetRec)},
		{"lambda", new Procedure(Lambda)},
		{"cons", new Function(Cons)},
		{"car", new Function(Car)},
		{"cdr", new Function(Cdr)},
		{"quote", new Procedure(Quote)},
		{"quasiquote", new Procedure(QuasiQuote)},
		{"eval", new Procedure(Evaluate)},
		{"define-macro", new Procedure(DefineMacro)},
		{"begin", new Procedure(Begin)},
		{"define", new Procedure(Define)},
		{"apply", new Procedure(Apply)},
		{"load", new Procedure(Load)},
		{"display", new Function(Display)},
		{"number?", new Function(Is<Number>)},
		{"string?", new Function(Is<String>)},
		{"symbol?", new Function(Is<Symbol>)},
		{"pair?", new Function(Is<Pair>)},
		{"procedure?", new Function(e => (Is<Function>(e).Bool || Is<Procedure>(e).Bool) ? True : False)},
		{"and", new Procedure(And)},
		{"or", new Procedure(Or)},
		{"gensym", new Function(GenSym)},
		{"string->symbol", new Function(StringToSymbol)},
		{"symbol->string", new Function(SymbolToString)},
		{"number->string", new Function(NumberToString)},
		{"string->number", new Function(StringToNumber)},
		{"string->list", new Function(StringToList)},
		{"list->string", new Function(ListToString)},
	}.ToImmutableDictionary();

	public static Environment NativeFunction(this Environment env, string fname, Func<object> fn) =>
		InternalDefineNativeFunction(fname, os => fn(), env);

	public static Environment NativeFunction<T>(this Environment env, string fname, Func<T, object> fn) =>
		InternalDefineNativeFunction(fname, os => fn((T) os[0]), env);

	public static Environment NativeFunction<T0, T1>(this Environment env, string fname, Func<T0, T1, object> fn) =>
		InternalDefineNativeFunction(fname, os => fn((T0) os[0], (T1) os[1]), env);

	private static Environment InternalDefineNativeFunction(string fname, Func<object[], object> fn, Environment env)
	{
		Expression ConvertNativeToScheme(object obj) =>
			obj switch
			{
				int or short or decimal or byte or long or float or double or uint or ulong or ushort => new Number(System.Convert.ToDecimal(obj)),
				string stringValue => new String(stringValue),
				char characterValue => new Character(characterValue.ToString()),
				bool booleanValue => booleanValue ? True : False,
				IEnumerable<object> e => e.Select(ConvertNativeToScheme).Reverse()
					.Aggregate((Expression) NilExpr, (acc, expr) => Cons(expr, acc)),
				var o => new NativeObject(o)
			};

		object ConvertSchemeToNative(Expression e) => Interpreter.ToNativeObject(e);

		Expression WrapNativeFunction(Expression exprs)
		{
			var parameters = ConvertSchemeToNative(exprs);
			var result = fn.Invoke(parameters is IEnumerable<object> enumerable ? enumerable.ToArray() : [parameters]);
			var nativeResult = ConvertNativeToScheme(result);
			return nativeResult;
		}

		var fnative = new Function(WrapNativeFunction);
		return env.Add(fname, fnative);
	}

	private static ImmutableArray<Expression> FlatPairChain(List p) =>
		Fold(ImmutableArray<Expression>.Empty, (acc, e) => acc.Add(e), p);

	private static List Zip(Expression ps, Expression args)
	{
		return ps switch
		{
			Nil => NilExpr,
			Symbol sym => Cons(new VarArgs(sym, args), NilExpr),
			Pair => ZipDotted(NilExpr, ps, args)
		};

		List ZipDotted(List acc, Expression pps, Expression pas) =>
			(pps, pas) switch
			{
				(Pair (Symbol p, _), Pair (var a, _)) => Cons(Cons(p, a), ZipDotted(acc, Cdr(pps), Cdr(pas))),
				(Symbol p, var tas) => Cons(new VarArgs(p, tas), acc),
				(Nil, Nil) => acc,
				_ => throw SyntaxError($"parameters were expected but were passed.", ps)
			};
	}

	private static Expression Map(ExpressionsProcessor fn, Expression s) =>
		s switch
		{
			Nil => NilExpr,
			Pair {Car: var h, Cdr: var t} => Cons(fn(h), Map(fn, t)),
			var o => fn(o)
		};

	private static Expression Find(Func<Expression, bool> predicate, Expression expr) =>
		expr switch
		{
			Nil => NilExpr,
			Pair {Car: var car, Cdr: Nil} => car,
			Pair {Car: var car, Cdr: var cdr} => predicate(car) ? car : Find(predicate, cdr),
			var e => predicate(e) ? e : NilExpr
		};

	private static T Fold<T>(T acc, Func<T, Expression, T> fn, Expression p) =>
		p switch
		{
			Nil => acc,
			Pair {Car: var car, Cdr: var cdr} => Fold(fn(acc, car), fn, cdr),
			var e => fn(acc, e)
		};

	public static string Print(Expression expr) =>
		expr switch
		{
			Nil => "'()",
			List lst => $"({PrintList(lst)})",
			String str => $"\"{str.Value}\"",
			Symbol sym => sym.Value,
			Number num => num.Value.ToString(),
			Boolean b => b.Bool ? "#t" : "#f",
			Character c => $"#\\{c.Value}",
			Function or Procedure => "procedure",
			NativeObject o => $"{o.Value}",
			DummyExpression => string.Empty,
			_ => throw new ArgumentOutOfRangeException(nameof(expr))
		};

	private static string PrintList(Expression lst) =>
		lst switch
		{
			Pair {Car: var car, Cdr: Nil} => $"{Print(car)}",
			Pair {Car: var car, Cdr: var cdr and not Pair} => $"{PrintList(car)} . {PrintList(cdr)}",
			Pair {Car: var car, Cdr: var cdr} => Print(car) + " " + PrintList(cdr),
			_ => Print(lst)
		};

	public static object ToNativeObject(Expression e) =>
		e switch
		{
			Number (var value) => value,
			String (var stringValue) => stringValue,
			Character (var characterValue) => characterValue,
			Boolean (var boolValue) => boolValue,
			NativeObject (var objValue) => objValue,
			Symbol (var symValue) => symValue,
			Pair pairValue => FlatPairChain(pairValue).Select(ToNativeObject),
			Nil _ => false,
			DummyExpression _ => "Done",
			_ => throw new Exception("Undefined")
		};

	private static SyntaxException SyntaxError(string msg, Expression e) =>
		new($"[Syntax error] {msg} {Print(e)}");

	public class SyntaxException(string msg) : Exception(msg);
}

[DebuggerStepThrough]
public static class FunctionExtensions
{
	public static T Then<R, T>(this R me, Func<R, T> then) => then(me);
}
