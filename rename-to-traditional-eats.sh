#!/bin/bash

# Script to rename traditionaleats to TraditionalEats
# This script renames folders, files, and updates content

set -e

echo "🔄 Starting rename from traditionaleats to TraditionalEats..."

# Step 1: Rename all directories
echo "📁 Renaming directories..."

# Services
mv "src/services/traditionaleats.IdentityService" "src/services/TraditionalEats.IdentityService" 2>/dev/null || true
mv "src/services/traditionaleats.CustomerService" "src/services/TraditionalEats.CustomerService" 2>/dev/null || true
mv "src/services/traditionaleats.OrderService" "src/services/TraditionalEats.OrderService" 2>/dev/null || true
mv "src/services/traditionaleats.CatalogService" "src/services/TraditionalEats.CatalogService" 2>/dev/null || true
mv "src/services/traditionaleats.PaymentService" "src/services/TraditionalEats.PaymentService" 2>/dev/null || true
mv "src/services/traditionaleats.DeliveryService" "src/services/TraditionalEats.DeliveryService" 2>/dev/null || true
mv "src/services/traditionaleats.NotificationService" "src/services/TraditionalEats.NotificationService" 2>/dev/null || true
mv "src/services/traditionaleats.RestaurantService" "src/services/TraditionalEats.RestaurantService" 2>/dev/null || true
mv "src/services/traditionaleats.PromotionService" "src/services/TraditionalEats.PromotionService" 2>/dev/null || true
mv "src/services/traditionaleats.ReviewService" "src/services/TraditionalEats.ReviewService" 2>/dev/null || true
mv "src/services/traditionaleats.SupportService" "src/services/TraditionalEats.SupportService" 2>/dev/null || true
mv "src/services/traditionaleats.AIService" "src/services/TraditionalEats.AIService" 2>/dev/null || true

# BFFs
mv "src/bff/traditionaleats.Web.Bff" "src/bff/TraditionalEats.Web.Bff" 2>/dev/null || true
mv "src/bff/traditionaleats.Mobile.Bff" "src/bff/TraditionalEats.Mobile.Bff" 2>/dev/null || true

# Gateway
mv "src/gateway/traditionaleats.ApiGateway" "src/gateway/TraditionalEats.ApiGateway" 2>/dev/null || true

# Apps
mv "src/apps/traditionaleats.WebApp" "src/apps/TraditionalEats.WebApp" 2>/dev/null || true
mv "src/apps/traditionaleats.MobileApp" "src/apps/TraditionalEats.MobileApp" 2>/dev/null || true

# Shared
mv "src/shared/traditionaleats.BuildingBlocks" "src/shared/TraditionalEats.BuildingBlocks" 2>/dev/null || true
mv "src/shared/traditionaleats.Contracts" "src/shared/TraditionalEats.Contracts" 2>/dev/null || true

# Solution file
mv "traditionaleats.sln" "TraditionalEats.sln" 2>/dev/null || true

echo "✅ Directory renaming complete"

# Step 2: Rename .csproj files
echo "📄 Renaming .csproj files..."

find . -name "traditionaleats.*.csproj" -type f | while read file; do
    newfile=$(echo "$file" | sed 's/traditionaleats/TraditionalEats/g')
    mv "$file" "$newfile" 2>/dev/null || true
done

echo "✅ .csproj file renaming complete"

# Step 3: Update content in files (using sed)
echo "📝 Updating content in files..."

# Update all occurrences in code files
find . -type f \( -name "*.cs" -o -name "*.csproj" -o -name "*.razor" -o -name "*.ts" -o -name "*.tsx" -o -name "*.json" -o -name "*.md" -o -name "*.sln" -o -name "*.html" -o -name "*.css" \) ! -path "*/node_modules/*" ! -path "*/.git/*" ! -path "*/bin/*" ! -path "*/obj/*" | while read file; do
    if [ -f "$file" ]; then
        sed -i '' 's/traditionaleats/TraditionalEats/g' "$file" 2>/dev/null || true
    fi
done

echo "✅ Content update complete"

echo ""
echo "✨ Rename complete! Please verify the changes and test the build."
echo "⚠️  Note: You may need to:"
echo "   1. Close and reopen your IDE"
echo "   2. Run 'dotnet restore'"
echo "   3. Update any Git remotes if needed"
