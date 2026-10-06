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
            else if (mode.equals("wallet")) wallet();
            else if (mode.equals("resume")) resume();
            else if (mode.equals("late-unlock")) verifyLateUnlockRemainsLocked();
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
    private void simplifiedSettings() throws Exception {
        check(has("Bitcoin.\nUnfairly private."), "Requested wallet branding");
        click("Settings");
        check(!has("PERSONAL BITCOIN NODE"), "Personal-node section removed");
        for (View view : views()) if (view instanceof EditText) {
            CharSequence hint = ((EditText)view).getHint();
            check(hint == null || !hint.toString().startsWith("RPC"), "RPC controls removed");
        }
        check(has("Save and reconnect"), "Normal network settings remain available");
        click("‹");
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
            field("Original Wasabi password / BIP39 passphrase", PASSWORD);
            field("Recovery words", WORDS);
            click("Recover");
            waitFor(() -> has("TOTAL BALANCE"), 30000, "Recovery opens wallet");
        }
        waitFor(() -> hasPart("Connected"), 90000, "Recovered wallet synchronized before receive");
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
            waitFor(() -> hasPart("Native qualification"), 10000, "Locked wallet list");
            clickPart("Native qualification");
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

    private void resume() throws Exception {
        // A saved wallet is listed while its runtime is still initializing.
        // Match wallet()'s startup gate before measuring password authorization.
        long startup = android.os.SystemClock.elapsedRealtime();
        waitFor(() -> hasPart("Connected") || hasPart("Synchronizing"), 90000, "Restarted engine initialized");
        status("ENGINE_READY_AFTER_MS=" + (android.os.SystemClock.elapsedRealtime() - startup));
        unlock();
        waitFor(() -> hasPart("Connected"), 90000, "Update/restart resumes synchronization with the saved RPC key");
        File journal = new File(activity.getFilesDir(), "Wasabi/submissions-RegTest.json");
        String bytes = new String(read(journal), StandardCharsets.UTF_8);
        check(bytes.contains(arguments.getString("transaction")), "Pending transaction journal survives update");
        check(has("TOTAL BALANCE"), "Recovered wallet opens after process death/update");
    }
    private void unlock() throws Exception {
        waitFor(() -> hasPart("Native qualification"), 60000, "Wallet survives restart");
        clickPart("Native qualification");
        field("Wallet password", PASSWORD);
        long started = android.os.SystemClock.elapsedRealtime();
        click("Unlock");
        waitFor(() -> has("TOTAL BALANCE"), 10000, "Original password still unlocks");
        status("UNLOCK_COMPLETED_AFTER_MS=" + (android.os.SystemClock.elapsedRealtime() - started));
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
