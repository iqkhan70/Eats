import React, { useEffect, useMemo, useState, useCallback } from "react";
import {
  View,
  Text,
  StyleSheet,
  ScrollView,
  TouchableOpacity,
  Alert,
  ActivityIndicator,
  TextInput,
  RefreshControl,
} from "react-native";
import { Ionicons } from "@expo/vector-icons";
import { useRouter } from "expo-router";
import { authService } from "../../services/auth";
import { api } from "../../services/api";

interface Category {
  categoryId: string;
  name: string;
  description?: string | null;
  imageUrl?: string | null;
  displayOrder: number;
  createdAt: string;
  menuItemCount: number;
}

export default function AdminCategoriesScreen() {
  const router = useRouter();
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [categories, setCategories] = useState<Category[]>([]);
  const [creating, setCreating] = useState(false);
  const [editingCategoryId, setEditingCategoryId] = useState<string | null>(null);

  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [imageUrl, setImageUrl] = useState("");
  const [displayOrder, setDisplayOrder] = useState("0");

  const sortedCategories = useMemo(() => {
    return [...categories].sort((a, b) => {
      if (a.displayOrder !== b.displayOrder) return a.displayOrder - b.displayOrder;
      return a.name.localeCompare(b.name);
    });
  }, [categories]);

  const load = useCallback(async () => {
    try {
      const res = await api.get("/MobileBff/admin/categories");
      const data = (res.data ?? []) as Category[];
      setCategories(Array.isArray(data) ? data : []);

      if (displayOrder === "0" && Array.isArray(data) && data.length > 0) {
        const max = Math.max(...data.map((c) => c.displayOrder ?? 0));
        setDisplayOrder(String(max + 1));
      }
    } catch (e: any) {
      setCategories([]);
      Alert.alert("Error", "Failed to load categories.");
    }
  }, [displayOrder]);

  useEffect(() => {
    (async () => {
      const admin = await authService.isAdmin();
      if (!admin) {
        Alert.alert("Access Denied", "You must be an admin to access this page.");
        router.back();
        return;
      }

      await load();
      setLoading(false);
    })();
  }, [load, router]);

  const onRefresh = useCallback(async () => {
    setRefreshing(true);
    await load();
    setRefreshing(false);
  }, [load]);

  const createCategory = useCallback(async () => {
    const trimmedName = name.trim();
    if (!trimmedName) {
      Alert.alert("Validation", "Name is required.");
      return;
    }

    const parsedOrder = Number(displayOrder);
    const order = Number.isFinite(parsedOrder) ? parsedOrder : 0;

    setCreating(true);
    try {
      if (editingCategoryId) {
        await api.put(`/MobileBff/admin/categories/${editingCategoryId}`, {
          name: trimmedName,
          description: description ?? "",
          imageUrl: imageUrl ?? "",
          displayOrder: order,
        });
      } else {
        await api.post("/MobileBff/admin/categories", {
          name: trimmedName,
          description: description.trim() ? description.trim() : null,
          imageUrl: imageUrl.trim() ? imageUrl.trim() : null,
          displayOrder: order,
        });
      }

      Alert.alert("Success", editingCategoryId ? "Category updated." : "Category created.");
      setName("");
      setDescription("");
      setImageUrl("");
      setDisplayOrder(String(order + 1));
      setEditingCategoryId(null);
      await load();
    } catch (e: any) {
      const message =
        e?.response?.data?.message ||
        e?.response?.data?.error ||
        "Failed to create category.";
      Alert.alert("Error", String(message));
    } finally {
      setCreating(false);
    }
  }, [name, description, imageUrl, displayOrder, load, editingCategoryId]);

  const startEdit = useCallback((c: Category) => {
    setEditingCategoryId(c.categoryId);
    setName(c.name ?? "");
    setDescription(c.description ?? "");
    setImageUrl(c.imageUrl ?? "");
    setDisplayOrder(String(c.displayOrder ?? 0));
  }, []);

  const cancelEdit = useCallback(() => {
    setEditingCategoryId(null);
    setName("");
    setDescription("");
    setImageUrl("");
    setDisplayOrder("0");
  }, []);

  const deleteCategory = useCallback(
    (c: Category) => {
      Alert.alert(
        "Confirm Delete",
        `Delete category '${c.name}'?`,
        [
          { text: "Cancel", style: "cancel" },
          {
            text: "Delete",
            style: "destructive",
            onPress: async () => {
              try {
                await api.delete(`/MobileBff/admin/categories/${c.categoryId}`);
                if (editingCategoryId === c.categoryId) cancelEdit();
                await load();
                Alert.alert("Deleted", "Category deleted.");
              } catch (e: any) {
                const message =
                  e?.response?.data?.message ||
                  e?.response?.data?.error ||
                  "Failed to delete category.";
                Alert.alert("Error", String(message));
              }
            },
          },
        ],
        { cancelable: true }
      );
    },
    [cancelEdit, editingCategoryId, load]
  );

  if (loading) {
    return (
      <View style={styles.centerContainer}>
        <ActivityIndicator size="large" color="#0097a7" />
        <Text style={styles.loadingText}>Loading...</Text>
      </View>
    );
  }

  return (
    <ScrollView
      style={styles.container}
      refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} />}
      keyboardShouldPersistTaps="handled"
    >
      <View style={styles.header}>
        <TouchableOpacity onPress={() => router.back()} style={styles.backButton}>
          <Ionicons name="chevron-back" size={28} color="#fff" />
        </TouchableOpacity>
        <Text style={styles.headerTitle}>Categories</Text>
        <View style={styles.placeholder} />
      </View>

      <View style={styles.content}>
        <View style={styles.card}>
          <Text style={styles.cardTitle}>Add Category</Text>
          {editingCategoryId ? (
            <Text style={styles.mutedText}>Editing mode</Text>
          ) : null}

          <Text style={styles.label}>Name</Text>
          <TextInput
            style={styles.input}
            placeholder="e.g. Grocery"
            value={name}
            onChangeText={setName}
            autoCapitalize="words"
          />

          <Text style={styles.label}>Description (optional)</Text>
          <TextInput
            style={styles.input}
            placeholder="Short description"
            value={description}
            onChangeText={setDescription}
          />

          <Text style={styles.label}>Image URL (optional)</Text>
          <TextInput
            style={styles.input}
            placeholder="https://..."
            value={imageUrl}
            onChangeText={setImageUrl}
            autoCapitalize="none"
          />

          <Text style={styles.label}>Display Order</Text>
          <TextInput
            style={styles.input}
            placeholder="0"
            value={displayOrder}
            onChangeText={setDisplayOrder}
            keyboardType="number-pad"
          />

          <TouchableOpacity
            style={[styles.primaryButton, creating ? styles.buttonDisabled : null]}
            onPress={createCategory}
            disabled={creating}
          >
            {creating ? (
              <ActivityIndicator color="#fff" />
            ) : (
              <>
                <Ionicons name={editingCategoryId ? "save" : "add"} size={18} color="#fff" />
                <Text style={styles.primaryButtonText}>
                  {editingCategoryId ? "Save" : "Create"}
                </Text>
              </>
            )}
          </TouchableOpacity>

          {editingCategoryId ? (
            <TouchableOpacity style={styles.secondaryButton} onPress={cancelEdit} disabled={creating}>
              <Text style={styles.secondaryButtonText}>Cancel Edit</Text>
            </TouchableOpacity>
          ) : null}
        </View>

        <View style={styles.card}>
          <View style={styles.cardHeaderRow}>
            <Text style={styles.cardTitle}>Existing Categories</Text>
            <Text style={styles.countText}>{sortedCategories.length}</Text>
          </View>

          {sortedCategories.length === 0 ? (
            <Text style={styles.mutedText}>No categories found.</Text>
          ) : (
            sortedCategories.map((c) => (
              <View key={c.categoryId} style={styles.row}>
                <View style={styles.rowLeft}>
                  <Text style={styles.rowTitle}>{c.name}</Text>
                  <Text style={styles.rowSub}>
                    Order: {c.displayOrder} • Items: {c.menuItemCount}
                  </Text>
                </View>
                <View style={styles.rowActions}>
                  <TouchableOpacity style={styles.iconButton} onPress={() => startEdit(c)}>
                    <Ionicons name="create-outline" size={18} color="#0097a7" />
                  </TouchableOpacity>
                  <TouchableOpacity style={styles.iconButton} onPress={() => deleteCategory(c)}>
                    <Ionicons name="trash-outline" size={18} color="#d32f2f" />
                  </TouchableOpacity>
                </View>
              </View>
            ))
          )}
        </View>
      </View>
    </ScrollView>
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
    backgroundColor: "#f5f5f5",
  },
  loadingText: {
    marginTop: 16,
    fontSize: 16,
    color: "#666",
  },
  header: {
    backgroundColor: "#0097a7",
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
  content: {
    padding: 16,
    gap: 16,
  },
  card: {
    backgroundColor: "#fff",
    borderRadius: 12,
    padding: 16,
    shadowColor: "#000",
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.08,
    shadowRadius: 4,
    elevation: 2,
  },
  cardHeaderRow: {
    flexDirection: "row",
    justifyContent: "space-between",
    alignItems: "center",
    marginBottom: 8,
  },
  cardTitle: {
    fontSize: 18,
    fontWeight: "700",
    color: "#333",
  },
  countText: {
    fontSize: 14,
    color: "#666",
  },
  label: {
    marginTop: 12,
    marginBottom: 6,
    fontSize: 12,
    color: "#666",
  },
  input: {
    backgroundColor: "#fafafa",
    borderWidth: 1,
    borderColor: "#e0e0e0",
    borderRadius: 10,
    paddingHorizontal: 12,
    paddingVertical: 10,
    fontSize: 14,
    color: "#333",
  },
  primaryButton: {
    marginTop: 16,
    backgroundColor: "#0097a7",
    borderRadius: 10,
    paddingVertical: 12,
    flexDirection: "row",
    justifyContent: "center",
    alignItems: "center",
    gap: 8,
  },
  primaryButtonText: {
    color: "#fff",
    fontWeight: "700",
    fontSize: 15,
  },
  buttonDisabled: {
    opacity: 0.7,
  },
  mutedText: {
    marginTop: 8,
    color: "#777",
  },
  row: {
    paddingVertical: 12,
    borderTopWidth: 1,
    borderTopColor: "#f0f0f0",
    flexDirection: "row",
    justifyContent: "space-between",
    alignItems: "center",
  },
  rowLeft: {
    gap: 4,
    flexShrink: 1,
  },
  rowActions: {
    flexDirection: "row",
    gap: 10,
    marginLeft: 12,
    alignItems: "center",
  },
  iconButton: {
    width: 34,
    height: 34,
    borderRadius: 10,
    backgroundColor: "#f6f6f6",
    justifyContent: "center",
    alignItems: "center",
  },
  rowTitle: {
    fontSize: 15,
    fontWeight: "700",
    color: "#333",
  },
  rowSub: {
    fontSize: 12,
    color: "#666",
  },
  secondaryButton: {
    marginTop: 10,
    backgroundColor: "#f2f2f2",
    borderRadius: 10,
    paddingVertical: 12,
    alignItems: "center",
  },
  secondaryButtonText: {
    color: "#333",
    fontWeight: "700",
    fontSize: 14,
  },
});

