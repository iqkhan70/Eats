import React from "react";
import { View, Text, StyleSheet, ScrollView } from "react-native";
import { useRouter } from "expo-router";
import AppHeader from "../../components/AppHeader";

export default function TermsScreen() {
  const router = useRouter();

  return (
    <View style={styles.container}>
      <AppHeader title="Terms of Service" onBack={() => router.back()} />
      <ScrollView style={styles.scroll} contentContainerStyle={styles.content}>
        <Text style={styles.intro}>
          Welcome to Kram. We're here to connect you with local businesses and the services they
          offer.
        </Text>

        <Text style={styles.section}>Who we are</Text>
        <Text style={styles.body}>
          Kram (kram.tech) is the service provider for this platform. We operate the app and
          website that lets you discover businesses, place orders or book services, and—if you're
          a vendor—manage your offerings and orders.
        </Text>

        <Text style={styles.section}>Using the service</Text>
        <Text style={styles.body}>
          When you use Kram, you agree to use it in a respectful way. Don't misuse the platform,
          harass others, or violate any laws. We reserve the right to suspend accounts that break
          these guidelines.
        </Text>

        <Text style={styles.section}>Orders and payments</Text>
        <Text style={styles.body}>
          Orders and transactions are placed through our platform. Payment is processed securely.
          Vendors receive orders through the app and fulfill them. We're not the business—we're
          the connection between you and them.
        </Text>

        <Text style={styles.section}>Changes</Text>
        <Text style={styles.body}>
          We may update these terms from time to time. We'll notify you of significant changes
          through the app or by email. Continued use means you accept the updated terms.
        </Text>

        <Text style={styles.section}>Contact</Text>
        <Text style={styles.body}>
          Questions? Reach out at support@kram.tech. We're happy to help.
        </Text>

        <Text style={styles.footer}>Last updated: March 2025</Text>
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
    padding: 20,
    paddingBottom: 40,
  },
  intro: {
    fontSize: 16,
    color: "#374151",
    lineHeight: 26,
    marginBottom: 24,
  },
  section: {
    fontSize: 17,
    fontWeight: "600",
    color: "#111827",
    marginTop: 20,
    marginBottom: 8,
  },
  body: {
    fontSize: 15,
    color: "#4b5563",
    lineHeight: 24,
  },
  footer: {
    fontSize: 13,
    color: "#9ca3af",
    marginTop: 32,
  },
});
