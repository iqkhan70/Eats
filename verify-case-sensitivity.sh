#!/bin/bash

# Script to verify all TraditionalEats references are case-correct for Unix compatibility

set -e

echo "🔍 Verifying case sensitivity for TraditionalEats references..."
echo ""

ERRORS=0

# Check 1: Verify all folder names are correct case
echo "📁 Checking folder names..."
for dir in src/services/TraditionalEats.* src/bff/TraditionalEats.* src/apps/TraditionalEats.* src/shared/TraditionalEats.* src/gateway/TraditionalEats.*; do
    if [ -d "$dir" ]; then
        basename_dir=$(basename "$dir")
        if [[ "$basename_dir" =~ ^TraditionalEats ]]; then
            echo "  ✅ $basename_dir"
        else
            echo "  ❌ ERROR: $basename_dir (should start with TraditionalEats)"
            ERRORS=$((ERRORS + 1))
        fi
    fi
done

# Check 2: Verify all .csproj file names match folder names
echo ""
echo "📄 Checking .csproj file names..."
for dir in src/services/TraditionalEats.* src/bff/TraditionalEats.* src/apps/TraditionalEats.* src/shared/TraditionalEats.* src/gateway/TraditionalEats.*; do
    if [ -d "$dir" ]; then
        basename_dir=$(basename "$dir")
        expected_csproj="${basename_dir}.csproj"
        csproj_file=$(find "$dir" -name "*.csproj" -type f | head -1)
        if [ -f "$csproj_file" ]; then
            csproj_name=$(basename "$csproj_file")
            if [ "$csproj_name" = "$expected_csproj" ]; then
                echo "  ✅ $csproj_name"
            else
                echo "  ❌ ERROR: $csproj_name (expected $expected_csproj)"
                ERRORS=$((ERRORS + 1))
            fi
        fi
    fi
done

# Check 3: Verify ProjectReference paths match actual folder names
echo ""
echo "🔗 Checking ProjectReference paths..."
for dir in src/services/TraditionalEats.* src/bff/TraditionalEats.* src/apps/TraditionalEats.* src/shared/TraditionalEats.* src/gateway/TraditionalEats.*; do
    if [ -d "$dir" ]; then
        csproj_file=$(find "$dir" -name "*.csproj" -type f | head -1)
        if [ -f "$csproj_file" ]; then
            refs=$(grep -E "ProjectReference.*Include" "$csproj_file" | grep -i "traditionaleats" || true)
            if [ -n "$refs" ]; then
                while IFS= read -r ref; do
                    # Extract path from ProjectReference
                    path=$(echo "$ref" | sed -n 's/.*Include="\([^"]*\)".*/\1/p')
                    if [ -n "$path" ]; then
                        # Check if path uses correct case
                        if [[ "$path" =~ TraditionalEats ]]; then
                            # Verify the referenced file actually exists
                            full_path=$(cd "$dir" && cd "$(dirname "$path")" 2>/dev/null && pwd)/$(basename "$path") 2>/dev/null || echo ""
                            if [ -f "$full_path" ]; then
                                echo "  ✅ $(basename "$dir"): $path"
                            else
                                # Try to find the actual file
                                actual_file=$(find . -name "$(basename "$path")" -type f 2>/dev/null | head -1)
                                if [ -n "$actual_file" ]; then
                                    echo "  ⚠️  $(basename "$dir"): Path might be wrong: $path"
                                    echo "      Found at: $actual_file"
                                else
                                    echo "  ❌ $(basename "$dir"): Missing reference: $path"
                                    ERRORS=$((ERRORS + 1))
                                fi
                            fi
                        else
                            echo "  ❌ $(basename "$dir"): Wrong case in path: $path"
                            ERRORS=$((ERRORS + 1))
                        fi
                    fi
                done <<< "$refs"
            fi
        fi
    fi
done

# Check 4: Verify namespace declarations
echo ""
echo "📦 Checking namespace declarations..."
for file in $(find src -name "*.cs" -type f ! -path "*/bin/*" ! -path "*/obj/*" ! -path "*/Migrations/*" 2>/dev/null | head -10); do
    namespace=$(grep -E "^namespace " "$file" | head -1 || true)
    if [ -n "$namespace" ]; then
        if [[ "$namespace" =~ TraditionalEats ]]; then
            echo "  ✅ $(basename "$file"): $namespace"
        elif [[ "$namespace" =~ [Tt]raditionaleats ]]; then
            echo "  ❌ $(basename "$file"): Wrong case: $namespace"
            ERRORS=$((ERRORS + 1))
        fi
    fi
done

# Check 5: Verify using statements
echo ""
echo "📚 Checking using statements..."
for file in $(find src -name "*.cs" -type f ! -path "*/bin/*" ! -path "*/obj/*" ! -path "*/Migrations/*" 2>/dev/null | head -10); do
    usings=$(grep -E "^using.*TraditionalEats" "$file" || true)
    if [ -n "$usings" ]; then
        while IFS= read -r using_line; do
            if [[ "$using_line" =~ TraditionalEats ]]; then
                echo "  ✅ $(basename "$file"): $using_line"
            elif [[ "$using_line" =~ [Tt]raditionaleats ]]; then
                echo "  ❌ $(basename "$file"): Wrong case: $using_line"
                ERRORS=$((ERRORS + 1))
            fi
        done <<< "$usings"
    fi
done

# Summary
echo ""
if [ $ERRORS -eq 0 ]; then
    echo "✅ All checks passed! Case sensitivity looks correct for Unix environments."
else
    echo "❌ Found $ERRORS error(s). Please fix the issues above."
    exit 1
fi
