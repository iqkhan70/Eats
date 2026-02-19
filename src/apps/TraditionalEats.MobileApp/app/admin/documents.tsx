import React, { useState, useEffect } from "react";
import {
  View,
  Text,
  StyleSheet,
  ScrollView,
  TouchableOpacity,
  Alert,
  ActivityIndicator,
  RefreshControl,
  Linking,
  TextInput,
} from "react-native";
import { Ionicons } from "@expo/vector-icons";
import { useRouter, useFocusEffect } from "expo-router";
import { api } from "../../services/api";
import { authService } from "../../services/auth";
import AppHeader from "../../components/AppHeader";

interface Document {
  documentId: string;
  vendorId: string;
  vendorName?: string;
  fileName: string;
  documentType: string;
  fileSize: number;
  contentType: string;
  isActive: boolean;
  uploadedAt: string;
  updatedAt: string;
  expiresAt?: string;
  notes?: string;
  downloadUrl?: string;
}

export default function AdminDocumentsScreen() {
  const router = useRouter();
  const [documents, setDocuments] = useState<Document[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [isAuthenticated, setIsAuthenticated] = useState(false);
  const [isAdmin, setIsAdmin] = useState(false);
  const [statusFilter, setStatusFilter] = useState<"all" | "active" | "inactive">("all");
  const [vendorNameFilter, setVendorNameFilter] = useState("");

  const displayedDocuments = React.useMemo(() => {
    const name = vendorNameFilter.trim().toLowerCase();
    if (!name) return documents;
    return documents.filter(
      (d) => (d.vendorName ?? "").toLowerCase().includes(name) || (d.vendorId ?? "").toLowerCase().includes(name)
    );
  }, [documents, vendorNameFilter]);

  useEffect(() => {
    checkAuthAndLoad();
  }, []);

  useFocusEffect(
    React.useCallback(() => {
      if (isAuthenticated && isAdmin) {
        loadDocuments();
      }
    }, [isAuthenticated, isAdmin, statusFilter]),
  );

  const checkAuthAndLoad = async () => {
    const authenticated = await authService.isAuthenticated();
    setIsAuthenticated(authenticated);

    if (authenticated) {
      const admin = await authService.isAdmin();
      setIsAdmin(admin);

      if (admin) {
        await loadDocuments();
      } else {
        Alert.alert(
          "Access Denied",
          "You must be an admin to access this page.",
        );
        router.back();
      }
    } else {
      Alert.alert(
        "Authentication Required",
        "Please log in to access documents.",
      );
      router.push("/login");
    }
  };

  const loadDocuments = async () => {
    try {
      setLoading(true);
      const queryParams = new URLSearchParams();
      if (statusFilter !== "all") queryParams.append("isActive", statusFilter === "active" ? "true" : "false");

      const queryString = queryParams.toString();
      const response = await api.get<Document[]>(
        `/MobileBff/documents/admin/all${queryString ? `?${queryString}` : ""}`,
      );
      setDocuments(response.data || []);
    } catch (error: any) {
      if (error.response?.status === 401) {
        Alert.alert("Session Expired", "Please log in again.");
        await authService.logout();
        router.push("/login");
      } else if (error.response?.status === 403) {
        Alert.alert(
          "Access Denied",
          "You do not have permission to access this page.",
        );
        router.back();
      } else {
        console.error("Error loading documents:", error);
        Alert.alert("Error", "Failed to load documents. Please try again.");
      }
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  };

  const onRefresh = () => {
    setRefreshing(true);
    loadDocuments();
  };

  const downloadDocument = async (doc: Document) => {
    if (!doc.downloadUrl) {
      Alert.alert("Error", "Download URL not available");
      return;
    }

    try {
      const supported = await Linking.canOpenURL(doc.downloadUrl);
      if (supported) {
        await Linking.openURL(doc.downloadUrl);
      } else {
        Alert.alert("Error", "Cannot open this URL");
      }
    } catch (error) {
      console.error("Error downloading document:", error);
      Alert.alert("Error", "Failed to download document");
    }
  };

  const toggleDocumentStatus = async (doc: Document) => {
    try {
      await api.patch(`/MobileBff/documents/admin/${doc.documentId}/status`, {
        isActive: !doc.isActive,
      });

      Alert.alert(
        "Success",
        `Document ${doc.isActive ? "deactivated" : "activated"} successfully`,
      );
      await loadDocuments();
    } catch (error: any) {
      console.error("Error updating document status:", error);
      Alert.alert("Error", "Failed to update document status");
    }
  };

  const deleteDocument = async (doc: Document) => {
    Alert.alert(
      "Delete Document",
      "Are you sure you want to delete this document? This action cannot be undone.",
      [
        { text: "Cancel", style: "cancel" },
        {
          text: "Delete",
          style: "destructive",
          onPress: async () => {
            try {
              await api.delete(`/MobileBff/documents/admin/${doc.documentId}`);
              Alert.alert("Success", "Document deleted successfully");
              await loadDocuments();
            } catch (error: any) {
              console.error("Error deleting document:", error);
              Alert.alert("Error", "Failed to delete document");
            }
          },
        },
      ],
    );
  };

  const formatFileSize = (bytes: number): string => {
    const sizes = ["B", "KB", "MB", "GB"];
    if (bytes === 0) return "0 B";
    const i = Math.floor(Math.log(bytes) / Math.log(1024));
    return Math.round(bytes / Math.pow(1024, i) * 100) / 100 + " " + sizes[i];
  };

  const formatDate = (dateString: string): string => {
    const date = new Date(dateString);
    return date.toLocaleDateString() + " " + date.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });
  };

  if (loading && !refreshing) {
    return (
      <View style={styles.centerContainer}>
        <ActivityIndicator size="large" color="#6200ee" />
        <Text style={styles.loadingText}>Loading documents...</Text>
      </View>
    );
  }

  if (!isAuthenticated || !isAdmin) {
    return null;
  }

  return (
    <View style={styles.container}>
      <AppHeader title="All Documents" />

      {/* Filter section */}
      <View style={styles.filterContainer}>
        <View style={styles.filterRow}>
          <TouchableOpacity
            style={[
              styles.filterButton,
              statusFilter === "all" && styles.filterButtonActive,
            ]}
            onPress={() => setStatusFilter("all")}
          >
            <Text
              style={[
                styles.filterButtonText,
                statusFilter === "all" && styles.filterButtonTextActive,
              ]}
            >
              All
            </Text>
          </TouchableOpacity>
          <TouchableOpacity
            style={[
              styles.filterButton,
              statusFilter === "active" && styles.filterButtonActive,
            ]}
            onPress={() => setStatusFilter("active")}
          >
            <Text
              style={[
                styles.filterButtonText,
                statusFilter === "active" && styles.filterButtonTextActive,
              ]}
            >
              Active
            </Text>
          </TouchableOpacity>
          <TouchableOpacity
            style={[
              styles.filterButton,
              statusFilter === "inactive" && styles.filterButtonActive,
            ]}
            onPress={() => setStatusFilter("inactive")}
          >
            <Text
              style={[
                styles.filterButtonText,
                statusFilter === "inactive" && styles.filterButtonTextActive,
              ]}
            >
              Inactive
            </Text>
          </TouchableOpacity>
        </View>
        <View style={styles.vendorFilterContainer}>
          <TextInput
            style={styles.vendorFilterInput}
            placeholder="Filter by Vendor Name"
            value={vendorNameFilter}
            onChangeText={setVendorNameFilter}
            placeholderTextColor="#999"
          />
          {vendorNameFilter.length > 0 && (
            <TouchableOpacity
              onPress={() => setVendorNameFilter("")}
              style={styles.clearButton}
            >
              <Ionicons name="close-circle" size={20} color="#666" />
            </TouchableOpacity>
          )}
        </View>
      </View>

      {documents.length === 0 ? (
        <View style={styles.emptyContainer}>
          <Ionicons name="document-outline" size={64} color="#ccc" />
          <Text style={styles.emptyText}>No documents found</Text>
        </View>
      ) : displayedDocuments.length === 0 ? (
        <View style={styles.emptyContainer}>
          <Ionicons name="search-outline" size={64} color="#ccc" />
          <Text style={styles.emptyText}>No documents match vendor name</Text>
        </View>
      ) : (
        <ScrollView
          style={styles.scrollView}
          refreshControl={
            <RefreshControl refreshing={refreshing} onRefresh={onRefresh} />
          }
        >
          {displayedDocuments.map((doc) => (
            <View key={doc.documentId} style={styles.documentCard}>
              <View style={styles.documentHeader}>
                <View style={styles.documentInfo}>
                  <Ionicons name="document-text" size={24} color="#6200ee" />
                  <View style={styles.documentDetails}>
                    <Text style={styles.documentName} numberOfLines={1}>
                      {doc.fileName}
                    </Text>
                    <Text style={styles.documentMeta}>
                      {doc.documentType} • {formatFileSize(doc.fileSize)}
                    </Text>
                    <Text style={styles.documentVendor}>
                      Vendor: {doc.vendorName ?? doc.vendorId}
                    </Text>
                    <Text style={styles.documentDate}>
                      Uploaded: {formatDate(doc.uploadedAt)}
                    </Text>
                  </View>
                </View>
                <View
                  style={[
                    styles.statusBadge,
                    doc.isActive ? styles.statusBadgeActive : styles.statusBadgeInactive,
                  ]}
                >
                  <Text
                    style={[
                      styles.statusText,
                      doc.isActive ? styles.statusTextActive : styles.statusTextInactive,
                    ]}
                  >
                    {doc.isActive ? "Active" : "Inactive"}
                  </Text>
                </View>
              </View>

              <View style={styles.documentActions}>
                <TouchableOpacity
                  style={styles.actionButton}
                  onPress={() => downloadDocument(doc)}
                >
                  <Ionicons name="download" size={20} color="#6200ee" />
                  <Text style={styles.actionButtonText}>Download</Text>
                </TouchableOpacity>
                <TouchableOpacity
                  style={styles.actionButton}
                  onPress={() => toggleDocumentStatus(doc)}
                >
                  <Ionicons
                    name={doc.isActive ? "toggle" : "toggle-outline"}
                    size={20}
                    color="#FF9500"
                  />
                  <Text style={styles.actionButtonText}>
                    {doc.isActive ? "Deactivate" : "Activate"}
                  </Text>
                </TouchableOpacity>
                <TouchableOpacity
                  style={styles.actionButton}
                  onPress={() => deleteDocument(doc)}
                >
                  <Ionicons name="trash" size={20} color="#FF3B30" />
                  <Text style={[styles.actionButtonText, styles.deleteText]}>
                    Delete
                  </Text>
                </TouchableOpacity>
              </View>
            </View>
          ))}
        </ScrollView>
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: "#f5f5f5",
  },
  centerContainer: {
    flex: 1,
    justifyContent: "center",
    alignItems: "center",
  },
  loadingText: {
    marginTop: 16,
    fontSize: 16,
    color: "#666",
  },
  header: {
    flexDirection: "row",
    alignItems: "center",
    justifyContent: "space-between",
    padding: 16,
    backgroundColor: "#fff",
    borderBottomWidth: 1,
    borderBottomColor: "#e0e0e0",
  },
  backButton: {
    padding: 8,
  },
  headerTitle: {
    fontSize: 20,
    fontWeight: "bold",
    flex: 1,
    textAlign: "center",
  },
  placeholder: {
    width: 40,
  },
  filterContainer: {
    backgroundColor: "#fff",
    padding: 16,
    borderBottomWidth: 1,
    borderBottomColor: "#e0e0e0",
  },
  filterRow: {
    flexDirection: "row",
    marginBottom: 12,
  },
  filterButton: {
    paddingHorizontal: 16,
    paddingVertical: 8,
    borderRadius: 20,
    backgroundColor: "#f0f0f0",
    marginRight: 8,
  },
  filterButtonActive: {
    backgroundColor: "#6200ee",
  },
  filterButtonText: {
    color: "#666",
    fontSize: 14,
    fontWeight: "500",
  },
  filterButtonTextActive: {
    color: "#fff",
  },
  vendorFilterContainer: {
    flexDirection: "row",
    alignItems: "center",
  },
  vendorFilterInput: {
    flex: 1,
    borderWidth: 1,
    borderColor: "#e0e0e0",
    borderRadius: 8,
    padding: 12,
    fontSize: 14,
    backgroundColor: "#f9f9f9",
  },
  clearButton: {
    marginLeft: 8,
    padding: 4,
  },
  scrollView: {
    flex: 1,
  },
  emptyContainer: {
    flex: 1,
    justifyContent: "center",
    alignItems: "center",
    padding: 32,
  },
  emptyText: {
    fontSize: 16,
    color: "#666",
    marginTop: 16,
  },
  documentCard: {
    backgroundColor: "#fff",
    margin: 16,
    marginBottom: 0,
    padding: 16,
    borderRadius: 8,
    shadowColor: "#000",
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.1,
    shadowRadius: 4,
    elevation: 3,
  },
  documentHeader: {
    flexDirection: "row",
    justifyContent: "space-between",
    alignItems: "flex-start",
    marginBottom: 12,
  },
  documentInfo: {
    flexDirection: "row",
    flex: 1,
  },
  documentDetails: {
    marginLeft: 12,
    flex: 1,
  },
  documentName: {
    fontSize: 16,
    fontWeight: "600",
    color: "#000",
    marginBottom: 4,
  },
  documentMeta: {
    fontSize: 14,
    color: "#666",
    marginBottom: 4,
  },
  documentVendor: {
    fontSize: 12,
    color: "#999",
    marginBottom: 4,
  },
  documentDate: {
    fontSize: 12,
    color: "#999",
  },
  statusBadge: {
    paddingHorizontal: 12,
    paddingVertical: 4,
    borderRadius: 12,
  },
  statusBadgeActive: {
    backgroundColor: "#34C759",
  },
  statusBadgeInactive: {
    backgroundColor: "#8E8E93",
  },
  statusText: {
    fontSize: 12,
    fontWeight: "600",
  },
  statusTextActive: {
    color: "#fff",
  },
  statusTextInactive: {
    color: "#fff",
  },
  documentActions: {
    flexDirection: "row",
    justifyContent: "space-around",
    paddingTop: 12,
    borderTopWidth: 1,
    borderTopColor: "#e0e0e0",
  },
  actionButton: {
    flexDirection: "row",
    alignItems: "center",
    padding: 8,
  },
  actionButtonText: {
    marginLeft: 4,
    fontSize: 14,
    color: "#6200ee",
  },
  deleteText: {
    color: "#FF3B30",
  },
});
