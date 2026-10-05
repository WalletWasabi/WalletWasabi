#!/usr/bin/env bash
set -euo pipefail

# android-emulator-runner counts boot attempts but does not bound an individual
# adb getprop process. A wedged API 24 guest previously consumed the whole job.
# Apply this only to the disposable GitHub runner, after its SDK installation.
[[ "${RUNNER_ENVIRONMENT:-}" == github-hosted && "$(uname -s)" == Linux ]]
task_adb="${ANDROID_HOME:?}/platform-tools/adb"
[[ -x "$task_adb" && ! -e "$task_adb.unbounded" ]]
mv -- "$task_adb" "$task_adb.unbounded"
cat > "$task_adb" <<'WRAPPER'
#!/usr/bin/env bash
set -euo pipefail
task_real="${BASH_SOURCE[0]}.unbounded"
case " $* " in
    *" shell getprop sys.boot_completed "*)
        exec timeout --foreground 2s "$task_real" "$@"
        ;;
    *)
        # Instrumentation intentionally blocks while tests execute. Its driver
        # owns the test deadline; do not apply the boot-query timeout to it.
        exec "$task_real" "$@"
        ;;
esac
WRAPPER
chmod 755 "$task_adb"
"$task_adb" version
