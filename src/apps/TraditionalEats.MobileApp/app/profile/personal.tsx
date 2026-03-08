import React, { useState, useEffect } from "react";
import {
  View,
  Text,
  StyleSheet,
  ScrollView,
  TextInput,
  TouchableOpacity,
  Alert,
  ActivityIndicator,
  KeyboardAvoidingView,
  Platform,
} from "react-native";
import { Ionicons } from "@expo/vector-icons";
import { useRouter } from "expo-router";
import { api } from "../../services/api";
import AppHeader from "../../components/AppHeader";

interface CustomerProfile {
  customerId: string;
  userId: string;
  firstName: string;
  lastName: string;
  email?: string | null;
  phoneNumber?: string | null;
}

export default function PersonalInfoScreen() {
  const router = useRouter();
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [firstName, setFirstName] = useState("");
  const [lastName, setLastName] = useState("");
  const [email, setEmail] = useState(""); // Read-only, displayed but not editable
  const [phoneNumber, setPhoneNumber] = useState("");

  useEffect(() => {
    loadProfile();
  }, []);

  const loadProfile = async () => {
    try {
      setLoading(true);
      const res = await api.get<CustomerProfile>("/MobileBff/customer/me");
      const d = res.data;
      setFirstName(d?.firstName ?? "");
      setLastName(d?.lastName ?? "");
      setEmail(d?.email ?? "");
      setPhoneNumber(d?.phoneNumber ?? "");
    } catch (e: any) {
      const status = e?.response?.status;
      if (status === 404) {
        Alert.alert(
          "Profile Not Found",
          "Your profile has not been set up yet. Please complete your profile.",
          [{ text: "OK", onPress: () => router.back() }]
        );
      } else {
        Alert.alert("Error", e?.response?.data?.message ?? "Failed to load profile");
      }
    } finally {
      setLoading(false);
    }
  };

  const saveProfile = async () => {
    if (!firstName.trim() || !lastName.trim()) {
      Alert.alert("Validation", "First name and last name are required.");
      return;
    }

    try {
      setSaving(true);
      await api.patch("/MobileBff/customer/me", {
        firstName: firstName.trim(),
        lastName: lastName.trim(),
        phoneNumber: phoneNumber.trim() || null,
      });
      Alert.alert("Success", "Profile updated successfully.");
      router.back();
    } catch (e: any) {
      Alert.alert("Error", e?.response?.data?.message ?? "Failed to update profile");
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return (
      <View style={styles.centerContainer}>
        <ActivityIndicator size="large" color="#f97316" />
        <Text style={styles.loadingText}>Loading...</Text>
      </View>
    );
  }

  return (
    <KeyboardAvoidingView
      style={styles.container}
      behavior={Platform.OS === "ios" ? "padding" : "height"}
      keyboardVerticalOffset={Platform.OS === "ios" ? 0 : 20}
    >
      <AppHeader title="Personal Information" />

      <ScrollView
        style={styles.scrollView}
        contentContainerStyle={styles.content}
        keyboardShouldPersistTaps="handled"
      >
        <View style={styles.card}>
          <Text style={styles.label}>First Name</Text>
          <TextInput
            style={styles.input}
            value={firstName}
            onChangeText={setFirstName}
            placeholder="First name"
            placeholderTextColor="#999"
            autoCapitalize="words"
          />

          <Text style={styles.label}>Last Name</Text>
          <TextInput
            style={styles.input}
            value={lastName}
            onChangeText={setLastName}
            placeholder="Last name"
            placeholderTextColor="#999"
            autoCapitalize="words"
          />

          <Text style={styles.label}>Email</Text>
          <Text style={styles.readOnlyValue}>{email || "—"}</Text>
          <Text style={styles.hintText}>Email cannot be changed (tied to your account)</Text>

          <Text style={styles.label}>Phone Number</Text>
          <TextInput
            style={styles.input}
            value={phoneNumber}
            onChangeText={setPhoneNumber}
            placeholder="Phone (optional)"
            placeholderTextColor="#999"
            keyboardType="phone-pad"
          />

          <TouchableOpacity
            style={[styles.saveButton, saving && styles.buttonDisabled]}
            onPress={saveProfile}
            disabled={saving}
          >
            {saving ? (
              <ActivityIndicator size="small" color="#fff" />
            ) : (
              <>
                <Ionicons name="save" size={18} color="#fff" />
                <Text style={styles.saveButtonText}>Save Changes</Text>
              </>
            )}
          </TouchableOpacity>
        </View>
      </ScrollView>
    </KeyboardAvoidingView>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: "#f5f5f5" },
  centerContainer: {
    flex: 1,
    justifyContent: "center",
    alignItems: "center",
    backgroundColor: "#f5f5f5",
  },
  loadingText: { marginTop: 16, fontSize: 16, color: "#666" },
  scrollView: { flex: 1 },
  content: { padding: 16, paddingBottom: 100 },
  card: {
    backgroundColor: "#fff",
    borderRadius: 12,
    padding: 16,
    shadowColor: "#000",
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.1,
    shadowRadius: 4,
    elevation: 3,
  },
  label: {
    fontSize: 14,
    fontWeight: "600",
    color: "#333",
    marginBottom: 6,
    marginTop: 12,
  },
  input: {
    borderWidth: 1,
    borderColor: "#ddd",
    borderRadius: 8,
    paddingHorizontal: 12,
    paddingVertical: 10,
    fontSize: 16,
    backgroundColor: "#fafafa",
  },
  readOnlyValue: {
    fontSize: 16,
    color: "#333",
    paddingVertical: 12,
    paddingHorizontal: 12,
    backgroundColor: "#f0f0f0",
    borderRadius: 8,
    marginBottom: 4,
  },
  hintText: {
    fontSize: 12,
    color: "#666",
    marginBottom: 12,
    fontStyle: "italic",
  },
  saveButton: {
    flexDirection: "row",
    alignItems: "center",
    justifyContent: "center",
    gap: 8,
    backgroundColor: "#f97316",
    paddingVertical: 14,
    borderRadius: 8,
    marginTop: 24,
  },
  saveButtonText: { color: "#fff", fontSize: 16, fontWeight: "600" },
  buttonDisabled: { opacity: 0.6 },
});
