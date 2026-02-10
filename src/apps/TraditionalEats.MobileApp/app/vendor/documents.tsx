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
  Modal,
  TextInput,
  Platform,
} from "react-native";
import { Ionicons } from "@expo/vector-icons";
import { useRouter, useFocusEffect } from "expo-router";
import * as DocumentPicker from "expo-document-picker";
import { api } from "../../services/api";
import { authService } from "../../services/auth";

interface Document {
  documentId: string;
  vendorId: string;
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

export default function VendorDocumentsScreen() {
  const router = useRouter();
  const [documents, setDocuments] = useState<Document[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [isAuthenticated, setIsAuthenticated] = useState(false);
  const [isVendor, setIsVendor] = useState(false);
  const [uploadModalVisible, setUploadModalVisible] = useState(false);
  const [uploading, setUploading] = useState(false);
  const [statusFilter, setStatusFilter] = useState<"all" | "active" | "inactive">("all");

  // Upload form state
  const [selectedFile, setSelectedFile] = useState<DocumentPicker.DocumentPickerAsset | null>(null);
  const [documentType, setDocumentType] = useState("");
  const [notes, setNotes] = useState("");
  const [expiresAt, setExpiresAt] = useState("");

  const documentTypes = [
    "BusinessLicense",
    "HealthCertificate",
    "Insurance",
    "TaxDocument",
    "IdentityVerification",
    "Other",
  ];

  useEffect(() => {
    checkAuthAndLoad();
  }, []);

  useFocusEffect(
    React.useCallback(() => {
      if (isAuthenticated && isVendor) {
        loadDocuments();
      }
    }, [isAuthenticated, isVendor, statusFilter]),
  );

  const checkAuthAndLoad = async () => {
    const authenticated = await authService.isAuthenticated();
    setIsAuthenticated(authenticated);

    if (authenticated) {
      const vendor = await authService.isVendor();
      setIsVendor(vendor);

      if (vendor) {
        await loadDocuments();
      } else {
        Alert.alert(
          "Access Denied",
          "You must be a vendor to access this page.",
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
      const queryParam = statusFilter !== "all" ? `?isActive=${statusFilter === "active"}` : "";
      const response = await api.get<Document[]>(
        `/MobileBff/documents/vendor/my-documents${queryParam}`,
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

  const pickDocument = async () => {
    try {
      const result = await DocumentPicker.getDocumentAsync({
        type: ["application/pdf", "image/*", "application/msword", "application/vnd.openxmlformats-officedocument.wordprocessingml.document"],
        copyToCacheDirectory: true,
      });

      if (!result.canceled && result.assets && result.assets.length > 0) {
        const file = result.assets[0];
        // Check file size (10MB limit)
        if (file.size && file.size > 10 * 1024 * 1024) {
          Alert.alert("File Too Large", "File size must be less than 10MB");
          return;
        }
        setSelectedFile(file);
      }
    } catch (error) {
      console.error("Error picking document:", error);
      Alert.alert("Error", "Failed to pick document");
    }
  };

  const uploadDocument = async () => {
    if (!selectedFile || !documentType) {
      Alert.alert("Validation Error", "Please select a file and document type");
      return;
    }

    try {
      setUploading(true);

      const formData = new FormData();
      formData.append("file", {
        uri: selectedFile.uri,
        name: selectedFile.name || "document",
        type: selectedFile.mimeType || "application/octet-stream",
      } as any);
      formData.append("documentType", documentType);
      if (notes) formData.append("notes", notes);
      if (expiresAt) formData.append("expiresAt", expiresAt);

      // Don't set Content-Type header - axios will set it automatically with boundary for FormData
      await api.post("/MobileBff/documents/upload", formData);

      Alert.alert("Success", "Document uploaded successfully");
      setUploadModalVisible(false);
      resetUploadForm();
      await loadDocuments();
    } catch (error: any) {
      console.error("Error uploading document:", error);
      Alert.alert(
        "Upload Failed",
        error.response?.data?.message || "Failed to upload document. Please try again.",
      );
    } finally {
      setUploading(false);
    }
  };

  const resetUploadForm = () => {
    setSelectedFile(null);
    setDocumentType("");
    setNotes("");
    setExpiresAt("");
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
      await api.patch(`/MobileBff/documents/${doc.documentId}/status`, {
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
        <ActivityIndicator size="large" color="#007AFF" />
        <Text style={styles.loadingText}>Loading documents...</Text>
      </View>
    );
  }

  if (!isAuthenticated || !isVendor) {
    return null;
  }

  return (
    <View style={styles.container}>
      <View style={styles.header}>
        <TouchableOpacity onPress={() => router.back()} style={styles.backButton}>
          <Ionicons name="arrow-back" size={24} color="#000" />
        </TouchableOpacity>
        <Text style={styles.headerTitle}>My Documents</Text>
        <TouchableOpacity
          onPress={() => setUploadModalVisible(true)}
          style={styles.uploadButton}
        >
          <Ionicons name="add" size={24} color="#007AFF" />
        </TouchableOpacity>
      </View>

      {/* Filter buttons */}
      <View style={styles.filterContainer}>
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

      {documents.length === 0 ? (
        <View style={styles.emptyContainer}>
          <Ionicons name="document-outline" size={64} color="#ccc" />
          <Text style={styles.emptyText}>No documents uploaded yet</Text>
          <TouchableOpacity
            style={styles.uploadButtonLarge}
            onPress={() => setUploadModalVisible(true)}
          >
            <Text style={styles.uploadButtonLargeText}>Upload Your First Document</Text>
          </TouchableOpacity>
        </View>
      ) : (
        <ScrollView
          style={styles.scrollView}
          refreshControl={
            <RefreshControl refreshing={refreshing} onRefresh={onRefresh} />
          }
        >
          {documents.map((doc) => (
            <View key={doc.documentId} style={styles.documentCard}>
              <View style={styles.documentHeader}>
                <View style={styles.documentInfo}>
                  <Ionicons name="document-text" size={24} color="#007AFF" />
                  <View style={styles.documentDetails}>
                    <Text style={styles.documentName} numberOfLines={1}>
                      {doc.fileName}
                    </Text>
                    <Text style={styles.documentMeta}>
                      {doc.documentType} • {formatFileSize(doc.fileSize)}
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
                  <Ionicons name="download" size={20} color="#007AFF" />
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
              </View>
            </View>
          ))}
        </ScrollView>
      )}

      {/* Upload Modal */}
      <Modal
        visible={uploadModalVisible}
        animationType="slide"
        transparent={true}
        onRequestClose={() => setUploadModalVisible(false)}
      >
        <View style={styles.modalOverlay}>
          <View style={styles.modalContent}>
            <View style={styles.modalHeader}>
              <Text style={styles.modalTitle}>Upload Document</Text>
              <TouchableOpacity onPress={() => setUploadModalVisible(false)}>
                <Ionicons name="close" size={24} color="#000" />
              </TouchableOpacity>
            </View>

            <ScrollView style={styles.modalBody}>
              <View style={styles.formGroup}>
                <Text style={styles.label}>Document Type *</Text>
                <ScrollView horizontal showsHorizontalScrollIndicator={false}>
                  <View style={styles.typeButtons}>
                    {documentTypes.map((type) => (
                      <TouchableOpacity
                        key={type}
                        style={[
                          styles.typeButton,
                          documentType === type && styles.typeButtonActive,
                        ]}
                        onPress={() => setDocumentType(type)}
                      >
                        <Text
                          style={[
                            styles.typeButtonText,
                            documentType === type && styles.typeButtonTextActive,
                          ]}
                        >
                          {type}
                        </Text>
                      </TouchableOpacity>
                    ))}
                  </View>
                </ScrollView>
              </View>

              <View style={styles.formGroup}>
                <Text style={styles.label}>File *</Text>
                <TouchableOpacity
                  style={styles.filePickerButton}
                  onPress={pickDocument}
                >
                  <Ionicons name="document-attach" size={24} color="#007AFF" />
                  <Text style={styles.filePickerText}>
                    {selectedFile
                      ? selectedFile.name
                      : "Tap to select a file"}
                  </Text>
                </TouchableOpacity>
                {selectedFile && (
                  <Text style={styles.fileSizeText}>
                    Size: {formatFileSize(selectedFile.size || 0)}
                  </Text>
                )}
              </View>

              <View style={styles.formGroup}>
                <Text style={styles.label}>Notes (Optional)</Text>
                <TextInput
                  style={styles.textInput}
                  multiline
                  numberOfLines={3}
                  value={notes}
                  onChangeText={setNotes}
                  placeholder="Add any notes about this document..."
                />
              </View>

              <View style={styles.formGroup}>
                <Text style={styles.label}>Expiration Date (Optional)</Text>
                <TextInput
                  style={styles.textInput}
                  value={expiresAt}
                  onChangeText={setExpiresAt}
                  placeholder="YYYY-MM-DD"
                />
              </View>
            </ScrollView>

            <View style={styles.modalFooter}>
              <TouchableOpacity
                style={styles.cancelButton}
                onPress={() => {
                  setUploadModalVisible(false);
                  resetUploadForm();
                }}
              >
                <Text style={styles.cancelButtonText}>Cancel</Text>
              </TouchableOpacity>
              <TouchableOpacity
                style={[
                  styles.uploadButtonModal,
                  (!selectedFile || !documentType || uploading) &&
                    styles.uploadButtonModalDisabled,
                ]}
                onPress={uploadDocument}
                disabled={!selectedFile || !documentType || uploading}
              >
                {uploading ? (
                  <ActivityIndicator color="#fff" />
                ) : (
                  <Text style={styles.uploadButtonModalText}>Upload</Text>
                )}
              </TouchableOpacity>
            </View>
          </View>
        </View>
      </Modal>
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
  uploadButton: {
    padding: 8,
  },
  filterContainer: {
    flexDirection: "row",
    padding: 16,
    backgroundColor: "#fff",
    borderBottomWidth: 1,
    borderBottomColor: "#e0e0e0",
  },
  filterButton: {
    paddingHorizontal: 16,
    paddingVertical: 8,
    borderRadius: 20,
    backgroundColor: "#f0f0f0",
    marginRight: 8,
  },
  filterButtonActive: {
    backgroundColor: "#007AFF",
  },
  filterButtonText: {
    color: "#666",
    fontSize: 14,
    fontWeight: "500",
  },
  filterButtonTextActive: {
    color: "#fff",
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
    marginBottom: 24,
  },
  uploadButtonLarge: {
    backgroundColor: "#007AFF",
    paddingHorizontal: 24,
    paddingVertical: 12,
    borderRadius: 8,
  },
  uploadButtonLargeText: {
    color: "#fff",
    fontSize: 16,
    fontWeight: "600",
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
    color: "#007AFF",
  },
  deleteText: {
    color: "#FF3B30",
  },
  modalOverlay: {
    flex: 1,
    backgroundColor: "rgba(0, 0, 0, 0.5)",
    justifyContent: "flex-end",
  },
  modalContent: {
    backgroundColor: "#fff",
    borderTopLeftRadius: 20,
    borderTopRightRadius: 20,
    maxHeight: "90%",
  },
  modalHeader: {
    flexDirection: "row",
    justifyContent: "space-between",
    alignItems: "center",
    padding: 16,
    borderBottomWidth: 1,
    borderBottomColor: "#e0e0e0",
  },
  modalTitle: {
    fontSize: 20,
    fontWeight: "bold",
  },
  modalBody: {
    padding: 16,
  },
  formGroup: {
    marginBottom: 20,
  },
  label: {
    fontSize: 16,
    fontWeight: "600",
    marginBottom: 8,
    color: "#000",
  },
  typeButtons: {
    flexDirection: "row",
    gap: 8,
  },
  typeButton: {
    paddingHorizontal: 16,
    paddingVertical: 8,
    borderRadius: 20,
    backgroundColor: "#f0f0f0",
    marginRight: 8,
  },
  typeButtonActive: {
    backgroundColor: "#007AFF",
  },
  typeButtonText: {
    fontSize: 14,
    color: "#666",
  },
  typeButtonTextActive: {
    color: "#fff",
    fontWeight: "600",
  },
  filePickerButton: {
    flexDirection: "row",
    alignItems: "center",
    padding: 16,
    borderWidth: 2,
    borderColor: "#007AFF",
    borderStyle: "dashed",
    borderRadius: 8,
    backgroundColor: "#f9f9f9",
  },
  filePickerText: {
    marginLeft: 12,
    fontSize: 14,
    color: "#007AFF",
  },
  fileSizeText: {
    marginTop: 8,
    fontSize: 12,
    color: "#666",
  },
  textInput: {
    borderWidth: 1,
    borderColor: "#e0e0e0",
    borderRadius: 8,
    padding: 12,
    fontSize: 16,
    minHeight: 80,
    textAlignVertical: "top",
  },
  modalFooter: {
    flexDirection: "row",
    justifyContent: "space-between",
    padding: 16,
    borderTopWidth: 1,
    borderTopColor: "#e0e0e0",
  },
  cancelButton: {
    flex: 1,
    padding: 12,
    borderRadius: 8,
    backgroundColor: "#f0f0f0",
    marginRight: 8,
    alignItems: "center",
  },
  cancelButtonText: {
    fontSize: 16,
    color: "#666",
    fontWeight: "600",
  },
  uploadButtonModal: {
    flex: 1,
    padding: 12,
    borderRadius: 8,
    backgroundColor: "#007AFF",
    alignItems: "center",
  },
  uploadButtonModalDisabled: {
    backgroundColor: "#ccc",
  },
  uploadButtonModalText: {
    fontSize: 16,
    color: "#fff",
    fontWeight: "600",
  },
});
