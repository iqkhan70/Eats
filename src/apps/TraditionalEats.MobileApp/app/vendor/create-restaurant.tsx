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
  Image,
} from "react-native";
import { Ionicons } from "@expo/vector-icons";
import { useRouter } from "expo-router";
import * as ImagePicker from "expo-image-picker";
import * as Linking from "expo-linking";
import { api } from "../../services/api";
import { authService } from "../../services/auth";
import AppHeader from "../../components/AppHeader";
import { APP_CONFIG } from "../../config/api.config";

export default function CreateRestaurantScreen() {
  const router = useRouter();
  const [saving, setSaving] = useState(false);
  const [formData, setFormData] = useState({
    name: "",
    description: "",
    cuisineType: "",
    address: "",
    phoneNumber: "",
    email: "",
    imageUrl: "",
  });
  const [uploadingImage, setUploadingImage] = useState(false);

  const getRestaurantImageDisplayUrl = (imageUrl: string) => {
    if (!imageUrl) return "";
    if (imageUrl.startsWith("http://") || imageUrl.startsWith("https://"))
      return imageUrl;
    const base = APP_CONFIG.API_BASE_URL.replace(/\/$/, "");
    return `${base}/MobileBff/menu-image?path=${encodeURIComponent(imageUrl)}`;
  };

  const pickAndUploadImage = async () => {
    try {
      const { status } = await ImagePicker.requestMediaLibraryPermissionsAsync();
      if (status !== "granted") {
        Alert.alert(
          "Photo Access Needed",
          "Kram needs access to your photos to upload images. Please allow access in Settings.",
          [
            { text: "Open Settings", onPress: () => Linking.openSettings() },
            { text: "Cancel", style: "cancel" },
          ]
        );
        return;
      }
      const result = await ImagePicker.launchImageLibraryAsync({
        mediaTypes: ["images"],
        allowsEditing: true,
        aspect: [16, 9],
        quality: 0.8,
      });
      if (result.canceled || !result.assets?.[0]) return;
      const asset = result.assets[0];
      if (asset.fileSize && asset.fileSize > 5 * 1024 * 1024) {
        Alert.alert("Image Too Large", "Max 5MB");
        return;
      }
      setUploadingImage(true);
      const uri = asset.uri;
      const rawName = uri.split("/").pop() || "";
      const hasImageExt = /\.(jpg|jpeg|png|webp|gif|heic|heif)$/i.test(rawName);
      const fileName = hasImageExt ? rawName : "image.jpg";
      const mimeType = asset.mimeType && /^image\//i.test(asset.mimeType) ? asset.mimeType : "image/jpeg";
      const fd = new FormData();
      fd.append("file", {
        uri,
        name: fileName,
        type: mimeType,
      } as any);
      if (formData.imageUrl) fd.append("replacePath", formData.imageUrl);
      const res = await api.post<{ imageUrl: string }>(
        "/MobileBff/documents/upload-restaurant-image",
        fd
      );
      const url = res.data?.imageUrl;
      if (url) setFormData((p) => ({ ...p, imageUrl: url }));
      else Alert.alert("Upload Failed", "Could not upload image");
    } catch (error: any) {
      console.error("Restaurant image upload error:", error);
      Alert.alert("Upload Failed", error.response?.data?.message || "Failed to upload");
    } finally {
      setUploadingImage(false);
    }
  };

  const handleSave = async () => {
    if (!formData.name.trim()) {
      Alert.alert("Validation Error", "Vendor name is required");
      return;
    }

    if (!formData.address.trim()) {
      Alert.alert("Validation Error", "Address is required");
      return;
    }

    try {
      setSaving(true);
      const response = await api.post(
        "/MobileBff/vendor/restaurants",
        formData,
      );

      Alert.alert("Success", "Vendor created successfully", [
        {
          text: "OK",
          onPress: () => {
            // Navigate back to vendor dashboard
            router.back();
          },
        },
      ]);
    } catch (error: any) {
      console.error("Error creating restaurant:", error);
      const errorMessage =
        error.response?.data?.error ||
        error.message ||
        "Failed to create vendor";
      Alert.alert("Error", errorMessage);
    } finally {
      setSaving(false);
    }
  };

  return (
    <KeyboardAvoidingView
      style={styles.container}
      behavior={Platform.OS === "ios" ? "padding" : "height"}
      keyboardVerticalOffset={Platform.OS === "ios" ? 0 : 0}
    >
      <AppHeader title="Create Vendor" />

      <ScrollView
        style={styles.scrollView}
        contentContainerStyle={styles.scrollContent}
        keyboardShouldPersistTaps="handled"
        keyboardDismissMode="on-drag"
        showsVerticalScrollIndicator={true}
      >
        <View style={styles.form}>
          <View style={styles.formGroup}>
            <Text style={styles.label}>Vendor Name *</Text>
            <TextInput
              style={styles.input}
              value={formData.name}
              onChangeText={(text) => setFormData({ ...formData, name: text })}
              placeholder="Enter vendor name"
              autoFocus
            />
          </View>

          <View style={styles.formGroup}>
            <Text style={styles.label}>Description</Text>
            <TextInput
              style={[styles.input, styles.textArea]}
              value={formData.description}
              onChangeText={(text) =>
                setFormData({ ...formData, description: text })
              }
              placeholder="Enter description"
              multiline
              numberOfLines={4}
            />
          </View>

          <View style={styles.formGroup}>
            <Text style={styles.label}>Cuisine Type</Text>
            <TextInput
              style={styles.input}
              value={formData.cuisineType}
              onChangeText={(text) =>
                setFormData({ ...formData, cuisineType: text })
              }
              placeholder="e.g., Italian, Mexican, Chinese"
            />
          </View>

          <View style={styles.formGroup}>
            <Text style={styles.label}>Address *</Text>
            <TextInput
              style={styles.input}
              value={formData.address}
              onChangeText={(text) =>
                setFormData({ ...formData, address: text })
              }
              placeholder="Enter full address"
            />
          </View>

          <View style={styles.formGroup}>
            <Text style={styles.label}>Phone Number</Text>
            <TextInput
              style={styles.input}
              value={formData.phoneNumber}
              onChangeText={(text) =>
                setFormData({ ...formData, phoneNumber: text })
              }
              placeholder="Enter phone number"
              keyboardType="phone-pad"
            />
          </View>

          <View style={styles.formGroup}>
            <Text style={styles.label}>Email</Text>
            <TextInput
              style={styles.input}
              value={formData.email}
              onChangeText={(text) => setFormData({ ...formData, email: text })}
              placeholder="Enter email address"
              keyboardType="email-address"
              autoCapitalize="none"
            />
          </View>

          <View style={styles.formGroup}>
            <Text style={styles.label}>Vendor Image</Text>
            {formData.imageUrl ? (
              <View style={styles.imagePreviewRow}>
                <Image
                  source={{ uri: getRestaurantImageDisplayUrl(formData.imageUrl) }}
                  style={styles.imagePreview}
                />
                <TouchableOpacity
                  onPress={() => setFormData({ ...formData, imageUrl: "" })}
                  style={styles.removeImageButton}
                >
                  <Ionicons name="close-circle" size={24} color="#c62828" />
                </TouchableOpacity>
              </View>
            ) : (
              <TouchableOpacity
                style={styles.uploadButton}
                onPress={pickAndUploadImage}
                disabled={uploadingImage}
              >
                {uploadingImage ? (
                  <ActivityIndicator size="small" color="#6200ee" />
                ) : (
                  <>
                    <Ionicons name="image-outline" size={24} color="#6200ee" />
                    <Text style={styles.uploadButtonText}>
                      {uploadingImage ? "Uploading..." : "Upload Image"}
                    </Text>
                  </>
                )}
              </TouchableOpacity>
            )}
            <Text style={styles.caption}>JPEG, PNG, WebP or GIF. Max 5MB.</Text>
          </View>

          <TouchableOpacity
            style={[styles.saveButton, saving && styles.saveButtonDisabled]}
            onPress={handleSave}
            disabled={saving}
          >
            {saving ? (
              <ActivityIndicator size="small" color="#fff" />
            ) : (
              <>
                <Ionicons
                  name="checkmark"
                  size={20}
                  color="#fff"
                  style={{ marginRight: 8 }}
                />
                <Text style={styles.saveButtonText}>Create Vendor</Text>
              </>
            )}
          </TouchableOpacity>
        </View>
      </ScrollView>
    </KeyboardAvoidingView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: "#f5f5f5",
  },
  scrollView: {
    flex: 1,
  },
  scrollContent: {
    paddingBottom: 320,
  },
  header: {
    backgroundColor: "#6200ee",
    padding: 16,
    paddingTop: 60,
    flexDirection: "row",
    alignItems: "center",
    justifyContent: "space-between",
  },
  backButton: {
    padding: 8,
  },
  headerTitle: {
    fontSize: 20,
    fontWeight: "bold",
    color: "#fff",
    flex: 1,
    textAlign: "center",
  },
  placeholder: {
    width: 40,
  },
  form: {
    padding: 16,
  },
  formGroup: {
    marginBottom: 20,
  },
  label: {
    fontSize: 14,
    fontWeight: "600",
    color: "#333",
    marginBottom: 8,
  },
  input: {
    backgroundColor: "#fff",
    borderWidth: 1,
    borderColor: "#e0e0e0",
    borderRadius: 8,
    padding: 12,
    fontSize: 16,
    color: "#333",
  },
  textArea: {
    height: 100,
    textAlignVertical: "top",
  },
  saveButton: {
    backgroundColor: "#6200ee",
    padding: 16,
    borderRadius: 8,
    flexDirection: "row",
    alignItems: "center",
    justifyContent: "center",
    marginTop: 8,
  },
  saveButtonDisabled: {
    opacity: 0.6,
  },
  saveButtonText: {
    color: "#fff",
    fontSize: 16,
    fontWeight: "600",
  },
  imagePreviewRow: {
    flexDirection: "row",
    alignItems: "center",
    gap: 12,
  },
  imagePreview: {
    width: 120,
    height: 80,
    borderRadius: 8,
  },
  removeImageButton: { padding: 4 },
  uploadButton: {
    flexDirection: "row",
    alignItems: "center",
    justifyContent: "center",
    gap: 8,
    padding: 16,
    backgroundColor: "#f0e6ff",
    borderRadius: 8,
    borderWidth: 2,
    borderColor: "#6200ee",
    borderStyle: "dashed",
  },
  uploadButtonText: { color: "#6200ee", fontWeight: "600" },
  caption: { fontSize: 12, color: "#666", marginTop: 4 },
});
