import React, { useState } from "react";
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
import { api } from "../../../services/api";
import AppHeader from "../../../components/AppHeader";

export default function NewAddressScreen() {
  const router = useRouter();
  const [saving, setSaving] = useState(false);
  const [line1, setLine1] = useState("");
  const [line2, setLine2] = useState("");
  const [city, setCity] = useState("");
  const [state, setState] = useState("");
  const [zipCode, setZipCode] = useState("");
  const [label, setLabel] = useState("");
  const [isDefault, setIsDefault] = useState(false);

  const saveAddress = async () => {
    if (!line1.trim() || !city.trim() || !state.trim() || !zipCode.trim()) {
      Alert.alert("Validation", "Street, city, state, and ZIP are required.");
      return;
    }

    try {
      setSaving(true);
      await api.post("/MobileBff/customer/addresses", {
        line1: line1.trim(),
        line2: line2.trim() || null,
        city: city.trim(),
        state: state.trim(),
        zipCode: zipCode.trim(),
        label: label.trim() || null,
        isDefault,
        latitude: null,
        longitude: null,
        geoHash: null,
      });
      Alert.alert("Success", "Address added successfully.");
      router.back();
    } catch (e: any) {
      Alert.alert("Error", e?.response?.data?.message ?? "Failed to add address");
    } finally {
      setSaving(false);
    }
  };

  return (
    <KeyboardAvoidingView
      style={styles.container}
      behavior={Platform.OS === "ios" ? "padding" : "height"}
      keyboardVerticalOffset={Platform.OS === "ios" ? 0 : 20}
    >
      <AppHeader title="Add Address" />

      <ScrollView
        style={styles.scrollView}
        contentContainerStyle={styles.content}
        keyboardShouldPersistTaps="handled"
      >
        <View style={styles.card}>
          <Text style={styles.label}>Street Address</Text>
          <TextInput
            style={styles.input}
            value={line1}
            onChangeText={setLine1}
            placeholder="Street address"
            placeholderTextColor="#999"
          />

          <Text style={styles.label}>Apt, Suite, etc. (optional)</Text>
          <TextInput
            style={styles.input}
            value={line2}
            onChangeText={setLine2}
            placeholder="Apt, suite, unit"
            placeholderTextColor="#999"
          />

          <Text style={styles.label}>City</Text>
          <TextInput
            style={styles.input}
            value={city}
            onChangeText={setCity}
            placeholder="City"
            placeholderTextColor="#999"
          />

          <Text style={styles.label}>State</Text>
          <TextInput
            style={styles.input}
            value={state}
            onChangeText={setState}
            placeholder="State"
            placeholderTextColor="#999"
          />

          <Text style={styles.label}>ZIP Code</Text>
          <TextInput
            style={styles.input}
            value={zipCode}
            onChangeText={setZipCode}
            placeholder="ZIP code"
            placeholderTextColor="#999"
            keyboardType="numeric"
          />

          <Text style={styles.label}>Label (optional)</Text>
          <TextInput
            style={styles.input}
            value={label}
            onChangeText={setLabel}
            placeholder="e.g. Home, Work"
            placeholderTextColor="#999"
          />

          <TouchableOpacity
            style={styles.checkboxRow}
            onPress={() => setIsDefault(!isDefault)}
          >
            <Ionicons
              name={isDefault ? "checkbox" : "square-outline"}
              size={24}
              color={isDefault ? "#f97316" : "#666"}
            />
            <Text style={styles.checkboxLabel}>Set as default address</Text>
          </TouchableOpacity>

          <TouchableOpacity
            style={[styles.saveButton, saving && styles.buttonDisabled]}
            onPress={saveAddress}
            disabled={saving}
          >
            {saving ? (
              <ActivityIndicator size="small" color="#fff" />
            ) : (
              <>
                <Ionicons name="add" size={18} color="#fff" />
                <Text style={styles.saveButtonText}>Add Address</Text>
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
  label: { fontSize: 14, fontWeight: "600", color: "#333", marginBottom: 6, marginTop: 12 },
  input: {
    borderWidth: 1,
    borderColor: "#ddd",
    borderRadius: 8,
    paddingHorizontal: 12,
    paddingVertical: 10,
    fontSize: 16,
    backgroundColor: "#fafafa",
  },
  checkboxRow: { flexDirection: "row", alignItems: "center", gap: 12, marginTop: 20 },
  checkboxLabel: { fontSize: 16, color: "#333" },
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
