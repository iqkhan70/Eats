import React, { useState, useEffect } from "react";
import {
  View,
  Text,
  StyleSheet,
  ScrollView,
  TouchableOpacity,
  Alert,
  ActivityIndicator,
  TextInput,
  KeyboardAvoidingView,
  Platform,
} from "react-native";
import { Ionicons } from "@expo/vector-icons";
import { useRouter } from "expo-router";
import { authService } from "../../services/auth";
import { api } from "../../services/api";
import AppHeader from "../../components/AppHeader";

const AVAILABLE_ROLES = [
  "Customer",
  "Vendor",
  "Admin",
  "Coordinator",
  "Driver",
];

interface UserRolesResponse {
  roles: string[];
}

export default function AdminUsersScreen() {
  const router = useRouter();
  const [isAdmin, setIsAdmin] = useState(false);
  const [loading, setLoading] = useState(true);
  const [userEmail, setUserEmail] = useState("");
  const [currentRoles, setCurrentRoles] = useState<string[] | null>(null);
  const [loadingRoles, setLoadingRoles] = useState(false);
  const [assigningRole, setAssigningRole] = useState(false);
  const [revokingRole, setRevokingRole] = useState<string | null>(null);
  const [selectedRole, setSelectedRole] = useState<string | null>(null);
  const [errorMessage, setErrorMessage] = useState("");

  useEffect(() => {
    checkAuth();
  }, []);

  const checkAuth = async () => {
    const admin = await authService.isAdmin();
    setIsAdmin(admin);

    if (!admin) {
      Alert.alert(
        "Access Denied",
        "You must be an admin to access this page.",
      );
      router.back();
    }

    setLoading(false);
  };

  const loadUserRoles = async () => {
    const email = userEmail.trim();
    if (!email) {
      setErrorMessage("Please enter a user email");
      Alert.alert("Validation", "Please enter a user email");
      return;
    }

    try {
      setLoadingRoles(true);
      setErrorMessage("");
      setCurrentRoles(null);

      const res = await api.get<UserRolesResponse>(
        `/MobileBff/admin/users/${encodeURIComponent(email)}/roles`,
      );
      const roles = res.data?.roles ?? [];
      setCurrentRoles(Array.isArray(roles) ? roles : []);
    } catch (e: any) {
      const status = e?.response?.status;
      const msg =
        e?.response?.data?.message ??
        e?.message ??
        "Failed to load user roles";
      setErrorMessage(msg);
      setCurrentRoles(null);
      if (status === 401) {
        Alert.alert("Session Expired", "Please log in again.");
        await authService.logout();
        router.replace("/login");
      } else if (status === 404) {
        Alert.alert("User Not Found", "No user exists with this email address.");
      } else {
        Alert.alert("Error", msg);
      }
    } finally {
      setLoadingRoles(false);
    }
  };

  const assignRole = async () => {
    const email = userEmail.trim();
    if (!email || !selectedRole) {
      Alert.alert("Validation", "Please enter email and select a role.");
      return;
    }

    try {
      setAssigningRole(true);
      setErrorMessage("");

      await api.post("/MobileBff/admin/users/assign-role", {
        email,
        role: selectedRole,
      });
      Alert.alert("Success", `Role '${selectedRole}' assigned to ${email}`);
      setSelectedRole(null);
      await loadUserRoles();
    } catch (e: any) {
      const status = e?.response?.status;
      const msg =
        e?.response?.data?.message ??
        e?.message ??
        "Failed to assign role";
      setErrorMessage(msg);
      Alert.alert(status === 404 ? "User Not Found" : "Error", msg);
    } finally {
      setAssigningRole(false);
    }
  };

  const revokeRole = async (role: string) => {
    const email = userEmail.trim();
    if (!email) return;

    Alert.alert(
      "Revoke Role",
      `Revoke "${role}" from ${email}?`,
      [
        { text: "Cancel", style: "cancel" },
        {
          text: "Revoke",
          style: "destructive",
          onPress: async () => {
            try {
              setRevokingRole(role);
              setErrorMessage("");

              await api.post("/MobileBff/admin/users/revoke-role", {
                email,
                role,
              });
              Alert.alert("Success", `Role '${role}' revoked from ${email}`);
              await loadUserRoles();
            } catch (e: any) {
              const status = e?.response?.status;
              const msg =
                e?.response?.data?.message ??
                e?.message ??
                "Failed to revoke role";
              setErrorMessage(msg);
              Alert.alert(status === 404 ? "User Not Found" : "Error", msg);
            } finally {
              setRevokingRole(null);
            }
          },
        },
      ],
    );
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
      <AppHeader title="Manage Users" />

      <ScrollView
        style={styles.scrollView}
        contentContainerStyle={styles.content}
        keyboardShouldPersistTaps="handled"
        keyboardDismissMode="on-drag"
        showsVerticalScrollIndicator={true}
      >
        <View style={styles.card}>
          <Text style={styles.cardTitle}>Manage User Roles</Text>
          <TextInput
            style={styles.input}
            placeholder="Enter user email"
            placeholderTextColor="#999"
            value={userEmail}
            onChangeText={(t) => {
              setUserEmail(t);
              setCurrentRoles(null);
            }}
            autoCapitalize="none"
            autoCorrect={false}
            keyboardType="email-address"
          />
          <TouchableOpacity
            style={[styles.primaryButton, loadingRoles && styles.buttonDisabled]}
            onPress={loadUserRoles}
            disabled={loadingRoles}
          >
            {loadingRoles ? (
              <ActivityIndicator size="small" color="#fff" />
            ) : (
              <>
                <Ionicons name="search" size={18} color="#fff" />
                <Text style={styles.primaryButtonText}>Load User Roles</Text>
              </>
            )}
          </TouchableOpacity>
        </View>

        {errorMessage ? (
          <View style={styles.errorCard}>
            <Ionicons name="alert-circle" size={20} color="#c62828" />
            <Text style={styles.errorText}>{errorMessage}</Text>
          </View>
        ) : null}

        {currentRoles !== null && (
          <>
            <View style={styles.card}>
              <Text style={styles.cardTitle}>
                Roles for: {userEmail.trim()}
              </Text>
              {currentRoles.length === 0 ? (
                <Text style={styles.mutedText}>No roles assigned</Text>
              ) : (
                <>
                {currentRoles.length === 1 && (
                  <Text style={styles.hintText}>
                    User must have at least one role. Cannot revoke the last role.
                  </Text>
                )}
                <View style={styles.rolesList}>
                  {currentRoles.map((role) => (
                    <View key={role} style={styles.roleRow}>
                      <View style={styles.roleBadge}>
                        <Text style={styles.roleBadgeText}>{role}</Text>
                      </View>
                      <TouchableOpacity
                        style={[
                          styles.revokeButton,
                          (revokingRole === role || currentRoles.length === 1) &&
                            styles.buttonDisabled,
                        ]}
                        onPress={() => revokeRole(role)}
                        disabled={revokingRole === role || currentRoles.length === 1}
                      >
                        {revokingRole === role ? (
                          <ActivityIndicator size="small" color="#c62828" />
                        ) : (
                          <>
                            <Ionicons name="close" size={16} color="#c62828" />
                            <Text style={styles.revokeButtonText}>Revoke</Text>
                          </>
                        )}
                      </TouchableOpacity>
                    </View>
                  ))}
                </View>
                </>
              )}
            </View>

            <View style={styles.card}>
              <Text style={styles.cardTitle}>Assign New Role</Text>
              <View style={styles.roleChips}>
                {AVAILABLE_ROLES.map((role) => (
                  <TouchableOpacity
                    key={role}
                    style={[
                      styles.roleChip,
                      selectedRole === role && styles.roleChipActive,
                    ]}
                    onPress={() =>
                      setSelectedRole(selectedRole === role ? null : role)
                    }
                  >
                    <Text
                      style={[
                        styles.roleChipText,
                        selectedRole === role && styles.roleChipTextActive,
                      ]}
                    >
                      {role}
                    </Text>
                  </TouchableOpacity>
                ))}
              </View>
              <TouchableOpacity
                style={[
                  styles.primaryButton,
                  (assigningRole || !selectedRole) && styles.buttonDisabled,
                ]}
                onPress={assignRole}
                disabled={assigningRole || !selectedRole}
              >
                {assigningRole ? (
                  <ActivityIndicator size="small" color="#fff" />
                ) : (
                  <>
                    <Ionicons name="add" size={18} color="#fff" />
                    <Text style={styles.primaryButtonText}>Assign Role</Text>
                  </>
                )}
              </TouchableOpacity>
            </View>
          </>
        )}
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
  content: { padding: 16, paddingBottom: 320 },
  card: {
    backgroundColor: "#fff",
    borderRadius: 12,
    padding: 16,
    marginBottom: 16,
    shadowColor: "#000",
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.1,
    shadowRadius: 4,
    elevation: 3,
  },
  cardTitle: {
    fontSize: 16,
    fontWeight: "600",
    color: "#333",
    marginBottom: 12,
  },
  input: {
    borderWidth: 1,
    borderColor: "#ddd",
    borderRadius: 8,
    paddingHorizontal: 12,
    paddingVertical: 10,
    fontSize: 16,
    marginBottom: 12,
    backgroundColor: "#fafafa",
  },
  primaryButton: {
    flexDirection: "row",
    alignItems: "center",
    justifyContent: "center",
    gap: 8,
    backgroundColor: "#f97316",
    paddingVertical: 12,
    borderRadius: 8,
  },
  primaryButtonText: { color: "#fff", fontSize: 16, fontWeight: "600" },
  buttonDisabled: { opacity: 0.6 },
  errorCard: {
    flexDirection: "row",
    alignItems: "center",
    gap: 8,
    backgroundColor: "#ffebee",
    padding: 12,
    borderRadius: 8,
    marginBottom: 16,
    borderLeftWidth: 4,
    borderLeftColor: "#c62828",
  },
  errorText: { flex: 1, fontSize: 14, color: "#c62828" },
  mutedText: { fontSize: 14, color: "#666" },
  hintText: {
    fontSize: 12,
    color: "#666",
    marginBottom: 12,
    fontStyle: "italic",
  },
  rolesList: { gap: 10 },
  roleRow: {
    flexDirection: "row",
    alignItems: "center",
    justifyContent: "space-between",
    marginBottom: 8,
  },
  roleBadge: {
    backgroundColor: "#f97316",
    paddingHorizontal: 12,
    paddingVertical: 6,
    borderRadius: 16,
  },
  roleBadgeText: { color: "#fff", fontSize: 14, fontWeight: "600" },
  revokeButton: {
    flexDirection: "row",
    alignItems: "center",
    gap: 4,
    paddingHorizontal: 12,
    paddingVertical: 6,
    borderRadius: 8,
    borderWidth: 1,
    borderColor: "#c62828",
  },
  revokeButtonText: { fontSize: 14, color: "#c62828", fontWeight: "600" },
  roleChips: {
    flexDirection: "row",
    flexWrap: "wrap",
    gap: 8,
    marginBottom: 16,
  },
  roleChip: {
    paddingHorizontal: 14,
    paddingVertical: 8,
    borderRadius: 20,
    backgroundColor: "#e8e8e8",
    borderWidth: 1,
    borderColor: "#ddd",
  },
  roleChipActive: {
    backgroundColor: "#f97316",
    borderColor: "#f97316",
  },
  roleChipText: { fontSize: 14, color: "#333", fontWeight: "500" },
  roleChipTextActive: { color: "#fff" },
});
