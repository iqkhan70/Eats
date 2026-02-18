import { Stack } from "expo-router";
import { StatusBar } from "expo-status-bar";
import { SafeAreaProvider } from "react-native-safe-area-context";

export default function RootLayout() {
  return (
    <SafeAreaProvider>
      <Stack
        screenOptions={{
          // We render a consistent in-app header in screens (SafeAreaView)
          // to avoid duplicate headers and to keep back button behavior
          // consistent across screens.
          headerShown: false,
        }}
      >
        <Stack.Screen name="(tabs)" options={{ headerShown: false }} />
        <Stack.Screen name="index" options={{ title: "Kram" }} />
        <Stack.Screen name="login" options={{ title: "Sign In" }} />
        <Stack.Screen
          name="register"
          options={{ title: "Sign Up", presentation: "modal" }}
        />
      </Stack>
      <StatusBar style="light" />
    </SafeAreaProvider>
  );
}
