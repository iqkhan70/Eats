import React, { useState, useEffect, useCallback } from "react";
import {
  View,
  Text,
  StyleSheet,
  ScrollView,
  TouchableOpacity,
  Alert,
  ActivityIndicator,
  RefreshControl,
} from "react-native";
import { Ionicons } from "@expo/vector-icons";
import { useRouter, useFocusEffect } from "expo-router";
import { authService } from "../../services/auth";
import { api } from "../../services/api";
import AppHeader from "../../components/AppHeader";

interface VendorApprovalDto {
  id: string;
  userId: string;
  userEmail: string;
  requestedAt: string;
}

export default function AdminVendorApprovalsScreen() {
  const router = useRouter();
  const [isAdmin, setIsAdmin] = useState(false);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [approvals, setApprovals] = useState<VendorApprovalDto[]>([]);
  const [approvingId, setApprovingId] = useState<string | null>(null);

  const loadApprovals = useCallback(async () => {
    try {
      const res = await api.get<VendorApprovalDto[]>(
        "/MobileBff/admin/vendor-approvals",
      );
      const data = res.data;
      setApprovals(Array.isArray(data) ? data : []);
    } catch (e) {
      console.error("Failed to load vendor approvals:", e);
      setApprovals([]);
    }
  }, []);

  useFocusEffect(
    useCallback(() => {
      let mounted = true;
      const run = async () => {
        const admin = await authService.isAdmin();
        if (!mounted) return;
        setIsAdmin(admin);
        if (!admin) {
          Alert.alert(
            "Access Denied",
            "You must be an admin to access this page.",
          );
          router.back();
          return;
        }
        setLoading(true);
        await loadApprovals();
        if (mounted) setLoading(false);
      };
      run();
      return () => {
        mounted = false;
      };
    }, [loadApprovals, router]),
  );

  const onRefresh = async () => {
    setRefreshing(true);
    await loadApprovals();
    setRefreshing(false);
  };

  const approveRequest = async (item: VendorApprovalDto) => {
    try {
      setApprovingId(item.id);
      await api.post(
        `/MobileBff/admin/vendor-approvals/${item.id}/approve`,
      );
      await loadApprovals();
      Alert.alert("Approved", `Vendor role assigned to ${item.userEmail}`);
    } catch (e: any) {
      const msg =
        e?.response?.data?.message ?? "Failed to approve. Please try again.";
      Alert.alert("Error", msg);
    } finally {
      setApprovingId(null);
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
    <View style={styles.container}>
      <AppHeader title="Vendor Approvals" />

      <ScrollView
        style={styles.scrollView}
        contentContainerStyle={styles.content}
        refreshControl={
          <RefreshControl refreshing={refreshing} onRefresh={onRefresh} />
        }
      >
        {approvals.length === 0 ? (
          <View style={styles.emptyCard}>
            <Ionicons name="checkmark-circle" size={64} color="#4caf50" />
            <Text style={styles.emptyTitle}>No Pending Approvals</Text>
            <Text style={styles.emptyText}>
              All vendor requests have been processed.
            </Text>
          </View>
        ) : (
          <View style={styles.list}>
            {approvals.map((item) => (
              <View key={item.id} style={styles.card}>
                <View style={styles.cardContent}>
                  <Text style={styles.email}>{item.userEmail}</Text>
                  <Text style={styles.date}>
                    Requested:{" "}
                    {new Date(item.requestedAt).toLocaleDateString("en-US", {
                      month: "short",
                      day: "numeric",
                      year: "numeric",
                      hour: "2-digit",
                      minute: "2-digit",
                    })}
                  </Text>
                </View>
                <TouchableOpacity
                  style={[
                    styles.approveButton,
                    approvingId === item.id && styles.approveButtonDisabled,
                  ]}
                  onPress={() => approveRequest(item)}
                  disabled={approvingId === item.id}
                >
                  {approvingId === item.id ? (
                    <ActivityIndicator size="small" color="#fff" />
                  ) : (
                    <>
                      <Ionicons name="checkmark" size={18} color="#fff" />
                      <Text style={styles.approveButtonText}>Approve</Text>
                    </>
                  )}
                </TouchableOpacity>
              </View>
            ))}
          </View>
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
  emptyCard: {
    backgroundColor: "#fff",
    borderRadius: 16,
    padding: 32,
    alignItems: "center",
    borderWidth: 1,
    borderColor: "#e0e0e0",
  },
  emptyTitle: {
    fontSize: 20,
    fontWeight: "700",
    color: "#333",
    marginTop: 16,
  },
  emptyText: {
    fontSize: 14,
    color: "#666",
    marginTop: 8,
    textAlign: "center",
  },
  list: { gap: 12 },
  card: {
    backgroundColor: "#fff",
    borderRadius: 12,
    padding: 16,
    flexDirection: "row",
    alignItems: "center",
    justifyContent: "space-between",
    borderWidth: 1,
    borderColor: "#e0e0e0",
  },
  cardContent: { flex: 1 },
  email: { fontSize: 16, fontWeight: "600", color: "#333" },
  date: { fontSize: 12, color: "#666", marginTop: 4 },
  approveButton: {
    flexDirection: "row",
    alignItems: "center",
    gap: 6,
    backgroundColor: "#22c55e",
    paddingHorizontal: 16,
    paddingVertical: 10,
    borderRadius: 8,
  },
  approveButtonDisabled: { opacity: 0.7 },
  approveButtonText: { fontSize: 14, fontWeight: "600", color: "#fff" },
});
