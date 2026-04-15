import { Stack } from "expo-router";
import { StatusBar } from "expo-status-bar";
import { SafeAreaProvider } from "react-native-safe-area-context";
import { useEffect } from "react";
import { authService } from "../services/auth";
import {
  configurePushNotificationsAsync,
  observeNotificationResponses,
  syncPushTokenAsync,
} from "../services/pushNotifications";
import { useRouter } from "expo-router";

function PushNotificationsBootstrap() {
  const router = useRouter();

  useEffect(() => {
    void configurePushNotificationsAsync();

    void authService.isAuthenticated().then((isAuthenticated) => {
      if (isAuthenticated) {
        void authService.getUserRoles().then((roles) => {
          if (roles.includes("Vendor") || roles.includes("Staff") || roles.includes("Admin")) {
            void syncPushTokenAsync();
          }
        });
      }
    });

    return observeNotificationResponses((url) => {
      router.push(url as never);
    });
  }, [router]);

  return null;
}

export default function RootLayout() {
  return (
    <SafeAreaProvider>
      <PushNotificationsBootstrap />
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
