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
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Mobile;
using WalletWasabi.Wallets;
using Color = Android.Graphics.Color;
using Orientation = Android.Widget.Orientation;

namespace WalletWasabi.Android;

[Activity(Name = "io.wasabiwallet.android.MainActivity", Label = "Wasabi Wallet", Theme = "@style/WasabiTheme", MainLauncher = true, Exported = true,
	LaunchMode = LaunchMode.SingleTask, ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
[IntentFilter([Intent.ActionView], Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable], DataScheme = "bitcoin")]
public sealed class MainActivity : Activity
{
	private static readonly Color Background = Color.Rgb(17, 21, 18);
	private static readonly Color Surface = Color.Rgb(28, 34, 29);
	private static readonly Color Accent = Color.Rgb(163, 230, 53);
	private static readonly Color Muted = Color.Rgb(151, 166, 155);
	private LinearLayout _root = null!;
	private LinearLayout _body = null!;
	private TextView _status = null!;
	private Action? _back;
	private System.Threading.Timer? _refresh;
	private Action? _updateScreen;
	private string? _payment;
	private bool _foreground;
	private bool _busy;
	private bool _externalFlow;
	private bool _uiLocked = true;
	private string _screen = "wallets";
	private DateTime _lastInteraction = DateTime.UtcNow;
	private Action<string>? _scanned;
	private string? _exportPath;
	private string? _importName;
	private HashSet<OutPoint>? _selectedCoins;
	private string _sendAddress = "";
	private string _sendAmount = "";
	private string _sendLabel = "";
	private string _sendFee = "";
	private bool _sendAll;

	private WalletSession? Session => WalletRuntime.Session;
	private int Dp(float value) => (int)(value * Resources!.DisplayMetrics!.Density);

	protected override void OnCreate(Bundle? savedInstanceState)
	{
		base.OnCreate(savedInstanceState);
#if !WASABI_UI_TEST
		Window!.AddFlags(WindowManagerFlags.Secure);
#endif
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
		_lastInteraction = DateTime.UtcNow;
		if (Session is null && WalletRuntime.Error is null) { StartWalletService(); }
		if (_uiLocked && !_externalFlow) { ShowWallets(); }
		_externalFlow = false;
	}

	protected override void OnStop()
	{
		_foreground = false;
		if (!_externalFlow) { LockUi(); }
		base.OnStop();
	}

	protected override void OnDestroy()
	{
		_refresh?.Dispose();
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
		_lastInteraction = DateTime.UtcNow;
		return base.DispatchTouchEvent(e);
	}

#pragma warning disable CS0672, CA1422

	public override void OnBackPressed()
	{
		if (!_busy && _back is { } back) { back(); }
		else if (!_busy) { MoveTaskToBack(true); }
	}
#pragma warning restore CS0672, CA1422

	private void LockUi()
	{
		_uiLocked = true;
		_selectedCoins = null;
		ClearSendDraft();
		_payment = null;
		if (!_busy && Session is { IsMixing: false } session) { session.Lock(); }
		ShowWallets();
	}

	private void Refresh()
	{
		if (IsFinishing || IsDestroyed || !_foreground) { return; }
		if (!_busy && !_uiLocked && DateTime.UtcNow - _lastInteraction > TimeSpan.FromMinutes(2)) { LockUi(); }
		var session = Session;
		_status.Text = WalletRuntime.Error is { } error ? error
			: session?.SynchronizationError is { } syncError ? syncError
			: session is null || !session.IsReady ? $"●  Tor {WalletRuntime.Bootstrap}%"
			: session.Global.GetPeerCount() == 0 ? "●  Connecting to Bitcoin peers"
			: session.IsSynchronized ? $"●  Connected · {session.Global.Network.Name}"
			: $"●  Synchronizing · {session.Global.FilterHeaders.TipHeight} blocks · {session.Global.GetPeerCount()} peers";
		if (_screen == "wallets" && _body.Tag?.ToString() != WalletListSignature()) { ShowWallets(); }
		_updateScreen?.Invoke();
	}

	private string WalletListSignature() => Session is { } session ? string.Join('|', session.Global.WalletManager.GetWallets().Select(w => w.WalletName)) + "/ready" : "loading";

	private void Screen(string title, string screen, Action? back = null)
	{
		_screen = screen;
		_back = back;
		_updateScreen = null;
		_root = new LinearLayout(this) { Orientation = Orientation.Vertical };
		_root.SetBackgroundColor(Background);
		_root.SetPadding(Dp(24), Dp(12), Dp(24), Dp(12));
		var header = Row();
		if (back is not null) { header.AddView(Button("‹", back, false, 48), new LinearLayout.LayoutParams(Dp(48), Dp(48))); }
		var heading = Text(title, 24, Color.White, true);
		header.AddView(heading, new LinearLayout.LayoutParams(0, Dp(64), 1));
		_root.AddView(header);
		var scroll = new ScrollView(this) { FillViewport = true };
		_body = Column();
		_body.SetPadding(0, Dp(8), 0, Dp(24));
		scroll.AddView(_body);
		_root.AddView(scroll, new LinearLayout.LayoutParams(-1, 0, 1));
		_status = Text("●  Connecting privately", 12, Muted);
		_status.SetPadding(0, Dp(12), 0, Dp(12));
		_root.AddView(_status);
		// Android's inset handling replaces the padding on the view that consumes
		// system windows. Keep content spacing on a separate inner container.
		var safeArea = new FrameLayout(this);
		safeArea.SetFitsSystemWindows(true);
		safeArea.SetBackgroundColor(Background);
		safeArea.AddView(_root, new FrameLayout.LayoutParams(-1, -1));
		SetContentView(safeArea);
	}

	private void ShowWallets()
	{
		Screen("wasabi", "wallets");
		_body.Tag = WalletListSignature();
		var brand = new ImageView(this);
		brand.SetImageResource(Resource.Drawable.wasabi_icon);
		_body.AddView(brand, new LinearLayout.LayoutParams(Dp(88), Dp(88)) { Gravity = GravityFlags.CenterHorizontal, BottomMargin = Dp(20) });
		AddText("Bitcoin.\nPrivately yours.", 36, Color.White, true);
		Gap(24);
		if (Session is { } session)
		{
			foreach (var wallet in session.Global.WalletManager.GetWallets())
			{
				var target = wallet;
				AddButton("◈   " + wallet.WalletName + "   ›", () => ShowUnlock(target), false);
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

	private void ShowUnlock(Wallet wallet)
	{
		Screen(wallet.WalletName, "unlock", ShowWallets);
		AddText("Unlock your wallet", 28, Color.White, true);
		var password = Field("Wallet password", true);
		AddButton("Unlock", () => Work(async () =>
		{
			await Task.Run(() => Session!.Unlock(wallet, password.Text ?? ""));
			password.Text = "";
			_uiLocked = false;
			if (_payment is not null) { ShowSend(); } else { ShowHome(); }
		}));
	}

	private void ShowImport()
	{
		if (Session is null) { Alert("Starting the wallet engine. Try again in a moment."); return; }
		Screen("Import wallet", "import", ShowWallets);
		var name = Field("Wallet name");
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

	private void ShowCreate(bool recover)
	{
		if (Session is null) { Alert("Starting the wallet engine. Try again in a moment."); return; }
		Screen(recover ? "Recover wallet" : "Create wallet", "create", ShowWallets);
		var name = Field("Wallet name");
		var password = Field(recover ? "Original Wasabi password / BIP39 passphrase" : "Wallet password", true);
		EditText? confirm = recover ? null : Field("Repeat password", true);
		EditText? words = recover ? Field("Recovery words") : null;
		if (words is not null) { Multiline(words); }
		AddText(recover ? "Use the same password that created the wallet. A different passphrase opens a different wallet." : "This password is also your recovery passphrase. Keep it with your recovery words.", 14, Muted);
		AddButton(recover ? "Recover" : "Continue", () => Work(async () =>
		{
			var walletName = name.Text?.Trim() ?? "";
			var secret = password.Text ?? "";
			if (Session!.Global.WalletManager.ValidateWalletName(walletName) is { } error) { throw new ArgumentException(error.Message); }
			if (!recover && (secret.Length < 8 || secret != confirm!.Text)) { throw new ArgumentException("Use at least 8 characters and repeat the same password."); }
			var mnemonic = recover ? new Mnemonic(words!.Text!.Trim()) : new Mnemonic(Wordlist.English, WordCount.Twelve);
			if (!mnemonic.IsValidChecksum) { throw new FormatException("Invalid recovery words."); }
			password.Text = "";
			if (confirm is not null) { confirm.Text = ""; }
			if (words is not null) { words.Text = ""; }
			if (recover)
			{
				await Task.Run(() => Session.CreateAsync(walletName, secret, mnemonic, true));
				_uiLocked = false;
				ShowHome();
			}
			else { ShowRecoveryWords(walletName, secret, mnemonic); }
		}));
	}

	private void ShowRecoveryWords(string name, string password, Mnemonic mnemonic)
	{
		Screen("Recovery words", "backup", ShowWallets);
		AddText("Write these down", 28, Color.White, true);
		AddText("Keep the words and your password offline. Anyone with both can spend your bitcoin.", 14, Muted);
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
		AddButton("I wrote them down", () => ShowConfirmWords(name, password, mnemonic));
	}

	private void ShowConfirmWords(string name, string password, Mnemonic mnemonic)
	{
		Screen("Check your backup", "backup", ShowWallets);
		var positions = new HashSet<int>();
		while (positions.Count < 3) { positions.Add(RandomNumberGenerator.GetInt32(mnemonic.Words.Length)); }
		var fields = positions.Order().Select(i => (Index: i, Field: Field($"Word {i + 1}"))).ToArray();
		AddButton("Create wallet", () => Work(async () =>
		{
			if (fields.Any(f => !string.Equals(f.Field.Text?.Trim(), mnemonic.Words[f.Index], StringComparison.OrdinalIgnoreCase))) { throw new ArgumentException("Check the recovery words and try again."); }
			await Task.Run(() => Session!.CreateAsync(name, password, mnemonic, false));
			_uiLocked = false;
			ShowHome();
		}));
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
		actions.AddView(Button("↑  Send", ShowSend), new LinearLayout.LayoutParams(0, Dp(60), 1) { MarginEnd = Dp(10) });
		actions.AddView(Button("↓  Receive", ShowReceive, false), new LinearLayout.LayoutParams(0, Dp(60), 1));
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
			var preview = await Task.Run(() => Session.Preview(request, paymentAmount, new FeeRate(rate), _selectedCoins?.ToArray(), all.Checked));
			Screen("Review transaction", "review", ShowSend);
			var sent = Money.Satoshis(preview.Transaction.Transaction.Outputs.Where(o => o.ScriptPubKey == request.Address.ScriptPubKey).Sum(o => o.Value.Satoshi));
			AddText(sent.ToString(false, false) + " BTC", 32, Color.White, true);
			AddText("TO", 12, Muted);
			AddText(request.Address.ToString(), 15, Color.White).SetTextIsSelectable(true);
			Gap(16);
			AddText($"Network fee   {preview.Fee.Satoshi:N0} sats", 16, Muted);
			AddText($"Total   {(sent + preview.Fee).ToString(false, false)} BTC", 18, Color.White);
			AddText($"{preview.SpentCoins.Count()} inputs · {Session.Global.Network.Name}", 14, Muted);
			var password = Field("Confirm wallet password", true);
			AddButton("Confirm and send", () => Work(async () =>
			{
				var secret = password.Text ?? "";
				password.Text = "";
				using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
				var transactionId = await Task.Run(() => Session!.SendAsync(preview, secret, timeout.Token));
				Screen("Transaction sent", "sent", ShowHome);
				AddText("✓", 72, Accent, true);
				AddText("Waiting for confirmation", 24, Color.White, true);
				AddText(transactionId, 14, Muted).SetTextIsSelectable(true);
				AddButton("Copy transaction ID", () => Copy(transactionId), false);
				AddButton("Done", ShowHome);
			}));
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
		var transactions = await Session!.Current!.BuildHistorySummaryAsync(true);
		Screen("Transactions", "history", ShowHome);
		if (transactions.Count == 0) { AddText("Your transactions will appear here", 20, Muted); }
		foreach (var transaction in transactions.OrderByDescending(t => t.FirstSeen))
		{
			var tx = transaction;
			var card = Column();
			card.Background = Rounded(Surface);
			card.SetPadding(Dp(16), Dp(16), Dp(16), Dp(16));
			card.AddView(Text($"{(tx.IsOwnCoinjoin() ? "◈  CoinJoin" : tx.Amount > Money.Zero ? "↓  Received" : "↑  Sent")}    {tx.Amount.ToString(false, false)} BTC", 17, tx.Amount > Money.Zero ? Accent : Color.White, true));
			card.AddView(Text($"{tx.FirstSeen.LocalDateTime:g} · {(tx.Transaction.Confirmed ? "Confirmed" : "Pending")}", 13, Muted));
			card.Click += (_, _) => ShowTransaction(tx);
			_body.AddView(card, new LinearLayout.LayoutParams(-1, -2) { BottomMargin = Dp(12) });
		}
	});

	private void ShowTransaction(TransactionSummary tx)
	{
		Screen("Transaction details", "history", ShowHistory);
		AddText(tx.Amount.ToString(false, false) + " BTC", 30, Color.White, true);
		AddText(tx.Transaction.Confirmed ? "Confirmed" : "Pending", 16, Accent);
		Gap(20);
		AddText("TRANSACTION ID", 12, Muted);
		AddText(tx.GetHash().ToString(), 14, Color.White).SetTextIsSelectable(true);
		AddText($"Fee: {tx.GetFee()?.Satoshi.ToString("N0", CultureInfo.InvariantCulture) ?? "Unknown"} sats", 16, Muted);
		AddText("Label: " + tx.Labels, 16, Muted);
		foreach (var output in tx.Transaction.Transaction.Outputs) { AddText($"{output.Value.ToString(false, false)} BTC\n{output.ScriptPubKey.GetDestinationAddress(Session!.Global.Network)}", 13, Muted); }
		AddButton("Copy transaction ID", () => Copy(tx.GetHash().ToString()));
	}

	private void ShowPrivacy()
	{
		Screen("Privacy", "privacy", ShowHome);
		var ring = new PrivacyRing(this) { Progress = Session!.Current!.GetPrivacyPercentage() };
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
			else { Session.StartCoinJoin(); }
			ShowPrivacy();
		}));
		AddButton("Coordinator settings", ShowSettings, false);
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
				if (recovered.SegwitExtPubKey != original.SegwitExtPubKey || original.TaprootExtPubKey is not null && recovered.TaprootExtPubKey != original.TaprootExtPubKey) { throw new InvalidOperationException("These words and passphrase belong to a different wallet."); }
				Alert("Backup verified.");
			}));
		});
		AddButton("Export encrypted wallet file", () =>
		{
			Session!.Current!.KeyManager.ToFile();
			_exportPath = Session.Current.KeyManager.FilePath;
			_externalFlow = true;
			var intent = new Intent(Intent.ActionCreateDocument).AddCategory(Intent.CategoryOpenable)!.SetType("application/json")!.PutExtra(Intent.ExtraTitle, Session.Current.WalletName + ".json");
			StartActivityForResult(intent, 4);
		}, false);
	}

	private void ShowSettings()
	{
		Screen("Settings", "settings", Session?.IsUnlocked is true && !_uiLocked ? ShowHome : ShowWallets);
		var settings = MobileSettings.Load(WalletRuntime.DataDir(this));
		AddText("BITCOIN NETWORK", 12, Muted);
		var network = new Spinner(this);
		var names = new[] { "main", "testnet", "signet" };
		network.Adapter = new ArrayAdapter<string>(this, global::Android.Resource.Layout.SimpleSpinnerDropDownItem, names);
		network.SetSelection(Array.IndexOf(names, settings.Network) is var index && index >= 0 ? index : 0);
		_body.AddView(network, new LinearLayout.LayoutParams(-1, Dp(56)));
		var coordinator = Field("Coordinator URL (optional)");
		coordinator.Text = settings.Coordinator;
		var identifier = Field("Coordinator identifier");
		identifier.Text = settings.CoordinatorIdentifier;
		AddText("Coordinator details must come from its operator. The network has separate wallets and history.", 14, Muted);
		Gap(16);
		AddText("PERSONAL BITCOIN NODE", 12, Muted);
		var node = Field("RPC URL (optional onion or localhost)");
		node.Text = settings.BitcoinRpcUri;
		var credentials = Field("RPC user:password", true);
		credentials.Text = settings.BitcoinRpcCredentials;
		AddButton("Save and reconnect", () => Work(async () =>
		{
			if (Session?.IsMixing is true) { throw new InvalidOperationException("Stop CoinJoin before changing settings."); }
			var updated = settings with
			{
				Network = names[network.SelectedItemPosition],
				Coordinator = coordinator.Text?.Trim() ?? "",
				CoordinatorIdentifier = identifier.Text?.Trim() ?? "",
				BitcoinRpcUri = node.Text?.Trim() ?? "",
				BitcoinRpcCredentials = credentials.Text ?? ""
			};
			updated.Save(WalletRuntime.DataDir(this));
			credentials.Text = "";
			await WalletRuntime.StopAsync();
			_uiLocked = true;
			StartWalletService();
			ShowWallets();
		}));
		Gap(24);
		AddText("Wasabi Wallet for Android · 0.1.0", 14, Muted);
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
		if (resultCode != Result.Ok) { return; }
		if (requestCode == 3 && data?.GetStringExtra("payment") is { } payment) { try { _scanned?.Invoke(payment); } catch (Exception ex) { Alert(ex.Message); } }
		if (requestCode == 4 && data?.Data is { } uri && _exportPath is { } path)
		{
			Work(async () =>
			{
				await using var source = File.OpenRead(path);
				await using var target = ContentResolver!.OpenOutputStream(uri, "wt") ?? throw new IOException("Could not open the backup destination.");
				await source.CopyToAsync(target);
				_exportPath = null;
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
		try { await action(); }
		catch (Exception ex) { if (!IsFinishing) { Alert(ex.Message); } }
		finally { _busy = false; if (!_foreground && !_externalFlow) { LockUi(); } }
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
		button.Click += (_, _) => { _lastInteraction = DateTime.UtcNow; if (!_busy) { action(); } };
		return button;
	}
	private void AddButton(string value, Action action, bool primary = true) => _body.AddView(Button(value, action, primary), new LinearLayout.LayoutParams(-1, Dp(58)) { TopMargin = Dp(12) });
	private EditText Field(string hint, bool secret = false)
	{
		var field = new EditText(this) { Hint = hint, TextSize = 16, InputType = secret ? InputTypes.ClassText | InputTypes.TextVariationPassword : InputTypes.ClassText | InputTypes.TextFlagNoSuggestions };
		if (OperatingSystem.IsAndroidVersionAtLeast(26)) { field.ImportantForAutofill = ImportantForAutofill.NoExcludeDescendants; }
		if (OperatingSystem.IsAndroidVersionAtLeast(26)) { field.ImeOptions |= (ImeAction)global::Android.Views.InputMethods.ImeFlags.NoPersonalizedLearning; }
		field.SetTextColor(Color.White);
		field.SetHintTextColor(Muted);
		field.SetPadding(Dp(14), Dp(12), Dp(14), Dp(12));
		field.Background = Rounded(Surface);
		field.TextChanged += (_, _) => _lastInteraction = DateTime.UtcNow;
		_body.AddView(field, new LinearLayout.LayoutParams(-1, -2) { TopMargin = Dp(12), BottomMargin = Dp(12), Height = Dp(64) });
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
