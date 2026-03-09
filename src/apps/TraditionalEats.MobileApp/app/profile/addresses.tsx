import React, { useState, useEffect } from "react";
import {
  View,
  Text,
  StyleSheet,
  ScrollView,
  TouchableOpacity,
  Alert,
  ActivityIndicator,
} from "react-native";
import { Ionicons } from "@expo/vector-icons";
import { useRouter, useFocusEffect } from "expo-router";
import { api } from "../../services/api";
import AppHeader from "../../components/AppHeader";

interface AddressDto {
  addressId?: string;
  line1: string;
  line2?: string | null;
  city: string;
  state: string;
  zipCode: string;
  isDefault?: boolean;
  label?: string | null;
}

export default function AddressesScreen() {
  const router = useRouter();
  const [loading, setLoading] = useState(true);
  const [addresses, setAddresses] = useState<AddressDto[]>([]);
  const [deletingId, setDeletingId] = useState<string | null>(null);

  const loadAddresses = async () => {
    try {
      const res = await api.get<AddressDto[]>("/MobileBff/customer/addresses");
      const data = res.data;
      setAddresses(Array.isArray(data) ? data : []);
    } catch (e: any) {
      const status = e?.response?.status;
      if (status === 404) {
        setAddresses([]);
        Alert.alert(
          "Profile Not Found",
          "Your profile has not been set up yet. Add your personal information first."
        );
      } else {
        setAddresses([]);
      }
    } finally {
      setLoading(false);
    }
  };

  useFocusEffect(
    React.useCallback(() => {
      loadAddresses();
    }, [])
  );

  const deleteAddress = (addr: AddressDto) => {
    const id = addr.addressId;
    if (!id) return;

    Alert.alert(
      "Delete Address",
      "Are you sure you want to delete this address?",
      [
        { text: "Cancel", style: "cancel" },
        {
          text: "Delete",
          style: "destructive",
          onPress: async () => {
            try {
              setDeletingId(id);
              await api.delete(`/MobileBff/customer/addresses/${id}`);
              await loadAddresses();
            } catch (e: any) {
              Alert.alert("Error", e?.response?.data?.message ?? "Failed to delete address");
            } finally {
              setDeletingId(null);
            }
          },
        },
      ]
    );
  };

  const formatAddress = (a: AddressDto) => {
    const parts = [a.line1, a.line2, [a.city, a.state, a.zipCode].filter(Boolean).join(", ")];
    return parts.filter(Boolean).join("\n");
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
    <View style={styles.container}>
      <AppHeader title="Addresses" />

      <ScrollView style={styles.scrollView} contentContainerStyle={styles.content}>
        <View style={styles.infoCard}>
          <Text style={styles.infoText}>
            Saved addresses are for delivery when it becomes available. Right now we're pickup
            only—your name from your profile is shown to the vendor at checkout.
          </Text>
        </View>
        {addresses.length === 0 ? (
          <View style={styles.emptyCard}>
            <Ionicons name="location-outline" size={48} color="#999" />
            <Text style={styles.emptyText}>No addresses yet</Text>
            <Text style={styles.emptySubtext}>
              Add your address now—it'll be ready for faster checkout when we launch delivery
            </Text>
            <TouchableOpacity
              style={styles.addButton}
              onPress={() => router.push("/profile/addresses/new")}
            >
              <Ionicons name="add" size={20} color="#fff" />
              <Text style={styles.addButtonText}>Add Address</Text>
            </TouchableOpacity>
          </View>
        ) : (
          <>
            {addresses.map((addr) => (
              <View key={addr.addressId ?? addr.line1} style={styles.addressCard}>
                <View style={styles.addressHeader}>
                  {addr.isDefault && (
                    <View style={styles.defaultBadge}>
                      <Text style={styles.defaultBadgeText}>Default</Text>
                    </View>
                  )}
                  {addr.label && (
                    <Text style={styles.addressLabel}>{addr.label}</Text>
                  )}
                </View>
                <Text style={styles.addressText}>{formatAddress(addr)}</Text>
                <View style={styles.addressActions}>
                  <TouchableOpacity
                    style={styles.editButton}
                    onPress={() =>
                      router.push(`/profile/addresses/${addr.addressId}/edit` as any)
                    }
                  >
                    <Ionicons name="pencil" size={18} color="#f97316" />
                    <Text style={styles.editButtonText}>Edit</Text>
                  </TouchableOpacity>
                  <TouchableOpacity
                    style={[styles.deleteButton, deletingId === addr.addressId && styles.buttonDisabled]}
                    onPress={() => deleteAddress(addr)}
                    disabled={deletingId === addr.addressId}
                  >
                    {deletingId === addr.addressId ? (
                      <ActivityIndicator size="small" color="#c62828" />
                    ) : (
                      <>
                        <Ionicons name="trash" size={18} color="#c62828" />
                        <Text style={styles.deleteButtonText}>Delete</Text>
                      </>
                    )}
                  </TouchableOpacity>
                </View>
              </View>
            ))}
            <TouchableOpacity
              style={styles.addButtonSecondary}
              onPress={() => router.push("/profile/addresses/new")}
            >
              <Ionicons name="add" size={20} color="#f97316" />
              <Text style={styles.addButtonSecondaryText}>Add Another Address</Text>
            </TouchableOpacity>
          </>
        )}
      </ScrollView>
    </View>
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
  content: { padding: 16, paddingBottom: 32 },
  infoCard: {
    backgroundColor: "#fff",
    borderRadius: 12,
    padding: 16,
    marginBottom: 16,
    shadowColor: "#000",
    shadowOffset: { width: 0, height: 1 },
    shadowOpacity: 0.05,
    shadowRadius: 2,
    elevation: 2,
  },
  infoText: {
    fontSize: 14,
    color: "#4b5563",
    lineHeight: 22,
  },
  emptyCard: {
    backgroundColor: "#fff",
    borderRadius: 12,
    padding: 32,
    alignItems: "center",
    shadowColor: "#000",
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.1,
    shadowRadius: 4,
    elevation: 3,
  },
  emptyText: { fontSize: 18, fontWeight: "600", color: "#333", marginTop: 16 },
  emptySubtext: { fontSize: 14, color: "#666", marginTop: 4 },
  addButton: {
    flexDirection: "row",
    alignItems: "center",
    gap: 8,
    backgroundColor: "#f97316",
    paddingHorizontal: 20,
    paddingVertical: 12,
    borderRadius: 8,
    marginTop: 24,
  },
  addButtonText: { color: "#fff", fontSize: 16, fontWeight: "600" },
  addressCard: {
    backgroundColor: "#fff",
    borderRadius: 12,
    padding: 16,
    marginBottom: 12,
    shadowColor: "#000",
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.1,
    shadowRadius: 4,
    elevation: 3,
  },
  addressHeader: { flexDirection: "row", alignItems: "center", gap: 8, marginBottom: 8 },
  defaultBadge: {
    backgroundColor: "#f97316",
    paddingHorizontal: 8,
    paddingVertical: 2,
    borderRadius: 4,
  },
  defaultBadgeText: { color: "#fff", fontSize: 12, fontWeight: "600" },
  addressLabel: { fontSize: 14, fontWeight: "600", color: "#666" },
  addressText: { fontSize: 15, color: "#333", lineHeight: 22 },
  addressActions: { flexDirection: "row", gap: 16, marginTop: 12, paddingTop: 12, borderTopWidth: 1, borderTopColor: "#eee" },
  editButton: { flexDirection: "row", alignItems: "center", gap: 4 },
  editButtonText: { fontSize: 14, color: "#f97316", fontWeight: "600" },
  deleteButton: { flexDirection: "row", alignItems: "center", gap: 4 },
  deleteButtonText: { fontSize: 14, color: "#c62828", fontWeight: "600" },
  buttonDisabled: { opacity: 0.6 },
  addButtonSecondary: {
    flexDirection: "row",
    alignItems: "center",
    justifyContent: "center",
    gap: 8,
    paddingVertical: 14,
    borderRadius: 8,
    borderWidth: 1,
    borderColor: "#f97316",
    marginTop: 8,
  },
  addButtonSecondaryText: { fontSize: 16, color: "#f97316", fontWeight: "600" },
});
