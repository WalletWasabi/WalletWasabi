package io.wasabiwallet.android.tests;

import android.app.Activity;
import android.app.Instrumentation;
import android.app.Application;
import android.content.Intent;
import android.content.pm.ApplicationInfo;
import android.content.pm.PackageInfo;
import android.os.Build;
import android.os.Bundle;
import android.view.View;
import android.view.ViewGroup;
import android.view.WindowManager;
import android.widget.Button;
import android.widget.EditText;
import android.widget.TextView;
import android.accessibilityservice.AccessibilityService;
import java.io.File;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.List;
import java.util.concurrent.atomic.AtomicReference;
import java.util.concurrent.atomic.AtomicBoolean;

// Pure Java: no second Mono runtime or copied wallet assemblies enter the app.
// This APK is co-signed for testing, kept out of delivery, and emulator-only.
public final class ReleaseUiInstrumentation extends Instrumentation {
    private static final String PACKAGE = "io.wasabiwallet.android.personal";
    private static final String PASSWORD = "public native Android test passphrase";
    private static final String WORDS = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";
    private Activity activity;
    private Bundle arguments;
    private final AtomicBoolean mainStopped = new AtomicBoolean();

    @Override public void onCreate(Bundle values) { super.onCreate(values); arguments = values; start(); }
    @Override public void onStart() {
        Bundle result = new Bundle();
        try {
            check(Build.HARDWARE.contains("ranchu") || Build.HARDWARE.contains("goldfish"), "Emulator-only harness");
            activity = startActivitySync(mainIntent());
            activity.getApplication().registerActivityLifecycleCallbacks(new Application.ActivityLifecycleCallbacks() {
                public void onActivityCreated(Activity current, Bundle state) { }
                public void onActivityStarted(Activity current) { }
                public void onActivityResumed(Activity current) { }
                public void onActivityPaused(Activity current) { }
                public void onActivityStopped(Activity current) { if (current == activity) mainStopped.set(true); }
                public void onActivitySaveInstanceState(Activity current, Bundle state) { }
                public void onActivityDestroyed(Activity current) { }
            });
            waitFor(() -> has("Recover a wallet"), 20000, "Main screen");
            if (Build.VERSION.SDK_INT >= 33 && accessible("Allow Wasabi Wallet to send you notifications?", false)) {
                check(accessible("Allow", true), "Normal notification permission prompt");
                Thread.sleep(1000);
            }
            boolean screenshotsAllowed = arguments.getString("screenshot-policy", "blocked").equals("allow");
            check(((activity.getWindow().getAttributes().flags & WindowManager.LayoutParams.FLAG_SECURE) == 0) == screenshotsAllowed, "Actual Release screenshot policy");
            if (screenshotsAllowed) {
                android.graphics.Bitmap screenshot = getUiAutomation().takeScreenshot();
                check(screenshot != null, "Actual Release screenshot capture");
                screenshot.recycle();
            }
            PackageInfo info = getTargetContext().getPackageManager().getPackageInfo(PACKAGE, 0);
            check((info.applicationInfo.flags & ApplicationInfo.FLAG_DEBUGGABLE) == 0, "Actual APK is not debuggable");
            check((info.applicationInfo.flags & ApplicationInfo.FLAG_ALLOW_BACKUP) == 0, "Actual APK disables backup");
            check(info.applicationInfo.targetSdkVersion == 36, "Actual target SDK");
            String mode = arguments.getString("mode", "security");
            if (mode.equals("setup-rpc")) setupRpc();
            else if (mode.equals("settings")) simplifiedSettings();
            else if (mode.equals("onboarding")) onboarding();
            else if (mode.equals("durable-create")) durableCreate();
            else if (mode.equals("durable-reopen")) durableReopen();
            else if (mode.equals("wallet")) wallet();
            else if (mode.equals("resume")) resume();
            else if (mode.equals("late-unlock")) verifyLateUnlockRemainsLocked();
            else if (mode.equals("lost-device-key")) lostDeviceKey();
            else check(mode.equals("security"), "Known test mode");
            result.putString("stream", "PASS: actual Release " + mode + " UI\n");
            result.putInt("api", Build.VERSION.SDK_INT);
            result.putString("architecture", Build.SUPPORTED_ABIS[0]);
            result.putString("pageSize", Long.toString(android.system.Os.sysconf(android.system.OsConstants._SC_PAGESIZE)));
            result.putInt("versionCode", info.versionCode);
            finish(Activity.RESULT_OK, result);
        } catch (Throwable failure) {
            result.putString("stream", "FAIL: " + android.util.Log.getStackTraceString(failure) + "\n");
            finish(Activity.RESULT_CANCELED, result);
        }
    }

    private Intent mainIntent() { return new Intent().setClassName(PACKAGE, "io.wasabiwallet.android.MainActivity").addFlags(Intent.FLAG_ACTIVITY_NEW_TASK); }
    private String fieldValue(String hint) {
        for (View view : views()) if (view instanceof EditText && hint.equals(((EditText)view).getHint().toString())) return ((EditText)view).getText().toString();
        throw new IllegalStateException("Field missing: " + hint);
    }
    private void openCreateForm(String action) throws Exception {
        long deadline = android.os.SystemClock.elapsedRealtime() + 90000;
        while (true) {
            click(action);
            if (has("Wallet name") || views().stream().anyMatch(v -> v instanceof EditText && "Wallet name".contentEquals(((EditText)v).getHint()))) return;
            if (accessible("OK", true)) Thread.sleep(250);
            check(android.os.SystemClock.elapsedRealtime() < deadline, "Engine available for onboarding");
        }
    }
    private List<String> backedUpWords() {
        List<String> words = new ArrayList<>();
        for (int i=1; i<=12; i++) {
            final String prefix=i+" ";
            String displayed=texts().stream().filter(t -> t.startsWith(prefix)).findFirst().orElseThrow(() -> new IllegalStateException("Numbered recovery word missing"));
            words.add(displayed.substring(prefix.length()).trim());
        }
        return words;
    }
    private int requestedWord() {
        return Integer.parseInt(texts().stream().filter(t -> t.matches("Word [0-9]+" )).findFirst().orElseThrow(() -> new IllegalStateException("Recovery word choice missing")).split(" ")[1]);
    }
    private void onboardingPreview() throws Exception {
        android.graphics.Bitmap screenshot=getUiAutomation().takeScreenshot();
        check(screenshot!=null,"Selectable-word preview captured");
        try (java.io.FileOutputStream output=new java.io.FileOutputStream(new File(activity.getCacheDir(),"onboarding-choices.png"))) {
            check(screenshot.compress(android.graphics.Bitmap.CompressFormat.PNG,100,output),"Selectable-word preview saved");
        } finally { screenshot.recycle(); }
    }
    private void onboarding() throws Exception {
        check(getTargetContext().getPackageManager().getPackageInfo(PACKAGE,0).versionCode >= 18,"Onboarding update installed");
        status("ONBOARDING_RECOVERY_NAVIGATION_STARTED");
        Thread.sleep(2000);
        openCreateForm("Recover a wallet");
        check(fieldValue("Wallet name").equals("First Wallet"),"Recovery suggests first available name");
        field("Wallet name","Recovery navigation");
        field("Recovery words",WORDS);
        click("Continue");
        field("Original Wasabi password / BIP39 passphrase",PASSWORD);
        click("‹");
        check(fieldValue("Wallet name").equals("Recovery navigation") && fieldValue("Recovery words").equals(WORDS),"Recovery back retains name and words");
        click("Continue");
        check(fieldValue("Original Wasabi password / BIP39 passphrase").equals(PASSWORD),"Recovery back retains original password");
        sendKeyDownUpSync(android.view.KeyEvent.KEYCODE_BACK);
        waitFor(() -> views().stream().anyMatch(v -> v instanceof EditText && "Recovery words".contentEquals(((EditText)v).getHint())),10000,"System Back returns to the recovery words form");
        check(fieldValue("Recovery words").equals(WORDS),"System Back returns one recovery step");
        click("‹");
        for (String expected : new String[]{"First Wallet","Second Wallet"}) {
            status("ONBOARDING_CREATE_"+expected.toUpperCase(java.util.Locale.ROOT).replace(' ','_')+"_STARTED");
            openCreateForm("Create a wallet");
            check(fieldValue("Wallet name").equals(expected),"Sequential default wallet name");
            field("Wallet password",PASSWORD);
            field("Repeat password",PASSWORD);
            click("Continue");
            waitFor(() -> has("I wrote them down"),10000,"Recovery words displayed");
            List<String> words=backedUpWords();
            click("‹");
            check(fieldValue("Wallet name").equals(expected) && fieldValue("Wallet password").equals(PASSWORD) && fieldValue("Repeat password").equals(PASSWORD),"Creation back retains form");
            click("Continue");
            check(backedUpWords().equals(words),"Creation back preserves the exact generated seed");
            click("I wrote them down");
            check(views().stream().noneMatch(v -> v instanceof EditText),"Confirmation uses no text fields");
            int first=requestedWord();
            String correct=words.get(first-1);
            String wrong=texts().stream().filter(t -> t.matches("[a-z]+") && !t.equals(correct)).findFirst().orElseThrow(() -> new IllegalStateException("Distractor missing"));
            click(wrong);
            check(requestedWord()==first && !has("Create wallet"),"Wrong choice cannot advance or create a wallet");
            if (expected.equals("First Wallet")) onboardingPreview();
            click(correct);
            int second=requestedWord();
            click("‹");
            check(requestedWord()==first,"Confirmation Back returns to previous word");
            click(correct);
            check(requestedWord()==second,"Confirmation Back preserves challenge order");
            click(words.get(second-1));
            int third=requestedWord();
            click(words.get(third-1));
            check(has("Backup confirmed") && has("Create wallet"),"All three selected words are required");
            click("‹");
            check(requestedWord()==third && !has("Create wallet"),"Back from completion requires the last word again");
            click(words.get(third-1));
            click("Create wallet");
            waitFor(() -> has("TOTAL BALANCE"),90000,"New unfunded wallet opens");
            click("Lock wallet");
        }
        openCreateForm("Create a wallet");
        check(fieldValue("Wallet name").equals("Third Wallet"),"Third default after first and second exist");
        click("‹");
        status("ONBOARDING_NAMES_AND_SELECTABLE_WORDS_AND_BACK=PASS");
    }
    private String storageWalletName() { return arguments.getString("wallet-name", "Storage regression 19"); }
    private void durableCreate() throws Exception {
        check(getTargetContext().getPackageManager().getPackageInfo(PACKAGE,0).versionCode >= 19,"Storage update installed");
        Thread.sleep(2000);
        check(!hasPart(storageWalletName()),"Preserve an existing fixture; use durable-reopen instead");
        openCreateForm("Create a wallet");
        field("Wallet name",storageWalletName());
        field("Wallet password",PASSWORD);
        field("Repeat password",PASSWORD);
        click("Continue");
        waitFor(() -> has("I wrote them down"),10000,"Private synthetic backup displayed");
        List<String> words=backedUpWords();
        click("I wrote them down");
        for (int i=0; i<3; i++) {
            int position = requestedWord();
            click(words.get(position-1));
            if (i < 2) {
                // Let the normal runtime/synchronization refresh run. The first
                // session adoption used to lock and discard this backup flow.
                Thread.sleep(1500);
                check(!has("Recover a wallet"), "Runtime refresh preserves the active backup confirmation");
                check(requestedWord() != position, "Next distinct recovery word remains selectable");
            }
        }
        click("Create wallet");
        waitFor(() -> has("TOTAL BALANCE"),90000,"Durable creation opens the unfunded wallet without a storage error");
        check(has(storageWalletName()),"Created wallet identity");
        click("Lock wallet");
        waitFor(() -> hasPart(storageWalletName()),10000,"Saved wallet listed after locking");
        durableReopen();
        status("DURABLE_WALLET_CREATION_AND_UNLOCK=PASS");
    }
    private void durableReopen() throws Exception {
        waitFor(() -> hasPart(storageWalletName()),90000,"Saved wallet survives runtime startup");
        clickPart(storageWalletName());
        waitFor(() -> has("Unlock your wallet"),10000,"Persisted wallet unlock screen");
        field("Wallet password",PASSWORD);
        click("Unlock");
        waitFor(() -> has("TOTAL BALANCE"),90000,"Original password opens the persisted wallet");
        check(has(storageWalletName()),"Reopened wallet identity");
        click("Lock wallet");
        status("DURABLE_WALLET_REOPEN=PASS");
    }
    private void simplifiedSettings() throws Exception {
        check(has("Bitcoin.\nUnfairly private."), "Requested wallet branding");
        if (getTargetContext().getPackageManager().getPackageInfo(PACKAGE, 0).versionCode >= 14) {
            check(has("Wasabi Wallet"), "Requested full wallet title");
        }
        click("Settings");
        checkSimplifiedSettings();
        click("‹");
    }
    private void checkSimplifiedSettings() {
        check(!has("PERSONAL BITCOIN NODE"), "Personal-node section removed");
        check(!has("WALLET UNLOCKING") && !has("Enable device unlocking") && !has("Remove device unlocking"), "Device unlocking is not a settings opt-in");
        for (View view : views()) if (view instanceof EditText) {
            CharSequence hint = ((EditText)view).getHint();
            check(hint == null || !hint.toString().startsWith("RPC"), "RPC controls removed");
        }
        check(has("Save and reconnect"), "Normal network settings remain available");
    }
    private void setupRpc() throws Exception {
        click("Settings");
        field("RPC URL (optional onion or localhost)", "http://127.0.0.1:18443/");
        field("RPC user:password", "wasabiandroid:wasabi-android-regtest");
        click("Save and reconnect");
        waitFor(() -> has("Recover a wallet"), 60000, "Saved settings");
        File vault = new File(activity.getFilesDir(), "Wasabi/vault");
        File[] files = vault.listFiles();
        check(files != null && files.length > 0, "Actual app stores RPC credentials in its Keystore vault");
        for (File file : files) {
            String text = new String(read(file), StandardCharsets.UTF_8);
            check(!text.contains("wasabi-android-regtest"), "No plaintext RPC fixture credential");
        }
    }

    private void wallet() throws Exception {
        waitFor(() -> hasPart("Connected") || hasPart("Synchronizing"), 90000, "Regtest engine initialized");
        if (hasPart("Native qualification")) unlock();
        else {
            click("Recover a wallet");
            field("Wallet name", "Native qualification");
            field("Recovery words", WORDS);
            if (getTargetContext().getPackageManager().getPackageInfo(PACKAGE, 0).versionCode >= 18) click("Continue");
            field("Original Wasabi password / BIP39 passphrase", PASSWORD);
            click("Recover");
            waitFor(() -> has("TOTAL BALANCE"), 30000, "Recovery opens wallet");
        }
        waitFor(() -> hasPart("Connected"), 90000, "Recovered wallet synchronized before receive");
        if (getTargetContext().getPackageManager().getPackageInfo(PACKAGE, 0).versionCode >= 13) {
            click("Lock wallet");
            click("Settings");
            checkSimplifiedSettings();
            click("‹");
            unlock();
            check(has("TOTAL BALANCE"), "Password fallback remains available after visiting simplified settings");
        }
        double beforeFunding = balance();
        click("↓  Receive");
        field("Label (who is paying you?)", "Native release fixture");
        click("Create address");
        waitFor(() -> address() != null, 10000, "Persisted receive address");
        status("RECEIVE=" + address());
        click("‹");
        waitFor(() -> balance() >= beforeFunding + 0.999999 && hasPart("Connected"), 90000, "Funded native wallet balance and synchronization");
        click("↑  Send");
        field("Bitcoin address or payment request", arguments.getString("destination"));
        field("Amount in BTC", "0.1");
        field("Fee rate in sat/vB", "2");
        click("Review transaction");
        waitFor(() -> has("Confirm and send"), 30000, "Immutable review");
        check(has(arguments.getString("destination")) && hasPart("Network fee") && hasPart("Total"), "Exact destination, fee and total displayed");
        field("Confirm wallet password", "incorrect fixture password");
        click("Confirm and send");
        waitFor(() -> has("Incorrect wallet password."), 60000, "Wrong authorization rejected");
        click("OK");
        status("AUTH_REJECTED");
        field("Confirm wallet password", PASSWORD);
        Button send = button("Confirm and send");
        runOnMainSync(() -> { send.performClick(); send.performClick(); });
        waitFor(() -> has("Transaction sent") || has("Submission pending"), 60000, "Native payment submission");
        String id = texts().stream().filter(t -> t.matches("[0-9a-f]{64}")).findFirst().orElseThrow(() -> new IllegalStateException("Transaction ID missing"));
        status("TRANSACTION=" + id);
        click("Done");
        if (getTargetContext().getPackageManager().getPackageInfo(PACKAGE, 0).versionCode >= 14) { verifyQueuedIdleStopIsRevoked(); }
        mainStopped.set(false);
        check(getUiAutomation().performGlobalAction(AccessibilityService.GLOBAL_ACTION_HOME), "The OS accepted the Home action");
        waitFor(() -> mainStopped.get(), 30000, "The OS stopped the backgrounded main activity");
        getTargetContext().startActivity(mainIntent());
        waitFor(() -> has("Recover a wallet"), 10000, "Backgrounding locks the interface");
        check(!has("TOTAL BALANCE"), "Balance is concealed after background lock");
        unlock();
        if (Boolean.parseBoolean(arguments.getString("inactivity", "false"))) {
            Thread.sleep(123000);
            waitFor(() -> has("Recover a wallet"), 10000, "Two-minute inactivity lock");
        }
        verifyLateUnlockRemainsLocked();
    }

    private void verifyLateUnlockRemainsLocked() throws Exception {
        // Exercise background/resume while the real managed password operation
        // has queued an asynchronous completion. No wallet test hooks are added.
        for (int attempt = 0; attempt < 3; attempt++) {
            openPasswordUnlock();
            field("Wallet password", PASSWORD);
            Button unlock = button("Unlock");
            runOnMainSync(() -> {
                unlock.performClick();
                callActivityOnStop(activity);
                callActivityOnResume(activity);
            });
            Thread.sleep(3000);
            check(has("Recover a wallet") && !has("TOTAL BALANCE"), "A late unlock cannot reopen a background-locked interface");
        }
        status("LATE_UNLOCK_LOCKED");
    }

    private void verifyQueuedIdleStopIsRevoked() throws Exception {
        // Cover more than two 30-second service timer periods. Briefly hold the
        // real main-thread queue while backgrounded, then resume before queued
        // decisions can execute. This exercises the actual Release service.
        for (int attempt = 0; attempt < 16; attempt++) {
            runOnMainSync(() -> {
                callActivityOnStop(activity);
                try { Thread.sleep(2250); } catch (InterruptedException error) { throw new RuntimeException(error); }
                callActivityOnResume(activity);
            });
            Thread.sleep(1750);
            check(hasPart("Native qualification") && hasPart("Connected"), "Returning before a queued idle stop preserves the active wallet runtime at attempt " + attempt + ": " + accessibleMessages());
        }
        status("QUEUED_IDLE_STOP_RESUME_PRESERVED");
    }

    private void resume() throws Exception {
        // A saved wallet is listed while its runtime is still initializing.
        // Match wallet()'s startup gate before measuring password authorization.
        long startup = android.os.SystemClock.elapsedRealtime();
        waitFor(() -> hasPart("Connected") || hasPart("Synchronizing"), 90000, "Restarted engine initialized");
        status("ENGINE_READY_AFTER_MS=" + (android.os.SystemClock.elapsedRealtime() - startup));
        unlock();
        waitFor(() -> hasPart("Connected"), 90000, "Update/restart resumes synchronization with the saved regtest backend");
        File journal = new File(activity.getFilesDir(), "Wasabi/submissions-RegTest.json");
        String bytes = new String(read(journal), StandardCharsets.UTF_8);
        check(bytes.contains(arguments.getString("transaction")), "Pending transaction journal survives update");
        if (getTargetContext().getPackageManager().getPackageInfo(PACKAGE, 0).versionCode >= 17) {
            // UI readiness can precede the next serialized reconciliation tick.
            // Observe the durable owner before finish() tears down the runtime.
            File walletFile = new File(activity.getFilesDir(), "Wasabi/Wallets/RegTest/Native qualification.json");
            org.json.JSONObject wallet = new org.json.JSONObject(new String(read(walletFile), StandardCharsets.UTF_8));
            String owner = hash("RegTest:" + wallet.getString("AccountKeyPath") + ":" + wallet.getString("ExtPubKey"));
            long reconciliation = android.os.SystemClock.elapsedRealtime();
            waitFor(() -> journalOwner(journal, arguments.getString("transaction"), owner), 30000, "Pending transaction uniquely reconciles to its wallet account");
            status("JOURNAL_OWNER_RECONCILED_AFTER_MS=" + (android.os.SystemClock.elapsedRealtime() - reconciliation));
        }
        check(has("TOTAL BALANCE"), "Recovered wallet opens after process death/update");
    }
    private boolean journalOwner(File journal, String transaction, String owner) {
        try {
            org.json.JSONArray entries = new org.json.JSONArray(new String(read(journal), StandardCharsets.UTF_8));
            int matches = 0;
            for (int i = 0; i < entries.length(); i++) {
                org.json.JSONObject entry = entries.getJSONObject(i);
                if (entry.getString("TransactionId").equals(transaction)) {
                    if (!entry.getString("WalletId").equals(owner)) return false;
                    matches++;
                }
            }
            return matches == 1;
        } catch (Exception failure) { return false; }
    }
    private void unlock() throws Exception {
        openPasswordUnlock();
        field("Wallet password", PASSWORD);
        long started = android.os.SystemClock.elapsedRealtime();
        click("Unlock");
        waitFor(() -> has("TOTAL BALANCE"), 60000, "Original password still unlocks");
        status("UNLOCK_COMPLETED_AFTER_MS=" + (android.os.SystemClock.elapsedRealtime() - started));
    }
    private void openPasswordUnlock() throws Exception {
        waitFor(() -> {
            check(!has("TOTAL BALANCE"), "A delayed operation cannot reopen the locked wallet");
            if (has("Recover a wallet") && hasPart("Native qualification")) { clickPart("Native qualification"); }
            for (View view : views()) if (view instanceof EditText && "Wallet password".contentEquals(((EditText)view).getHint())) { return true; }
            return false;
        }, 60000, "Wallet password screen becomes available after pending work");
    }
    private String hash(String value) throws Exception {
        byte[] bytes = java.security.MessageDigest.getInstance("SHA-256").digest(value.getBytes(StandardCharsets.UTF_8));
        StringBuilder hex = new StringBuilder();
        for (byte b : bytes) hex.append(String.format(java.util.Locale.ROOT, "%02X", b & 255));
        return hex.toString();
    }
    private void lostDeviceKey() throws Exception {
        waitFor(() -> hasPart("Connected") || hasPart("Synchronizing"), 90000, "Engine initialized for device-key loss");
        File root = new File(activity.getFilesDir(), "Wasabi");
        org.json.JSONObject wallet = new org.json.JSONObject(new String(read(new File(root, "Wallets/RegTest/Native qualification.json")), StandardCharsets.UTF_8));
        String reference = hash("RegTest:" + wallet.getString("AccountKeyPath") + ":" + wallet.getString("ExtPubKey"));
        File vault = new File(root, "vault");
        check(vault.isDirectory() || vault.mkdir(), "Qualification vault directory");
        File envelope = new File(vault, hash("wallet:" + reference) + ".json");
        check(!envelope.exists() && !new File(envelope.getPath() + ".old").exists(), "Never replace an existing wallet credential");
        // This synthetic envelope names a key that has never existed. The actual
        // Release vault must refuse it and retain original-password recovery.
        String data = "{\"Alias\":\"wasabi-ui-missing-" + java.util.UUID.randomUUID() + "\",\"Iv\":\"AAAAAAAAAAAAAAAA\",\"Ciphertext\":\"AAAAAAAAAAAAAAAAAAAAAA==\"}";
        try {
            try (java.io.FileOutputStream stream = new java.io.FileOutputStream(envelope)) { stream.write(data.getBytes(StandardCharsets.UTF_8)); stream.getFD().sync(); }
            clickPart("Native qualification");
            waitFor(() -> has("Use device unlock"), 10000, "Unavailable enrolled key falls back to the original password");
            check(!has("TOTAL BALANCE"), "Missing device key cannot unlock the wallet");
            field("Wallet password", "incorrect fixture password");
            click("Unlock");
            waitFor(() -> has("Incorrect wallet password."), 60000, "Key loss does not bypass password verification");
            click("OK");
            field("Wallet password", PASSWORD);
            click("Unlock");
            waitFor(() -> has("TOTAL BALANCE"), 60000, "Original password restores access after device-key loss");
            click("Lock wallet");
            click("Settings");
            checkSimplifiedSettings();
            click("‹");
            status("DEVICE_KEY_LOSS_PASSWORD_RECOVERY");
        } finally { check(!envelope.exists() || envelope.delete(), "Remove only the synthetic missing-key envelope"); }
    }
    private byte[] read(File file) throws Exception {
        try (java.io.FileInputStream stream = new java.io.FileInputStream(file); java.io.ByteArrayOutputStream out = new java.io.ByteArrayOutputStream()) {
            byte[] block = new byte[8192]; int size;
            while ((size = stream.read(block)) != -1) { out.write(block, 0, size); }
            return out.toByteArray();
        }
    }
    private void status(String message) { Bundle status = new Bundle(); status.putString("stream", message + "\n"); sendStatus(1, status); }
    private List<View> views() {
        AtomicReference<List<View>> result = new AtomicReference<>();
        runOnMainSync(() -> { List<View> list = new ArrayList<>(); collect(activity.findViewById(android.R.id.content), list); result.set(list); });
        return result.get();
    }
    private void collect(View view, List<View> list) { list.add(view); if (view instanceof ViewGroup) { ViewGroup group = (ViewGroup)view; for (int i=0;i<group.getChildCount();i++) collect(group.getChildAt(i),list); } }
    private List<String> texts() { List<String> list = new ArrayList<>(); for (View view : views()) if (view instanceof TextView) list.add(((TextView)view).getText().toString()); return list; }
    private boolean has(String text) { return texts().contains(text) || accessible(text, false); }
    private boolean hasPart(String text) { return texts().stream().anyMatch(t -> t.contains(text)); }
    private String address() { return texts().stream().filter(t -> t.matches("bcrt1[a-z0-9]+" )).findFirst().orElse(null); }
    private double balance() { return texts().stream().filter(t -> t.matches("[0-9]+(\\.[0-9]+)? BTC")).mapToDouble(t -> Double.parseDouble(t.split(" ")[0])).max().orElse(0); }
    private Button button(String text) { for (View view : views()) if (view instanceof Button && ((Button)view).getText().toString().equals(text)) return (Button)view; throw new IllegalStateException("Button missing: " + text); }
    private void click(String text) {
        for (View view : views()) if (view instanceof Button && ((Button)view).getText().toString().equals(text)) { runOnMainSync(() -> view.performClick()); return; }
        check(accessible(text, true), "Button missing: " + text);
    }
    private boolean accessible(String text, boolean click) {
        android.view.accessibility.AccessibilityNodeInfo root = getUiAutomation().getRootInActiveWindow();
        if (root == null) return false;
        try {
            List<android.view.accessibility.AccessibilityNodeInfo> nodes = root.findAccessibilityNodeInfosByText(text);
            boolean result = false;
            for (android.view.accessibility.AccessibilityNodeInfo node : nodes) {
                try { if (text.contentEquals(node.getText() == null ? "" : node.getText())) result |= !click || node.performAction(android.view.accessibility.AccessibilityNodeInfo.ACTION_CLICK); }
                finally { node.recycle(); }
            }
            return result;
        } finally { root.recycle(); }
    }
    private void clickPart(String text) { for (View view : views()) if (view instanceof Button && ((Button)view).getText().toString().contains(text)) { runOnMainSync(() -> view.performClick()); return; } throw new IllegalStateException("Button missing: " + text); }
    private void field(String hint, String text) { for (View view : views()) if (view instanceof EditText && hint.equals(((EditText)view).getHint().toString())) { runOnMainSync(() -> ((EditText)view).setText(text)); return; } throw new IllegalStateException("Field missing: " + hint); }
    private interface Condition { boolean met() throws Exception; }
    private String accessibleMessages() {
        android.view.accessibility.AccessibilityNodeInfo root = getUiAutomation().getRootInActiveWindow();
        if (root == null) return "no active accessibility window";
        try { List<String> messages = new ArrayList<>(); collectMessages(root, messages); return messages.toString(); }
        finally { root.recycle(); }
    }
    private void collectMessages(android.view.accessibility.AccessibilityNodeInfo node, List<String> messages) {
        if (!node.isPassword() && node.getText() != null) messages.add(node.getText().toString());
        for (int i=0;i<node.getChildCount();i++) { android.view.accessibility.AccessibilityNodeInfo child=node.getChild(i); if (child != null) try { collectMessages(child,messages); } finally { child.recycle(); } }
    }
    private void waitFor(Condition condition, int milliseconds, String label) throws Exception { long deadline = android.os.SystemClock.elapsedRealtime()+milliseconds; while (!condition.met()) { if (android.os.SystemClock.elapsedRealtime()>deadline) throw new IllegalStateException(label + ": " + accessibleMessages()); Thread.sleep(250); } }
    private static void check(boolean value, String label) { if (!value) throw new IllegalStateException(label); }
}
