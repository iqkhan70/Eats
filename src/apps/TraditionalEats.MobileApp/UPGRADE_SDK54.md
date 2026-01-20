# Upgrading to Expo SDK 54 - Step by Step

## The Problem
If `expo install --fix` is detecting SDK 50 instead of SDK 54, it's because the installed packages don't match SDK 54 yet.

## Solution

### Step 1: Clean install
```bash
# Remove node_modules and lock file
rm -rf node_modules package-lock.json

# Install dependencies fresh
npm install
```

### Step 2: Verify expo version
```bash
# Check what version of expo is installed
npm list expo
```

It should show `expo@54.x.x`. If it shows 50.x.x, you need to install expo 54 first:
```bash
npm install expo@~54.0.0
```

### Step 3: Fix dependencies
```bash
# This will install SDK 54 compatible versions
npx expo install --fix
```

### Step 4: If you still get conflicts
If `expo install --fix` still tries to install SDK 50 packages, try:

```bash
# Install with legacy peer deps to resolve conflicts
npm install --legacy-peer-deps

# Then run expo install --fix again
npx expo install --fix
```

### Step 5: Verify installation
```bash
# Check for any issues
npx expo-doctor
```

## Expected Versions for SDK 54

- `expo`: ~54.0.0
- `react`: 18.3.1
- `react-native`: 0.76.5
- `expo-router`: ~4.0.0
- `react-native-maps`: 1.18.0

## Troubleshooting

If you continue to have issues:

1. Delete `node_modules` and `package-lock.json`
2. Make sure `package.json` has `expo: ~54.0.0`
3. Run `npm install expo@~54.0.0` first
4. Then run `npx expo install --fix`

The key is that `expo install --fix` reads the installed `expo` package version to determine which SDK to use. If `expo@50` is in node_modules, it will try to install SDK 50 packages.
