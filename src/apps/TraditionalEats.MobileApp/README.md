# TraditionalEats Mobile App

React Native mobile application built with Expo for the TraditionalEats platform.

## Prerequisites

- Node.js (v20.19.4 or higher) - Required for Expo SDK 54
- npm or yarn
- Expo CLI: `npm install -g expo-cli` (or use `npx expo` without global install)
- iOS Simulator (for Mac) with Xcode 16.1+ or Android Emulator
- Expo Go app on your physical device (optional)

**Note**: This project uses Expo SDK 54. Make sure your Node.js version is compatible.

## Setup

1. **Install dependencies** (first time only):
```bash
npm install
```

2. **Install missing peer dependencies**:
```bash
npx expo install expo-font expo-constants expo-linking
```

3. **Create placeholder assets** (if assets don't exist):
```bash
# Run the Node.js script to create minimal placeholder PNGs
npm run create-assets

# Or manually: Create icon.png, splash.png, adaptive-icon.png, favicon.png
# You can use online tools like https://www.appicon.co/
```

4. **Fix dependency versions** (after updating to SDK 54):
```bash
# Use legacy peer deps to resolve conflicts
npm install --legacy-peer-deps

# Then fix Expo packages
npx expo install --fix
```

   This ensures all Expo packages are compatible with SDK 54. The `.npmrc` file is configured to use `legacy-peer-deps` by default.

3. **Start the development server**:
```bash
npm start
```

   This will:
   - Start the Expo development server
   - Show a QR code you can scan with Expo Go app
   - Display options to open in iOS simulator, Android emulator, or web

4. **Run on specific platform**:
```bash
# iOS Simulator (Mac only)
npm run ios

# Android Emulator
npm run android

# Web browser
npm run web
```

**Note**: 
- Make sure the Mobile BFF is running on port 5102 for the app to connect to the backend.
- If you encounter dependency issues, run `npx expo-doctor` to diagnose problems.

## Project Structure

```
app/
  (tabs)/          # Tab navigation screens
    index.tsx      # Home screen
    restaurants.tsx
    orders.tsx
    profile.tsx
  _layout.tsx      # Root layout
  index.tsx        # Welcome/landing screen

services/
  api.ts          # API client with axios
  auth.ts         # Authentication service

types/
  index.ts        # TypeScript type definitions
```

## Features

- **Home Screen**: Search, categories, nearby restaurants
- **Restaurants**: Browse and search restaurants
- **Orders**: View order history
- **Profile**: User profile and settings
- **Authentication**: Login/Register (to be implemented)
- **API Integration**: Ready for backend integration

## Configuration

Update the API base URL in `services/api.ts`:
- Development: `http://localhost:5102/api` (Mobile BFF)
- Production: `https://api.traditionaleats.com/api`

## Upgrading to SDK 54

If you're upgrading from SDK 50, follow these steps:

1. Update `package.json` (already done)
2. Run `npm install` to install new dependencies
3. Run `npx expo install --fix` to fix any version mismatches
4. Run `npx expo-doctor` to check for issues
5. If using native code, you may need to regenerate iOS/Android folders

## Next Steps

1. Implement authentication screens (login/register)
2. Connect to backend API endpoints
3. Add restaurant menu viewing
4. Implement cart and checkout
5. Add order tracking
6. Integrate location services
7. Add push notifications
