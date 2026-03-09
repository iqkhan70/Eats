import React from "react";
import {
  View,
  Text,
  StyleSheet,
  ScrollView,
  TouchableOpacity,
} from "react-native";
import { Ionicons } from "@expo/vector-icons";
import { useRouter } from "expo-router";
import Constants from "expo-constants";
import AppHeader from "../../components/AppHeader";

function SettingRow({
  icon,
  label,
  onPress,
  value,
  isLast,
}: {
  icon: keyof typeof Ionicons.glyphMap;
  label: string;
  onPress?: () => void;
  value?: string;
  isLast?: boolean;
}) {
  const content = (
    <>
      <Ionicons name={icon} size={24} color="#f97316" />
      <Text style={styles.rowLabel}>{label}</Text>
      {value != null ? (
        <Text style={styles.rowValue}>{value}</Text>
      ) : (
        <Ionicons name="chevron-forward" size={20} color="#9ca3af" />
      )}
    </>
  );

  if (onPress) {
    return (
      <TouchableOpacity
        style={[styles.row, isLast && styles.rowLast]}
        onPress={onPress}
        activeOpacity={0.7}
      >
        {content}
      </TouchableOpacity>
    );
  }
  return <View style={[styles.row, isLast && styles.rowLast]}>{content}</View>;
}

export default function SettingsScreen() {
  const router = useRouter();
  const version = Constants.expoConfig?.version ?? "1.0.0";

  return (
    <View style={styles.container}>
      <AppHeader title="Settings" onBack={() => router.back()} />
      <ScrollView style={styles.scroll} contentContainerStyle={styles.scrollContent}>
        <Text style={styles.sectionTitle}>Settings</Text>
        <View style={styles.card}>
          <SettingRow
            icon="person-outline"
            label="Account"
            onPress={() => router.push("/profile/personal")}
          />
          <SettingRow
            icon="notifications-outline"
            label="Notifications"
            onPress={() => router.push("/profile/notifications")}
          />
          <SettingRow
            icon="help-circle-outline"
            label="Help & Support"
            onPress={() => router.push("/profile/support")}
            isLast
          />
        </View>

        <Text style={[styles.sectionTitle, { marginTop: 24 }]}>About</Text>
        <View style={styles.card}>
          <SettingRow
            icon="information-circle-outline"
            label="App Version"
            value={version}
          />
          <SettingRow
            icon="document-text-outline"
            label="Terms of Service"
            onPress={() => router.push("/profile/terms")}
          />
          <SettingRow
            icon="shield-outline"
            label="Privacy Policy"
            onPress={() => router.push("/profile/privacy")}
            isLast
          />
        </View>
      </ScrollView>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: "#f9fafb",
  },
  scroll: {
    flex: 1,
  },
  scrollContent: {
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
    padding: 16,
    borderBottomWidth: StyleSheet.hairlineWidth,
    borderBottomColor: "#e5e7eb",
  },
  rowLabel: {
    flex: 1,
    fontSize: 16,
    color: "#111827",
    marginLeft: 12,
  },
  rowLast: {
    borderBottomWidth: 0,
  },
});
