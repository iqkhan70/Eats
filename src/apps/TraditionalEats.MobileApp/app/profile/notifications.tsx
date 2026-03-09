import React from "react";
import { View, Text, StyleSheet } from "react-native";
import { useRouter } from "expo-router";
import AppHeader from "../../components/AppHeader";

export default function NotificationsScreen() {
  const router = useRouter();

  return (
    <View style={styles.container}>
      <AppHeader title="Notifications" onBack={() => router.back()} />
      <View style={styles.content}>
        <Text style={styles.text}>
          Notification preferences will be available here soon.
        </Text>
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: "#f9fafb",
  },
  content: {
    flex: 1,
    padding: 20,
    justifyContent: "center",
  },
  text: {
    fontSize: 16,
    color: "#6b7280",
    textAlign: "center",
  },
});
