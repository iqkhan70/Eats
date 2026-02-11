import { Stack } from "expo-router";
import { StatusBar } from "expo-status-bar";
import { SafeAreaProvider } from "react-native-safe-area-context";

export default function RootLayout() {
  return (
    <SafeAreaProvider>
      <Stack
        screenOptions={{
          headerStyle: {
            backgroundColor: "#6200ee",
          },
          headerTintColor: "#fff",
          headerTitleStyle: {
            fontWeight: "bold",
          },
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
