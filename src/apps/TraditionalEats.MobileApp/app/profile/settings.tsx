import React, { useState } from "react";
import {
  View,
  Text,
  StyleSheet,
  ScrollView,
  TouchableOpacity,
  Alert,
  ActivityIndicator,
} from "react-native";
import { Ionicons } from "@expo/vector-icons";
import { useRouter } from "expo-router";
import Constants from "expo-constants";
import AppHeader from "../../components/AppHeader";
import { authService } from "../../services/auth";

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
  const [deleting, setDeleting] = useState(false);

  const handleDeleteAccount = () => {
    Alert.alert(
      "Delete Account",
      "Are you sure you want to delete your account? This action is permanent and cannot be undone. All your data, order history, and saved information will be removed.",
      [
        { text: "Cancel", style: "cancel" },
        {
          text: "Delete My Account",
          style: "destructive",
          onPress: () => {
            Alert.alert(
              "Final Confirmation",
              "This will permanently delete your account. You will not be able to recover it.",
              [
                { text: "Go Back", style: "cancel" },
                {
                  text: "Delete Permanently",
                  style: "destructive",
                  onPress: async () => {
                    setDeleting(true);
                    try {
                      await authService.deleteAccount();
                      Alert.alert(
                        "Account Deleted",
                        "Your account has been permanently deleted.",
                        [
                          {
                            text: "OK",
                            onPress: () => router.replace("/(tabs)"),
                          },
                        ]
                      );
                    } catch (e: any) {
                      Alert.alert(
                        "Error",
                        e?.response?.data?.message ||
                          e?.message ||
                          "Failed to delete account. Please try again."
                      );
                    } finally {
                      setDeleting(false);
                    }
                  },
                },
              ]
            );
          },
        },
      ]
    );
  };

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

        <Text style={[styles.sectionTitle, { marginTop: 24, color: "#d32f2f" }]}>
          Danger Zone
        </Text>
        <View style={styles.card}>
          <TouchableOpacity
            style={[styles.row, styles.rowLast]}
            onPress={handleDeleteAccount}
            activeOpacity={0.7}
            disabled={deleting}
          >
            {deleting ? (
              <ActivityIndicator size="small" color="#d32f2f" />
            ) : (
              <Ionicons name="trash-outline" size={24} color="#d32f2f" />
            )}
            <Text style={[styles.rowLabel, { color: "#d32f2f" }]}>
              Delete Account
            </Text>
            <Ionicons name="chevron-forward" size={20} color="#d32f2f" />
          </TouchableOpacity>
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
  rowValue: {
    fontSize: 14,
    color: "#6b7280",
  },
  rowLast: {
    borderBottomWidth: 0,
  },
  sectionTitle: {
    fontSize: 14,
    fontWeight: "600",
    color: "#6b7280",
    marginBottom: 8,
    textTransform: "uppercase",
    letterSpacing: 0.5,
  },
});
