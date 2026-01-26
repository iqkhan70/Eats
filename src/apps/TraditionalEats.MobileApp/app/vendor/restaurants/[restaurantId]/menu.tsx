import React, { useState, useEffect } from 'react';
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
  const params = useLocalSearchParams<{ restaurantId: string }>();
  const restaurantId = params.restaurantId;

  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [menuItems, setMenuItems] = useState<MenuItem[]>([]);
  const [restaurantName, setRestaurantName] = useState('');

  useEffect(() => {
    if (restaurantId) {
      loadMenuItems();
    }
  }, [restaurantId]);

  const loadMenuItems = async () => {
    try {
      setLoading(true);
      const response = await api.get<MenuItem[]>(`/MobileBff/restaurants/${restaurantId}/menu`);
      setMenuItems(response.data || []);
      
      // Try to get restaurant name from vendor restaurants list
      try {
        const restaurantsResponse = await api.get<any[]>('/MobileBff/vendor/my-restaurants');
        const restaurant = restaurantsResponse.data?.find(r => r.restaurantId === restaurantId);
        if (restaurant) {
          setRestaurantName(restaurant.name);
        }
      } catch (e) {
        // Ignore error, just use restaurant ID
      }
    } catch (error: any) {
      console.error('Error loading menu items:', error);
      if (error.response?.status === 404) {
        setMenuItems([]);
      } else {
        Alert.alert('Error', 'Failed to load menu items');
      }
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  };

  const onRefresh = async () => {
    setRefreshing(true);
    await loadMenuItems();
  };

  const handleToggleAvailability = async (item: MenuItem) => {
    try {
      await api.patch(`/MobileBff/menu-items/${item.menuItemId}/availability`, {
        isAvailable: !item.isAvailable,
      });
      Alert.alert('Success', `Menu item ${!item.isAvailable ? 'enabled' : 'disabled'} successfully`);
      await loadMenuItems();
    } catch (error: any) {
      console.error('Error toggling availability:', error);
      Alert.alert('Error', 'Failed to update menu item availability');
    }
  };

  const handleEditItem = (item: MenuItem) => {
    router.push(`/vendor/restaurants/${restaurantId}/menu-items/${item.menuItemId}/edit`);
  };

  const handleAddItem = () => {
    router.push(`/vendor/restaurants/${restaurantId}/menu-items/new`);
  };

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

      {menuItems.length === 0 ? (
        <View style={styles.emptyContainer}>
          <Ionicons name="restaurant-outline" size={64} color="#ccc" />
          <Text style={styles.emptyText}>No menu items yet</Text>
          <Text style={styles.emptySubtext}>Add your first menu item to get started</Text>
          <TouchableOpacity style={styles.addButtonLarge} onPress={handleAddItem}>
            <Text style={styles.addButtonText}>Add Menu Item</Text>
          </TouchableOpacity>
        </View>
      ) : (
        <View style={styles.menuList}>
          {menuItems.map((item) => (
            <View key={item.menuItemId} style={styles.menuItemCard}>
              <View style={styles.menuItemHeader}>
                <View style={styles.menuItemInfo}>
                  <Text style={styles.menuItemName}>{item.name}</Text>
                  <Text style={styles.menuItemPrice}>${item.price.toFixed(2)}</Text>
                </View>
                <View style={[styles.availabilityBadge, item.isAvailable ? styles.availableBadge : styles.unavailableBadge]}>
                  <Text style={styles.availabilityText}>
                    {item.isAvailable ? 'Available' : 'Unavailable'}
                  </Text>
                </View>
              </View>
              
              {item.description && (
                <Text style={styles.menuItemDescription} numberOfLines={2}>
                  {item.description}
                </Text>
              )}
              
              {item.categoryName && (
                <Text style={styles.menuItemCategory}>{item.categoryName}</Text>
              )}

              <View style={styles.actionButtons}>
                <TouchableOpacity
                  style={[styles.actionButton, styles.toggleButton]}
                  onPress={() => handleToggleAvailability(item)}
                >
                  <Ionicons 
                    name={item.isAvailable ? "eye-off-outline" : "eye-outline"} 
                    size={18} 
                    color={item.isAvailable ? "#f57c00" : "#4caf50"} 
                  />
                  <Text style={[styles.toggleButtonText, { color: item.isAvailable ? "#f57c00" : "#4caf50" }]}>
                    {item.isAvailable ? 'Disable' : 'Enable'}
                  </Text>
                </TouchableOpacity>
                
                <TouchableOpacity
                  style={[styles.actionButton, styles.editButton]}
                  onPress={() => handleEditItem(item)}
                >
                  <Ionicons name="create-outline" size={18} color="#6200ee" />
                  <Text style={styles.editButtonText}>Edit</Text>
                </TouchableOpacity>
              </View>
            </View>
          ))}
        </View>
      )}

      <TouchableOpacity style={styles.fab} onPress={handleAddItem}>
        <Ionicons name="add" size={28} color="#fff" />
      </TouchableOpacity>
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#f5f5f5',
  },
  centerContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    backgroundColor: '#f5f5f5',
  },
  loadingText: {
    marginTop: 16,
    fontSize: 16,
    color: '#666',
  },
  header: {
    backgroundColor: '#6200ee',
    padding: 16,
    paddingTop: 60,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
  },
  backButton: {
    padding: 8,
  },
  headerTitle: {
    fontSize: 20,
    fontWeight: 'bold',
    color: '#fff',
    flex: 1,
    textAlign: 'center',
  },
  addButton: {
    padding: 8,
  },
  emptyContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    padding: 32,
    minHeight: 400,
  },
  emptyText: {
    fontSize: 20,
    fontWeight: 'bold',
    color: '#333',
    marginTop: 16,
  },
  emptySubtext: {
    fontSize: 14,
    color: '#666',
    marginTop: 8,
    textAlign: 'center',
  },
  addButtonLarge: {
    marginTop: 24,
    paddingHorizontal: 24,
    paddingVertical: 12,
    backgroundColor: '#6200ee',
    borderRadius: 8,
  },
  addButtonText: {
    color: '#fff',
    fontSize: 16,
    fontWeight: '600',
  },
  menuList: {
    padding: 16,
  },
  menuItemCard: {
    backgroundColor: '#fff',
    borderRadius: 12,
    padding: 16,
    marginBottom: 16,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.1,
    shadowRadius: 4,
    elevation: 3,
  },
  menuItemHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'flex-start',
    marginBottom: 8,
  },
  menuItemInfo: {
    flex: 1,
  },
  menuItemName: {
    fontSize: 18,
    fontWeight: 'bold',
    color: '#333',
    marginBottom: 4,
  },
  menuItemPrice: {
    fontSize: 16,
    fontWeight: '600',
    color: '#6200ee',
  },
  availabilityBadge: {
    paddingHorizontal: 8,
    paddingVertical: 4,
    borderRadius: 12,
  },
  availableBadge: {
    backgroundColor: '#e8f5e9',
  },
  unavailableBadge: {
    backgroundColor: '#ffebee',
  },
  availabilityText: {
    fontSize: 12,
    fontWeight: '600',
    color: '#333',
  },
  menuItemDescription: {
    fontSize: 14,
    color: '#666',
    marginBottom: 8,
  },
  menuItemCategory: {
    fontSize: 12,
    color: '#999',
    marginBottom: 12,
  },
  actionButtons: {
    flexDirection: 'row',
    gap: 8,
    marginTop: 8,
  },
  actionButton: {
    flex: 1,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    paddingVertical: 10,
    paddingHorizontal: 12,
    borderRadius: 8,
    gap: 4,
  },
  toggleButton: {
    backgroundColor: '#f5f5f5',
    borderWidth: 1,
    borderColor: '#e0e0e0',
  },
  toggleButtonText: {
    fontSize: 14,
    fontWeight: '600',
  },
  editButton: {
    backgroundColor: '#f3e5f5',
    borderWidth: 1,
    borderColor: '#6200ee',
  },
  editButtonText: {
    color: '#6200ee',
    fontSize: 14,
    fontWeight: '600',
  },
  fab: {
    position: 'absolute',
    right: 16,
    bottom: 16,
    width: 56,
    height: 56,
    borderRadius: 28,
    backgroundColor: '#6200ee',
    justifyContent: 'center',
    alignItems: 'center',
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 4 },
    shadowOpacity: 0.3,
    shadowRadius: 4,
    elevation: 5,
  },
});
