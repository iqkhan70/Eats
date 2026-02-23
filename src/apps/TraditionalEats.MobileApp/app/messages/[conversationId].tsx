import React from "react";
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
import VendorChat from "../../components/VendorChat";
import AppHeader from "../../components/AppHeader";

export default function MessageThreadScreen() {
  const params = useLocalSearchParams<{
    conversationId?: string;
    restaurantId?: string;
    vendorName?: string;
  }>();

  const conversationId = params.conversationId ?? "";
  const restaurantId =
    typeof params.restaurantId === "string" ? params.restaurantId : "";
  const vendorName = typeof params.vendorName === "string" ? params.vendorName : "";

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
        <AppHeader title={vendorName.trim() ? `${vendorName} – Chat` : "Chat"} />

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
