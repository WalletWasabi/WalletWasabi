using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Text;
using Android.Views;
using Android.Views.InputMethods;
using Android.Widget;
using Gma.QrCodeNet.Encoding;
using NBitcoin;
using System.Globalization;
using System.Security.Cryptography;
using Stopwatch = System.Diagnostics.Stopwatch;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Mobile;
using WalletWasabi.Wallets;
using Color = Android.Graphics.Color;
using Orientation = Android.Widget.Orientation;

namespace WalletWasabi.Android;

[Activity(Name = "io.wasabiwallet.android.MainActivity", Label = AppIdentity.ApplicationLabel, Theme = "@style/WasabiTheme", MainLauncher = true, Exported = true,
	LaunchMode = LaunchMode.SingleTask, ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
[IntentFilter([Intent.ActionView], Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable], DataScheme = "bitcoin")]
public sealed class MainActivity : Activity
{
	private static readonly Color Background = Color.Rgb(17, 21, 18);
	private static readonly Color Surface = Color.Rgb(28, 34, 29);
	private static readonly Color Accent = Color.Rgb(163, 230, 53);
	private static readonly Color Muted = Color.Rgb(151, 166, 155);
	private const string RecoveryRequirement = "You need BOTH the Recovery Words AND the Password to recover your wallet.";
	private LinearLayout _root = null!;
	private LinearLayout _body = null!;
	private TextView _status = null!;
	private TextView _syncDetails = null!;
	private ProgressBar _syncProgress = null!;
	private ProgressBar _workIndicator = null!;
	private readonly SynchronizationProgressTracker _synchronization = new();
	private Action? _back;
	private System.Threading.Timer? _refresh;
	private Action? _updateScreen;
	private string? _payment;
	private bool _foreground;
	private bool _busy;
	private bool _externalFlow;
	private bool _uiLocked = true;
	private bool _authenticating;
	private bool _deviceEnrollmentUnavailable;
	private readonly HashSet<string> _unavailableDeviceKeys = [];
	private readonly CancellationTokenSource _activityLifetime = new();
	private string _screen = "wallets";
	private long _lastInteraction = Stopwatch.GetTimestamp();
	private long _uiGeneration;
	private long? _workGeneration;
	private Action<string>? _scanned;
	private byte[]? _exportPayload;
	private string? _importName;
	private WalletCreationDraft? _creationDraft;
	private BackNavigationCallback? _backNavigationCallback;
	private HashSet<OutPoint>? _selectedCoins;
	private string _sendAddress = "";
	private string _sendAmount = "";
	private string _sendLabel = "";
	private string _sendFee = "";
	private bool _sendAll;

	private WalletSession? Session => WalletRuntime.Session;
	private WalletSession? _observedSession;
	private CredentialVault Vault => new(this, WalletRuntime.DataDir(this));
	private int Dp(float value) => (int)(value * Resources!.DisplayMetrics!.Density);

	protected override void OnCreate(Bundle? savedInstanceState)
	{
		base.OnCreate(savedInstanceState);
		if (OperatingSystem.IsAndroidVersionAtLeast(33))
		{
			_backNavigationCallback = new BackNavigationCallback(NavigateBack);
			OnBackInvokedDispatcher.RegisterOnBackInvokedCallback(global::Android.Window.IOnBackInvokedDispatcher.PriorityDefault, _backNavigationCallback);
		}
		Window!.SetSoftInputMode(SoftInput.AdjustResize);
		_payment = Intent?.Data?.Scheme == "bitcoin" ? Intent.DataString : null;
		StartWalletService();
		ShowWallets();
		_refresh = new System.Threading.Timer(_ => RunOnUiThread(Refresh), null, 500, 1500);
		if (OperatingSystem.IsAndroidVersionAtLeast(33) && CheckSelfPermission(Manifest.Permission.PostNotifications) != Permission.Granted)
		{
			RequestPermissions([Manifest.Permission.PostNotifications], 2);
		}
	}

	private void StartWalletService()
	{
		var intent = new Intent(this, typeof(WalletService));
		if (OperatingSystem.IsAndroidVersionAtLeast(26)) { StartForegroundService(intent); }
		else { StartService(intent); }
	}

	protected override void OnResume()
	{
		base.OnResume();
		_foreground = true;
		WalletRuntime.InterfaceForeground = true;
		_lastInteraction = Stopwatch.GetTimestamp();
		if (Session is null && WalletRuntime.Error is null) { StartWalletService(); }
		if (_uiLocked && !_externalFlow && !_authenticating) { ShowWallets(); }
		_externalFlow = false;
	}

	protected override void OnStop()
	{
		_foreground = false;
		WalletRuntime.InterfaceForeground = false;
		LockUi();
		base.OnStop();
	}

	protected override void OnDestroy()
	{
		if (OperatingSystem.IsAndroidVersionAtLeast(33) && _backNavigationCallback is { } callback)
		{
			OnBackInvokedDispatcher.UnregisterOnBackInvokedCallback(callback);
			callback.Dispose();
			_backNavigationCallback = null;
		}
		ClearCreationDraft();
		_refresh?.Dispose();
		_activityLifetime.Cancel();
		_activityLifetime.Dispose();
		base.OnDestroy();
	}

	protected override void OnNewIntent(Intent? intent)
	{
		base.OnNewIntent(intent);
		if (intent?.Data?.Scheme == "bitcoin")
		{
			_payment = intent.DataString;
			if (!_uiLocked && Session?.IsUnlocked is true) { ShowSend(); }
		}
	}

	public override bool DispatchTouchEvent(MotionEvent? e)
	{
		_lastInteraction = Stopwatch.GetTimestamp();
		return base.DispatchTouchEvent(e);
	}

#pragma warning disable CS0672, CA1422

	public override void OnBackPressed() => NavigateBack();
#pragma warning restore CS0672, CA1422

	private void NavigateBack()
	{
		if (!_busy && _back is { } back) { back(); }
		else if (!_busy) { MoveTaskToBack(true); }
	}

	private sealed class BackNavigationCallback(Action navigate) : Java.Lang.Object, global::Android.Window.IOnBackInvokedCallback
	{
		public void OnBackInvoked() => navigate();
	}

	private void LockUi()
	{
		_uiGeneration++;
		_uiLocked = true;
		_selectedCoins = null;
		ClearSendDraft();
		Session?.Lock(preserveProposal: _authenticating);
		ShowWallets();
	}

	private void Refresh()
	{
		if (IsFinishing || IsDestroyed || !_foreground) { return; }
		if ((!_uiLocked || _screen is "create" or "backup" or "unlock") && Stopwatch.GetElapsedTime(_lastInteraction) > TimeSpan.FromMinutes(2)) { LockUi(); }
		var session = Session;
		if (!_busy && session is null && WalletRuntime.Error is null && WalletRuntime.Snapshot.Lifecycle == RuntimeLifecycle.Stopped) { StartWalletService(); }
		ObserveSession(session);
		ShowSynchronization();
		if (_screen == "wallets" && _body.Tag?.ToString() != WalletListSignature()) { ShowWallets(); }
		_updateScreen?.Invoke();
	}

	private void ObserveSession(WalletSession? session)
	{
		if (ReferenceEquals(_observedSession, session)) { return; }
		_observedSession = session;
		_synchronization.Reset();
		LockUi();
	}

	private string WalletListSignature() => Session is { } session ? string.Join('|', session.Global.WalletManager.GetWallets().Select(w => w.WalletName)) + "/ready" : "loading";

	private void Screen(string title, string screen, Action? back = null)
	{
		// A completion queued before background/inactivity lock must not replace
		// the locked screen with wallet details, even after the activity resumes.
		if (screen != "wallets" && _workGeneration is { } generation && generation != _uiGeneration)
		{ throw new System.OperationCanceledException("Unlock the wallet and try again."); }
		_screen = screen;
		_back = back;
		_updateScreen = null;
		_root = new LinearLayout(this) { Orientation = Orientation.Vertical };
		_root.SetBackgroundColor(Background);
		_root.SetPadding(Dp(24), Dp(12), Dp(24), Dp(12));
		var header = Row();
		if (back is not null) { header.AddView(Button("‹", back, false, 48), new LinearLayout.LayoutParams(Dp(48), Dp(48))); }
		var heading = Text(title, 24, Color.White, true);
		heading.SetMinHeight(Dp(64));
		header.AddView(heading, new LinearLayout.LayoutParams(0, -2, 1));
		_workIndicator = new ProgressBar(this, null, global::Android.Resource.Attribute.ProgressBarStyleSmall) { Indeterminate = true, ContentDescription = "Working", Visibility = _busy ? ViewStates.Visible : ViewStates.Gone };
		_workIndicator.IndeterminateTintList = global::Android.Content.Res.ColorStateList.ValueOf(Accent);
		header.AddView(_workIndicator, new LinearLayout.LayoutParams(Dp(24), Dp(24)) { MarginStart = Dp(12) });
		_root.AddView(header);
		var scroll = new ScrollView(this) { FillViewport = true };
		_body = Column();
		_body.SetPadding(0, Dp(8), 0, Dp(24));
		scroll.AddView(_body);
		_root.AddView(scroll, new LinearLayout.LayoutParams(-1, 0, 1));
		_status = Text("Connecting through Tor", 14, Muted);
		_status.SetPadding(0, Dp(10), 0, Dp(4));
		_root.AddView(_status);
		_syncProgress = new ProgressBar(this, null, global::Android.Resource.Attribute.ProgressBarStyleHorizontal) { Max = 1000, Indeterminate = true };
		_syncProgress.ProgressTintList = global::Android.Content.Res.ColorStateList.ValueOf(Accent);
		_syncProgress.ProgressBackgroundTintList = global::Android.Content.Res.ColorStateList.ValueOf(Surface);
		_syncProgress.IndeterminateTintList = global::Android.Content.Res.ColorStateList.ValueOf(Accent);
		_root.AddView(_syncProgress, new LinearLayout.LayoutParams(-1, Dp(6)) { TopMargin = Dp(2), BottomMargin = Dp(6) });
		_syncDetails = Text("", 12, Muted);
		_syncDetails.SetPadding(0, 0, 0, Dp(8));
		_root.AddView(_syncDetails);
		// Android's inset handling replaces the padding on the view that consumes
		// system windows. Keep content spacing on a separate inner container.
		var safeArea = new FrameLayout(this);
		safeArea.SetFitsSystemWindows(true);
		safeArea.SetBackgroundColor(Background);
		safeArea.AddView(_root, new FrameLayout.LayoutParams(-1, -1));
		SetContentView(safeArea);
		ShowSynchronization();
	}

	private void ShowSynchronization()
	{
		var snapshot = WalletRuntime.Synchronization;
		snapshot = snapshot with { Error = WalletRuntime.Error ?? snapshot.Error };
		var progress = _synchronization.Update(snapshot);
		_status.Text = progress.Stage switch
		{
			SynchronizationStage.Tor => "Connecting through Tor",
			SynchronizationStage.Starting => "Opening Bitcoin connection",
			SynchronizationStage.Peers => "Finding Bitcoin peers",
			SynchronizationStage.Headers => "Synchronizing · Verifying Bitcoin history",
			SynchronizationStage.Filters => "Synchronizing · Downloading wallet data",
			SynchronizationStage.Wallet => "Synchronizing · Scanning wallet",
			SynchronizationStage.Ready => "●  Connected · " + snapshot.Network,
			_ => progress.Error ?? "Connection interrupted"
		};
		_status.SetTextColor(progress.Stage == SynchronizationStage.Ready ? Accent : Muted);
		_syncProgress.Visibility = progress.Stage is SynchronizationStage.Ready or SynchronizationStage.Failed ? ViewStates.Gone : ViewStates.Visible;
		_syncProgress.Indeterminate = progress.Fraction is null;
		_syncProgress.Progress = (int)Math.Round((progress.Fraction ?? 0) * 1000);
		var details = new List<string>();
		if (progress.Fraction is { } fraction) { details.Add(fraction.ToString("P0", CultureInfo.InvariantCulture)); }
		if (progress.Stage is SynchronizationStage.Headers or SynchronizationStage.Filters or SynchronizationStage.Wallet)
		{
			details.Add(progress.Target is { } target ? $"{progress.Position:N0} / {target:N0} blocks" : $"{progress.Position:N0} blocks verified");
		}
		if (progress.WaitingForData) { details.Add("Waiting for data"); }
		else if (progress.EstimatedRemaining is { } remaining) { details.Add("About " + SyncDuration(remaining) + " left in this stage"); }
		else if (progress.Stage is not (SynchronizationStage.Ready or SynchronizationStage.Failed)) { details.Add(SyncDuration(progress.Elapsed) + " elapsed"); }
		if (snapshot.Peers > 0) { details.Add(snapshot.Peers + " peers"); }
		_syncDetails.Text = string.Join(" · ", details);
		_syncDetails.Visibility = details.Count == 0 ? ViewStates.Gone : ViewStates.Visible;
		_syncProgress.ContentDescription = _status.Text + ". " + _syncDetails.Text;
	}

	private static string SyncDuration(TimeSpan duration) => duration.TotalHours >= 1 ? $"{Math.Ceiling(duration.TotalHours):0} hr"
		: duration.TotalMinutes >= 1 ? $"{Math.Ceiling(duration.TotalMinutes):0} min" : $"{Math.Max(0, Math.Ceiling(duration.TotalSeconds)):0} sec";

	private void ShowWallets()
	{
		ClearCreationDraft();
		Screen("Wasabi Wallet", "wallets");
		_body.Tag = WalletListSignature();
		var brand = new ImageView(this) { ContentDescription = "Wasabi Wallet logo" };
		brand.SetImageResource(Resource.Drawable.wasabi_logo);
		_body.AddView(brand, new LinearLayout.LayoutParams(Dp(88), Dp(88)) { Gravity = GravityFlags.CenterHorizontal, BottomMargin = Dp(20) });
		AddText("Bitcoin.\nUnfairly private.", 36, Color.White, true);
		Gap(24);
		if (Session is { } session)
		{
			foreach (var wallet in session.Global.WalletManager.GetWallets())
			{
				var target = wallet;
				AddButton("◈   " + wallet.WalletName + "   ›", () => OpenWallet(target), false);
			}
		}
		AddButton("Create a wallet", () => ShowCreate(false));
		AddButton("Recover a wallet", () => ShowCreate(true), false);
		AddButton("Import wallet file", ShowImport, false);
		AddButton("Settings", ShowSettings, false);
		Gap(16);
		AddText("Your keys stay on this device.\nConnections are routed through Tor.", 14, Muted);
		if (WalletRuntime.Error is not null) { AddButton("Retry connection", async () => { await WalletRuntime.StopAsync(); StartWalletService(); ShowWallets(); }); }
	}

	private void OpenWallet(Wallet wallet)
	{
		ShowUnlock(wallet);
		if (Vault.HasWalletPassword(WalletSession.WalletReference(wallet)))
		{
			Work(() => WithDeviceAuthorization(wallet, "Unlock " + wallet.WalletName,
				_ => { ShowUnlockedWallet(); return Task.CompletedTask; }, () => ShowUnlock(wallet, passwordOnly: true)));
		}
	}

	private void ShowUnlockedWallet()
	{
		if (_payment is not null) { ShowSend(); } else { ShowHome(); }
	}

	private void ShowUnlock(Wallet wallet, bool passwordOnly = false)
	{
		ObserveSession(Session);
		Screen(wallet.WalletName, "unlock", ShowWallets);
		AddText("Unlock your wallet", 28, Color.White, true);
		if (!passwordOnly && Vault.HasWalletPassword(WalletSession.WalletReference(wallet)))
		{
			AddButton("Unlock", () => Work(() => WithDeviceAuthorization(wallet, "Unlock " + wallet.WalletName,
				_ => { ShowUnlockedWallet(); return Task.CompletedTask; }, () => ShowUnlock(wallet, passwordOnly: true))));
			AddButton("Use wallet password", () => ShowUnlock(wallet, passwordOnly: true), false);
			return;
		}
		var password = Field("Wallet password", true);
		AddButton("Unlock", () => Work(async () =>
		{
			var session = Session ?? throw new InvalidOperationException("Reconnect the wallet first.");
			var secret = password.Text ?? "";
			password.Text = "";
			await UnlockInBackgroundAsync(session, wallet, secret, _activityLifetime.Token);
			await EnsureDeviceUnlockAsync(session, wallet, secret);
			_uiLocked = false;
			ShowUnlockedWallet();
		}));
		if (Vault.HasWalletPassword(WalletSession.WalletReference(wallet)))
		{
			AddButton("Use device unlock", () => Work(async () =>
			{
				await WithDeviceAuthorization(wallet, "Unlock " + wallet.WalletName,
					_ => { ShowUnlockedWallet(); return Task.CompletedTask; }, () => ShowUnlock(wallet, passwordOnly: true));
			}), false);
		}
	}

	private async Task UnlockInBackgroundAsync(WalletSession session, Wallet wallet, string password, CancellationToken cancellationToken)
	{
		var generation = _uiGeneration;
		try
		{
			await Task.Run(() => session.Unlock(wallet, password), cancellationToken);
			cancellationToken.ThrowIfCancellationRequested();
			if (!_foreground || Session != session || _uiGeneration != generation)
			{ throw new System.OperationCanceledException("Return to Wasabi and unlock again."); }
		}
		catch { session.Lock(preserveProposal: _authenticating); throw; }
	}

	private async Task EnsureDeviceUnlockAsync(WalletSession session, Wallet wallet, string password)
	{
		var reference = WalletSession.WalletReference(wallet);
		var vault = Vault;
		if (_deviceEnrollmentUnavailable || (vault.HasWalletPassword(reference) && !_unavailableDeviceKeys.Contains(reference)) || !vault.CanEnrollWalletPassword) { return; }
		var generation = _uiGeneration;
		using var deadline = CancellationTokenSource.CreateLinkedTokenSource(_activityLifetime.Token);
		deadline.CancelAfter(TimeSpan.FromMinutes(1));
		_authenticating = true;
		try
		{
			try { await vault.EnrollWalletPasswordAsync(reference, password, deadline.Token); }
			catch (Exception error) when (error is System.OperationCanceledException or PlatformNotSupportedException or IOException)
			{
				if (error is not System.OperationCanceledException) { _deviceEnrollmentUnavailable = true; }
				// Password access was already verified. A cancelled setup cannot undo a
				// background lock or give an operation a new authorization generation.
				if (!_foreground || Session != session || generation != _uiGeneration)
				{ session.Lock(); throw new System.OperationCanceledException("Return to Wasabi and unlock again."); }
				return;
			}
			await ResumeAuthorizedWalletAsync(session, wallet, password, deadline.Token);
			_unavailableDeviceKeys.Remove(reference);
		}
		catch { session.Lock(); throw; }
		finally { _authenticating = false; }
	}

	private async Task ResumeAuthorizedWalletAsync(WalletSession session, Wallet wallet, string password, CancellationToken cancellationToken)
	{
		// Device-credential confirmation can briefly put this activity behind the
		// system lock screen. Only a successful per-use grant permits resuming.
		for (var i = 0; !_foreground && i < 40; i++) { await Task.Delay(50, cancellationToken); }
		if (!_foreground || Session != session) { session.Lock(); throw new System.OperationCanceledException("Return to Wasabi and authorize again."); }
		await UnlockInBackgroundAsync(session, wallet, password, cancellationToken);
		_workGeneration = _uiGeneration;
		_lastInteraction = Stopwatch.GetTimestamp();
		_uiLocked = false;
	}

	private async Task WithDeviceAuthorization(Wallet wallet, string purpose, Func<string, Task> action, Action passwordFallback)
	{
		var session = Session ?? throw new InvalidOperationException("Reconnect the wallet first.");
		var generation = _uiGeneration;
		using var deadline = CancellationTokenSource.CreateLinkedTokenSource(_activityLifetime.Token);
		deadline.CancelAfter(TimeSpan.FromMinutes(1));
		_authenticating = true;
		try
		{
			string password;
			try { password = await Vault.RetrieveWalletPasswordAsync(WalletSession.WalletReference(wallet), purpose, deadline.Token); }
			catch (System.OperationCanceledException)
			{
				if (_foreground && Session == session && generation == _uiGeneration) { passwordFallback(); }
				return;
			}
			catch (Exception error) when (error is InvalidOperationException or PlatformNotSupportedException or IOException or System.Text.Json.JsonException or FormatException)
			{
				_unavailableDeviceKeys.Add(WalletSession.WalletReference(wallet));
				if (_foreground && Session == session && generation == _uiGeneration) { passwordFallback(); }
				return;
			}
			await ResumeAuthorizedWalletAsync(session, wallet, password, deadline.Token);
			await action(password);
		}
		finally { _authenticating = false; }
	}

	private void AddAuthorization(string title, string purpose, Func<string, Task> action)
	{
		var wallet = Session?.Current ?? throw new InvalidOperationException("Unlock a wallet first.");
		var panel = Column();
		_body.AddView(panel, Wrap);
		void PasswordFallback()
		{
			panel.RemoveAllViews();
			var password = Field("Confirm wallet password", true, panel);
			panel.AddView(Button(title, () => Work(async () =>
			{
				var secret = password.Text ?? "";
				password.Text = "";
				if (!_foreground || _uiLocked) { throw new System.OperationCanceledException("Unlock the wallet and review again."); }
				await action(secret);
			})), new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(12) });
		}
		if (Vault.HasWalletPassword(WalletSession.WalletReference(wallet)))
		{
			panel.AddView(Button(title, () => Work(() => WithDeviceAuthorization(wallet, purpose, action, PasswordFallback))),
				new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(12) });
			panel.AddView(Button("Use wallet password", PasswordFallback, false), new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(12) });
		}
		else { PasswordFallback(); }
	}

	private void ShowImport() => ShowImport(null);

	private void ShowImport(string? savedName)
	{
		if (Session is null) { Alert("Starting the wallet engine. Try again in a moment."); return; }
		ObserveSession(Session);
		Screen("Import wallet", "import", ShowWallets);
		var name = Field("Wallet name");
		name.Text = savedName ?? SuggestWalletName();
		AddText("Choose an encrypted Wasabi wallet JSON backup. Use its original password to unlock it.", 16, Muted);
		AddButton("Choose wallet file", () =>
		{
			var value = name.Text?.Trim() ?? "";
			if (Session!.Global.WalletManager.ValidateWalletName(value) is { } error) { Alert(error.Message); return; }
			_importName = value;
			_externalFlow = true;
			StartActivityForResult(new Intent(Intent.ActionOpenDocument).AddCategory(Intent.CategoryOpenable)!.SetType("*/*"), 5);
		});
	}

	private sealed class WalletCreationDraft(string name)
	{
		public string Name { get; set; } = name;
		public string Password { get; set; } = "";
		public string RepeatPassword { get; set; } = "";
		public string RecoveryWords { get; set; } = "";
		public Mnemonic? GeneratedMnemonic { get; set; }
		public void Clear()
		{
			Password = "";
			RepeatPassword = "";
			RecoveryWords = "";
			GeneratedMnemonic = null;
		}
	}

	private void ClearCreationDraft()
	{
		_creationDraft?.Clear();
		_creationDraft = null;
	}

	private void ShowCreate(bool recover, WalletCreationDraft? draft = null)
	{
		if (Session is null) { Alert("Starting the wallet engine. Try again in a moment."); return; }
		// Bind the flow before accepting a password or generating a backup. The
		// periodic refresh must not first adopt this runtime in the next step.
		ObserveSession(Session);
		draft ??= new WalletCreationDraft(SuggestWalletName());
		_creationDraft = draft;
		Screen(recover ? "Recover wallet" : "Create wallet", "create", ShowWallets);
		var name = Field("Wallet name");
		name.Text = draft.Name;
		if (recover)
		{
			var words = Field("Recovery words");
			Multiline(words);
			words.Text = draft.RecoveryWords;
			AddButton("Continue", () => Work(() =>
			{
				draft.Name = name.Text?.Trim() ?? "";
				draft.RecoveryWords = words.Text?.Trim() ?? "";
				if (Session!.Global.WalletManager.ValidateWalletName(draft.Name) is { } error) { throw new ArgumentException(error.Message); }
				var mnemonic = new Mnemonic(draft.RecoveryWords);
				if (!mnemonic.IsValidChecksum) { throw new FormatException("Invalid recovery words."); }
				ShowRecoveryPassword(draft);
				return Task.CompletedTask;
			}));
			return;
		}
		var password = Field("Wallet password", true);
		password.Text = draft.Password;
		var confirm = Field("Repeat password", true);
		confirm.Text = draft.RepeatPassword;
		AddText(RecoveryRequirement, 14, Muted);
		AddButton("Continue", () => Work(() =>
		{
			draft.Name = name.Text?.Trim() ?? "";
			draft.Password = password.Text ?? "";
			draft.RepeatPassword = confirm.Text ?? "";
			if (Session!.Global.WalletManager.ValidateWalletName(draft.Name) is { } error) { throw new ArgumentException(error.Message); }
			if (draft.Password.Length < 8 || draft.Password != draft.RepeatPassword) { throw new ArgumentException("Use at least 8 characters and repeat the same password."); }
			// Returning to the form must preserve the words already backed up.
			draft.GeneratedMnemonic ??= new Mnemonic(Wordlist.English, WordCount.Twelve);
			ShowRecoveryWords(draft);
			return Task.CompletedTask;
		}));
	}

	private void ShowRecoveryPassword(WalletCreationDraft draft)
	{
		EditText? password = null;
		Screen("Recover wallet", "create", () =>
		{
			draft.Password = password?.Text ?? "";
			ShowCreate(true, draft);
		});
		password = Field("Original Wasabi password / BIP39 passphrase", true);
		password.Text = draft.Password;
		AddText("Use the same password that created the wallet. A different passphrase opens a different wallet.", 14, Muted);
		AddButton("Recover", () => Work(async () =>
		{
			draft.Password = password.Text ?? "";
			await CreateWalletInBackgroundAsync(draft.Name, draft.Password, new Mnemonic(draft.RecoveryWords), true);
		}));
	}

	private void ShowRecoveryWords(WalletCreationDraft draft)
	{
		var mnemonic = draft.GeneratedMnemonic ?? throw new InvalidOperationException("Restart wallet creation.");
		Screen("Recovery words", "backup", () => ShowCreate(false, draft));
		AddText("Write these down", 28, Color.White, true);
		AddText(RecoveryRequirement, 14, Muted);
		Gap(20);
		for (var i = 0; i < mnemonic.Words.Length; i += 3)
		{
			var row = Row();
			for (var j = i; j < Math.Min(i + 3, mnemonic.Words.Length); j++)
			{
				var word = Text($"{j + 1}  {mnemonic.Words[j]}", 14, Color.White);
				word.Background = Rounded(Surface);
				word.SetPadding(Dp(10), Dp(16), Dp(4), Dp(16));
				row.AddView(word, new LinearLayout.LayoutParams(0, -2, 1) { MarginEnd = Dp(6), BottomMargin = Dp(8) });
			}
			_body.AddView(row);
		}
		Gap(16);
		AddButton("I wrote them down", () => ShowConfirmWords(draft));
	}

	private string SuggestWalletName() => WalletNameSuggestion.Next(Session!.Global.WalletManager.WalletDirectories
		.EnumerateWalletFiles().Select(file => System.IO.Path.GetFileNameWithoutExtension(file.Name)));

	private void ShowConfirmWords(WalletCreationDraft draft)
	{
		var mnemonic = draft.GeneratedMnemonic ?? throw new InvalidOperationException("Restart wallet creation.");
		var confirmation = new RecoveryWordConfirmation(mnemonic);
		var step = 0;
		var generation = _uiGeneration;
		void ShowQuestion()
		{
			Screen("Check your backup", "backup", () =>
			{
				if (step == 0) { ShowRecoveryWords(draft); }
				else { step--; confirmation.ResetFrom(step); ShowQuestion(); }
			});
			var progress = Row();
			progress.ContentDescription = $"{step} of {confirmation.Questions.Count} recovery words confirmed";
			for (var i = 0; i < confirmation.Questions.Count; i++)
			{
				var segment = new View(this) { Background = Rounded(i < step ? Accent : Surface) };
				progress.AddView(segment, new LinearLayout.LayoutParams(0, Dp(4), 1) { MarginEnd = i < 2 ? Dp(8) : 0 });
			}
			_body.AddView(progress, new LinearLayout.LayoutParams(-1, -2) { BottomMargin = Dp(32) });
			if (step == confirmation.Questions.Count)
			{
				AddText("✓", 56, Accent, true);
				AddText("Backup confirmed", 28, Color.White, true);
				AddButton("Create wallet", () => Work(async () =>
				{
					if (!confirmation.IsComplete) { throw new InvalidOperationException("Confirm your recovery words first."); }
					await CreateWalletInBackgroundAsync(draft.Name, draft.Password, mnemonic, false);
				}));
				return;
			}
			var question = confirmation.Questions[step];
			var currentStep = step;
			AddText($"Word {question.Position}", 28, Color.White, true);
			Gap(16);
			for (var rowIndex = 0; rowIndex < 3; rowIndex++)
			{
				var row = Row();
				for (var column = 0; column < 2; column++)
				{
					var word = question.Choices[rowIndex * 2 + column];
					Button? choice = null;
					choice = Button(word, () =>
					{
						if (step != currentStep || generation != _uiGeneration || !_foreground || _creationDraft != draft) { return; }
						if (!confirmation.Select(question.Position, word))
						{
							choice!.SetTextColor(Color.Rgb(255, 137, 137));
							choice.ContentDescription = word + ". Incorrect; choose another word.";
							return;
						}
						step++;
						ShowQuestion();
					}, false);
					row.AddView(choice, new LinearLayout.LayoutParams(0, -2, 1) { MarginEnd = column == 0 ? Dp(12) : 0 });
				}
				_body.AddView(row, new LinearLayout.LayoutParams(-1, -2) { BottomMargin = Dp(12) });
			}
		}
		ShowQuestion();
	}

	private async Task CreateWalletInBackgroundAsync(string name, string password, Mnemonic mnemonic, bool recover)
	{
		var session = Session ?? throw new InvalidOperationException("Reconnect the wallet first.");
		var generation = _uiGeneration;
		var wallet = await Task.Run(() => session.CreateAsync(name, password, mnemonic, recover));
		// Once saved, setup no longer needs a plaintext backup/password draft.
		ClearCreationDraft();
		if (!_foreground || Session != session || generation != _uiGeneration)
		{ session.Lock(); throw new System.OperationCanceledException("Return to Wasabi and unlock the saved wallet."); }
		await EnsureDeviceUnlockAsync(session, wallet, password);
		_uiLocked = false;
		ShowHome();
	}

	private void ShowHome()
	{
		if (_uiLocked || Session?.Current is not { } wallet) { ShowWallets(); return; }
		_selectedCoins = null;
		ClearSendDraft();
		Screen(wallet.WalletName, "home", LockUi);
		AddText("TOTAL BALANCE", 12, Muted);
		var balance = AddText("—", 38, Color.White, true);
		var fiat = AddText("", 16, Muted);
		Gap(24);
		var actions = Row();
		actions.AddView(Button("↑  Send", ShowSend), new LinearLayout.LayoutParams(0, -2, 1) { MarginEnd = Dp(10) });
		actions.AddView(Button("↓  Receive", ShowReceive, false), new LinearLayout.LayoutParams(0, -2, 1));
		_body.AddView(actions);
		Gap(24);
		var privateCard = Card();
		var ring = new PrivacyRing(this);
		privateCard.AddView(ring, new LinearLayout.LayoutParams(Dp(86), Dp(86)));
		var privacyBody = Column();
		privacyBody.AddView(Text("Privacy", 20, Color.White, true));
		var privateText = Text("", 14, Muted);
		privacyBody.AddView(privateText);
		privateCard.AddView(privacyBody, new LinearLayout.LayoutParams(0, -2, 1));
		privateCard.Click += (_, _) => ShowPrivacy();
		_body.AddView(privateCard);
		Gap(20);
		AddButton("Transaction history   ›", ShowHistory, false);
		AddButton("Coins   ›", () => ShowCoins(false), false);
		AddButton("Wallet backup   ›", ShowBackup, false);
		AddButton("Settings   ›", ShowSettings, false);
		AddButton("Lock wallet", LockUi, false);
		_updateScreen = () =>
		{
			var total = wallet.GetAllCoins().Unspent().TotalAmount();
			balance.Text = wallet.Loaded ? total.ToDecimal(MoneyUnit.BTC).ToString("0.########", CultureInfo.InvariantCulture) + " BTC" : "—";
			fiat.Text = wallet.Loaded && Session!.Global.Status.UsdExchangeRate > 0 ? "$" + (total.ToDecimal(MoneyUnit.BTC) * Session.Global.Status.UsdExchangeRate).ToString("N2", CultureInfo.InvariantCulture) : "";
			ring.Progress = wallet.GetPrivacyPercentage();
			privateText.Text = total == Money.Zero ? "Receive bitcoin to get started" : $"{wallet.GetPrivacyPercentage()}% private";
		};
		_updateScreen();
	}

	private void ShowReceive()
	{
		Screen("Receive", "receive", ShowHome);
		var label = Field("Label (who is paying you?)");
		var amount = Field("Amount in BTC (optional)");
		amount.InputType = InputTypes.ClassNumber | InputTypes.NumberFlagDecimal;
		AddButton("Create address", () => Work(() =>
		{
			var requestedAmount = string.IsNullOrWhiteSpace(amount.Text) ? null : PaymentRequest.ParseAmount(amount.Text!);
			var address = Session!.Receive(label.Text ?? "");
			var request = new PaymentRequest(BitcoinAddress.Create(address, Session.Global.Network), requestedAmount, label.Text ?? "", "");
			ShowAddress(request);
			return Task.CompletedTask;
		}));
	}

	private void ShowAddress(PaymentRequest request)
	{
		Screen("Receive", "receive", ShowHome);
		var matrix = new QrEncoder(ErrorCorrectionLevel.M).Encode(request.ToUri()).Matrix;
		const int scale = 8;
		using var bitmap = Bitmap.CreateBitmap((matrix.Width + 8) * scale, (matrix.Height + 8) * scale, Bitmap.Config.Argb8888!);
		using (var canvas = new Canvas(bitmap))
		using (var paint = new Paint { Color = Color.Black })
		{
			canvas.DrawColor(Color.White);
			for (var x = 0; x < matrix.Width; x++)
			for (var y = 0; y < matrix.Height; y++)
			{
				if (matrix[x, y]) { canvas.DrawRect((x + 4) * scale, (y + 4) * scale, (x + 5) * scale, (y + 5) * scale, paint); }
			}
		}
		var image = new ImageView(this);
		image.SetImageBitmap(bitmap);
		image.ContentDescription = "Bitcoin payment QR code";
		_body.AddView(image, new LinearLayout.LayoutParams(-1, Dp(280)) { BottomMargin = Dp(24) });
		var address = AddText(request.Address.ToString(), 16, Color.White);
		address.SetTextIsSelectable(true);
		if (request.Amount is { } value) { AddText(value.ToString(false, false) + " BTC", 22, Accent); }
		AddButton("Copy address", () => Copy(request.Address.ToString()));
		AddButton("Share request", () => Share(request.ToUri()), false);
		AddButton("New address", ShowReceive, false);
	}

	private void ShowSend()
	{
		if (Session?.Current is null) { ShowWallets(); return; }
		Screen("Send bitcoin", "send", ShowHome);
		var address = Field("Bitcoin address or payment request");
		var amount = Field("Amount in BTC");
		amount.InputType = InputTypes.ClassNumber | InputTypes.NumberFlagDecimal;
		var label = Field("Label (who are you paying?)");
		var fee = Field("Fee rate in sat/vB");
		fee.InputType = InputTypes.ClassNumber | InputTypes.NumberFlagDecimal;
		fee.Text = (Session.Global.Status.FeeRates?.Estimations.FirstOrDefault(x => x.Key >= 3).Value?.SatoshiPerByte ?? 1m).ToString("0.##", CultureInfo.InvariantCulture);
		var all = new CheckBox(this) { Text = "Send all selected funds", TextSize = 15 };
		all.SetTextColor(Color.White);
		_body.AddView(all);
		address.Text = _sendAddress;
		amount.Text = _sendAmount;
		label.Text = _sendLabel;
		if (_sendFee.Length > 0) { fee.Text = _sendFee; }
		all.Checked = _sendAll;
		void SaveDraft()
		{
			_sendAddress = address.Text ?? "";
			_sendAmount = amount.Text ?? "";
			_sendLabel = label.Text ?? "";
			_sendFee = fee.Text ?? "";
			_sendAll = all.Checked;
		}
		if (_payment is { } incoming)
		{
			try
			{
				var parsed = PaymentRequest.Parse(incoming, Session.Global.Network);
				address.Text = parsed.Address.ToString();
				amount.Text = parsed.Amount?.ToDecimal(MoneyUnit.BTC).ToString("0.########", CultureInfo.InvariantCulture) ?? "";
				label.Text = parsed.Label;
			}
			catch (Exception ex) { Alert(ex.Message); }
			_payment = null;
		}
		AddButton("Scan QR", () => Scan(value =>
		{
			var request = PaymentRequest.Parse(value, Session!.Global.Network);
			address.Text = request.Address.ToString();
			if (request.Amount is { } money) { amount.Text = money.ToDecimal(MoneyUnit.BTC).ToString("0.########", CultureInfo.InvariantCulture); }
			label.Text = request.Label;
		}), false);
		AddButton(_selectedCoins is null ? "Choose coins" : $"{_selectedCoins.Count} coins selected", () => { SaveDraft(); ShowCoins(true); }, false);
		AddButton("Review transaction", () => Work(async () =>
		{
			SaveDraft();
			var request = PaymentRequest.Parse(address.Text ?? "", Session!.Global.Network) with { Label = label.Text ?? "" };
			var paymentAmount = all.Checked ? Money.Zero : string.IsNullOrWhiteSpace(amount.Text) && request.Amount is { } requested ? requested : PaymentRequest.ParseAmount(amount.Text ?? "");
			if (!decimal.TryParse(fee.Text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var rate) || rate is < 1 or > 10000) { throw new FormatException("Use a fee rate between 1 and 10,000 sat/vB."); }
			var preview = await Task.Run(() => Session.PrepareAsync(request, paymentAmount, new FeeRate(rate), _selectedCoins?.ToArray(), all.Checked));
			Screen("Review transaction", "review", ShowSend);
			var sent = Money.Satoshis(preview.AmountSatoshis);
			AddText(sent.ToString(false, false) + " BTC", 32, Color.White, true);
			AddText("TO", 12, Muted);
			AddText(request.Address.ToString(), 15, Color.White).SetTextIsSelectable(true);
			Gap(16);
			AddText($"Network fee   {preview.FeeSatoshis:N0} sats", 16, Muted);
			AddText($"Total   {Money.Satoshis(preview.TotalSatoshis).ToString(false, false)} BTC", 18, Color.White);
			AddText($"{preview.Inputs.Length} inputs · {preview.Network}", 14, Muted);
			AddAuthorization("Confirm and send", $"Send {sent.ToString(false, false)} BTC", async secret =>
			{
				using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
				var receipt = await Task.Run(() => Session!.ConfirmAsync(preview.Id, secret, timeout.Token));
				var transactionId = receipt.TransactionId;
				Screen(receipt.State == SubmissionState.Uncertain ? "Submission pending" : "Transaction sent", "sent", ShowHome);
				AddText(receipt.State == SubmissionState.Uncertain ? "◷" : "✓", 72, Accent, true);
				AddText(receipt.State == SubmissionState.Uncertain ? "Checking broadcast outcome" : "Waiting for confirmation", 24, Color.White, true);
				AddText(transactionId, 14, Muted).SetTextIsSelectable(true);
				AddButton("Copy transaction ID", () => Copy(transactionId), false);
				AddButton("Done", ShowHome);
			});
		}));
	}

	private void ShowCoins(bool select)
	{
		Screen(select ? "Choose coins" : "Your coins", "coins", select ? ShowSend : ShowHome);
		_selectedCoins ??= [];
		foreach (var coin in Session!.Current!.GetAllCoins().Unspent().OrderByDescending(c => c.Amount))
		{
			var value = coin;
			var row = Column();
			row.SetPadding(Dp(16), Dp(12), Dp(16), Dp(12));
			row.Background = Rounded(Surface);
			var check = new CheckBox(this) { Text = coin.Amount.ToString(false, false) + " BTC", TextSize = 18, Checked = _selectedCoins.Contains(coin.Outpoint), Enabled = select && coin.IsAvailable() && coin.Confirmed };
			check.SetTextColor(Color.White);
			check.CheckedChange += (_, e) => { if (e.IsChecked) { _selectedCoins.Add(value.Outpoint); } else { _selectedCoins.Remove(value.Outpoint); } };
			row.AddView(check);
			row.AddView(Text($"{(coin.Confirmed ? "Confirmed" : "Pending")} · anonymity {coin.AnonymitySet:0.#} · {coin.HdPubKey.Labels}", 13, Muted));
			if (!select)
			{
				var exclude = new CheckBox(this) { Text = "Exclude from CoinJoin", Checked = coin.IsExcludedFromCoinJoin };
				exclude.SetTextColor(Muted);
				exclude.CheckedChange += (_, e) => Session!.Current!.ExcludeCoinFromCoinJoin(value.Outpoint, e.IsChecked);
				row.AddView(exclude);
			}
			_body.AddView(row, new LinearLayout.LayoutParams(-1, -2) { BottomMargin = Dp(10) });
		}
		if (!Session.Current.GetAllCoins().Unspent().Any()) { AddText("No coins yet", 20, Muted); }
		if (select) { AddButton("Use selected coins", ShowSend); AddButton("Use automatic selection", () => { _selectedCoins = null; ShowSend(); }, false); }
	}

	private void ShowHistory() => Work(async () =>
	{
		var session = Session!;
		var wallet = session.Current!;
		var transactions = await wallet.BuildHistorySummaryAsync(true);
		if (!_foreground || !session.IsUnlocked || Session != session || session.Current != wallet) { return; }
		var submissions = session.SubmissionHistory;
		Screen("Transactions", "history", ShowHome);
		if (transactions.Count == 0 && submissions.IsEmpty) { AddText("Your transactions will appear here", 20, Muted); }
		foreach (var submission in submissions.Where(s => transactions.All(t => t.GetHash().ToString() != s.TransactionId)).OrderByDescending(s => s.CreatedAt))
		{
			var card = Column();
			card.Background = Rounded(Surface);
			card.SetPadding(Dp(16), Dp(16), Dp(16), Dp(16));
			card.AddView(Text($"◷  {SubmissionTitle(submission.Operation)}    {Money.Satoshis(submission.AmountSatoshis).ToString(false, false)} BTC", 17, Color.White, true));
			card.AddView(Text($"{submission.CreatedAt.LocalDateTime:g} · {SubmissionStateText(submission.State)}", 13, Muted));
			card.Click += (_, _) => ShowSubmission(submission);
			_body.AddView(card, new LinearLayout.LayoutParams(-1, -2) { BottomMargin = Dp(12) });
		}
		foreach (var transaction in transactions.OrderByDescending(t => t.FirstSeen))
		{
			var tx = transaction;
			var card = Column();
			card.Background = Rounded(Surface);
			card.SetPadding(Dp(16), Dp(16), Dp(16), Dp(16));
			card.AddView(Text($"{(tx.IsOwnCoinjoin() ? "◈  CoinJoin" : tx.Amount > Money.Zero ? "↓  Received" : "↑  Sent")}    {tx.Amount.ToString(false, false)} BTC", 17, tx.Amount > Money.Zero ? Accent : Color.White, true));
			card.AddView(Text($"{tx.FirstSeen.LocalDateTime:g} · {TransactionState(tx)}", 13, Muted));
			card.Click += (_, _) => ShowTransaction(tx);
			_body.AddView(card, new LinearLayout.LayoutParams(-1, -2) { BottomMargin = Dp(12) });
		}
	});
	private static string SubmissionTitle(PaymentOperation operation) => operation switch { PaymentOperation.Cancel => "Cancellation", PaymentOperation.SpeedUp => "Speed-up", _ => "Sent" };
	private static string SubmissionStateText(SubmissionState state) => state == SubmissionState.Uncertain ? "Checking submission" : state.ToString();
	private string TransactionState(TransactionSummary tx) => tx.Transaction.Confirmed ? "Confirmed"
		: Session!.SubmissionHistory.FirstOrDefault(s => s.TransactionId == tx.GetHash().ToString()) is { } submission ? SubmissionStateText(submission.State) : "Pending";
	private void ShowSubmission(SubmissionDetails submission)
	{
		Screen(SubmissionTitle(submission.Operation), "history", ShowHistory);
		AddText(Money.Satoshis(submission.AmountSatoshis).ToString(false, false) + " BTC", 30, Color.White, true);
		AddText(SubmissionStateText(submission.State), 16, Accent);
		AddText(submission.TransactionId, 14, Muted).SetTextIsSelectable(true);
		AddText($"Fee: {submission.FeeSatoshis:N0} sats", 16, Muted);
		AddText($"Total: {Money.Satoshis(checked(submission.AmountSatoshis + submission.FeeSatoshis)).ToString(false, false)} BTC", 16, Muted);
		foreach (var output in submission.Outputs.Where(o => o.IsRecipient || !o.IsWalletOutput)) { AddText($"{Money.Satoshis(output.AmountSatoshis).ToString(false, false)} BTC\n{output.Address ?? output.ScriptHex}", 13, Muted); }
		AddButton("Copy transaction ID", () => Copy(submission.TransactionId));
	}

	private void ShowTransaction(TransactionSummary tx)
	{
		Screen("Transaction details", "history", ShowHistory);
		AddText(tx.Amount.ToString(false, false) + " BTC", 30, Color.White, true);
		AddText(TransactionState(tx), 16, Accent);
		Gap(20);
		AddText("TRANSACTION ID", 12, Muted);
		AddText(tx.GetHash().ToString(), 14, Color.White).SetTextIsSelectable(true);
		AddText($"Fee: {tx.GetFee()?.Satoshi.ToString("N0", CultureInfo.InvariantCulture) ?? "Unknown"} sats", 16, Muted);
		AddText("Label: " + tx.Labels, 16, Muted);
		foreach (var output in tx.Transaction.Transaction.Outputs) { AddText($"{output.Value.ToString(false, false)} BTC\n{output.ScriptPubKey.GetDestinationAddress(Session!.Global.Network)}", 13, Muted); }
		AddButton("Copy transaction ID", () => Copy(tx.GetHash().ToString()));
		if (!tx.Transaction.Confirmed && !tx.IsOwnCoinjoin())
		{
			if (tx.Transaction.IsSpeedupable(Session!.Current!.KeyManager)) { AddButton("Speed up", () => ShowReplacement(tx, PaymentOperation.SpeedUp), false); }
			if (tx.Transaction.IsCancellable(Session!.Current!.KeyManager)) { AddButton("Cancel payment", () => ShowReplacement(tx, PaymentOperation.Cancel), false); }
		}
	}

	private void ShowReplacement(TransactionSummary transaction, PaymentOperation operation)
	{
		Screen(operation == PaymentOperation.SpeedUp ? "Speed up" : "Cancel payment", "replacement", () => ShowTransaction(transaction));
		EditText? fee = operation == PaymentOperation.SpeedUp ? Field("New fee rate in sat/vB") : null;
		if (fee is not null) { fee.InputType = InputTypes.ClassNumber | InputTypes.NumberFlagDecimal; fee.Text = "5"; }
		AddButton("Review", () => Work(async () =>
		{
			FeeRate? rate = null;
			if (fee is not null)
			{
				if (!decimal.TryParse(fee.Text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value) || value is < 1 or > 10000) { throw new FormatException("Use a fee rate between 1 and 10,000 sat/vB."); }
				rate = new FeeRate(value);
			}
			using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
			var session = Session ?? throw new InvalidOperationException("Reconnect the wallet first.");
			var proposal = await Task.Run(() => session.PrepareReplacementAsync(transaction.GetHash().ToString(), operation, rate, timeout.Token), timeout.Token);
			Screen(operation == PaymentOperation.SpeedUp ? "Review speed-up" : "Review cancellation", "review", () => ShowTransaction(transaction));
			foreach (var output in proposal.Outputs.Where(o => o.IsRecipient || !o.IsWalletOutput)) { AddText(Money.Satoshis(output.AmountSatoshis).ToString(false, false) + " BTC", 24, Color.White, true); AddText(output.Address ?? output.ScriptHex, 14, Muted).SetTextIsSelectable(true); }
			if (proposal.AmountSatoshis == 0) { AddText(operation == PaymentOperation.Cancel ? "Return pending funds to this wallet" : "Add a transaction to accelerate confirmation", 22, Color.White, true); }
			AddText($"Network fee   {proposal.FeeSatoshis:N0} sats", 18, Muted);
			AddText($"Total   {Money.Satoshis(proposal.TotalSatoshis).ToString(false, false)} BTC", 18, Color.White);
			AddAuthorization(operation == PaymentOperation.SpeedUp ? "Confirm speed-up" : "Confirm cancellation", "Authorize transaction replacement", async secret =>
			{
				using var submitTimeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
				var receipt = await Task.Run(() => session.ConfirmAsync(proposal.Id, secret, submitTimeout.Token), submitTimeout.Token);
				Screen("Submission pending", "sent", ShowHistory);
				AddText("◷", 72, Accent, true);
				AddText(receipt.TransactionId, 14, Muted).SetTextIsSelectable(true);
				AddButton("Transaction history", ShowHistory);
			});
		}));
	}

	private void ShowPrivacy()
	{
		Screen("Privacy", "privacy", ShowHome);
		var ring = new PrivacyRing(this) { Progress = Session!.Current!.GetPrivacyPercentage(), ContentDescription = $"Wallet privacy {Session.Current.GetPrivacyPercentage():N0} percent" };
		_body.AddView(ring, new LinearLayout.LayoutParams(-1, Dp(180)));
		AddText($"{Session.Current.GetPrivacyPercentage()}% private", 26, Color.White, true);
		AddText(Session.Settings.Coordinator.Length == 0 ? "Set a coordinator in Settings to use CoinJoin." : Session.Settings.Coordinator, 14, Muted);
		AddText("CoinJoin uses signing keys while it runs. Keep Wasabi open or its background notification active until the round finishes.", 14, Muted);
		var state = AddText(Session.CoinJoinStatus, 16, Accent);
		_updateScreen = () => { state.Text = Session?.CoinJoinStatus ?? "Stopped"; ring.Progress = Session?.Current?.GetPrivacyPercentage() ?? 0; };
		AddButton(Session.IsMixing ? "Stop CoinJoin safely" : "Start CoinJoin", () => Work(async () =>
		{
			if (Session!.IsMixing)
			{
				using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(10));
				await Session.StopCoinJoinAsync(timeout.Token);
			}
			else
			{
				ShowCoinJoinAuthorization();
				return;
			}
			ShowPrivacy();
		}));
		AddButton("Coordinator settings", ShowSettings, false);
	}

	private void ShowCoinJoinAuthorization()
	{
		Screen("Authorize CoinJoin", "privacy", ShowPrivacy);
		AddText("Coordinator: " + Session!.Settings.Coordinator, 16, Muted);
		AddAuthorization("Start CoinJoin", "Authorize CoinJoin", async secret =>
		{
			await Session!.StartCoinJoinAsync(secret);
			ShowPrivacy();
		});
	}

	private void ShowBackup()
	{
		Screen("Wallet backup", "backup", ShowHome);
		AddText("Your recovery words and original password restore your wallet. Wasabi does not save the recovery words.", 16, Muted);
		AddButton("Verify recovery words", () =>
		{
			Screen("Verify backup", "backup", ShowBackup);
			var words = Field("Recovery words");
			Multiline(words);
			var password = Field("Original password / passphrase", true);
			AddButton("Verify", () => Work(async () =>
			{
				var mnemonic = new Mnemonic(words.Text!);
				if (!mnemonic.IsValidChecksum) { throw new FormatException("Invalid recovery words."); }
				var original = Session!.Current!.KeyManager;
				var secret = password.Text ?? "";
				var recovered = await Task.Run(() => KeyManager.Recover(mnemonic, secret, Session.Global.Network, original.SegwitAccountKeyPath, original.TaprootAccountKeyPath));
				words.Text = "";
				password.Text = "";
				try
				{
					if (recovered.SegwitExtPubKey != original.SegwitExtPubKey || original.TaprootExtPubKey is not null && recovered.TaprootExtPubKey != original.TaprootExtPubKey) { throw new InvalidOperationException("These words and passphrase belong to a different wallet."); }
				}
				finally { recovered.ClearCachedSecrets(); }
				Alert("Backup verified.");
			}));
		});
		AddButton("Export encrypted wallet file", () =>
		{
			Screen("Authorize backup", "backup", ShowBackup);
			var name = Session!.Current!.WalletName;
			AddAuthorization("Export encrypted backup", "Authorize wallet backup", async secret =>
			{
				_exportPayload = await Session!.ExportEncryptedBackupAsync(secret, _activityLifetime.Token);
				_externalFlow = true;
				var intent = new Intent(Intent.ActionCreateDocument).AddCategory(Intent.CategoryOpenable)!.SetType("application/json")!.PutExtra(Intent.ExtraTitle, name + ".json");
				StartActivityForResult(intent, 4);
			});
		}, false);
	}

	private void ShowSettings()
	{
		Screen("Settings", "settings", Session?.IsUnlocked is true && !_uiLocked ? ShowHome : ShowWallets);
		var settings = WalletRuntime.ReadSettings(this);
		AddText("BITCOIN NETWORK", 12, Muted);
		var network = new Spinner(this);
		var names = AppIdentity.IsPersonal ? new[] { "main", "testnet", "signet" } : new[] { "testnet", "signet" };
		network.Adapter = new ArrayAdapter<string>(this, global::Android.Resource.Layout.SimpleSpinnerDropDownItem, names);
		network.SetSelection(Array.IndexOf(names, settings.Network) is var index && index >= 0 ? index : 0);
		_body.AddView(network, new LinearLayout.LayoutParams(-1, Dp(56)));
		var coordinator = Field("Coordinator URL (optional)");
		coordinator.Text = settings.Coordinator;
		var identifier = Field("Coordinator identifier");
		identifier.Text = settings.CoordinatorIdentifier;
		AddText("Coordinator details must come from its operator. The network has separate wallets and history.", 14, Muted);
		AddButton("Save and reconnect", () => Work(async () =>
		{
			if (Session?.IsMixing is true) { throw new InvalidOperationException("Stop CoinJoin before changing settings."); }
			var updated = settings with
			{
				Network = names[network.SelectedItemPosition],
				Coordinator = coordinator.Text?.Trim() ?? "",
				CoordinatorIdentifier = identifier.Text?.Trim() ?? ""
			};
			updated.Validate();
			updated.Save(WalletRuntime.DataDir(this));
			await WalletRuntime.StopAsync();
			_uiLocked = true;
			StartWalletService();
			ShowWallets();
		}));
		Gap(24);
		var version = PackageManager!.GetPackageInfo(PackageName!, global::Android.Content.PM.PackageInfoFlags.MetaData)!.VersionName;
		AddText((AppIdentity.IsPersonal ? "Wasabi Wallet for Android · " : "Wasabi Wallet Test · ") + version, 14, Muted);
		AddText("Tor 0.4.9.13 · Bitcoin keys and signing use the shared Wasabi engine.", 13, Muted);
		AddButton("Open-source licenses", () => Work(async () =>
		{
			Screen("Licenses", "licenses", ShowSettings);
			foreach (var asset in new[] { "Wasabi", "Tor" })
			{
				using var reader = new StreamReader(Assets!.Open("Legal/" + asset + ".txt"));
				AddText(await reader.ReadToEndAsync(), 12, Muted);
				Gap(24);
			}
			AddText("ZXing.Net · Apache License 2.0\nCopyright ZXing authors\nhttps://github.com/micjahn/ZXing.Net", 12, Muted);
		}), false);
	}

	private void Scan(Action<string> result)
	{
		_scanned = result;
		if (CheckSelfPermission(Manifest.Permission.Camera) != Permission.Granted) { RequestPermissions([Manifest.Permission.Camera], 3); return; }
		_externalFlow = true;
		StartActivityForResult(new Intent(this, typeof(ScannerActivity)), 3);
	}

	public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
	{
		base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
		if (requestCode == 3 && grantResults.Length > 0 && grantResults[0] == Permission.Granted && _scanned is { } action) { Scan(action); }
	}

#pragma warning disable CS0672
	protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
	{
		base.OnActivityResult(requestCode, resultCode, data);
		if (resultCode != Result.Ok)
		{
			if (requestCode == 4) { _exportPayload = null; }
			if (requestCode == 5) { var savedName = _importName; _importName = null; ShowImport(savedName); }
			return;
		}
		if (requestCode == 3 && data?.GetStringExtra("payment") is { } payment)
		{
			try
			{
				_ = PaymentRequest.Parse(payment, Session?.Global.Network ?? WalletRuntime.ReadSettings(this).GetNetwork());
				_payment = payment;
				if (!_uiLocked) { ShowSend(); } else { ShowWallets(); }
			}
			catch (Exception ex) { Alert(ex.Message); }
		}
		if (requestCode == 4 && data?.Data is { } uri && _exportPayload is { } payload)
		{
			Work(async () =>
			{
				await using var target = ContentResolver!.OpenOutputStream(uri, "wt") ?? throw new IOException("Could not open the backup destination.");
				try { await target.WriteAsync(payload); }
				finally { _exportPayload = null; }
				Alert("Encrypted wallet exported. Keep your recovery words and password too.");
			});
		}
		if (requestCode == 5 && data?.Data is { } sourceUri && _importName is { } name)
		{
			Work(async () =>
			{
				await using var source = ContentResolver!.OpenInputStream(sourceUri) ?? throw new IOException("Could not read the wallet backup.");
				using var buffer = new MemoryStream();
				var block = new byte[8192];
				int read;
				while ((read = await source.ReadAsync(block)) > 0)
				{
					if (buffer.Length + read > 4 * 1024 * 1024) { throw new FormatException("The wallet backup is too large."); }
					await buffer.WriteAsync(block.AsMemory(0, read));
				}
				var wallet = await Task.Run(() => Session!.ImportAsync(name, System.Text.Encoding.UTF8.GetString(buffer.ToArray())));
				_importName = null;
				ShowUnlock(wallet);
			});
		}
	}
#pragma warning restore CS0672

	private async void Work(Func<Task> action)
	{
		if (_busy) { return; }
		_busy = true;
		_workIndicator.Visibility = ViewStates.Visible;
		_workGeneration = _uiGeneration;
		try { await action(); }
		catch (Exception ex)
		{
			// Never put exception messages, wallet data or credentials in logcat.
			global::Android.Util.Log.Warn("WasabiWallet", "Wallet action failed: " + ex.GetType().FullName);
			if (!IsFinishing && _foreground && _workGeneration == _uiGeneration) { Alert(ex.Message); }
		}
		finally
		{
			var lockedDuringOperation = _workGeneration != _uiGeneration;
			_workGeneration = null;
			_busy = false;
			_workIndicator.Visibility = ViewStates.Gone;
			if (lockedDuringOperation || !_foreground && !_externalFlow) { LockUi(); }
		}
	}

	private void Alert(string message) => new AlertDialog.Builder(this).SetTitle("Wasabi Wallet")!.SetMessage(message)!.SetPositiveButton("OK", (_, _) => { })!.Show();
	private void Copy(string value)
	{
		var clipboard = (global::Android.Content.ClipboardManager)GetSystemService(ClipboardService)!;
		var clip = ClipData.NewPlainText("Bitcoin", value);
		if (OperatingSystem.IsAndroidVersionAtLeast(33)) { clip!.Description!.Extras = new PersistableBundle(); clip.Description.Extras.PutBoolean("android.content.extra.IS_SENSITIVE", true); }
		clipboard.PrimaryClip = clip;
		Toast.MakeText(this, "Copied", ToastLength.Short)!.Show();
	}
	private void Share(string value)
	{
		_externalFlow = true;
		StartActivity(Intent.CreateChooser(new Intent(Intent.ActionSend).SetType("text/plain")!.PutExtra(Intent.ExtraText, value), "Share Bitcoin request"));
	}
	private static LinearLayout.LayoutParams Wrap => new(-1, -2) { BottomMargin = 12 };
	private LinearLayout Column() => new(this) { Orientation = Orientation.Vertical };
	private LinearLayout Row() { var row = new LinearLayout(this) { Orientation = Orientation.Horizontal }; row.SetGravity(GravityFlags.CenterVertical); return row; }
	private LinearLayout Card()
	{
		var row = Row();
		row.SetPadding(Dp(14), Dp(10), Dp(14), Dp(10));
		row.Background = Rounded(Surface);
		return row;
	}
	private GradientDrawable Rounded(Color color)
	{
		var drawable = new GradientDrawable();
		drawable.SetColor(color);
		drawable.SetCornerRadius(Dp(18));
		return drawable;
	}
	private TextView Text(string value, float size, Color color, bool bold = false)
	{
		var text = new TextView(this) { Text = value, TextSize = size, Gravity = GravityFlags.CenterVertical };
		text.SetTextColor(color);
		if (bold) { text.SetTypeface(Typeface.Default, TypefaceStyle.Bold); }
		return text;
	}
	private TextView AddText(string value, float size, Color color, bool bold = false)
	{
		var text = Text(value, size, color, bold);
		_body.AddView(text, Wrap);
		return text;
	}
	private Button Button(string value, Action action, bool primary = true, int height = 56)
	{
		var button = new Button(this) { Text = value, TextSize = 16, Gravity = GravityFlags.Center, ContentDescription = value };
		button.SetAllCaps(false);
		button.SetMinHeight(Dp(height));
		button.SetTextColor(primary ? Background : Color.White);
		button.Background = Rounded(primary ? Accent : Surface);
		button.SetPadding(Dp(12), Dp(12), Dp(12), Dp(12));
		button.Click += (_, _) => { _lastInteraction = Stopwatch.GetTimestamp(); if (!_busy) { action(); } };
		if (value == "‹") { button.ContentDescription = "Back"; }
		return button;
	}
	private void AddButton(string value, Action action, bool primary = true) => _body.AddView(Button(value, action, primary), new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(12) });
	private EditText Field(string hint, bool secret = false, LinearLayout? parent = null)
	{
		var field = new EditText(this) { Hint = hint, TextSize = 16, InputType = secret ? InputTypes.ClassText | InputTypes.TextVariationPassword : InputTypes.ClassText | InputTypes.TextFlagNoSuggestions };
		if (OperatingSystem.IsAndroidVersionAtLeast(26)) { field.ImportantForAutofill = ImportantForAutofill.NoExcludeDescendants; }
		if (OperatingSystem.IsAndroidVersionAtLeast(26)) { field.ImeOptions |= (ImeAction)global::Android.Views.InputMethods.ImeFlags.NoPersonalizedLearning; }
		field.SetTextColor(Color.White);
		field.SetHintTextColor(Muted);
		field.SetPadding(Dp(14), Dp(12), Dp(14), Dp(12));
		field.Background = Rounded(Surface);
		field.TextChanged += (_, _) => _lastInteraction = Stopwatch.GetTimestamp();
		field.SetMinHeight(Dp(64));
		(parent ?? _body).AddView(field, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(12), BottomMargin = Dp(12) });
		return field;
	}
	private void Gap(int height) => _body.AddView(new View(this), new LinearLayout.LayoutParams(1, Dp(height)));
	private void ClearSendDraft() { _sendAddress = ""; _sendAmount = ""; _sendLabel = ""; _sendFee = ""; _sendAll = false; }
	private static void Multiline(EditText field)
	{
		field.SetMinLines(3);
		field.InputType = InputTypes.ClassText | InputTypes.TextFlagMultiLine | InputTypes.TextFlagNoSuggestions;
		field.LayoutParameters!.Height = ViewGroup.LayoutParams.WrapContent;
	}
}
