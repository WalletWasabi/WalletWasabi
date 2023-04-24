# Wasabi Coding Conventions

## Automatic code clean up

**Visual Studio IDE:**

**DO** use [CodeMaid](https://www.codemaid.net/), a Visual Studio extension to automatically clean up your code on saving the file.
CodeMaid is a non-intrusive code cleanup tool.

Wasabi's CodeMaid settings [can be found in the root of the repository](https://github.com/zkSNACKs/WalletWasabi/blob/master/CodeMaid.config). They are automatically picked up by Visual Studio when you open the project, assuming the CodeMaid extension is installed. Unfortunately CodeMaid has no Visual Studio Code extension yet. You can check out the progress on this [under this GitHub issue](https://github.com/codecadwallader/codemaid/issues/273).

**Rider IDE:**

In Rider, you can achieve similar functionality by going to `Settings -> Tools -> Action on Save` and enabling `Reformat and Cleanup Code` and changing `Profile` to `Reformat Code`.

And also enable `Enable EditorConfig support` in `Settings -> Editor -> Code Style`.

![image](https://user-images.githubusercontent.com/16364053/159900227-627f4b67-e793-421b-836a-09660971c807.png)
![image](https://user-images.githubusercontent.com/16364053/159900956-539868b7-9fd2-44ed-9ec6-d58569bd9dbb.png)

## .editorconfig

Not only CodeMaid, but Visual Studio also enforces consistent coding style through [`.editorconfig`](https://github.com/zkSNACKs/WalletWasabi/blob/master/.editorconfig) file.

If you are using Visual Studio Code make sure to add the following settings to your settings file:

```json
    "omnisharp.enableEditorConfigSupport": true,
    "omnisharp.enableRoslynAnalyzers": true,
    "editor.formatOnSave": true,
```

## Refactoring

If you are a new contributor **DO** keep refactoring pull requests short, uncomplex and easy to verify. It requires a certain level of experience to know where the code belongs to and to understand the full ramification (including rebase effort of open pull requests) - [source](https://github.com/bitcoin/bitcoin/blob/master/CONTRIBUTING.md#refactoring).

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

**DO NOT** mix awaitable and non-awaitable locks.

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

**DO** use `is null` instead of `== null`. It was a performance consideration in the past but from C# 7.0 it does not matter anymore, today we use this convention to keep our code consistent.

```cs
if (foo is null) return;
```

## Empty quotes

**DO** use `""` instead of `string.Empty` for consistency.

```cs
if (foo is null)
{
	return "";
}
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
private async void Synchronizer_ResponseArrivedAsync(object? sender, EventArgs e)
{
	try
	{
		await FooAsync();
	}
	catch (Exception ex)
	{
		Logger.LogError<MyClass2>(ex);
	}
}
```
- [Async/Await - Best Practices](https://docs.microsoft.com/en-us/archive/msdn-magazine/2013/march/async-await-best-practices-in-asynchronous-programming)

## `ConfigureAwait(false)`

Basically every async library method should use `ConfigureAwait(false)` except:
- Methods that touch objects on the UI Thread, like modifying UI controls. 
- Methods that are unit tests, xUnit [Fact].

**Usage:**
```cs
await MyMethodAsync().ConfigureAwait(false);
```

**Top level synchronization**
```cs
// Later we need to modify UI control so we need to sync back to this thread, thus don't use .ConfigureAwait(false);.
// Note: inside MyMethodAsync() you can still use .ConfigureAwait(false);.
var result = await MyMethodAsync();

// At this point we are still on the UI thread, so you can safely touch UI elements. 
myUiControl.Text = result;
```

- [ConfigureAwait FAQ](https://devblogs.microsoft.com/dotnet/configureawait-faq/)

## Never throw AggregateException and Exception in a mixed way
It causes confusion and awkward catch clauses.
[Example](https://github.com/zkSNACKs/WalletWasabi/pull/10353/files) 

---

# UI Coding Conventions

The following is a list of UI specific coding conventions. Follow these any time you are contributing code in the following projects:
 - `WalletWasabi.Fluent`
 - `WalletWasabi.Fluent.Desktop`
 - `WalletWasabi.Fluent.Generators`

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

## Subscribe triggered once on initialization

When you subscribe with the usage of `.WhenAnyValue()` right after the creation one call of Subcription will be triggered. This is by design and most of the cases it is fine. Still you can supress this behaviour by adding `Skip(1)`. 

```cs
this.WhenAnyValue(x => x.PreferPsbtWorkflow)
	.Skip(1)
	.Subscribe(value =>
	{
		// Expensive operation, that should not run unnecessary. 
	});
```

- [Example](https://stackoverflow.com/questions/36705139/why-does-whenanyvalue-observable-trigger-on-subscription)

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
		.ToProperty(this, x => x.CanDoIt, scheduler: RxApp.MainThreadScheduler);
  }
  
  public bool CanDoIt => _canDoIt?.Value ?? false;
}
```

**DO** always subscribe to these `ObservableAsPropertyHelper`s after their initialization is done.

## No code in Code-behind files (.xaml.cs)

All the logic should go into `ViewModels` or `Behaviors`.

## Main MUST be Synchronous

For Avalonia applications the Main method must be synchronous. No async-await here! If you await inside Main before Avalonia has initialised its renderloop / UIThread, then OSX will stop working. Why? Because OSX applications (and some of Unixes) assume that the very first thread created in a program is the UIThread. Cocoa apis check this and crash if they are not called from Thread 0. Awaiting means that Avalonia may not be able to capture Thread 0 for the UIThread.

## Avoid Binding expressions with SubProperties
If you have a `Binding` expression i.e. `{Binding MyProperty.ChildProperty}` then most likely the UI design is flawed and you have broken the MVVM pattern.

This kind of Binding demonstrates that your View is dependent not on just 1 ViewModel, but multiple Viewmodels and a very specific relationship between them.

If you find yourself having to do this, please re-think the UI design. To follow the MVVM pattern correctly to ensure the UI remains maintainable and testable then we should have a 1-1 view, viewmodel relationship. That means every view should only depend on a single viewmodel.

## Familiarise yourself with MVVM Pattern
It is very important for us to follow the MVVM pattern in UI code. Whenever difficulties arise in refactoring the UI or adding new UI features its usually where we have ventured from this path.

Some pointers on how to recognise if we are breaking MVVM:

* Putting code in .xaml.cs (code-behind files)
* Putting business logic inside control code
* Views that depend on more than 1 viewmodel class.

If it seems not possible to implement something without breaking some of this advice please consult with @danwalmsley.

## Avoid using Grid as much as possible, Use Panel instead 
If you don't need any row or column splitting for your child controls, just use `Panel` as your default container control instead of `Grid` since it is a moderately memory and CPU intensive control.

## ViewModel Hierarchy

The ViewModel structure should reflect the UI structure as much as possible. This means that ViewModels can have *child* ViewModels directly referenced in their code, just like Views have direct reference to *child* views.

❌ **DO NOT** write ViewModel code that depends on *parent* or *sibling* ViewModels in the logical UI structure. This harms both testability and maintainability.

Examples: 

 - ✔️ `MainViewModel` represents the Main Wasabi UI and references `NavBarViewModel`.
 - ✔️ `NavBarViewModel` represents the left-side navigation bar and references `WalletListViewModel`.
 - ❌ `NavBarViewModel` code must NOT reference `MainViewModel` (its logical parent).
 - ❌ `WalletListViewModel` code must NOT reference `NavBarViewModel` (its logical parent).
 - ❌ `WalletListViewModel` code must NOT reference other ViewModels that are logical children of `NavBarViewModel` (its logical siblings).

## UI Models

The UI Model classes (which comprise the *Model* part of the MVVM pattern) sit as an abstraction layer between the UI and the larger Wasabi Object Model (which lives in the `WalletWasabi` project). This layer is responsible for:

 - Exposing Wasabi data and functionality in a UI-friendly manner. Usually in the form of Observables.

 - Avoiding tight coupling between UI code and business logic. This is critical for testability of UI code, mainly ViewModels.

❌ **DO NOT** write ViewModel code that depends directly on `WalletWasabi` objects such as `Wallet`, `KeyManager`, `HdPubKey`, etc.

✔️ **DO** write ViewModel code that depends on `IWalletModel`, `IWalletListModel`, `IAddress`, etc.

❌ **DO NOT** convert regular .NET properties from `WalletWasabi` objects into observables or INPC properties in ViewModel code. 

❌ **DO NOT** convert regular .NET events from `WalletWasabi` objects into observables in ViewModel code.

✔️ If such conversions are required, **DO** write them into the UI Model layer.

## UiContext

ViewModels that depend on external components (such as Navigation, Clipboard, QR Reader, etc) can access these via the `ViewModelBase.UIContext` property. For instance:

 - Get text from clipboard: `var text = await UIContext.Clipboard.GetTextAsync();`

 - Generate QR Code: `await UIContext.QrGenerator.Generate(data);`

 - Open a popup or navigate to another Viewmodel: `UIContext.Navigate().To(....)`

This is done to facilitate unit testing of viewmodels, since all dependencies that live inside the `UiContext` are designed to be mock-friendly.

❌ **DO NOT** write Viewmodel code that directly depends on external device-specific components or code that might otherwise not work in the context of a unit test.

## Source-Generated ViewModel Constructors

Whenever a ViewModel references its `UiContext` property, the `UiContext` object becomes an actual **dependency** of said ViewModel. It must therefore be initialized, ideally as a constructor parameter.

In order to minimize the amount of boilerplate required for such initialization, several things occur in this case:
 - A new constructor is generated for that ViewModel, including all parameters of any existing constructor plus the UiContext. 
 - This generated constructor initializes the `UiContext` *after* running the code of the manually written constructor (if any).
 - A Roslyn Analyzer inspects any manually written constructors in the ViewModel to prevent references to `UiContext` in the constructor body, before the above mentioned initialization can take place, resulting in `NullReferenceException`s.
 - The Analyzer demands the manually written constructor to be declared `private`, so that external instatiation of the ViewModel is done by calling the source-generated constructor.

❌ Writing code that directly references `UiContext` in a ViewModel's constructor body will result in a compile-time error.

❌ Writing code that indirectly references `UiContext` in ViewModel's constructor body will result in a run-time `NullReferenceException`.

✔️ Writing code that directly or indirectly references `UiContext` inside a lambda expression in a ViewModel's constructor body is okay, since this code is deferred to a later time at run-time when the `UiContext` property has already been properly initialized.

Example:

```csharp
    // ❌ BAD, constructor should be private
    public AddressViewModel(IAddress address)
	{
		if (condition)
		{
			//❌ BAD, UiContext is null at this point.
			UiContext.Navigate().To(someOtherViewModel); 
		}
	}

    // ✔️ GOOD, constructor is private
    private AddressViewModel(IAddress address)
	{
		//✔️ GOOD, UiContext is already initialized when the Command runs
		NextCommand = ReactiveCommand.Create(() => UiContext.Navigate().To(someOtherViewModel))); 
	}
```

If you absolutely must reference `UiContext` in the constructor, you can create a public constructor explicitly taking `UiContext` as a parameter:

```csharp
    // ✔️ GOOD, 
    public AddressViewModel(UiContext uiContext, IAddress address)
	{
		UiContext = uiContext;
		
		// ✔️Other code here can safely use the UiContext since it's explicitly initialized above.
	}
```

In this case, no additional constructors will be generated, and the analyzer will be satisfied.




