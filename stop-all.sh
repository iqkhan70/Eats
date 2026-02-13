#!/bin/bash
# Kill all Kram/TraditionalEats services (tmux session + any leftover dotnet processes + anything on service ports).

SESSION="eats"

# Ports used by start-all.sh services (5000-5009, 5011 AI, 5012 Chat, 5101 WebBff, 5102 MobileBff, 5300 WebApp)
PORTS="5000 5001 5002 5003 5004 5005 5006 5007 5008 5009 5011 5012 5101 5102 5143 5144 5300 5301"

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
