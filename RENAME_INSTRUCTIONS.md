# Renaming TraditionalEats to TraditionalEats

## Overview

This document explains how to rename the entire project from `TraditionalEats` to `TraditionalEats`, including:
- Folder names
- Project files (.csproj)
- Namespaces
- Package names
- Solution file
- All references in code

## Automated Script

A script has been created to automate this process: `rename-to-traditional-eats.sh`

### To Run the Script:

```bash
cd /Users/mohammedkhan/iq/Eats
./rename-to-traditional-eats.sh
```

**⚠️ Important:** 
- Make sure you have committed or backed up your work
- Close your IDE before running
- The script will rename folders, files, and update content

## What Gets Renamed

### Folders:
- `src/services/TraditionalEats.*` → `src/services/TraditionalEats.*`
- `src/bff/TraditionalEats.*` → `src/bff/TraditionalEats.*`
- `src/gateway/TraditionalEats.*` → `src/gateway/TraditionalEats.*`
- `src/apps/TraditionalEats.*` → `src/apps/TraditionalEats.*`
- `src/shared/TraditionalEats.*` → `src/shared/TraditionalEats.*`
- `TraditionalEats.sln` → `TraditionalEats.sln`

### Files:
- All `.csproj` files
- All code files (`.cs`, `.razor`, `.ts`, `.tsx`)
- Configuration files (`.json`, `.md`)
- Solution file (`.sln`)

### Content:
- Namespaces: `namespace TraditionalEats.*` → `namespace TraditionalEats.*`
- Project references
- Package names in `package.json`
- All string references

## After Running the Script

1. **Close and reopen your IDE** (VS Code, Rider, Visual Studio)
2. **Restore packages:**
   ```bash
   dotnet restore
   ```
3. **Rebuild solution:**
   ```bash
   dotnet build
   ```
4. **Update Git (if needed):**
   ```bash
   git add -A
   git commit -m "Rename TraditionalEats to TraditionalEats"
   ```

## Manual Steps (if script doesn't work)

If the script fails or you prefer manual steps:

1. **Rename folders** one by one
2. **Rename .csproj files** to match new folder names
3. **Update solution file** to reference new paths
4. **Find and replace** in all files:
   - Find: `TraditionalEats`
   - Replace: `TraditionalEats`

## Verification

After renaming, verify:
- ✅ Solution file opens correctly
- ✅ All projects build successfully
- ✅ No broken references
- ✅ Mobile app `package.json` updated
- ✅ All namespaces are correct

## Rollback

If something goes wrong:
```bash
git checkout .
git clean -fd
```

This will restore everything to the last commit.
