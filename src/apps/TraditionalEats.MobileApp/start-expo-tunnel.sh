#!/bin/bash

# Start Expo with tunnel mode - allows connection from anywhere (not just same WiFi)
# Use this when you're at a different location than your Mac
# 
# This script also sets the API URL to staging (www.caseflowstage.store) so the app
# can connect to the backend even when you're away from your development machine.

set -e

# Colors
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
RED='\033[0;31m'
NC='\033[0m'

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}Starting Expo with Tunnel Mode${NC}"
echo -e "${GREEN}========================================${NC}"
echo ""
echo -e "${YELLOW}This will allow your phone to connect from anywhere${NC}"
echo -e "${YELLOW}(not just on the same WiFi network)${NC}"
echo ""
echo -e "${BLUE}📡 API Configuration:${NC}"
echo -e "${BLUE}   Setting EXPO_PUBLIC_ENV=staging${NC}"
echo -e "${BLUE}   API will use: https://www.caseflowstage.store/api${NC}"
echo ""
echo -e "${BLUE}Note: Tunnel mode may be slower than LAN mode${NC}"
echo -e "${BLUE}For same WiFi, use: npx expo start --host lan${NC}"
echo ""

# Kill any existing Metro processes
pkill -f "expo start" 2>/dev/null || true
sleep 2

# Set environment variable to use staging API
export EXPO_PUBLIC_ENV=staging

# Start Expo with tunnel mode
echo -e "${GREEN}🚀 Starting Expo with tunnel...${NC}"
echo ""
echo -e "${YELLOW}Note: Tunnel may take 30-60 seconds to establish${NC}"
echo -e "${YELLOW}If it times out, try: npx expo start --tunnel --clear --max-workers 1${NC}"
echo ""

# Try tunnel with increased timeout
npx expo start --tunnel --clear || {
    echo ""
    echo -e "${RED}❌ Tunnel failed. Trying alternative method...${NC}"
    echo -e "${YELLOW}Attempting with reduced workers...${NC}"
    npx expo start --tunnel --clear --max-workers 1
}
