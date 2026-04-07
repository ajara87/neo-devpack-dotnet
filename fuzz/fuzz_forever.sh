#!/usr/bin/env bash
#
# Persistent fuzzing runner for neo-devpack-dotnet.
# Runs all .NET fuzz targets in parallel and restarts them after crashes
# so the campaign can run for days or weeks.

set -euo pipefail

FUZZ_ROOT="$(cd "$(dirname "$0")" && pwd)"
PROJECT="$FUZZ_ROOT/Neo.DevPack.Fuzz/Neo.DevPack.Fuzz.csproj"
HARNESS_DLL="$FUZZ_ROOT/Neo.DevPack.Fuzz/bin/Release/net10.0/Neo.DevPack.Fuzz.dll"
LOGDIR="$FUZZ_ROOT/logs"
PIDDIR="$FUZZ_ROOT/pids"
RUNNER="$FUZZ_ROOT/run_target_forever.sh"
LAUNCHER_PID_FILE="$PIDDIR/launcher.pid"

ALL_TARGETS=(
    fuzz_compile
    fuzz_structured_compile
    fuzz_template_projects
    fuzz_differential
    fuzz_devpack_runtime
)

if [ "$#" -eq 0 ]; then
    TARGETS=("${ALL_TARGETS[@]}")
else
    TARGETS=("$@")
fi

mkdir -p "$LOGDIR" "$PIDDIR"
PIDS=()
STOPPING=0

cleanup() {
    STOPPING=1

    for pid in "${PIDS[@]}"; do
        kill "$pid" 2>/dev/null || true
    done

    for pid in "${PIDS[@]}"; do
        wait "$pid" 2>/dev/null || true
    done

    rm -f "$FUZZ_ROOT/fuzz.pids" "$LAUNCHER_PID_FILE"
    find "$PIDDIR" -maxdepth 1 -type f -name '*.pid' -delete 2>/dev/null || true
}

trap cleanup EXIT
trap 'exit 0' INT TERM

if [ ! -x "$RUNNER" ]; then
    echo "Missing target runner script: $RUNNER" >&2
    exit 1
fi

if [ -f "$LAUNCHER_PID_FILE" ]; then
    EXISTING_LAUNCHER="$(cat "$LAUNCHER_PID_FILE")"
    if [ -n "$EXISTING_LAUNCHER" ] && kill -0 "$EXISTING_LAUNCHER" 2>/dev/null; then
        echo "A fuzz launcher is already running with PID $EXISTING_LAUNCHER." >&2
        exit 1
    fi

    rm -f "$LAUNCHER_PID_FILE"
fi

echo "=== neo-devpack-dotnet persistent fuzzer ==="
echo "Targets: ${TARGETS[*]}"
echo "Logs:    $LOGDIR/"
echo "Started: $(date)"
echo ""

if [ ! -f "$HARNESS_DLL" ]; then
    echo "Release harness not found. Building once..."
    dotnet build "$PROJECT" -c Release >/dev/null
fi

if [ ! -f "$HARNESS_DLL" ]; then
    echo "Missing harness assembly after build: $HARNESS_DLL" >&2
    exit 1
fi

printf '%s\n' "$$" > "$LAUNCHER_PID_FILE"

for target in "${TARGETS[@]}"; do
    CORPUS="$FUZZ_ROOT/corpus/$target"
    ARTIFACTS="$FUZZ_ROOT/artifacts/$target"
    LOGFILE="$LOGDIR/${target}.log"
    PIDFILE="$PIDDIR/${target}.pid"

    mkdir -p "$CORPUS" "$ARTIFACTS"

    if [ -f "$PIDFILE" ]; then
        EXISTING_PID="$(cat "$PIDFILE")"
        if [ -n "$EXISTING_PID" ] && kill -0 "$EXISTING_PID" 2>/dev/null; then
            echo "Target loop '$target' is already running with PID $EXISTING_PID." >&2
            exit 1
        fi

        rm -f "$PIDFILE"
    fi

    echo "[$target] Starting persistent fuzz loop..."
    "$RUNNER" \
        "$target" \
        "$HARNESS_DLL" \
        "$CORPUS" \
        "$ARTIFACTS" \
        "$FUZZ_ROOT/seeds" \
        "$FUZZ_ROOT/dotnet.dict" \
        "$PIDFILE" >> "$LOGFILE" 2>&1 &

    PIDS+=("$!")
    echo "[$target] PID=${PIDS[-1]} running in background"
done

echo ""
echo "Launcher PID: $$"
echo "All targets launched. PIDs:"
printf '%s\n' "${PIDS[@]}"
echo ""
echo "To monitor: tail -f $LOGDIR/<target>.log"
echo "To stop:    ./fuzz_stop.sh"
echo "To check:   ./fuzz_status.sh"

printf '%s\n' "${PIDS[@]}" > "$FUZZ_ROOT/fuzz.pids"
echo "PIDs saved to $FUZZ_ROOT/fuzz.pids"

while true; do
    set +e
    wait -n "${PIDS[@]}"
    STATUS=$?
    set -e

    if [ "$STOPPING" -eq 1 ]; then
        exit 0
    fi

    echo "A target loop exited unexpectedly with status $STATUS. Stopping the campaign." >&2
    exit "$STATUS"
done
