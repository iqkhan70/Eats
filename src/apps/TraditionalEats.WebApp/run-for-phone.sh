#!/bin/bash

# Script to run Blazor WebApp accessible from phone

echo "🔍 Finding your local IP address..."
IP=$(ipconfig getifaddr en0 2>/dev/null || ipconfig getifaddr en1 2>/dev/null || hostname -I | awk '{print $1}' || echo "localhost")

if [ "$IP" = "localhost" ]; then
    echo "⚠️  Could not automatically detect IP. Please find it manually:"
    echo "   macOS: ipconfig getifaddr en0"
    echo "   Linux: hostname -I"
    echo "   Windows: ipconfig"
    exit 1
fi

echo "✅ Your local IP: $IP"
echo ""
echo "📱 To access from your phone:"
echo "   1. Make sure your phone is on the same WiFi network"
echo "   2. Open browser and go to: http://$IP:5300"
echo ""
echo "⚠️  Note: Make sure Web BFF is running on port 5101 for API calls to work"
echo ""
read -p "Press Enter to start the WebApp..."

cd "$(dirname "$0")"
dotnet run
