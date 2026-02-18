#!/bin/bash

SESSION="eats"

echo "🚀 Starting TraditionalEats in tmux session: $SESSION"

# Kill old session if exists
tmux has-session -t $SESSION 2>/dev/null
if [ $? -eq 0 ]; then
    echo "⚠️  Existing session found. Killing it..."
    tmux kill-session -t $SESSION
fi

# Create new session
tmux new-session -d -s $SESSION

BASE_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
ARTIFACTS_BASE="$BASE_DIR/.artifacts"

mkdir -p "$ARTIFACTS_BASE"

echo "🔧 Restoring once (prevents parallel restore/build locks)..."
dotnet restore "$BASE_DIR/TraditionalEats.sln" >/dev/null || dotnet restore "$BASE_DIR/TraditionalEats.sln"

start_service () {
  NAME=$1
  PATH_TO_PROJECT=$2

  echo "▶ $NAME"

  tmux new-window -t $SESSION -n $NAME
  # NOTE: Running many `dotnet watch` processes in parallel can fight over shared project outputs (e.g. BuildingBlocks.pdb).
  # `--artifacts-path` isolates build outputs per service, avoiding CS2012 file-lock errors.
  tmux send-keys -t $SESSION:$NAME "cd $BASE_DIR/$PATH_TO_PROJECT && dotnet watch --no-restore --disable-build-servers --artifacts-path \"$ARTIFACTS_BASE/$NAME\" run" C-m
}

# ---------------- SERVICES ----------------
start_service "AIService" "src/services/TraditionalEats.AIService"
start_service "CatalogService" "src/services/TraditionalEats.CatalogService"
start_service "ChatService" "src/services/TraditionalEats.ChatService"
start_service "CustomerService" "src/services/TraditionalEats.CustomerService"
start_service "DeliveryService" "src/services/TraditionalEats.DeliveryService"
start_service "DocumentService" "src/services/TraditionalEats.DocumentService"
start_service "IdentityService" "src/services/TraditionalEats.IdentityService"
start_service "NotificationService" "src/services/TraditionalEats.NotificationService"
start_service "OrderService" "src/services/TraditionalEats.OrderService"
start_service "PaymentService" "src/services/TraditionalEats.PaymentService"
start_service "PromotionService" "src/services/TraditionalEats.PromotionService"
start_service "RestaurantService" "src/services/TraditionalEats.RestaurantService"
start_service "ReviewService" "src/services/TraditionalEats.ReviewService"
start_service "SupportService" "src/services/TraditionalEats.SupportService"

# ---------------- BFF ----------------
start_service "WebBff" "src/bff/TraditionalEats.Web.Bff"
start_service "MobileBff" "src/bff/TraditionalEats.Mobile.Bff"

# ---------------- APPS ----------------
start_service "WebApp" "src/apps/TraditionalEats.WebApp"

# Go to first window
tmux select-window -t $SESSION:0

echo "✅ All services launching..."
echo "Use Ctrl+b then window number to switch."
echo ""

tmux attach -t $SESSION
