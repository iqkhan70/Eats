import { Stack } from "expo-router";
import { StatusBar } from "expo-status-bar";
import { SafeAreaProvider } from "react-native-safe-area-context";
import { useCallback, useEffect, useState } from "react";
import { authService } from "../services/auth";
import {
  consumePendingNotificationUrlAsync,
  configurePushNotificationsAsync,
  observeNotificationResponses,
  setPendingNotificationUrlAsync,
  syncPushTokenAsync,
} from "../services/pushNotifications";
import { useRootNavigationState, useRouter } from "expo-router";

const NOTIFICATION_NAVIGATION_DELAY_MS = 500;

function PushNotificationsBootstrap() {
  const router = useRouter();
  const rootNavigationState = useRootNavigationState();
  const [pendingNotificationUrl, setPendingNotificationUrl] = useState<string | null>(null);

  const replaceWithDelay = useCallback(
    (url: string) => {
      setTimeout(() => {
        router.replace(url as never);
      }, NOTIFICATION_NAVIGATION_DELAY_MS);
    },
    [router],
  );

  const canOpenNotificationUrl = useCallback(async (url: string) => {
    if (!url.startsWith("/vendor/")) return true;

    const roles = await authService.getUserRoles();
    return (
      roles.includes("Vendor") ||
      roles.includes("Staff") ||
      roles.includes("Admin")
    );
  }, []);

  const navigateToNotificationUrl = useCallback(
    async (url: string) => {
      if (!url) return;

      if (!rootNavigationState?.key) {
        setPendingNotificationUrl(url);
        return;
      }

      setPendingNotificationUrl((currentUrl) => (currentUrl === url ? null : currentUrl));

      const isAuthenticated = await authService.isAuthenticated();
      if (!isAuthenticated) {
        await setPendingNotificationUrlAsync(url);
        requestAnimationFrame(() => {
          replaceWithDelay("/login");
        });
        return;
      }

      const canOpenUrl = await canOpenNotificationUrl(url);
      if (!canOpenUrl) {
        await setPendingNotificationUrlAsync(null);
        requestAnimationFrame(() => {
          replaceWithDelay("/(tabs)");
        });
        return;
      }

      requestAnimationFrame(() => {
        replaceWithDelay(url);
      });
    },
    [canOpenNotificationUrl, replaceWithDelay, rootNavigationState?.key],
  );

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
      void navigateToNotificationUrl(url);
    });
  }, [navigateToNotificationUrl]);

  useEffect(() => {
    if (!rootNavigationState?.key || !pendingNotificationUrl) return;

    const url = pendingNotificationUrl;
    setPendingNotificationUrl(null);
    void navigateToNotificationUrl(url);
  }, [navigateToNotificationUrl, pendingNotificationUrl, rootNavigationState?.key]);

  useEffect(() => {
    if (!rootNavigationState?.key) return;

    void authService.isAuthenticated().then(async (isAuthenticated) => {
      if (!isAuthenticated) return;

      const pendingUrl = await consumePendingNotificationUrlAsync();
      if (!pendingUrl) return;

      const canOpenUrl = await canOpenNotificationUrl(pendingUrl);
      if (!canOpenUrl) {
        requestAnimationFrame(() => {
          replaceWithDelay("/(tabs)");
        });
        return;
      }

      requestAnimationFrame(() => {
        replaceWithDelay(pendingUrl);
      });
    });
  }, [canOpenNotificationUrl, replaceWithDelay, rootNavigationState?.key]);

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
