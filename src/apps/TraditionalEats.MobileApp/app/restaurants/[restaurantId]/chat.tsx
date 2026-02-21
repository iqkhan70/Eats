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
  useWindowDimensions,
} from "react-native";
import { Ionicons } from "@expo/vector-icons";
import { useRouter, useLocalSearchParams } from "expo-router";
import { useHeaderHeight } from "@react-navigation/elements";
import VendorChat from "../../../components/VendorChat";
import { authService } from "../../../services/auth";
import { api } from "../../../services/api";
import { createOrGetVendorConversation } from "../../../services/vendorChat";

export default function RestaurantChatScreen() {
  const router = useRouter();
  const params = useLocalSearchParams<{ restaurantId?: string }>();
  const restaurantId = params.restaurantId ?? "";

  const headerHeight = useHeaderHeight();
  const { height: windowHeight } = useWindowDimensions();
  const chatMaxHeight = Math.round(windowHeight * 0.75);

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

        <View style={[styles.chatWrapper, { maxHeight: chatMaxHeight }]}>
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
  center: { flex: 1, alignItems: "center", justifyContent: "center" },
  loadingText: { marginTop: 12, color: "#666" },
  errorText: { color: "#c00" },
});
