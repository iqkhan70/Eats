import React from "react";
import {
  View,
  Text,
  StyleSheet,
  TouchableOpacity,
  KeyboardAvoidingView,
  Platform,
  SafeAreaView,
  useWindowDimensions,
} from "react-native";
import { Ionicons } from "@expo/vector-icons";
import { useRouter, useLocalSearchParams } from "expo-router";
import { useHeaderHeight } from "@react-navigation/elements";
import OrderChat from "../../../components/OrderChat";
import AppHeader from "../../../components/AppHeader";

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
  const { height: windowHeight } = useWindowDimensions();

  if (!orderId) return null;

  // Keep chat height under ~75% of window so send button is visible without scrolling
  const chatMaxHeight = Math.round(windowHeight * 0.75);

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

        {/* Chat */}
        <View style={[styles.chatWrapper, { maxHeight: chatMaxHeight }]}>
          <OrderChat orderId={orderId} fullScreen />
        </View>
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
