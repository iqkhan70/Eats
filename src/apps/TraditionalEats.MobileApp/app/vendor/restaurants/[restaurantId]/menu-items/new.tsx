import React, { useEffect, useState } from "react";
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
import { LinearGradient } from "expo-linear-gradient";
import { Ionicons } from "@expo/vector-icons";
import { useRouter, useLocalSearchParams } from "expo-router";
import * as ImagePicker from "expo-image-picker";
import * as Linking from "expo-linking";
import { api } from "../../../../../services/api";
import { APP_CONFIG } from "../../../../../config/api.config";

interface Category {
  categoryId: string;
  name: string;
  description?: string;
}

export default function CreateMenuItemScreen() {
  const router = useRouter();
  const params = useLocalSearchParams<{ restaurantId?: string }>();
  const restaurantId = params.restaurantId as string;

  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [categories, setCategories] = useState<Category[]>([]);
  const [formData, setFormData] = useState({
    categoryId: "",
    name: "",
    description: "",
    price: "",
    imageUrl: "",
  });
  const [uploadingImage, setUploadingImage] = useState(false);

  useEffect(() => {
    if (!restaurantId) return;
    loadCategories();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [restaurantId]);

  const getMenuImageDisplayUrl = (imageUrl: string) => {
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
        aspect: [1, 1],
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
      const formData2 = new FormData();
      formData2.append("file", {
        uri,
        name: fileName,
        type: mimeType,
      } as any);
      if (formData.imageUrl) formData2.append("replacePath", formData.imageUrl);
      const res = await api.post<{ imageUrl: string }>(
        "/MobileBff/documents/upload-menu-image",
        formData2
      );
      const url = res.data?.imageUrl;
      if (url) setFormData((p) => ({ ...p, imageUrl: url }));
      else Alert.alert("Upload Failed", "Could not upload image");
    } catch (error: any) {
      console.error("Image upload error:", error);
      Alert.alert("Upload Failed", error.response?.data?.message || "Failed to upload");
    } finally {
      setUploadingImage(false);
    }
  };

  const loadCategories = async () => {
    try {
      setLoading(true);
      const response = await api.get<Category[]>("/MobileBff/categories", {
        params: { __ts: Date.now() },
        headers: { "Cache-Control": "no-store", Pragma: "no-cache" },
      });
      setCategories(response.data || []);
    } catch (error: any) {
      console.error("Error loading categories:", error);
      Alert.alert("Error", "Failed to load categories");
    } finally {
      setLoading(false);
    }
  };

  const handleSave = async () => {
    if (!formData.name.trim()) {
      Alert.alert("Validation Error", "Menu item name is required");
      return;
    }
    if (!formData.categoryId) {
      Alert.alert("Validation Error", "Please select a category");
      return;
    }

    const price = parseFloat(formData.price);
    if (isNaN(price) || price <= 0) {
      Alert.alert("Validation Error", "Please enter a valid price");
      return;
    }

    try {
      setSaving(true);

      const request = {
        categoryId: formData.categoryId,
        name: formData.name.trim(),
        description: formData.description.trim() || null,
        price,
        imageUrl: formData.imageUrl.trim() || null,
        dietaryTags: null as string[] | null,
      };

      await api.post(
        `/MobileBff/restaurants/${restaurantId}/menu-items`,
        request,
      );

      // ✅ go back to menu list + force refresh
      router.replace({
        pathname: "/vendor/restaurants/[restaurantId]/menu",
        params: {
          restaurantId,
          refreshedAt: Date.now().toString(),
        },
      });
    } catch (error: any) {
      console.error("Error creating menu item:", error);
      const msg =
        error.response?.data?.error ||
        error.message ||
        "Failed to create menu item";
      Alert.alert("Error", msg);
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return (
      <View style={styles.centerContainer}>
        <ActivityIndicator size="large" color="#6200ee" />
        <Text style={styles.loadingText}>Loading categories...</Text>
      </View>
    );
  }

  return (
    <KeyboardAvoidingView
      style={styles.container}
      behavior={Platform.OS === "ios" ? "padding" : "height"}
      keyboardVerticalOffset={0}
    >
      <LinearGradient
        colors={["#f97316", "#eab308"]}
        start={{ x: 0, y: 0 }}
        end={{ x: 1, y: 0 }}
        style={styles.header}
      >
        <TouchableOpacity
          onPress={() => router.back()}
          style={styles.backButton}
        >
          <Ionicons name="chevron-back" size={28} color="#fff" />
        </TouchableOpacity>
        <Text style={styles.headerTitle}>Add Menu Item</Text>
        <View style={styles.placeholder} />
      </LinearGradient>

      <ScrollView
        style={styles.scrollView}
        contentContainerStyle={styles.scrollContent}
        keyboardShouldPersistTaps="handled"
        keyboardDismissMode="on-drag"
        showsVerticalScrollIndicator={true}
      >
        <View style={styles.form}>
          <View style={styles.formGroup}>
            <Text style={styles.label}>Category *</Text>
            <View style={styles.categoryContainer}>
              {categories.map((category) => (
                <TouchableOpacity
                  key={category.categoryId}
                  style={[
                    styles.categoryButton,
                    formData.categoryId === category.categoryId &&
                      styles.categoryButtonSelected,
                  ]}
                  onPress={() =>
                    setFormData({
                      ...formData,
                      categoryId: category.categoryId,
                    })
                  }
                >
                  <Text
                    style={[
                      styles.categoryButtonText,
                      formData.categoryId === category.categoryId &&
                        styles.categoryButtonTextSelected,
                    ]}
                  >
                    {category.name}
                  </Text>
                </TouchableOpacity>
              ))}
            </View>
          </View>

          <View style={styles.formGroup}>
            <Text style={styles.label}>Item Name *</Text>
            <TextInput
              style={styles.input}
              value={formData.name}
              onChangeText={(text) => setFormData({ ...formData, name: text })}
              placeholder="Enter menu item name"
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
            <Text style={styles.label}>Price *</Text>
            <View style={styles.priceContainer}>
              <Text style={styles.currencySymbol}>$</Text>
              <TextInput
                style={[styles.input, styles.priceInput]}
                value={formData.price}
                onChangeText={(text) => {
                  const cleaned = text.replace(/[^0-9.]/g, "");
                  if (cleaned.split(".").length <= 2) {
                    setFormData({ ...formData, price: cleaned });
                  }
                }}
                placeholder="0.00"
                keyboardType="decimal-pad"
              />
            </View>
          </View>

          <View style={styles.formGroup}>
            <Text style={styles.label}>Item Image</Text>
            {formData.imageUrl ? (
              <View style={styles.imagePreviewRow}>
                <Image
                  source={{ uri: getMenuImageDisplayUrl(formData.imageUrl) }}
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
                <Text style={styles.saveButtonText}>Create Menu Item</Text>
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
  scrollContent: { paddingBottom: 280 },
  centerContainer: {
    flex: 1,
    justifyContent: "center",
    alignItems: "center",
    backgroundColor: "#f5f5f5",
  },
  loadingText: { marginTop: 16, fontSize: 16, color: "#666" },
  header: {
    padding: 16,
    paddingTop: 60,
    flexDirection: "row",
    alignItems: "center",
    justifyContent: "space-between",
  },
  backButton: { padding: 8 },
  headerTitle: {
    fontSize: 20,
    fontWeight: "bold",
    color: "#fff",
    flex: 1,
    textAlign: "center",
  },
  placeholder: { width: 40 },
  form: { padding: 16 },
  formGroup: { marginBottom: 20 },
  label: { fontSize: 14, fontWeight: "600", color: "#333", marginBottom: 8 },
  categoryContainer: { flexDirection: "row", flexWrap: "wrap", gap: 8 },
  categoryButton: {
    paddingHorizontal: 16,
    paddingVertical: 10,
    borderRadius: 20,
    backgroundColor: "#fff",
    borderWidth: 2,
    borderColor: "#e0e0e0",
  },
  categoryButtonSelected: {
    backgroundColor: "#6200ee",
    borderColor: "#6200ee",
  },
  categoryButtonText: { fontSize: 14, color: "#333", fontWeight: "500" },
  categoryButtonTextSelected: { color: "#fff" },
  input: {
    backgroundColor: "#fff",
    borderWidth: 1,
    borderColor: "#e0e0e0",
    borderRadius: 8,
    padding: 12,
    fontSize: 16,
    color: "#333",
  },
  textArea: { height: 100, textAlignVertical: "top" },
  priceContainer: {
    flexDirection: "row",
    alignItems: "center",
    backgroundColor: "#fff",
    borderWidth: 1,
    borderColor: "#e0e0e0",
    borderRadius: 8,
    paddingLeft: 12,
  },
  currencySymbol: {
    fontSize: 16,
    fontWeight: "600",
    color: "#333",
    marginRight: 8,
  },
  priceInput: { flex: 1, borderWidth: 0, paddingLeft: 0 },
  saveButton: {
    backgroundColor: "#6200ee",
    padding: 16,
    borderRadius: 8,
    flexDirection: "row",
    alignItems: "center",
    justifyContent: "center",
    marginTop: 8,
  },
  saveButtonDisabled: { opacity: 0.7 },
  saveButtonText: { color: "#fff", fontSize: 16, fontWeight: "600" },
  imagePreviewRow: {
    flexDirection: "row",
    alignItems: "center",
    gap: 12,
  },
  imagePreview: {
    width: 80,
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
