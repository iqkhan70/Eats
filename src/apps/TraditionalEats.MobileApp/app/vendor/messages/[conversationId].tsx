import React, { useEffect } from "react";
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
import AsyncStorage from "@react-native-async-storage/async-storage";
import { getVendorConversationMessages } from "../../../services/vendorChat";
import VendorChat from "../../../components/VendorChat";
import AppHeader from "../../../components/AppHeader";
import { authService } from "../../../services/auth";

export default function VendorMessageThreadScreen() {
  const router = useRouter();
  const params = useLocalSearchParams<{
    conversationId?: string;
    restaurantId?: string;
    vendorName?: string;
  }>();
  const conversationId = params.conversationId ?? "";
  const restaurantId =
    typeof params.restaurantId === "string" ? params.restaurantId : "";
  const vendorName =
    typeof params.vendorName === "string" ? params.vendorName : "";

  const headerHeight = useHeaderHeight();
  const { height: windowHeight } = useWindowDimensions();
  const chatMaxHeight = Math.round(windowHeight * 0.75);

  useEffect(() => {
    if (!conversationId) return;
    void (async () => {
      try {
        let viewerUserId = "";
        try {
          const id = await authService.getUserId();
          viewerUserId = typeof id === "string" ? id : "";
        } catch {
          viewerUserId = "";
        }

        const key = viewerUserId
          ? `vendor_inbox_last_seen:${viewerUserId}:${conversationId}`
          : `vendor_inbox_last_seen:${conversationId}`;
        const legacyKey = `vendor_inbox_last_seen:${conversationId}`;
        let seenAt = Date.now();
        try {
          const msgs = await getVendorConversationMessages(conversationId);
          const latest = (msgs ?? []).reduce((max, m) => {
            const t = m?.sentAt ? new Date(m.sentAt).getTime() : 0;
            return Number.isFinite(t) && t > max ? t : max;
          }, 0);
          if (latest > 0) seenAt = latest;
        } catch {
          // ignore
        }
        const value = String(Math.max(Date.now(), seenAt));
        await AsyncStorage.setItem(key, value);
        await AsyncStorage.setItem(legacyKey, value);
      } catch {
        return;
      }
    })();
  }, [conversationId]);

  if (!conversationId) return null;

  return (
    <SafeAreaView style={styles.safe}>
      <KeyboardAvoidingView
        style={styles.kb}
        behavior={Platform.OS === "ios" ? "padding" : "height"}
        keyboardVerticalOffset={Platform.OS === "ios" ? headerHeight : 0}
      >
        <AppHeader
          title={vendorName.trim() ? `${vendorName} – Chat` : "Chat"}
        />

        <View style={[styles.chatWrapper, { maxHeight: chatMaxHeight }]}>
          <VendorChat
            conversationId={conversationId}
            viewerRole="Vendor"
            restaurantId={restaurantId || undefined}
            vendorName={vendorName || undefined}
          />
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
