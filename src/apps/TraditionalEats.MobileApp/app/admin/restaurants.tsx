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
import { LinearGradient } from 'expo-linear-gradient';
import { Ionicons } from '@expo/vector-icons';
import { useRouter } from 'expo-router';
import { api } from '../../services/api';
import { authService } from '../../services/auth';

interface Restaurant {
  restaurantId: string;
  name: string;
  description?: string;
  cuisineType?: string;
  address?: string;
  phoneNumber?: string;
  email?: string;
  imageUrl?: string;
  isActive: boolean;
  ownerId?: string;
}

export default function AdminRestaurantsScreen() {
  const router = useRouter();
  const [restaurants, setRestaurants] = useState<Restaurant[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [isAdmin, setIsAdmin] = useState(false);

  useEffect(() => {
    checkAuthAndLoad();
  }, []);

  const checkAuthAndLoad = async () => {
    const admin = await authService.isAdmin();
    setIsAdmin(admin);
    
    if (!admin) {
      Alert.alert('Access Denied', 'You must be an admin to access this page.');
      router.back();
      return;
    }
    
    await loadRestaurants();
  };

  const loadRestaurants = async () => {
    try {
      setLoading(true);
      const response = await api.get<Restaurant[]>('/MobileBff/admin/restaurants?take=100');
      setRestaurants(response.data || []);
    } catch (error: any) {
      console.error('Error loading restaurants:', error);
      if (error.response?.status === 401 || error.response?.status === 403) {
        Alert.alert('Access Denied', 'You do not have permission to view all restaurants.');
        router.back();
      } else {
        Alert.alert('Error', 'Failed to load restaurants. Please try again.');
      }
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  };

  const onRefresh = async () => {
    setRefreshing(true);
    await loadRestaurants();
  };

  const handleToggleStatus = async (restaurant: Restaurant) => {
    try {
      await api.patch(`/MobileBff/admin/restaurants/${restaurant.restaurantId}/status`, {
        isActive: !restaurant.isActive,
      });
      Alert.alert('Success', `Restaurant ${!restaurant.isActive ? 'activated' : 'deactivated'} successfully`);
      await loadRestaurants();
    } catch (error: any) {
      console.error('Error toggling status:', error);
      Alert.alert('Error', 'Failed to update restaurant status');
    }
  };

  const handleDeleteRestaurant = (restaurant: Restaurant) => {
    Alert.alert(
      'Delete Restaurant',
      `Are you sure you want to permanently delete "${restaurant.name}"? This action cannot be undone.`,
      [
        { text: 'Cancel', style: 'cancel' },
        {
          text: 'Delete',
          style: 'destructive',
          onPress: async () => {
            try {
              await api.delete(`/MobileBff/admin/restaurants/${restaurant.restaurantId}`);
              Alert.alert('Success', 'Restaurant deleted successfully.');
              await loadRestaurants();
            } catch (error: any) {
              console.error('Error deleting restaurant:', error);
              Alert.alert('Error', 'Failed to delete restaurant. Please try again.');
            }
          },
        },
      ]
    );
  };

  if (loading && restaurants.length === 0) {
    return (
      <View style={styles.centerContainer}>
        <ActivityIndicator size="large" color="#6200ee" />
        <Text style={styles.loadingText}>Loading restaurants...</Text>
      </View>
    );
  }

  return (
    <ScrollView
      style={styles.container}
      refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} />}
    >
      <LinearGradient
        colors={['#f97316', '#eab308']}
        start={{ x: 0, y: 0 }}
        end={{ x: 1, y: 0 }}
        style={styles.header}
      >
        <TouchableOpacity onPress={() => router.back()} style={styles.backButton}>
          <Ionicons name="chevron-back" size={28} color="#fff" />
        </TouchableOpacity>
        <Text style={styles.headerTitle}>All Restaurants</Text>
        <View style={styles.placeholder} />
      </LinearGradient>

      <View style={styles.statsBar}>
        <View style={styles.statItem}>
          <Text style={styles.statValue}>{restaurants.length}</Text>
          <Text style={styles.statLabel}>Total</Text>
        </View>
        <View style={styles.statItem}>
          <Text style={styles.statValue}>{restaurants.filter(r => r.isActive).length}</Text>
          <Text style={styles.statLabel}>Active</Text>
        </View>
        <View style={styles.statItem}>
          <Text style={styles.statValue}>{restaurants.filter(r => !r.isActive).length}</Text>
          <Text style={styles.statLabel}>Inactive</Text>
        </View>
      </View>

      {restaurants.length === 0 ? (
        <View style={styles.emptyContainer}>
          <Ionicons name="restaurant-outline" size={64} color="#ccc" />
          <Text style={styles.emptyText}>No restaurants found</Text>
        </View>
      ) : (
        <View style={styles.restaurantsList}>
          {restaurants.map((restaurant) => (
            <View key={restaurant.restaurantId} style={styles.restaurantCard}>
              <View style={styles.restaurantHeader}>
                <View style={styles.restaurantInfo}>
                  <Text style={styles.restaurantName}>{restaurant.name}</Text>
                  {restaurant.cuisineType && (
                    <Text style={styles.cuisineType}>{restaurant.cuisineType}</Text>
                  )}
                </View>
                <View style={[styles.statusBadge, restaurant.isActive ? styles.activeBadge : styles.inactiveBadge]}>
                  <Text style={styles.statusText}>{restaurant.isActive ? 'Active' : 'Inactive'}</Text>
                </View>
              </View>
              
              {restaurant.description && (
                <Text style={styles.restaurantDescription} numberOfLines={2}>
                  {restaurant.description}
                </Text>
              )}
              
              {restaurant.address && (
                <View style={styles.addressRow}>
                  <Ionicons name="location-outline" size={16} color="#666" />
                  <Text style={styles.addressText} numberOfLines={1}>{restaurant.address}</Text>
                </View>
              )}

              <View style={styles.actionButtons}>
                <TouchableOpacity
                  style={[styles.actionButton, styles.toggleButton]}
                  onPress={() => handleToggleStatus(restaurant)}
                >
                  <Ionicons 
                    name={restaurant.isActive ? "eye-off-outline" : "eye-outline"} 
                    size={18} 
                    color={restaurant.isActive ? "#f57c00" : "#4caf50"} 
                  />
                  <Text style={[styles.toggleButtonText, { color: restaurant.isActive ? "#f57c00" : "#4caf50" }]}>
                    {restaurant.isActive ? 'Deactivate' : 'Activate'}
                  </Text>
                </TouchableOpacity>
                
                <TouchableOpacity
                  style={[styles.actionButton, styles.deleteButton]}
                  onPress={() => handleDeleteRestaurant(restaurant)}
                >
                  <Ionicons name="trash-outline" size={18} color="#d32f2f" />
                  <Text style={styles.deleteButtonText}>Delete</Text>
                </TouchableOpacity>
              </View>
            </View>
          ))}
        </View>
      )}
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
  placeholder: {
    width: 40,
  },
  statsBar: {
    flexDirection: 'row',
    backgroundColor: '#fff',
    padding: 16,
    marginTop: 8,
    justifyContent: 'space-around',
    borderBottomWidth: 1,
    borderBottomColor: '#e0e0e0',
  },
  statItem: {
    alignItems: 'center',
  },
  statValue: {
    fontSize: 24,
    fontWeight: 'bold',
    color: '#6200ee',
  },
  statLabel: {
    fontSize: 12,
    color: '#666',
    marginTop: 4,
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
  restaurantsList: {
    padding: 16,
  },
  restaurantCard: {
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
  restaurantHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'flex-start',
    marginBottom: 8,
  },
  restaurantInfo: {
    flex: 1,
  },
  restaurantName: {
    fontSize: 18,
    fontWeight: 'bold',
    color: '#333',
    marginBottom: 4,
  },
  cuisineType: {
    fontSize: 12,
    color: '#999',
  },
  statusBadge: {
    paddingHorizontal: 8,
    paddingVertical: 4,
    borderRadius: 12,
  },
  activeBadge: {
    backgroundColor: '#4caf50',
  },
  inactiveBadge: {
    backgroundColor: '#ccc',
  },
  statusText: {
    fontSize: 12,
    fontWeight: '600',
    color: '#fff',
  },
  restaurantDescription: {
    fontSize: 14,
    color: '#666',
    marginBottom: 8,
  },
  addressRow: {
    flexDirection: 'row',
    alignItems: 'center',
    marginBottom: 12,
    gap: 4,
  },
  addressText: {
    fontSize: 12,
    color: '#666',
    flex: 1,
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
  deleteButton: {
    backgroundColor: '#ffebee',
    borderWidth: 1,
    borderColor: '#d32f2f',
  },
  deleteButtonText: {
    color: '#d32f2f',
    fontSize: 14,
    fontWeight: '600',
  },
});
