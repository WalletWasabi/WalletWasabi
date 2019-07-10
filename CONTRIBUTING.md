# Wasabi Coding Conventions

## CodeMaid

**DO** use [CodeMaid](http://www.codemaid.net/) is a Visual Studio extension to automatically clean up your code on saving the file.

CodeMaid is a non-intrusive code cleanup tool. Wasabi's CodeMaid settings [can be found in the root of the repository](https://github.com/zkSNACKs/WalletWasabi/blob/master/CodeMaid.config), and are automatically picked up by Visual Studio when you open the project. Assuming the CodeMaid extension is installed. Unfortunately CodeMaid has no Visual Studio Code extension yet. You can check out the progress on this [under this GitHub issue](https://github.com/codecadwallader/codemaid/issues/273).

## Comments

**DO** follow [Microsoft's C# commenting conventions](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/inside-a-program/coding-conventions#commenting-conventions).

- Place the comment on a separate line, not at the end of a line of code.
- Begin comment text with an uppercase letter.
- End comment text with a period.
- Insert one space between the comment delimiter (`//`) and the comment text, as shown in the following example.
- Do not create formatted blocks of asterisks around comments.

```cs
// The following declaration creates a query. It does not run
// the query.
```

## Asynchronous Locking

**DO NOT** use mix awaitable and non-awaitable locks.

```cs
// GOOD
private AsyncLock AsyncLock { get; } = new AsyncLock();
using (await AsyncLock.LockAsync())
{
	...
}

// GOOD
private object Lock { get; } = new object();
lock (Lock)
{
	...
}

// BAD
using (AsyncLock.Lock())
{
	...
}
```

## Null Check

**DO** use `is null` instead of `== null`. It was a performance consideration in the past but from C# 7.0 it does not matter anymore, today we use this convention to keep our code consisent.

```cs
	if (foo is null) return;
```

## Blocking

**DO NOT** block with `.Result, .Wait(), .GetAwaiter().GetResult()`. Never.

```cs
// BAD
IoHelpers.DeleteRecursivelyWithMagicDustAsync(Folder).GetAwaiter().GetResult();
```

## `async void`

**DO NOT** `async void`, except for event subscriptions. `async Task` instead.
**DO** `try catch` in `async void`, otherwise the software can crash.

```cs
{
	MyClass.SomethingHappened += MyClass_OnSomethingHappenedAsync;
}

// GOOD
private async void Synchronizer_ResponseArrivedAsync(object sender, EventArgs e)
{
	try
	{
		awat FooAsync();
	}
	catch (Exception ex)
	{
		Logger.LogError<MyClass2>(ex);
	}
}
```

## Disposing Subscriptions in ReactiveObjects

**DO** follow [ReactiveUI's Subscription Disposing Conventions](https://reactiveui.net/docs/guidelines/framework/dispose-your-subscriptions).

**DO** dispose your subscription if you are referencing another object. **DO** use the `.DisposeWith()` method.

```cs
Observable.FromEventPattern(...)
	.ObserveOn(RxApp.MainThreadScheduler)
	.Subscribe(...)
	.DisposeWith(Disposables);
```

**DO NOT** dispose your subscription if a component exposes an event and also subscribes to it itself. That's because the subscription is manifested as the component having a reference to itself. Same is true with Rx. If you're a VM and you e.g. WhenAnyValue against your own property, there's no need to clean that up because that is manifested as the VM having a reference to itself.

```cs
this.WhenAnyValue(...)
	.ObserveOn(RxApp.MainThreadScheduler)
	.Subscribe(...);
```

## ObservableAsPropertyHelpers Over Properties

**DO** follow [ReactiveUI's Oaph Over Properties Principle](https://reactiveui.net/docs/guidelines/framework/prefer-oaph-over-properties).

**DO** use  `ObservableAsPropertyHelper` with `WhenAny` when a property's value depends on another property, a set of properties, or an observable stream, rather than set the value explicitly.

```cs
public class RepositoryViewModel : ReactiveObject
{
  private ObservableAsPropertyHelper<bool> _canDoIt;
  
  public RepositoryViewModel()
  {
    _canDoIt = this.WhenAnyValue(...)
		.ToProperty(this, x => x.CanDoIt, scheduler: RxApp.MainThreadScheduler));
  }
  
  public bool CanDoIt => _canDoIt?.Value ?? false;
}
```
