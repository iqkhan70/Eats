import React from "react";
import { View, Text, StyleSheet, ScrollView } from "react-native";
import { useRouter } from "expo-router";
import AppHeader from "../../components/AppHeader";

export default function PrivacyScreen() {
  const router = useRouter();

  return (
    <View style={styles.container}>
      <AppHeader title="Privacy Policy" onBack={() => router.back()} />
      <ScrollView style={styles.scroll} contentContainerStyle={styles.content}>
        <Text style={styles.intro}>
          Your privacy matters to us. This policy explains how Kram (kram.tech) handles your
          information in plain terms.
        </Text>

        <Text style={styles.section}>Who we are</Text>
        <Text style={styles.body}>
          Kram is the service provider for this platform. We run the app and website you use to
          place orders or book services and—if you're a vendor—to manage your business.
        </Text>

        <Text style={styles.section}>What we collect</Text>
        <Text style={styles.body}>
          We collect what we need to run the service: your name, email, phone, address, and
          payment information when you place orders or book services. Vendors provide business
          details to list their offerings. We use this to process transactions, communicate with
          you, and improve the app.
        </Text>

        <Text style={styles.section}>How we use it</Text>
        <Text style={styles.body}>
          Your data is used to fulfill orders and services, send updates, and provide support. We don't
          sell your personal information to third parties. We may use anonymized data to improve
          our service.
        </Text>

        <Text style={styles.section}>Security</Text>
        <Text style={styles.body}>
          We take reasonable steps to protect your information. Payment data is handled by
          secure payment processors. We store your data securely and limit who can access it.
        </Text>

        <Text style={styles.section}>Your choices</Text>
        <Text style={styles.body}>
          You can update your profile, addresses, and payment methods in the app. You can
          permanently delete your account at any time from Settings → Delete Account. When
          you delete your account, all your personal data, order history, and saved
          information are removed from our systems.
        </Text>

        <Text style={styles.section}>Contact</Text>
        <Text style={styles.body}>
          Questions about your privacy? Email us at support@kram.tech.
        </Text>

        <Text style={styles.footer}>Last updated: March 2026</Text>
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
