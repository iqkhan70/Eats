#!/bin/bash

# Start Expo pointing to Production API (www.kram.tech)
# Use this when you want to run the app on your phone and connect to production backend
# Good for testing against live data, TestFlight validation, or demos

set -e

# Colors
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}Starting Expo → Production (www.kram.tech)${NC}"
echo -e "${GREEN}========================================${NC}"
echo ""
echo -e "${YELLOW}API will connect to: https://www.kram.tech${NC}"
echo ""
echo -e "${BLUE}📡 API Configuration:${NC}"
echo -e "${BLUE}   EXPO_PUBLIC_ENV=production${NC}"
echo -e "${BLUE}   API: https://www.kram.tech/api${NC}"
echo -e "${BLUE}   Chat: https://www.kram.tech/chatHub${NC}"
echo ""
echo -e "${BLUE}For same WiFi: LAN mode (default)${NC}"
echo -e "${BLUE}For remote access: use --tunnel flag below${NC}"
echo ""
echo -e "${YELLOW}After connecting: reload the app (shake → Reload) to ensure fresh bundle${NC}"
echo ""

# Kill any existing Metro processes
pkill -f "expo start" 2>/dev/null || true
sleep 2

# Start Expo with production API - pass env inline so Metro bundles it correctly
if [ "$1" = "--tunnel" ]; then
  echo -e "${GREEN}🚀 Starting Expo with tunnel...${NC}"
  echo ""
  EXPO_PUBLIC_ENV=production npx expo start --tunnel --clear
else
  echo -e "${GREEN}🚀 Starting Expo (LAN mode)...${NC}"
  echo ""
  EXPO_PUBLIC_ENV=production npx expo start --host lan --clear
fi
