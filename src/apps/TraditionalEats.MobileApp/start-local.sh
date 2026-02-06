#!/bin/bash

# Start Expo in local development mode
# Use this when you're on the same WiFi network as your development machine
# The app will connect to your local IP address for the API

set -e

# Colors
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}Starting Expo in Local Development Mode${NC}"
echo -e "${GREEN}========================================${NC}"
echo ""
echo -e "${YELLOW}This mode requires your phone to be on the same WiFi network${NC}"
echo ""
echo -e "${BLUE}📡 API Configuration:${NC}"
echo -e "${BLUE}   Using EXPO_PUBLIC_ENV=ip${NC}"
echo -e "${BLUE}   API will use your local IP from config/app.config.ts${NC}"
echo ""
echo -e "${BLUE}For remote access, use: ./start-expo-tunnel.sh${NC}"
echo ""

# Kill any existing Metro processes
pkill -f "expo start" 2>/dev/null || true
sleep 2

# Set environment variable to use local IP
export EXPO_PUBLIC_ENV=ip

# Start Expo in LAN mode
echo -e "${GREEN}🚀 Starting Expo...${NC}"
echo ""

npx expo start --host lan --clear
