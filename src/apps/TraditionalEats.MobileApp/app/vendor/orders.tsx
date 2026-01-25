import React, { useState, useEffect } from 'react';
import { View, Text, StyleSheet, ScrollView, TouchableOpacity, ActivityIndicator, RefreshControl, Alert } from 'react-native';
import { useRouter, useLocalSearchParams } from 'expo-router';
import { authService } from '../../services/auth';
import { api } from '../../services/api';

interface Restaurant {
  restaurantId: string;
  name: string;
  cuisineType: string;
  address: string;
  isActive: boolean;
}

interface OrderItem {
  orderItemId: string;
  name: string;
  quantity: number;
  totalPrice: number;
}

interface OrderStatusHistory {
  id: string;
  status: string;
  notes?: string;
  changedAt: string;
}

interface Order {
  orderId: string;
  customerId: string;
  restaurantId: string;
  status: string;
  createdAt: string;
  deliveryAddress?: string;
  total: number;
  items: OrderItem[];
  statusHistory?: OrderStatusHistory[];
}

const statusOptions = ['Pending', 'Preparing', 'Ready', 'Completed', 'Cancelled'];

const getStatusColor = (status: string): string => {
  switch (status) {
    case 'Pending':
      return '#FFA726';
    case 'Preparing':
      return '#42A5F5';
    case 'Ready':
      return '#66BB6A';
    case 'Completed':
      return '#78909C';
    case 'Cancelled':
      return '#EF5350';
    default:
      return '#78909C';
  }
};

export default function VendorOrdersScreen() {
  const router = useRouter();
  const params = useLocalSearchParams<{ restaurantId?: string }>();
  const [isAuthenticated, setIsAuthenticated] = useState(false);
  const [isVendor, setIsVendor] = useState(false);
  const [restaurants, setRestaurants] = useState<Restaurant[]>([]);
  const [orders, setOrders] = useState<Order[]>([]);
  const [selectedRestaurantId, setSelectedRestaurantId] = useState<string | null>(params.restaurantId || null);
  const [loadingRestaurants, setLoadingRestaurants] = useState(true);
  const [loadingOrders, setLoadingOrders] = useState(false);
  const [refreshing, setRefreshing] = useState(false);
  const [updatingStatus, setUpdatingStatus] = useState<string | null>(null);

  useEffect(() => {
    checkAuthAndLoad();
  }, []);

  const checkAuthAndLoad = async () => {
    const authenticated = await authService.isAuthenticated();
    setIsAuthenticated(authenticated);
    
    if (authenticated) {
      const vendor = await authService.isVendor();
      setIsVendor(vendor);
      
      if (vendor) {
        await loadRestaurants();
      }
    }
  };

  useEffect(() => {
    if (params.restaurantId && params.restaurantId !== selectedRestaurantId) {
      setSelectedRestaurantId(params.restaurantId);
    }
  }, [params.restaurantId]);

  useEffect(() => {
    if (isAuthenticated && isVendor) {
      loadRestaurants();
    }
  }, [isAuthenticated, isVendor]);

  useEffect(() => {
    if (restaurants.length > 0) {
      loadOrders();
    }
  }, [selectedRestaurantId, restaurants]);

  const loadRestaurants = async () => {
    try {
      setLoadingRestaurants(true);
      const response = await api.get('/MobileBff/vendor/my-restaurants');
      setRestaurants(response.data || []);
    } catch (error: any) {
      console.error('Error loading restaurants:', error);
    } finally {
      setLoadingRestaurants(false);
    }
  };

  const loadOrders = async () => {
    try {
      setLoadingOrders(true);
      let allOrders: Order[] = [];

      if (selectedRestaurantId) {
        const response = await api.get(`/MobileBff/vendor/restaurants/${selectedRestaurantId}/orders`);
        allOrders = response.data || [];
      } else {
        // Load orders for all restaurants
        for (const restaurant of restaurants) {
          try {
            const response = await api.get(`/MobileBff/vendor/restaurants/${restaurant.restaurantId}/orders`);
            if (response.data) {
              allOrders.push(...response.data);
            }
          } catch (error) {
            console.error(`Error loading orders for restaurant ${restaurant.restaurantId}:`, error);
          }
        }
      }

      // Sort by creation date (newest first)
      allOrders.sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime());
      setOrders(allOrders);
    } catch (error: any) {
      console.error('Error loading orders:', error);
    } finally {
      setLoadingOrders(false);
    }
  };

  const onRefresh = async () => {
    setRefreshing(true);
    await Promise.all([loadRestaurants(), loadOrders()]);
    setRefreshing(false);
  };

  const updateOrderStatus = async (orderId: string, newStatus: string) => {
    try {
      setUpdatingStatus(orderId);
      await api.put(`/MobileBff/orders/${orderId}/status`, {
        status: newStatus,
        notes: null
      });
      
      // Reload orders to get updated status
      await loadOrders();
    } catch (error: any) {
      console.error('Error updating order status:', error);
      alert('Failed to update order status');
    } finally {
      setUpdatingStatus(null);
    }
  };

  const showStatusPicker = (order: Order) => {
    // Simple implementation - in production, use a proper picker component
    const currentIndex = statusOptions.indexOf(order.status);
    const nextIndex = (currentIndex + 1) % statusOptions.length;
    const nextStatus = statusOptions[nextIndex];
    updateOrderStatus(order.orderId, nextStatus);
  };

  if (!isAuthenticated || !isVendor) {
    return (
      <View style={styles.container}>
        <Text style={styles.errorText}>You must be a vendor to access this page.</Text>
      </View>
    );
  }

  if (loadingRestaurants) {
    return (
      <View style={styles.container}>
        <ActivityIndicator size="large" color="#007AFF" />
        <Text style={styles.loadingText}>Loading restaurants...</Text>
      </View>
    );
  }

  if (restaurants.length === 0) {
    return (
      <View style={styles.container}>
        <Text style={styles.errorText}>You don't have any restaurants yet.</Text>
      </View>
    );
  }

  return (
    <View style={styles.container}>
      <View style={styles.header}>
        <TouchableOpacity onPress={() => router.back()} style={styles.backButton}>
          <Text style={styles.backButtonText}>← Back</Text>
        </TouchableOpacity>
        <Text style={styles.title}>Vendor Orders</Text>
      </View>

      <ScrollView
        style={styles.scrollView}
        refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} />}
      >
        <View style={styles.filterContainer}>
          <Text style={styles.filterLabel}>Filter by Restaurant:</Text>
          <ScrollView horizontal showsHorizontalScrollIndicator={false} style={styles.restaurantFilter}>
            <TouchableOpacity
              style={[styles.filterChip, !selectedRestaurantId && styles.filterChipActive]}
              onPress={() => setSelectedRestaurantId(null)}
            >
              <Text style={[styles.filterChipText, !selectedRestaurantId && styles.filterChipTextActive]}>
                All
              </Text>
            </TouchableOpacity>
            {restaurants.map((restaurant) => (
              <TouchableOpacity
                key={restaurant.restaurantId}
                style={[
                  styles.filterChip,
                  selectedRestaurantId === restaurant.restaurantId && styles.filterChipActive
                ]}
                onPress={() => setSelectedRestaurantId(restaurant.restaurantId)}
              >
                <Text
                  style={[
                    styles.filterChipText,
                    selectedRestaurantId === restaurant.restaurantId && styles.filterChipTextActive
                  ]}
                >
                  {restaurant.name}
                </Text>
              </TouchableOpacity>
            ))}
          </ScrollView>
        </View>

        {loadingOrders ? (
          <View style={styles.loadingContainer}>
            <ActivityIndicator size="large" color="#007AFF" />
            <Text style={styles.loadingText}>Loading orders...</Text>
          </View>
        ) : orders.length === 0 ? (
          <View style={styles.emptyContainer}>
            <Text style={styles.emptyText}>No orders found</Text>
          </View>
        ) : (
          orders.map((order) => (
            <TouchableOpacity
              key={order.orderId}
              style={styles.orderCard}
              onPress={() => router.push(`/orders/${order.orderId}`)}
            >
              <View style={styles.orderHeader}>
                <View>
                  <Text style={styles.orderId}>Order #{order.orderId.substring(0, 8)}</Text>
                  <Text style={styles.orderDate}>
                    {new Date(order.createdAt).toLocaleString()}
                  </Text>
                </View>
                <View
                  style={[styles.statusBadge, { backgroundColor: getStatusColor(order.status) }]}
                >
                  <Text style={styles.statusText}>{order.status}</Text>
                </View>
              </View>

              <View style={styles.orderItems}>
                {order.items.map((item) => (
                  <Text key={item.orderItemId} style={styles.orderItem}>
                    {item.quantity}x {item.name} - ${item.totalPrice.toFixed(2)}
                  </Text>
                ))}
              </View>

              {order.deliveryAddress && (
                <Text style={styles.deliveryAddress}>📍 {order.deliveryAddress}</Text>
              )}

              <View style={styles.orderFooter}>
                <Text style={styles.orderTotal}>Total: ${order.total.toFixed(2)}</Text>
                <TouchableOpacity
                  style={[
                    styles.statusButton,
                    { backgroundColor: getStatusColor(order.status) },
                    updatingStatus === order.orderId && styles.statusButtonDisabled
                  ]}
                  onPress={() => showStatusPicker(order)}
                  disabled={updatingStatus === order.orderId}
                >
                  {updatingStatus === order.orderId ? (
                    <ActivityIndicator size="small" color="white" />
                  ) : (
                    <Text style={styles.statusButtonText}>Update Status</Text>
                  )}
                </TouchableOpacity>
              </View>
            </TouchableOpacity>
          ))
        )}
      </ScrollView>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#F5F5F5',
  },
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    padding: 16,
    backgroundColor: 'white',
    borderBottomWidth: 1,
    borderBottomColor: '#E0E0E0',
  },
  backButton: {
    marginRight: 16,
  },
  backButtonText: {
    fontSize: 16,
    color: '#007AFF',
  },
  title: {
    fontSize: 20,
    fontWeight: 'bold',
  },
  scrollView: {
    flex: 1,
  },
  filterContainer: {
    backgroundColor: 'white',
    padding: 16,
    borderBottomWidth: 1,
    borderBottomColor: '#E0E0E0',
  },
  filterLabel: {
    fontSize: 14,
    fontWeight: '600',
    marginBottom: 8,
    color: '#333',
  },
  restaurantFilter: {
    flexDirection: 'row',
  },
  filterChip: {
    paddingHorizontal: 16,
    paddingVertical: 8,
    borderRadius: 20,
    backgroundColor: '#F0F0F0',
    marginRight: 8,
  },
  filterChipActive: {
    backgroundColor: '#007AFF',
  },
  filterChipText: {
    fontSize: 14,
    color: '#333',
  },
  filterChipTextActive: {
    color: 'white',
  },
  loadingContainer: {
    padding: 32,
    alignItems: 'center',
  },
  loadingText: {
    marginTop: 8,
    color: '#666',
  },
  emptyContainer: {
    padding: 32,
    alignItems: 'center',
  },
  emptyText: {
    fontSize: 16,
    color: '#666',
  },
  orderCard: {
    backgroundColor: 'white',
    margin: 16,
    padding: 16,
    borderRadius: 8,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.1,
    shadowRadius: 4,
    elevation: 3,
  },
  orderHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'flex-start',
    marginBottom: 12,
  },
  orderId: {
    fontSize: 16,
    fontWeight: 'bold',
    color: '#333',
  },
  orderDate: {
    fontSize: 12,
    color: '#666',
    marginTop: 4,
  },
  statusBadge: {
    paddingHorizontal: 12,
    paddingVertical: 6,
    borderRadius: 16,
  },
  statusText: {
    color: 'white',
    fontSize: 12,
    fontWeight: '600',
  },
  orderItems: {
    marginBottom: 12,
  },
  orderItem: {
    fontSize: 14,
    color: '#333',
    marginBottom: 4,
  },
  deliveryAddress: {
    fontSize: 12,
    color: '#666',
    marginBottom: 12,
  },
  orderFooter: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingTop: 12,
    borderTopWidth: 1,
    borderTopColor: '#E0E0E0',
  },
  orderTotal: {
    fontSize: 16,
    fontWeight: 'bold',
    color: '#333',
  },
  statusButton: {
    paddingHorizontal: 16,
    paddingVertical: 8,
    borderRadius: 8,
  },
  statusButtonDisabled: {
    opacity: 0.6,
  },
  statusButtonText: {
    color: 'white',
    fontSize: 12,
    fontWeight: '600',
  },
  errorText: {
    fontSize: 16,
    color: '#666',
    textAlign: 'center',
    marginTop: 32,
  },
});
