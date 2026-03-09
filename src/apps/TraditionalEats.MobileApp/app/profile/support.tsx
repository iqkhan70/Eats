import React, { useState } from "react";
import {
  View,
  Text,
  StyleSheet,
  ScrollView,
  TouchableOpacity,
  Linking,
  Alert,
} from "react-native";
import { Ionicons } from "@expo/vector-icons";
import { useRouter } from "expo-router";
import AppHeader from "../../components/AppHeader";

const SUPPORT_EMAIL =
  process.env.EXPO_PUBLIC_SUPPORT_EMAIL ?? "support@kram.tech";

const FAQ_ITEMS = [
  {
    q: "How do I place an order?",
    a: "Browse vendors, add items to your cart, and checkout. You can pay with a saved payment method or add a new one at checkout.",
  },
  {
    q: "How do I track my order?",
    a: "Go to Orders in the app to see your order status and when it's ready for pickup.",
  },
  {
    q: "How do I update my address or payment?",
    a: "Tap Profile → Addresses or Profile → Payment Methods to add or edit your information.",
  },
  {
    q: "I want to become a vendor. How?",
    a: "Go to Profile → Become a Vendor and submit your request. We'll review and get back to you.",
  },
];

function FaqItem({
  question,
  answer,
  expanded,
  onPress,
}: {
  question: string;
  answer: string;
  expanded: boolean;
  onPress: () => void;
}) {
  return (
    <TouchableOpacity
      style={styles.faqItem}
      onPress={onPress}
      activeOpacity={0.7}
    >
      <View style={styles.faqRow}>
        <Text style={styles.faqQuestion}>{question}</Text>
        <Ionicons
          name={expanded ? "chevron-up" : "chevron-down"}
          size={20}
          color="#6b7280"
        />
      </View>
      {expanded && <Text style={styles.faqAnswer}>{answer}</Text>}
    </TouchableOpacity>
  );
}

async function openMailto(email: string, subject: string) {
  const url = `mailto:${email}?subject=${encodeURIComponent(subject)}`;
  try {
    await Linking.openURL(url);
  } catch {
    Alert.alert(
      "Email",
      `No email app found. You can email us at ${email} with subject: ${subject}`,
      [{ text: "OK" }],
    );
  }
}

function ContactRow({
  icon,
  label,
  subject,
  isLast,
}: {
  icon: keyof typeof Ionicons.glyphMap;
  label: string;
  subject: string;
  isLast?: boolean;
}) {
  return (
    <TouchableOpacity
      style={[styles.contactRow, isLast && styles.contactRowLast]}
      onPress={() => openMailto(SUPPORT_EMAIL, subject)}
      activeOpacity={0.7}
    >
      <Ionicons name={icon} size={24} color="#f97316" />
      <Text style={styles.contactLabel}>{label}</Text>
      <Ionicons name="chevron-forward" size={20} color="#9ca3af" />
    </TouchableOpacity>
  );
}

export default function SupportScreen() {
  const router = useRouter();
  const [expandedIndex, setExpandedIndex] = useState<number | null>(null);

  return (
    <View style={styles.container}>
      <AppHeader title="Help & Support" onBack={() => router.back()} />
      <ScrollView
        style={styles.scroll}
        contentContainerStyle={styles.scrollContent}
      >
        <Text style={styles.sectionTitle}>FAQ</Text>
        {FAQ_ITEMS.map((item, i) => (
          <FaqItem
            key={i}
            question={item.q}
            answer={item.a}
            expanded={expandedIndex === i}
            onPress={() => setExpandedIndex(expandedIndex === i ? null : i)}
          />
        ))}

        <Text style={[styles.sectionTitle, { marginTop: 24 }]}>Contact</Text>
        <View style={styles.contactCard}>
          <ContactRow
            icon="mail-outline"
            label="Contact Support"
            subject="Support Request"
          />
          <ContactRow
            icon="bug-outline"
            label="Report a Problem"
            subject="Report a Problem"
          />
          <ContactRow
            icon="receipt-outline"
            label="Order Help"
            subject="Order Help"
          />
          <ContactRow
            icon="chatbubble-outline"
            label="Send Feedback"
            subject="Feedback"
          />
        </View>
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
  scrollContent: {
    padding: 16,
    paddingBottom: 32,
  },
  sectionTitle: {
    fontSize: 18,
    fontWeight: "600",
    color: "#111827",
    marginBottom: 12,
  },
  faqItem: {
    backgroundColor: "#fff",
    borderRadius: 12,
    padding: 16,
    marginBottom: 8,
    shadowColor: "#000",
    shadowOffset: { width: 0, height: 1 },
    shadowOpacity: 0.05,
    shadowRadius: 2,
    elevation: 2,
  },
  faqRow: {
    flexDirection: "row",
    justifyContent: "space-between",
    alignItems: "center",
  },
  faqQuestion: {
    fontSize: 16,
    fontWeight: "500",
    color: "#111827",
    flex: 1,
    marginRight: 8,
  },
  faqAnswer: {
    fontSize: 14,
    color: "#6b7280",
    marginTop: 12,
    lineHeight: 22,
  },
  contactCard: {
    backgroundColor: "#fff",
    borderRadius: 12,
    overflow: "hidden",
    shadowColor: "#000",
    shadowOffset: { width: 0, height: 1 },
    shadowOpacity: 0.05,
    shadowRadius: 2,
    elevation: 2,
  },
  contactRow: {
    flexDirection: "row",
    alignItems: "center",
    padding: 16,
    borderBottomWidth: StyleSheet.hairlineWidth,
    borderBottomColor: "#e5e7eb",
  },
  contactRowLast: {
    borderBottomWidth: 0,
  },
  contactLabel: {
    flex: 1,
    fontSize: 16,
    color: "#111827",
    marginLeft: 12,
  },
});
