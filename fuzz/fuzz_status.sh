#!/usr/bin/env bash
#
# Quick status check for running fuzz targets.

set -euo pipefail

FUZZ_ROOT="$(cd "$(dirname "$0")" && pwd)"
LOGDIR="$FUZZ_ROOT/logs"
PIDDIR="$FUZZ_ROOT/pids"
LAUNCHER_PID_FILE="$PIDDIR/launcher.pid"
TARGETS=(
    fuzz_compile
    fuzz_structured_compile
    fuzz_template_projects
    fuzz_differential
    fuzz_devpack_runtime
)

echo "=== neo-devpack-dotnet fuzzer status ==="
echo "Date: $(date)"
echo ""

if [ -f "$LAUNCHER_PID_FILE" ]; then
    LAUNCHER_PID="$(cat "$LAUNCHER_PID_FILE")"
    if [ -n "$LAUNCHER_PID" ] && kill -0 "$LAUNCHER_PID" 2>/dev/null; then
        echo "Launcher: running (PID $LAUNCHER_PID)"
    else
        echo "Launcher: stale PID file ($LAUNCHER_PID)"
    fi
else
    echo "Launcher: not running"
fi

echo ""

for target in "${TARGETS[@]}"; do
    CORPUS="$FUZZ_ROOT/corpus/$target"
    ARTIFACTS="$FUZZ_ROOT/artifacts/$target"
    LOGFILE="$LOGDIR/${target}.log"
    PIDFILE="$PIDDIR/${target}.pid"

    CORPUS_COUNT=$(find "$CORPUS" -type f 2>/dev/null | wc -l)
    CRASHES=$(find "$ARTIFACTS" -maxdepth 1 -type d -name "crash-*" 2>/dev/null | wc -l)

    LAST=""
    if [ -f "$LOGFILE" ]; then
        LAST=$(tail -1 "$LOGFILE" | head -c 160)
    fi

    LOOP_STATUS="not running"
    STATUS_ICON=" "
    if [ -f "$PIDFILE" ]; then
        LOOP_PID="$(cat "$PIDFILE")"
        if [ -n "$LOOP_PID" ] && kill -0 "$LOOP_PID" 2>/dev/null; then
            LOOP_STATUS="running (PID $LOOP_PID)"
            STATUS_ICON="*"
        else
            LOOP_STATUS="stale PID file ($LOOP_PID)"
            STATUS_ICON="!"
        fi
    fi

    WORKER_PID=""
    if WORKER_PID=$(pgrep -f "Neo.DevPack.Fuzz.dll run $target" | head -n 1); then
        STATUS_ICON="*"
    fi

    echo "[$STATUS_ICON] $target"
    echo "    Loop:   $LOOP_STATUS"
    if [ -n "$WORKER_PID" ]; then
        echo "    Worker: running (PID $WORKER_PID)"
    else
        echo "    Worker: idle"
    fi
    echo "    Corpus: $CORPUS_COUNT files | Crashes: $CRASHES"
    if [ -n "$LAST" ]; then
        echo "    Last:   $LAST"
    fi
    echo ""
done
