import React, { useEffect, useState } from "react";
import {
  SafeAreaView,
  View,
  Text,
  StyleSheet,
  TouchableOpacity,
  KeyboardAvoidingView,
  Platform,
  ActivityIndicator,
  Alert,
  Keyboard,
  TouchableWithoutFeedback,
} from "react-native";
import { Ionicons } from "@expo/vector-icons";
import { useRouter, useLocalSearchParams } from "expo-router";
import { useHeaderHeight } from "@react-navigation/elements";
import AsyncStorage from "@react-native-async-storage/async-storage";
import VendorChat from "../../../components/VendorChat";
import { authService } from "../../../services/auth";
import { api } from "../../../services/api";
import {
  createOrGetVendorConversation,
  getVendorConversationMessages,
} from "../../../services/vendorChat";

export default function RestaurantChatScreen() {
  const router = useRouter();
  const params = useLocalSearchParams<{ restaurantId?: string }>();
  const restaurantId = params.restaurantId ?? "";

  const headerHeight = useHeaderHeight();

  const [loading, setLoading] = useState(true);
  const [conversationId, setConversationId] = useState<string | null>(null);
  const [vendorName, setVendorName] = useState<string>("");

  useEffect(() => {
    let mounted = true;
    if (!restaurantId) return;

    (async () => {
      try {
        const { data } = await api.get<{ name?: string; Name?: string }>(
          `/MobileBff/restaurants/${restaurantId}`,
        );
        const name =
          typeof data?.name === "string"
            ? data.name
            : typeof data?.Name === "string"
              ? data.Name
              : "";
        if (mounted) setVendorName(name);
      } catch {
        if (mounted) setVendorName("");
      }
    })();

    return () => {
      mounted = false;
    };
  }, [restaurantId]);

  useEffect(() => {
    let mounted = true;
    (async () => {
      try {
        setLoading(true);
        const authenticated = await authService.isAuthenticated();
        if (!authenticated) {
          Alert.alert(
            "Login Required",
            "Please log in to chat with the vendor.",
          );
          router.replace("/login");
          return;
        }

        const isVendor = await authService.isVendor();
        const isAdmin = await authService.isAdmin();
        if (isVendor || isAdmin) {
          router.replace({
            pathname: "/vendor/messages",
            params: { restaurantId },
          } as any);
          return;
        }

        const convo = await createOrGetVendorConversation(restaurantId);
        if (mounted) setConversationId(convo.conversationId);

        // Mark as seen so the /messages unread dot clears even when entering chat from menu screen
        try {
          let viewerUserId = "";
          try {
            const id = await authService.getUserId();
            viewerUserId = typeof id === "string" ? id : "";
          } catch {
            viewerUserId = "";
          }

          const key = viewerUserId
            ? `vendor_chat_last_seen:${viewerUserId}:${convo.conversationId}`
            : `vendor_chat_last_seen:${convo.conversationId}`;
          const legacyKey = `vendor_chat_last_seen:${convo.conversationId}`;
          let seenAt = Date.now();
          try {
            const msgs = await getVendorConversationMessages(
              convo.conversationId,
            );
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
          // ignore
        }
      } catch (e: any) {
        console.error("Start vendor chat:", e);
        Alert.alert("Error", e?.message || "Failed to start chat");
        if (mounted) setConversationId(null);
      } finally {
        if (mounted) setLoading(false);
      }
    })();
    return () => {
      mounted = false;
    };
  }, [restaurantId, router]);

  if (!restaurantId) return null;

  return (
    <SafeAreaView style={styles.safe}>
      <KeyboardAvoidingView
        style={styles.kb}
        behavior={Platform.OS === "ios" ? "padding" : "height"}
        keyboardVerticalOffset={Platform.OS === "ios" ? headerHeight : 0}
      >
        <TouchableWithoutFeedback onPress={Keyboard.dismiss}>
          <View style={styles.kbInner}>
        <View style={styles.header}>
          <TouchableOpacity
            onPress={() => router.back()}
            style={styles.backButton}
            hitSlop={{ top: 12, bottom: 12, left: 12, right: 12 }}
          >
            <Ionicons name="chevron-back" size={28} color="#333" />
          </TouchableOpacity>

          <Text style={styles.title} numberOfLines={1}>
            {vendorName.trim() ? `${vendorName} – Chat` : "Vendor Chat"}
          </Text>

          <View style={styles.backButton} />
        </View>

        <View style={styles.chatWrapper}>
          {loading ? (
            <View style={styles.center}>
              <ActivityIndicator size="large" color="#6200ee" />
              <Text style={styles.loadingText}>Starting chat...</Text>
            </View>
          ) : conversationId ? (
            <VendorChat
              conversationId={conversationId}
              viewerRole="Customer"
              restaurantId={restaurantId}
              vendorName={vendorName}
            />
          ) : (
            <View style={styles.center}>
              <Text style={styles.errorText}>Could not start chat.</Text>
            </View>
          )}
        </View>
          </View>
        </TouchableWithoutFeedback>
      </KeyboardAvoidingView>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: "#f5f5f5" },
  kb: { flex: 1 },
  kbInner: { flex: 1 },
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
  center: { flex: 1, alignItems: "center", justifyContent: "center" },
  loadingText: { marginTop: 12, color: "#666" },
  errorText: { color: "#c00" },
});
