#!/bin/bash

# Create placeholder assets for Expo app
# This script creates simple colored PNG files as placeholders

ASSETS_DIR="./assets"
mkdir -p "$ASSETS_DIR"

# Create a simple 1x1 transparent PNG using ImageMagick or sips (macOS)
# If ImageMagick is not available, we'll create a note file instead

if command -v convert &> /dev/null; then
    # Using ImageMagick
    convert -size 1024x1024 xc:#6200ee "$ASSETS_DIR/icon.png"
    convert -size 2048x2048 xc:#6200ee "$ASSETS_DIR/splash.png"
    convert -size 1024x1024 xc:#6200ee "$ASSETS_DIR/adaptive-icon.png"
    convert -size 48x48 xc:#6200ee "$ASSETS_DIR/favicon.png"
    echo "✅ Created placeholder assets using ImageMagick"
elif command -v sips &> /dev/null; then
    # Using macOS sips (requires an existing image or we create a solid color)
    # For now, create a note
    echo "Note: Please add your app assets manually or use an online tool"
    echo "Required files:"
    echo "  - icon.png (1024x1024)"
    echo "  - splash.png (2048x2048)"
    echo "  - adaptive-icon.png (1024x1024)"
    echo "  - favicon.png (48x48)"
else
    echo "⚠️  ImageMagick or sips not found. Please create assets manually:"
    echo "  - icon.png (1024x1024)"
    echo "  - splash.png (2048x2048)"
    echo "  - adaptive-icon.png (1024x1024)"
    echo "  - favicon.png (48x48)"
    echo ""
    echo "You can use online tools like:"
    echo "  - https://www.favicon-generator.org/"
    echo "  - https://www.appicon.co/"
fi
