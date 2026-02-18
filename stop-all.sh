#!/bin/bash
# Kill all Kram/TraditionalEats services (tmux session + any leftover dotnet processes + anything on service ports).

SESSION="eats"

# Ports used by start-all.sh services.
# Note: keep this list in sync with the Kestrel endpoints across services.
# Common mapping in this repo:
# - 5000..5009: core microservices
# - 5010: SupportService
# - 5011: AIService
# - 5012: ChatService (SignalR + REST)
# - 5013: ReviewService
# - 5014: DocumentService
# - 5101: WebBff
# - 5102: MobileBff
# - 5300: WebApp
PORTS="5000 5001 5002 5003 5004 5005 5006 5007 5008 5009 5010 5011 5012 5013 5014 5101 5102 5143 5144 5300 5301"

echo "🛑 Stopping all services..."

# 1. Kill tmux session (stops every service started by start-all.sh)
if tmux has-session -t "$SESSION" 2>/dev/null; then
    echo "   Killing tmux session: $SESSION"
    tmux kill-session -t "$SESSION"
    echo "   ✓ Tmux session killed"
else
    echo "   No tmux session '$SESSION' running"
fi

# 2. Kill any leftover dotnet processes running TraditionalEats services
if command -v pkill &>/dev/null; then
    # dotnet watch can run either as `dotnet watch run` OR as `dotnet .../dotnet-watch.dll run`
    if pgrep -f "dotnet\\s+watch\\s+run|dotnet-watch\\.dll\\s+run" >/dev/null 2>&1; then
        echo "   Killing leftover dotnet watch processes..."
        pkill -f "dotnet\\s+watch\\s+run" 2>/dev/null || true
        pkill -f "dotnet-watch\\.dll\\s+run" 2>/dev/null || true
        sleep 1
        pkill -9 -f "dotnet\\s+watch\\s+run" 2>/dev/null || true
        pkill -9 -f "dotnet-watch\\.dll\\s+run" 2>/dev/null || true
        echo "   ✓ Dotnet watch processes killed"
    fi

    # Blazor WASM dev server can keep shared assemblies locked
    if pgrep -f "blazor-devserver\\.dll" >/dev/null 2>&1; then
        echo "   Killing leftover blazor-devserver processes..."
        pkill -f "blazor-devserver\\.dll" 2>/dev/null || true
        sleep 1
        pkill -9 -f "blazor-devserver\\.dll" 2>/dev/null || true
        echo "   ✓ Blazor dev server processes killed"
    fi

    # MSBuild servers spawned by dotnet watch can linger and contribute to file locks
    if pgrep -f "MSBuild\\.dll" >/dev/null 2>&1; then
        echo "   Killing leftover MSBuild server processes..."
        pkill -f "MSBuild\\.dll" 2>/dev/null || true
        sleep 1
        pkill -9 -f "MSBuild\\.dll" 2>/dev/null || true
        echo "   ✓ MSBuild server processes killed"
    fi

    if pgrep -f "dotnet.*TraditionalEats" >/dev/null 2>&1; then
        echo "   Killing leftover dotnet (TraditionalEats) processes..."
        pkill -f "dotnet.*TraditionalEats" 2>/dev/null || true
        sleep 1
        pkill -9 -f "dotnet.*TraditionalEats" 2>/dev/null || true
        echo "   ✓ Dotnet service processes killed"
    else
        echo "   No leftover dotnet TraditionalEats processes found"
    fi
fi

# 3. Kill anything still listening on service ports (fixes "address already in use", e.g. port 5002)
if command -v lsof &>/dev/null; then
    KILLED_PORTS=""
    for port in $PORTS; do
        PIDS=$(lsof -ti ":$port" 2>/dev/null)
        if [ -n "$PIDS" ]; then
            echo "$PIDS" | xargs kill -9 2>/dev/null || true
            KILLED_PORTS="$KILLED_PORTS $port"
        fi
    done
    if [ -n "$KILLED_PORTS" ]; then
        echo "   ✓ Killed processes on ports:$KILLED_PORTS"
    fi
fi

echo "✅ All services stopped."
