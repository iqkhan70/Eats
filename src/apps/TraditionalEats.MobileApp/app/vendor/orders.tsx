import React, { useEffect, useMemo, useRef, useState } from 'react';
import {
  View,
  Text,
  StyleSheet,
  ScrollView,
  TouchableOpacity,
  ActivityIndicator,
  RefreshControl,
  Alert,
} from 'react-native';
import { useRouter, useLocalSearchParams } from 'expo-router';
import axios from 'axios';
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
  specialInstructions?: string;
  total: number;
  items: OrderItem[];
  statusHistory?: OrderStatusHistory[];
}

const statusOptions = ['Pending', 'Preparing', 'Ready', 'Completed', 'Cancelled'] as const;
type OrderStatus = (typeof statusOptions)[number];

const allowedNextStatuses: Record<OrderStatus, OrderStatus[]> = {
  Pending: ['Preparing', 'Cancelled'],
  Preparing: ['Ready', 'Cancelled'],
  Ready: ['Completed', 'Cancelled'],
  Completed: [],
  Cancelled: [],
};

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

function safeJsonParse(input: unknown) {
  if (typeof input !== 'string') return input;
  try {
    return JSON.parse(input);
  } catch {
    return input;
  }
}

/** Pretty-print for RN console (avoids truncation) */
function logJson(label: string, obj: any) {
  try {
    console.log(label + ':\n' + JSON.stringify(obj, null, 2));
  } catch {
    console.log(label + ':', obj);
  }
}

function extractAspNetValidationErrors(data: any): string | null {
  const errors = data?.errors;
  if (!errors || typeof errors !== 'object') return null;

  const lines: string[] = [];
  for (const key of Object.keys(errors)) {
    const arr = errors[key];
    if (Array.isArray(arr)) {
      for (const msg of arr) lines.push(`${key}: ${msg}`);
    }
  }
  return lines.length ? lines.join('\n') : null;
}

function normalizeErrorMessage(err: unknown): string {
  if (axios.isAxiosError(err)) {
    const data = err.response?.data as any;

    const modelErrors = extractAspNetValidationErrors(data);
    if (modelErrors) return modelErrors;

    if (typeof data === 'string' && data.trim()) return data;
    if (data?.message) return String(data.message);
    if (data?.error) return String(data.error);
    if (data?.title && data?.detail) return `${data.title}\n${data.detail}`;
    if (data?.title) return String(data.title);
    if (data?.detail) return String(data.detail);

    return `Request failed (${err.response?.status ?? 'no status'})`;
  }

  if (err instanceof Error) return err.message;
  return 'Unexpected error';
}

/**
 * IMPORTANT:
 * Your backend expects:
 *   public record UpdateOrderStatusRequest(string Status, string? Notes);
 * So status MUST be a STRING. No numeric fallback.
 */
async function putUpdateOrderStatus(orderId: string, status: OrderStatus, notes?: string | null) {
  const payload = {
    status, // string only
    notes: notes ?? null,
  };

  // If your api baseURL does NOT already include "/api",
  // Base URL already includes /api, so use /MobileBff/... not /api/MobileBff/...
  const url = `/MobileBff/orders/${orderId}/status`;

  logJson('Updating order status (PUT)', { url, payload });

  await api.put(url, payload, {
    headers: { 'Content-Type': 'application/json' },
  });
}

export default function VendorOrdersScreen() {
  const router = useRouter();
  const params = useLocalSearchParams<{ restaurantId?: string }>();

  const [isAuthenticated, setIsAuthenticated] = useState(false);
  const [isVendor, setIsVendor] = useState(false);

  const [restaurants, setRestaurants] = useState<Restaurant[]>([]);
  const [orders, setOrders] = useState<Order[]>([]);

  const [selectedRestaurantId, setSelectedRestaurantId] = useState<string | null>(
    params.restaurantId || null
  );

  const [loadingRestaurants, setLoadingRestaurants] = useState(true);
  const [loadingOrders, setLoadingOrders] = useState(false);
  const [refreshing, setRefreshing] = useState(false);
  const [updatingStatus, setUpdatingStatus] = useState<string | null>(null);

  const didInitRef = useRef(false);
  const isMountedRef = useRef(true);

  useEffect(() => {
    isMountedRef.current = true;
    return () => {
      isMountedRef.current = false;
    };
  }, []);

  useEffect(() => {
    if (params.restaurantId && params.restaurantId !== selectedRestaurantId) {
      setSelectedRestaurantId(params.restaurantId);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [params.restaurantId]);

  useEffect(() => {
    if (didInitRef.current) return;
    didInitRef.current = true;
    void checkAuthAndLoad();
  }, []);

  const checkAuthAndLoad = async () => {
    try {
      const authenticated = await authService.isAuthenticated();
      if (!isMountedRef.current) return;

      setIsAuthenticated(authenticated);
      if (!authenticated) return;

      const vendor = await authService.isVendor();
      if (!isMountedRef.current) return;

      setIsVendor(vendor);

      if (vendor) {
        await loadRestaurants();
      }
    } catch (err) {
      console.error('Auth check failed:', err);
      Alert.alert('Error', 'Failed to validate authentication.');
    }
  };

  const activeRestaurants = useMemo(() => {
    return restaurants.filter((r) => r.isActive);
  }, [restaurants]);

  const loadRestaurants = async () => {
    try {
      setLoadingRestaurants(true);
      const response = await api.get('/MobileBff/vendor/my-restaurants');

      const data = response.data ?? [];
      if (!isMountedRef.current) return;

      setRestaurants(Array.isArray(data) ? data : []);
    } catch (err) {
      console.error('Error loading restaurants:', err);
      Alert.alert('Error', normalizeErrorMessage(err));
    } finally {
      if (isMountedRef.current) setLoadingRestaurants(false);
    }
  };

  const loadOrders = async () => {
    if (!isAuthenticated || !isVendor) return;
    if (restaurants.length === 0) return;

    try {
      setLoadingOrders(true);

      let allOrders: Order[] = [];

      if (selectedRestaurantId) {
        const response = await api.get(
          `/MobileBff/vendor/restaurants/${selectedRestaurantId}/orders`
        );
        const data = response.data ?? [];
        allOrders = Array.isArray(data) ? data : [];
      } else {
        for (const restaurant of activeRestaurants) {
          try {
            const response = await api.get(
              `/MobileBff/vendor/restaurants/${restaurant.restaurantId}/orders`
            );
            const data = response.data ?? [];
            if (Array.isArray(data)) allOrders.push(...data);
          } catch (innerErr) {
            console.error(
              `Error loading orders for restaurant ${restaurant.restaurantId}:`,
              innerErr
            );
          }
        }
      }

      allOrders.sort(
        (a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime()
      );

      if (!isMountedRef.current) return;
      setOrders(allOrders);
    } catch (err) {
      console.error('Error loading orders:', err);
      Alert.alert('Error', normalizeErrorMessage(err));
    } finally {
      if (isMountedRef.current) setLoadingOrders(false);
    }
  };

  useEffect(() => {
    if (isAuthenticated && isVendor && restaurants.length > 0) {
      void loadOrders();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selectedRestaurantId, restaurants, isAuthenticated, isVendor]);

  const onRefresh = async () => {
    setRefreshing(true);
    try {
      await loadRestaurants();
      await loadOrders();
    } finally {
      if (isMountedRef.current) setRefreshing(false);
    }
  };

  const updateOrderStatus = async (orderId: string, newStatus: OrderStatus) => {
    try {
      setUpdatingStatus(orderId);

      await putUpdateOrderStatus(orderId, newStatus, null);

      await loadOrders();
    } catch (err: unknown) {
      if (axios.isAxiosError(err)) {
        const parsedSent = safeJsonParse(err.config?.data);

        logJson('Error updating order status (axios)', {
          status: err.response?.status,
          responseData: err.response?.data,
          url: err.config?.url,
          method: err.config?.method,
          dataSent: parsedSent,
          headersSent: err.config?.headers,
        });
      } else {
        console.error('Error updating order status:', err);
      }

      Alert.alert('Update failed', normalizeErrorMessage(err));
    } finally {
      if (isMountedRef.current) setUpdatingStatus(null);
    }
  };

  const showStatusPicker = (order: Order) => {
    const current = order.status as OrderStatus;

    if (!statusOptions.includes(current)) {
      Alert.alert('Cannot update', `Unknown order status: ${order.status}`);
      return;
    }

    const nextList = allowedNextStatuses[current];
    if (!nextList || nextList.length === 0) {
      Alert.alert('Cannot update', `Order is already ${order.status}.`);
      return;
    }

    Alert.alert(
      'Update status',
      `Current: "${order.status}"`,
      [
        ...nextList.map((s) => ({
          text: s,
          onPress: () => void updateOrderStatus(order.orderId, s),
        })),
        { text: 'Cancel', style: 'cancel' },
      ]
    );
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
        <Text style={styles.errorText}>You don&apos;t have any restaurants yet.</Text>
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
          <ScrollView
            horizontal
            showsHorizontalScrollIndicator={false}
            style={styles.restaurantFilter}
          >
            <TouchableOpacity
              style={[styles.filterChip, !selectedRestaurantId && styles.filterChipActive]}
              onPress={() => setSelectedRestaurantId(null)}
            >
              <Text
                style={[
                  styles.filterChipText,
                  !selectedRestaurantId && styles.filterChipTextActive,
                ]}
              >
                All
              </Text>
            </TouchableOpacity>

            {restaurants.map((restaurant) => (
              <TouchableOpacity
                key={restaurant.restaurantId}
                style={[
                  styles.filterChip,
                  selectedRestaurantId === restaurant.restaurantId && styles.filterChipActive,
                ]}
                onPress={() => setSelectedRestaurantId(restaurant.restaurantId)}
              >
                <Text
                  style={[
                    styles.filterChipText,
                    selectedRestaurantId === restaurant.restaurantId && styles.filterChipTextActive,
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
              activeOpacity={0.85}
            >
              <View style={styles.orderHeader}>
                <View>
                  <Text style={styles.orderId}>Order #{order.orderId.substring(0, 8)}</Text>
                  <Text style={styles.orderDate}>{new Date(order.createdAt).toLocaleString()}</Text>
                </View>

                <View style={[styles.statusBadge, { backgroundColor: getStatusColor(order.status) }]}>
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

              {order.specialInstructions && (
                <View style={styles.specialInstructionsContainer}>
                  <Text style={styles.specialInstructionsLabel}>Special Instructions:</Text>
                  <Text style={styles.specialInstructionsText}>{order.specialInstructions}</Text>
                </View>
              )}
              {order.deliveryAddress && (
                <Text style={styles.deliveryAddress}>📍 {order.deliveryAddress}</Text>
              )}

              <View style={styles.orderFooter}>
                <Text style={styles.orderTotal}>Total: ${order.total.toFixed(2)}</Text>

                <TouchableOpacity
                  style={[
                    styles.statusButton,
                    { backgroundColor: getStatusColor(order.status) },
                    updatingStatus === order.orderId && styles.statusButtonDisabled,
                  ]}
                  onPress={() => showStatusPicker(order)}
                  disabled={updatingStatus === order.orderId}
                  activeOpacity={0.8}
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
