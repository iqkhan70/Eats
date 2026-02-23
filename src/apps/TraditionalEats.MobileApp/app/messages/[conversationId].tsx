import React, { useEffect } from "react";
import {
  KeyboardAvoidingView,
  Platform,
  SafeAreaView,
  StyleSheet,
  useWindowDimensions,
  View,
} from "react-native";
import { useLocalSearchParams } from "expo-router";
import { useHeaderHeight } from "@react-navigation/elements";
import AsyncStorage from "@react-native-async-storage/async-storage";
import VendorChat from "../../components/VendorChat";
import AppHeader from "../../components/AppHeader";
import { getVendorConversationMessages } from "../../services/vendorChat";
import { authService } from "../../services/auth";

export default function MessageThreadScreen() {
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

        const msgs = await getVendorConversationMessages(conversationId);
        const latest = (msgs ?? []).reduce((max, m) => {
          const t = m?.sentAt ? new Date(m.sentAt).getTime() : 0;
          return Number.isFinite(t) && t > max ? t : max;
        }, 0);

        const seenAt = Math.max(Date.now(), latest > 0 ? latest : 0);
        const key = viewerUserId
          ? `vendor_chat_last_seen:${viewerUserId}:${conversationId}`
          : `vendor_chat_last_seen:${conversationId}`;
        const legacyKey = `vendor_chat_last_seen:${conversationId}`;
        await AsyncStorage.setItem(key, String(seenAt));
        await AsyncStorage.setItem(legacyKey, String(seenAt));
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
            viewerRole="Customer"
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
  chatWrapper: { flex: 1, padding: 16 },
});
