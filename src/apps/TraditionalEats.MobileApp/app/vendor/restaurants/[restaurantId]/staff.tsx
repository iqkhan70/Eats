import React, { useEffect, useState, useCallback } from "react";
import {
  View,
  Text,
  StyleSheet,
  ScrollView,
  TouchableOpacity,
  TextInput,
  Alert,
  ActivityIndicator,
  RefreshControl,
} from "react-native";
import { LinearGradient } from "expo-linear-gradient";
import { Ionicons } from "@expo/vector-icons";
import { useRouter, useLocalSearchParams } from "expo-router";
import { api } from "../../../../services/api";
import { formatLocalDate } from "../../../../config/dateTime";

interface StaffMember {
  userId: string;
  restaurantId: string;
  createdAt: string;
  email?: string;
  displayName?: string;
}

export default function ManageStaffScreen() {
  const router = useRouter();
  const { restaurantId } = useLocalSearchParams<{ restaurantId: string }>();
  const [staff, setStaff] = useState<StaffMember[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [email, setEmail] = useState("");
  const [adding, setAdding] = useState(false);

  const loadStaff = useCallback(async () => {
    try {
      const res = await api.get<StaffMember[]>(
        `/MobileBff/vendor/restaurants/${restaurantId}/staff`,
      );
      setStaff(Array.isArray(res.data) ? res.data : []);
    } catch {
      setStaff([]);
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  }, [restaurantId]);

  useEffect(() => {
    loadStaff();
  }, [loadStaff]);

  const handleAddStaff = async () => {
    const trimmed = email.trim().toLowerCase();
    if (!trimmed) {
      Alert.alert("Error", "Please enter an email address.");
      return;
    }
    setAdding(true);
    try {
      await api.post(`/MobileBff/vendor/restaurants/${restaurantId}/staff`, {
        email: trimmed,
      });
      setEmail("");
      Alert.alert("Success", "Staff member added. They can now view orders for this restaurant.");
      await loadStaff();
    } catch (error: any) {
      const status = error.response?.status;
      const serverMsg = error.response?.data?.error || error.response?.data?.message || "";
      let msg: string;
      if (status === 404) {
        msg = `No account found for "${trimmed}". They need to sign up for an account first, then you can add them as staff.`;
      } else if (status === 409 || serverMsg.toLowerCase().includes("already")) {
        msg = "This person is already a staff member of this restaurant.";
      } else if (serverMsg) {
        msg = serverMsg;
      } else {
        msg = "Something went wrong. Please try again.";
      }
      Alert.alert("Couldn't Add Staff", msg);
    } finally {
      setAdding(false);
    }
  };

  const handleRemoveStaff = (member: StaffMember) => {
    const name = member.email || member.displayName || member.userId.slice(0, 8);
    Alert.alert(
      "Remove Staff",
      `Remove ${name} from this restaurant? They will immediately lose access to all orders and notifications.`,
      [
        { text: "Cancel", style: "cancel" },
        {
          text: "Remove",
          style: "destructive",
          onPress: async () => {
            try {
              await api.delete(
                `/MobileBff/vendor/restaurants/${restaurantId}/staff/${member.userId}`,
              );
              Alert.alert("Done", `${name} has been removed and their access has been revoked.`);
              await loadStaff();
            } catch {
              Alert.alert("Error", "Failed to remove staff member.");
            }
          },
        },
      ],
    );
  };

  if (loading) {
    return (
      <View style={styles.center}>
        <ActivityIndicator size="large" color="#f97316" />
      </View>
    );
  }

  return (
    <ScrollView
      style={styles.container}
      refreshControl={
        <RefreshControl
          refreshing={refreshing}
          onRefresh={() => {
            setRefreshing(true);
            loadStaff();
          }}
        />
      }
    >
      <LinearGradient
        colors={["#f97316", "#eab308"]}
        start={{ x: 0, y: 0 }}
        end={{ x: 1, y: 0 }}
        style={styles.header}
      >
        <TouchableOpacity onPress={() => router.back()} style={styles.backButton}>
          <Ionicons name="chevron-back" size={28} color="#fff" />
          <Text style={styles.backLabel}>Back</Text>
        </TouchableOpacity>

        <Text style={styles.headerTitle} numberOfLines={1}>
          Staff
        </Text>

        <View style={{ width: 60 }} />
      </LinearGradient>

      <View style={styles.addSection}>
        <Text style={styles.addLabel}>Add staff by email</Text>
        <Text style={styles.addHint}>
          They must have a registered account. They'll get the Staff role and can
          view/manage orders for this restaurant.
        </Text>
        <View style={styles.addRow}>
          <TextInput
            style={styles.input}
            placeholder="staff@example.com"
            value={email}
            onChangeText={setEmail}
            keyboardType="email-address"
            autoCapitalize="none"
            autoCorrect={false}
          />
          <TouchableOpacity
            style={[styles.addBtn, adding && styles.addBtnDisabled]}
            onPress={handleAddStaff}
            disabled={adding}
          >
            {adding ? (
              <ActivityIndicator size="small" color="#fff" />
            ) : (
              <Ionicons name="add" size={22} color="#fff" />
            )}
          </TouchableOpacity>
        </View>
      </View>

      <View style={styles.list}>
        {staff.length === 0 ? (
          <View style={styles.empty}>
            <Ionicons name="people-outline" size={48} color="#ccc" />
            <Text style={styles.emptyText}>No staff members yet</Text>
            <Text style={styles.emptyHint}>
              Add your cashier or front desk staff above
            </Text>
          </View>
        ) : (
          staff.map((member) => (
            <View key={member.userId} style={styles.staffCard}>
              <View style={styles.staffInfo}>
                <Ionicons name="person-circle-outline" size={36} color="#666" />
                <View style={styles.staffDetails}>
                  <Text style={styles.staffName}>
                    {member.displayName || member.email || member.userId.slice(0, 8)}
                  </Text>
                  {member.email && (
                    <Text style={styles.staffEmail}>{member.email}</Text>
                  )}
                  <Text style={styles.staffDate}>
                    Added {formatLocalDate(member.createdAt)}
                  </Text>
                </View>
              </View>
              <TouchableOpacity
                onPress={() => handleRemoveStaff(member)}
                style={styles.removeBtn}
              >
                <Ionicons name="close-circle" size={24} color="#dc3545" />
              </TouchableOpacity>
            </View>
          ))
        )}
      </View>
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: "#f5f5f5" },
  center: { flex: 1, justifyContent: "center", alignItems: "center" },
  header: {
    padding: 16,
    paddingTop: 60,
    flexDirection: "row",
    alignItems: "center",
    justifyContent: "space-between",
  },
  backButton: {
    flexDirection: "row",
    alignItems: "center",
    padding: 8,
    gap: 4,
  },
  backLabel: {
    fontSize: 16,
    fontWeight: "600",
    color: "#fff",
  },
  headerTitle: {
    fontSize: 20,
    fontWeight: "bold",
    color: "#fff",
    flex: 1,
    textAlign: "center",
  },
  addSection: {
    backgroundColor: "#fff",
    padding: 16,
    margin: 16,
    borderRadius: 12,
    shadowColor: "#000",
    shadowOffset: { width: 0, height: 1 },
    shadowOpacity: 0.08,
    shadowRadius: 4,
    elevation: 2,
  },
  addLabel: { fontSize: 15, fontWeight: "600", color: "#333", marginBottom: 4 },
  addHint: { fontSize: 12, color: "#888", marginBottom: 12 },
  addRow: { flexDirection: "row", gap: 8 },
  input: {
    flex: 1,
    borderWidth: 1,
    borderColor: "#ddd",
    borderRadius: 8,
    paddingHorizontal: 12,
    paddingVertical: 10,
    fontSize: 15,
    backgroundColor: "#fafafa",
  },
  addBtn: {
    backgroundColor: "#f97316",
    borderRadius: 8,
    width: 44,
    alignItems: "center",
    justifyContent: "center",
  },
  addBtnDisabled: { opacity: 0.5 },
  list: { flex: 1, paddingHorizontal: 16 },
  empty: { alignItems: "center", paddingTop: 60 },
  emptyText: { fontSize: 16, fontWeight: "600", color: "#999", marginTop: 12 },
  emptyHint: { fontSize: 13, color: "#bbb", marginTop: 4 },
  staffCard: {
    flexDirection: "row",
    alignItems: "center",
    justifyContent: "space-between",
    backgroundColor: "#fff",
    padding: 14,
    borderRadius: 10,
    marginBottom: 10,
    shadowColor: "#000",
    shadowOffset: { width: 0, height: 1 },
    shadowOpacity: 0.05,
    shadowRadius: 3,
    elevation: 1,
  },
  staffInfo: { flexDirection: "row", alignItems: "center", flex: 1, gap: 12 },
  staffDetails: { flex: 1 },
  staffName: { fontSize: 15, fontWeight: "600", color: "#333" },
  staffEmail: { fontSize: 13, color: "#666", marginTop: 2 },
  staffDate: { fontSize: 11, color: "#aaa", marginTop: 2 },
  removeBtn: { padding: 4 },
});
