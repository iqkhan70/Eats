import React, { useState, useEffect, useCallback } from 'react';
import {
  View,
  Text,
  StyleSheet,
  ScrollView,
  TouchableOpacity,
  Alert,
  ActivityIndicator,
  RefreshControl,
} from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useRouter, useLocalSearchParams } from 'expo-router';
import { api } from '../../../../services/api';

interface MenuItem {
  menuItemId: string;
  name: string;
  description?: string;
  price: number;
  imageUrl?: string;
  categoryId?: string;
  categoryName?: string;
  isAvailable: boolean;
}

export default function ManageMenuScreen() {
  const router = useRouter();

  // ✅ READ refreshedAt
  const params = useLocalSearchParams<{
    restaurantId?: string;
    refreshedAt?: string;
  }>();

  const restaurantId = params.restaurantId as string;
  const refreshedAt = params.refreshedAt;

  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [menuItems, setMenuItems] = useState<MenuItem[]>([]);
  const [restaurantName, setRestaurantName] = useState('');

  // -----------------------------
  // Helpers
  // -----------------------------
  const normalizeCategoryName = (raw: any): string | undefined => {
    if (typeof raw === 'string') return raw.trim() || undefined;
    if (typeof raw === 'number') return String(raw);
    if (raw && typeof raw === 'object') {
      return raw.name || raw.categoryName || undefined;
    }
    return undefined;
  };

  // -----------------------------
  // Data load
  // -----------------------------
  const loadMenuItems = useCallback(async () => {
    if (!restaurantId) return;

    try {
      if (!refreshing) setLoading(true);

      const response = await api.get<any[]>(
        `/MobileBff/restaurants/${restaurantId}/menu`,
        {
          params: { __ts: Date.now() },
          headers: { 'Cache-Control': 'no-store', Pragma: 'no-cache' },
        }
      );

      const mapped: MenuItem[] = (response.data || []).map((x: any) => ({
        menuItemId: x.menuItemId || x.id,
        name: x.name ?? '',
        description: x.description ?? undefined,
        price: typeof x.price === 'number' ? x.price : parseFloat(x.price) || 0,
        imageUrl: x.imageUrl ?? undefined,
        categoryId: x.categoryId ?? undefined,
        categoryName: normalizeCategoryName(x.categoryName ?? x.category),
        isAvailable: x.isAvailable ?? true,
      }));

      setMenuItems(mapped);

      // restaurant name (optional)
      try {
        const r = await api.get<any[]>('/MobileBff/vendor/my-restaurants', {
          params: { __ts: Date.now() },
        });
        const found = r.data?.find((x) => x.restaurantId === restaurantId);
        if (found?.name) setRestaurantName(found.name);
      } catch {}
    } catch (err) {
      console.error('Error loading menu items:', err);
      Alert.alert('Error', 'Failed to load menu items');
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  }, [restaurantId, refreshing]);

  // -----------------------------
  // Initial load
  // -----------------------------
  useEffect(() => {
    if (!restaurantId) return;
    loadMenuItems();
  }, [restaurantId, loadMenuItems]);

  // -----------------------------
  // 🔥 REFRESH TRIGGER FROM new.tsx
  // -----------------------------
  useEffect(() => {
    if (!restaurantId) return;
    if (!refreshedAt) return;

    console.log('🔄 Menu refresh triggered:', refreshedAt);
    loadMenuItems();
  }, [refreshedAt, restaurantId, loadMenuItems]);

  // -----------------------------
  // UI handlers
  // -----------------------------
  const onRefresh = async () => {
    setRefreshing(true);
    await loadMenuItems();
  };

  const handleAddItem = () => {
    router.push({
      pathname: '/vendor/restaurants/[restaurantId]/menu-items/new',
      params: { restaurantId },
    });
  };

  const handleEditItem = (item: MenuItem) => {
    router.push({
      pathname: '/vendor/restaurants/[restaurantId]/menu-items/[menuItemId]/edit',
      params: { restaurantId, menuItemId: item.menuItemId },
    });
  };

  // -----------------------------
  // Render
  // -----------------------------
  if (loading && menuItems.length === 0) {
    return (
      <View style={styles.centerContainer}>
        <ActivityIndicator size="large" color="#6200ee" />
        <Text style={styles.loadingText}>Loading menu items...</Text>
      </View>
    );
  }

  return (
    <ScrollView
      style={styles.container}
      refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} />}
    >
      <View style={styles.header}>
        <TouchableOpacity onPress={() => router.back()} style={styles.backButton}>
          <Ionicons name="arrow-back" size={24} color="#fff" />
        </TouchableOpacity>

        <Text style={styles.headerTitle} numberOfLines={1}>
          {restaurantName || 'Menu Items'}
        </Text>

        <TouchableOpacity onPress={handleAddItem} style={styles.addButton}>
          <Ionicons name="add" size={24} color="#fff" />
        </TouchableOpacity>
      </View>

      <View style={styles.menuList}>
        {menuItems.map((item) => (
          <View key={item.menuItemId} style={styles.menuItemCard}>
            <Text style={styles.menuItemName}>{item.name}</Text>
            <Text style={styles.menuItemPrice}>${item.price.toFixed(2)}</Text>
            {item.categoryName && <Text style={styles.menuItemCategory}>{item.categoryName}</Text>}

            <View style={styles.actionButtons}>
              <TouchableOpacity style={styles.editButton} onPress={() => handleEditItem(item)}>
                <Ionicons name="create-outline" size={18} color="#6200ee" />
                <Text style={styles.editButtonText}>Edit</Text>
              </TouchableOpacity>
            </View>
          </View>
        ))}
      </View>
    </ScrollView>
  );
}

// styles unchanged
const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: '#f5f5f5' },
  centerContainer: { flex: 1, justifyContent: 'center', alignItems: 'center' },
  loadingText: { marginTop: 16, color: '#666' },
  header: {
    backgroundColor: '#6200ee',
    padding: 16,
    paddingTop: 60,
    flexDirection: 'row',
    alignItems: 'center',
  },
  backButton: { padding: 8 },
  headerTitle: { flex: 1, textAlign: 'center', color: '#fff', fontSize: 20, fontWeight: 'bold' },
  addButton: { padding: 8 },
  menuList: { padding: 16 },
  menuItemCard: { backgroundColor: '#fff', padding: 16, borderRadius: 12, marginBottom: 12 },
  menuItemName: { fontSize: 18, fontWeight: 'bold' },
  menuItemPrice: { fontSize: 16, color: '#6200ee' },
  menuItemCategory: { fontSize: 12, color: '#999' },
  actionButtons: { marginTop: 8 },
  editButton: { flexDirection: 'row', alignItems: 'center' },
  editButtonText: { marginLeft: 6, color: '#6200ee' },
});
