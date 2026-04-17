import AsyncStorage from "@react-native-async-storage/async-storage";
import * as Application from "expo-application";
import * as Crypto from "expo-crypto";
import Constants from "expo-constants";
import { Platform } from "react-native";
import { api } from "./api";

const PUSH_DEVICE_ID_KEY = "push_device_id";
const LAST_PUSH_TOKEN_KEY = "last_push_token";
const PENDING_NOTIFICATION_URL_KEY = "pending_notification_url";
let notificationsConfigured = false;

type NotificationsModule = Awaited<typeof import("expo-notifications")>;

export async function configurePushNotificationsAsync(): Promise<void> {
  const Notifications = await getNotificationsModuleAsync();
  if (!Notifications || notificationsConfigured) return;

  Notifications.setNotificationHandler({
    handleNotification: async () => ({
      shouldShowBanner: true,
      shouldShowList: true,
      shouldPlaySound: true,
      shouldSetBadge: false,
    }),
  });

  if (Platform.OS === "android") {
    await Notifications.setNotificationChannelAsync("orders", {
      name: "Orders",
      importance: Notifications.AndroidImportance.MAX,
      vibrationPattern: [0, 250, 250, 250],
      lightColor: "#f97316",
      sound: "default",
      lockscreenVisibility: Notifications.AndroidNotificationVisibility.PUBLIC,
    });
  }

  notificationsConfigured = true;
}

export async function syncPushTokenAsync(): Promise<void> {
  try {
    await configurePushNotificationsAsync();
    const Notifications = await getNotificationsModuleAsync();
    if (!Notifications) return;

    const { status: existingStatus } = await Notifications.getPermissionsAsync();
    let finalStatus = existingStatus;

    if (existingStatus !== "granted") {
      const permissionResult = await Notifications.requestPermissionsAsync();
      finalStatus = permissionResult.status;
    }

    if (finalStatus !== "granted") {
      return;
    }

    const projectId =
      Constants.easConfig?.projectId ??
      Constants.expoConfig?.extra?.eas?.projectId;

    if (!projectId) {
      console.warn("Push notifications: missing Expo project ID");
      return;
    }

    const tokenResult = await Notifications.getExpoPushTokenAsync({ projectId });
    const pushToken = tokenResult.data?.trim();
    if (!pushToken) return;

    const deviceId = await getOrCreateDeviceIdAsync();
    const platform = Platform.OS === "ios" ? "ios" : "android";
    const deviceName =
      Application.applicationName?.trim() || Constants.deviceName?.trim() || null;

    await api.post("/MobileBff/notifications/push-tokens", {
      pushToken,
      deviceId,
      platform,
      deviceName,
    });

    await AsyncStorage.setItem(LAST_PUSH_TOKEN_KEY, pushToken);
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    if (Platform.OS === "android") {
      console.warn(
        "Push notification sync failed on Android. Verify Firebase/FCM is configured and app config points to google-services.json.",
        message,
      );
    } else {
      console.warn("Push notification sync failed:", message);
    }
  }
}

export async function unregisterPushTokenAsync(): Promise<void> {
  try {
    const [pushToken, deviceId] = await Promise.all([
      AsyncStorage.getItem(LAST_PUSH_TOKEN_KEY),
      AsyncStorage.getItem(PUSH_DEVICE_ID_KEY),
    ]);

    if (!pushToken && !deviceId) {
      return;
    }

    await api.delete("/MobileBff/notifications/push-tokens", {
      data: {
        pushToken: pushToken ?? undefined,
        deviceId: deviceId ?? undefined,
      },
    });
  } catch (error) {
    console.warn("Push notification unregister failed:", error);
  }
}

export async function setPendingNotificationUrlAsync(
  url: string | null | undefined,
): Promise<void> {
  const normalizedUrl = typeof url === "string" ? url.trim() : "";
  if (!normalizedUrl) {
    await AsyncStorage.removeItem(PENDING_NOTIFICATION_URL_KEY);
    return;
  }

  await AsyncStorage.setItem(PENDING_NOTIFICATION_URL_KEY, normalizedUrl);
}

export async function consumePendingNotificationUrlAsync(): Promise<string | null> {
  const url = await AsyncStorage.getItem(PENDING_NOTIFICATION_URL_KEY);
  await AsyncStorage.removeItem(PENDING_NOTIFICATION_URL_KEY);
  return url?.trim() || null;
}

function getNotificationUrl(
  response: {
    notification?: {
      request?: {
        content?: {
          data?: Record<string, unknown>;
        };
      };
    };
  } | null | undefined,
): string | null {
  const data = response?.notification.request.content.data;
  const orderId = typeof data?.orderId === "string" ? data.orderId.trim() : "";
  const restaurantId =
    typeof data?.restaurantId === "string" ? data.restaurantId.trim() : "";
  const urlValue = data?.url;
  if (orderId && restaurantId) {
    return `/vendor/orders/${encodeURIComponent(orderId)}?restaurantId=${encodeURIComponent(restaurantId)}`;
  }

  return typeof urlValue === "string" && urlValue.trim() ? urlValue : null;
}

export function observeNotificationResponses(
  onUrl: (url: string) => void,
): () => void {
  let lastHandledUrl: string | null = null;
  let subscription: { remove: () => void } | null = null;

  const maybeHandleUrl = (url: string | null) => {
    if (!url || url === lastHandledUrl) return;
    lastHandledUrl = url;
    onUrl(url);
  };

  void getNotificationsModuleAsync().then((Notifications) => {
    if (!Notifications) return;

    void Notifications.getLastNotificationResponseAsync().then((response) => {
      maybeHandleUrl(getNotificationUrl(response));
    });

    subscription = Notifications.addNotificationResponseReceivedListener(
      (response) => {
        maybeHandleUrl(getNotificationUrl(response));
      },
    );
  });

  return () => {
    subscription?.remove();
  };
}

async function getOrCreateDeviceIdAsync(): Promise<string> {
  const existing = await AsyncStorage.getItem(PUSH_DEVICE_ID_KEY);
  if (existing?.trim()) return existing;

  const newId =
    typeof Crypto.randomUUID === "function"
      ? Crypto.randomUUID()
      : `${Date.now()}-${Math.random().toString(36).slice(2)}`;

  await AsyncStorage.setItem(PUSH_DEVICE_ID_KEY, newId);
  return newId;
}

async function getNotificationsModuleAsync(): Promise<NotificationsModule | null> {
  if (isExpoGoAndroid()) {
    return null;
  }

  try {
    return await import("expo-notifications");
  } catch (error) {
    console.warn("Push notifications unavailable:", error);
    return null;
  }
}

function isExpoGoAndroid(): boolean {
  if (Platform.OS !== "android") return false;

  const executionEnvironment = Constants.executionEnvironment;
  const appOwnership = Constants.appOwnership;

  return (
    executionEnvironment === "storeClient" ||
    appOwnership === "expo"
  );
}
