import React, { useState } from "react";
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
import { useRouter } from "expo-router";
import { api } from "../../services/api";
import AppHeader from "../../components/AppHeader";

interface SyncResult {
  totalUsers: number;
  created: number;
  alreadyExisted: number;
  failed: number;
  errors?: string[];
}

export default function SyncUsersScreen() {
  const router = useRouter();
  const [syncing, setSyncing] = useState(false);
  const [result, setResult] = useState<SyncResult | null>(null);

  const runSync = async () => {
    try {
      setSyncing(true);
      setResult(null);
      const res = await api.post<SyncResult>("/MobileBff/admin/sync-users-to-customers");
      const data = res.data;
      setResult(data ?? null);
      const r = data;
      const total = r?.totalUsers ?? 0;
      const created = r?.created ?? 0;
      const existed = r?.alreadyExisted ?? 0;
      const failed = r?.failed ?? 0;
      Alert.alert(
        "Sync Complete",
        `Checked ${total} users: ${created} created, ${existed} already had records, ${failed} failed.`
      );
    } catch (e: any) {
      Alert.alert("Error", e?.response?.data?.message ?? "Sync failed");
    } finally {
      setSyncing(false);
    }
  };

  return (
    <View style={styles.container}>
      <AppHeader title="Sync Users to Customers" />

      <ScrollView style={styles.scrollView} contentContainerStyle={styles.content}>
        {syncing && (
          <View style={styles.statusBanner}>
            <ActivityIndicator size="small" color="#fff" />
            <Text style={styles.statusText}>Syncing users to customers...</Text>
          </View>
        )}
        {result && !syncing && (
          <View style={[styles.statusBanner, styles.statusSuccess]}>
            <Ionicons name="checkmark-circle" size={20} color="#fff" />
            <Text style={styles.statusText}>
              Done. {result.created} created, {result.alreadyExisted} already existed, {result.failed} failed.
            </Text>
          </View>
        )}
        <View style={styles.card}>
          <Ionicons name="sync-outline" size={48} color="#0d9488" style={styles.icon} />
          <Text style={styles.title}>Sync Identity Users to Customer Records</Text>
          <Text style={styles.description}>
            This will create Customer records for any Identity users who don't have one.
            Use this to fix sync issues from social login or other gaps.
          </Text>

          <TouchableOpacity
            style={[styles.syncButton, syncing && styles.buttonDisabled]}
            onPress={runSync}
            disabled={syncing}
          >
            {syncing ? (
              <ActivityIndicator size="small" color="#fff" />
            ) : (
              <>
                <Ionicons name="sync" size={20} color="#fff" />
                <Text style={styles.syncButtonText}>Run Sync</Text>
              </>
            )}
          </TouchableOpacity>
        </View>

        {result && (
          <View style={styles.resultCard}>
            <Text style={styles.resultTitle}>Last Sync Result</Text>
            <View style={styles.resultRow}>
              <Text style={styles.resultLabel}>Total users:</Text>
              <Text style={styles.resultValue}>{result.totalUsers}</Text>
            </View>
            <View style={styles.resultRow}>
              <Text style={styles.resultLabel}>Created:</Text>
              <Text style={[styles.resultValue, { color: "#0d9488" }]}>{result.created}</Text>
            </View>
            <View style={styles.resultRow}>
              <Text style={styles.resultLabel}>Already existed:</Text>
              <Text style={styles.resultValue}>{result.alreadyExisted}</Text>
            </View>
            <View style={styles.resultRow}>
              <Text style={styles.resultLabel}>Failed:</Text>
              <Text style={[styles.resultValue, result.failed > 0 && { color: "#c62828" }]}>{result.failed}</Text>
            </View>
            {result.errors && result.errors.length > 0 && (
              <View style={styles.errorsSection}>
                <Text style={styles.errorsTitle}>Errors:</Text>
                {result.errors.map((err, i) => (
                  <Text key={i} style={styles.errorText}>{err}</Text>
                ))}
              </View>
            )}
          </View>
        )}
      </ScrollView>
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: "#f5f5f5" },
  scrollView: { flex: 1 },
  content: { padding: 16, paddingBottom: 32 },
  statusBanner: {
    flexDirection: "row",
    alignItems: "center",
    gap: 10,
    backgroundColor: "#0d9488",
    padding: 14,
    borderRadius: 8,
    marginBottom: 16,
  },
  statusSuccess: { backgroundColor: "#0d9488" },
  statusText: { color: "#fff", fontSize: 15, fontWeight: "600" },
  card: {
    backgroundColor: "#fff",
    borderRadius: 12,
    padding: 24,
    alignItems: "center",
    shadowColor: "#000",
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.1,
    shadowRadius: 4,
    elevation: 3,
  },
  icon: { marginBottom: 16 },
  title: { fontSize: 18, fontWeight: "700", color: "#333", textAlign: "center", marginBottom: 12 },
  description: { fontSize: 14, color: "#666", textAlign: "center", lineHeight: 22, marginBottom: 24 },
  syncButton: {
    flexDirection: "row",
    alignItems: "center",
    gap: 8,
    backgroundColor: "#0d9488",
    paddingHorizontal: 24,
    paddingVertical: 14,
    borderRadius: 8,
  },
  syncButtonText: { color: "#fff", fontSize: 16, fontWeight: "600" },
  buttonDisabled: { opacity: 0.6 },
  resultCard: {
    backgroundColor: "#fff",
    borderRadius: 12,
    padding: 16,
    marginTop: 16,
    shadowColor: "#000",
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.1,
    shadowRadius: 4,
    elevation: 3,
  },
  resultTitle: { fontSize: 16, fontWeight: "600", color: "#333", marginBottom: 12 },
  resultRow: { flexDirection: "row", justifyContent: "space-between", marginBottom: 8 },
  resultLabel: { fontSize: 14, color: "#666" },
  resultValue: { fontSize: 14, fontWeight: "600", color: "#333" },
  errorsSection: { marginTop: 12, paddingTop: 12, borderTopWidth: 1, borderTopColor: "#eee" },
  errorsTitle: { fontSize: 14, fontWeight: "600", color: "#c62828", marginBottom: 8 },
  errorText: { fontSize: 12, color: "#c62828", marginBottom: 4 },
});
