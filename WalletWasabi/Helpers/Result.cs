using System.Collections.Generic;
using System.Linq;

namespace WalletWasabi.Helpers;

public record Unit
{
	public static readonly Unit Instance = new();
}

public record Result<TValue,TError>
{
	private readonly TValue? _value;
	private readonly TError? _error;

	private readonly bool _isSuccess;

	protected Result(TValue value)
	{
		_isSuccess = true;
		_value = value;
		_error = default;
	}

	protected Result(TError error)
	{
		_isSuccess = false;
		_value = default;
		_error = error;
	}

	public static Result<TValue, TError> Ok(TValue value) => value;
	public static Result<TValue, TError> Fail(TError error) => error;
	public static implicit operator Result<TValue, TError>(TValue value) => new(value);
	public static implicit operator Result<TValue, TError> (TError error) => new(error);

	public delegate T SuccessAction<out T>(TValue s);
	public delegate T FailureAction<out T>(TError e);

	public T Match<T>(SuccessAction<T> success, FailureAction<T> failure) =>
		_isSuccess
			? success(_value!)
			: failure(_error!);

	public void MatchDo(Action<TValue> success, Action<TError> failure)
	{
		if (_isSuccess)
		{
			success(_value!);
		}
		else
		{
			failure(_error!);
		}
	}

	public Result<T, TError> Then<T>(Func<TValue, T> f) =>
		Match(v => Result<T, TError>.Ok(f(v)), e => e);

	public static Result<TValue[], TError[]> Sequence(IEnumerable<Result<TValue, TError>> results)
	{
		Result<TValue[], TError[]> initialState = Array.Empty<TValue>();

		return results.Aggregate(initialState, (acc, s) =>
			(acc._isSuccess, s._isSuccess) switch
			{
				(true, true) => acc._value!.Append(s._value!).ToArray(),
				(true, false) => new[] {s._error!},
				(false, true) => acc._error!,
				(false, false) => acc._error!.Append(s._error!).ToArray()
			});
	}
}

public record Result<TError> : Result<Unit, TError>
{
	private Result(Unit value) : base(value)
	{
	}

	private Result(TError error) : base(error)
	{
	}

	public static implicit operator Result<TError> (TError error) => new(error);
	public static Result<TError> Ok() => new(Unit.Instance);
	public static Result<TError> Fail(TError error) => new(error);

	public static Result<TError[]> Sequence(IEnumerable<Result<TError>> results) =>
		Result<Unit, TError>.Sequence(results)
			.Match(
				_ => Result<TError[]>.Ok(),
				es => Result<TError[]>.Fail(es));
}

public static class ResultExtensions
{
	public static Result<TValue[], TError[]>
		SequenceResults<TValue, TError>(this IEnumerable<Result<TValue, TError>> results) =>
		Result<TValue, TError>.Sequence(results);

	public static Result<TError[]> SequenceResults<TError>(this IEnumerable<Result<TError>> results) =>
		Result<TError>.Sequence(results);
}
