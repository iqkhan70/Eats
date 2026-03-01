import React, { useEffect } from "react";
import {
  View,
  Text,
  StyleSheet,
  KeyboardAvoidingView,
  Platform,
  SafeAreaView,
  Keyboard,
  TouchableWithoutFeedback,
} from "react-native";
import { Ionicons } from "@expo/vector-icons";
import { useRouter, useLocalSearchParams } from "expo-router";
import { useHeaderHeight } from "@react-navigation/elements";
import AsyncStorage from "@react-native-async-storage/async-storage";
import OrderChat from "../../../components/OrderChat";
import AppHeader from "../../../components/AppHeader";
import { authService } from "../../../services/auth";
import { api } from "../../../services/api";
import { getVendorInbox } from "../../../services/vendorChat";

export default function OrderChatScreen() {
  const router = useRouter();
  const params = useLocalSearchParams<{
    orderId?: string;
    restaurantName?: string;
  }>();
  const orderId = params.orderId ?? "";
  const restaurantName =
    typeof params.restaurantName === "string" ? params.restaurantName : "";

  // ✅ This returns the real navigation header height (even if you aren't rendering one,
  // it still helps set a correct keyboard offset on iOS)
  const headerHeight = useHeaderHeight();

  if (!orderId) return null;

  useEffect(() => {
    if (!orderId) return;

    void (async () => {
      try {
        const isVendor = await authService.isVendor();
        if (!isVendor) return;

        // Resolve (restaurantId, customerId) for this order, then find the matching vendor conversation
        const { data: order } = await api.get<{
          restaurantId?: string;
          customerId?: string;
        }>(`/MobileBff/orders/${orderId}`);

        const restaurantId =
          typeof order?.restaurantId === "string" ? order.restaurantId : "";
        const customerId =
          typeof order?.customerId === "string" ? order.customerId : "";
        if (!restaurantId || !customerId) return;

        const inbox = await getVendorInbox();
        const match = inbox.find(
          (c) => c.restaurantId === restaurantId && c.customerId === customerId,
        );
        if (!match?.conversationId) return;

        let viewerUserId = "";
        try {
          const id = await authService.getUserId();
          viewerUserId = typeof id === "string" ? id : "";
        } catch {
          viewerUserId = "";
        }

        const key = viewerUserId
          ? `vendor_inbox_last_seen:${viewerUserId}:${match.conversationId}`
          : `vendor_inbox_last_seen:${match.conversationId}`;
        const legacyKey = `vendor_inbox_last_seen:${match.conversationId}`;

        const anyMatch = match as any;
        const last =
          typeof match.lastMessageAt === "string"
            ? match.lastMessageAt
            : typeof anyMatch?.LastMessageAt === "string"
              ? anyMatch.LastMessageAt
              : "";
        const updated =
          typeof match.updatedAt === "string"
            ? match.updatedAt
            : typeof anyMatch?.UpdatedAt === "string"
              ? anyMatch.UpdatedAt
              : "";
        const activityTs = Math.max(
          last ? new Date(last).getTime() : 0,
          updated ? new Date(updated).getTime() : 0,
        );
        const baseSeenAt =
          Number.isFinite(activityTs) && activityTs > 0
            ? activityTs
            : Date.now();
        const value = String(Math.max(Date.now(), baseSeenAt));
        await AsyncStorage.setItem(key, value);
        await AsyncStorage.setItem(legacyKey, value);
      } catch {
        return;
      }
    })();
  }, [orderId]);

  return (
    <SafeAreaView style={styles.safe}>
      <KeyboardAvoidingView
        style={styles.kb}
        behavior={Platform.OS === "ios" ? "padding" : "height"}
        // ✅ IMPORTANT: iOS needs an offset so input sits ABOVE the keyboard.
        // If you use a custom header (like you do), this still works well.
        keyboardVerticalOffset={Platform.OS === "ios" ? headerHeight : 0}
      >
        <AppHeader
          title={
            restaurantName.trim()
              ? `${restaurantName} – Chat`
              : `Order #${orderId.substring(0, 8)} – Chat`
          }
        />

        <TouchableWithoutFeedback onPress={Keyboard.dismiss}>
          <View style={styles.chatWrapper}>
            <OrderChat orderId={orderId} fullScreen />
          </View>
        </TouchableWithoutFeedback>
      </KeyboardAvoidingView>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  safe: {
    flex: 1,
    backgroundColor: "#f5f5f5",
  },
  kb: {
    flex: 1,
  },
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
  chatWrapper: {
    flex: 1,
    padding: 16,
  },
});
