import React from "react";
import { View, Text, StyleSheet, ScrollView, TouchableOpacity } from "react-native";
import { Ionicons } from "@expo/vector-icons";
import { useRouter } from "expo-router";
import AppHeader from "../../components/AppHeader";

export default function PaymentsScreen() {
  const router = useRouter();

  return (
    <View style={styles.container}>
      <AppHeader title="Payment Methods" onBack={() => router.back()} />
      <ScrollView style={styles.scroll} contentContainerStyle={styles.content}>
        <View style={styles.card}>
          <View style={styles.iconRow}>
            <Ionicons name="card-outline" size={40} color="#f97316" />
          </View>
          <Text style={styles.title}>Pay at checkout</Text>
          <Text style={styles.body}>
            When you place an order, you'll be taken to our secure checkout page to enter your
            payment details. We use Stripe to process payments—your card information is handled
            securely and never stored in our app.
          </Text>
          <Text style={styles.body}>
            No need to add a card here. Just add items to your cart, place your order, and pay
            when you're ready.
          </Text>
        </View>

        <TouchableOpacity
          style={styles.helpRow}
          onPress={() => router.push("/profile/support")}
          activeOpacity={0.7}
        >
          <Ionicons name="help-circle-outline" size={24} color="#f97316" />
          <Text style={styles.helpText}>Need help with payment?</Text>
          <Ionicons name="chevron-forward" size={20} color="#9ca3af" />
        </TouchableOpacity>
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
  content: {
    padding: 16,
    paddingBottom: 32,
  },
  card: {
    backgroundColor: "#fff",
    borderRadius: 12,
    padding: 20,
    shadowColor: "#000",
    shadowOffset: { width: 0, height: 1 },
    shadowOpacity: 0.05,
    shadowRadius: 2,
    elevation: 2,
  },
  iconRow: {
    alignItems: "center",
    marginBottom: 16,
  },
  title: {
    fontSize: 18,
    fontWeight: "600",
    color: "#111827",
    marginBottom: 12,
    textAlign: "center",
  },
  body: {
    fontSize: 15,
    color: "#4b5563",
    lineHeight: 24,
    marginBottom: 12,
  },
  helpRow: {
    flexDirection: "row",
    alignItems: "center",
    backgroundColor: "#fff",
    borderRadius: 12,
    padding: 16,
    marginTop: 16,
    shadowColor: "#000",
    shadowOffset: { width: 0, height: 1 },
    shadowOpacity: 0.05,
    shadowRadius: 2,
    elevation: 2,
  },
  helpText: {
    flex: 1,
    fontSize: 16,
    color: "#111827",
    marginLeft: 12,
  },
});
