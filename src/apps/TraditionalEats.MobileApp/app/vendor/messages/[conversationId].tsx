import React from "react";
import {
  SafeAreaView,
  View,
  Text,
  StyleSheet,
  TouchableOpacity,
  KeyboardAvoidingView,
  Platform,
  useWindowDimensions,
} from "react-native";
import { Ionicons } from "@expo/vector-icons";
import { useRouter, useLocalSearchParams } from "expo-router";
import { useHeaderHeight } from "@react-navigation/elements";
import VendorChat from "../../../components/VendorChat";
import AppHeader from "../../../components/AppHeader";

export default function VendorMessageThreadScreen() {
  const router = useRouter();
  const params = useLocalSearchParams<{ conversationId?: string }>();
  const conversationId = params.conversationId ?? "";

  const headerHeight = useHeaderHeight();
  const { height: windowHeight } = useWindowDimensions();
  const chatMaxHeight = Math.round(windowHeight * 0.75);

  if (!conversationId) return null;

  return (
    <SafeAreaView style={styles.safe}>
      <KeyboardAvoidingView
        style={styles.kb}
        behavior={Platform.OS === "ios" ? "padding" : "height"}
        keyboardVerticalOffset={Platform.OS === "ios" ? headerHeight : 0}
      >
        <AppHeader title="Chat" />

        <View style={[styles.chatWrapper, { maxHeight: chatMaxHeight }]}>
          <VendorChat conversationId={conversationId} viewerRole="Vendor" />
        </View>
      </KeyboardAvoidingView>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: "#f5f5f5" },
  kb: { flex: 1 },
  header: {
    flexDirection: "row",
    alignItems: "center",
    justifyContent: "space-between",
    paddingHorizontal: 8,
    paddingVertical: 12,
    backgroundColor: "#fff",
    borderBottomWidth: 1,
    borderBottomColor: "#eee",
  },
  backButton: {
    width: 40,
    height: 40,
    alignItems: "center",
    justifyContent: "center",
  },
  title: {
    flex: 1,
    fontSize: 18,
    fontWeight: "600",
    color: "#333",
    textAlign: "center",
  },
  chatWrapper: { flex: 1, padding: 16 },
});

