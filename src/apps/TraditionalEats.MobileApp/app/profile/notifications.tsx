import React, { useState, useEffect } from "react";
import {
  View,
  Text,
  StyleSheet,
  ScrollView,
  Switch,
  ActivityIndicator,
} from "react-native";
import { useRouter } from "expo-router";
import AppHeader from "../../components/AppHeader";
import {
  getNotificationPreferences,
  setNotificationPreferences,
  NotificationPreferences,
} from "../../services/notificationPreferences";

function PreferenceRow({
  title,
  subtitle,
  value,
  onValueChange,
}: {
  title: string;
  subtitle: string;
  value: boolean;
  onValueChange: (v: boolean) => void;
}) {
  return (
    <View style={styles.row}>
      <View style={styles.rowContent}>
        <Text style={styles.rowTitle}>{title}</Text>
        <Text style={styles.rowSubtitle}>{subtitle}</Text>
      </View>
      <Switch
        value={value}
        onValueChange={onValueChange}
        trackColor={{ false: "#e5e7eb", true: "#fed7aa" }}
        thumbColor={value ? "#f97316" : "#f3f4f6"}
      />
    </View>
  );
}

export default function NotificationsScreen() {
  const router = useRouter();
  const [loading, setLoading] = useState(true);
  const [prefs, setPrefs] = useState<NotificationPreferences>({
    orderUpdates: true,
    promotions: false,
  });

  useEffect(() => {
    loadPrefs();
  }, []);

  const loadPrefs = async () => {
    try {
      setLoading(true);
      const p = await getNotificationPreferences();
      setPrefs(p);
    } finally {
      setLoading(false);
    }
  };

  const updatePref = async <K extends keyof NotificationPreferences>(
    key: K,
    value: NotificationPreferences[K]
  ) => {
    const next = { ...prefs, [key]: value };
    setPrefs(next);
    await setNotificationPreferences(next);
  };

  return (
    <View style={styles.container}>
      <AppHeader title="Notifications" onBack={() => router.back()} />
      {loading ? (
        <View style={styles.loading}>
          <ActivityIndicator size="large" color="#f97316" />
        </View>
      ) : (
        <ScrollView style={styles.scroll} contentContainerStyle={styles.content}>
          <View style={styles.card}>
            <PreferenceRow
              title="Order Updates"
              subtitle="Order confirmed, ready for pickup, completed"
              value={prefs.orderUpdates}
              onValueChange={(v) => updatePref("orderUpdates", v)}
            />
            <View style={styles.divider} />
            <PreferenceRow
              title="Promotions"
              subtitle="Discounts and marketing messages"
              value={prefs.promotions}
              onValueChange={(v) => updatePref("promotions", v)}
            />
          </View>
        </ScrollView>
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: "#f9fafb",
  },
  loading: {
    flex: 1,
    justifyContent: "center",
    alignItems: "center",
  },
  scroll: {
    flex: 1,
  },
  content: {
    padding: 16,
    paddingBottom: 32,
  },
  card: {
    backgroundColor: "#fff",
    borderRadius: 12,
    overflow: "hidden",
    shadowColor: "#000",
    shadowOffset: { width: 0, height: 1 },
    shadowOpacity: 0.05,
    shadowRadius: 2,
    elevation: 2,
  },
  row: {
    flexDirection: "row",
    alignItems: "center",
    justifyContent: "space-between",
    padding: 16,
  },
  rowContent: {
    flex: 1,
    marginRight: 12,
  },
  rowTitle: {
    fontSize: 16,
    fontWeight: "600",
    color: "#111827",
  },
  rowSubtitle: {
    fontSize: 13,
    color: "#6b7280",
    marginTop: 4,
    lineHeight: 18,
  },
  divider: {
    height: StyleSheet.hairlineWidth,
    backgroundColor: "#e5e7eb",
    marginLeft: 16,
  },
});
