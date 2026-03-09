import AsyncStorage from "@react-native-async-storage/async-storage";

const STORAGE_KEY = "notificationPreferences";

export interface NotificationPreferences {
  orderUpdates: boolean;
  promotions: boolean;
}

const DEFAULTS: NotificationPreferences = {
  orderUpdates: true,
  promotions: false,
};

export async function getNotificationPreferences(): Promise<NotificationPreferences> {
  try {
    const raw = await AsyncStorage.getItem(STORAGE_KEY);
    if (!raw) return { ...DEFAULTS };
    const parsed = JSON.parse(raw) as Partial<NotificationPreferences>;
    return {
      orderUpdates: parsed.orderUpdates ?? DEFAULTS.orderUpdates,
      promotions: parsed.promotions ?? DEFAULTS.promotions,
    };
  } catch {
    return { ...DEFAULTS };
  }
}

export async function setNotificationPreferences(
  prefs: NotificationPreferences
): Promise<void> {
  await AsyncStorage.setItem(STORAGE_KEY, JSON.stringify(prefs));
}
