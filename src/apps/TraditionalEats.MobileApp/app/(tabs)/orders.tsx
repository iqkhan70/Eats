import React, { useCallback, useEffect, useMemo, useState } from 'react';
import {
  View,
  Text,
  StyleSheet,
  FlatList,
  ActivityIndicator,
  TouchableOpacity,
  RefreshControl,
} from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useRouter, useLocalSearchParams, useFocusEffect } from 'expo-router';
import { api } from '../../services/api';

interface Order {
  orderId: string;
  customerId: string;
  restaurantId: string;
  subtotal: number;
  tax: number;
  deliveryFee: number;
  total: number;
  status: string;
  deliveryAddress: string;
  createdAt: string;
  deliveredAt?: string;
  items: OrderItem[];
}

interface OrderItem {
  orderItemId: string;
  menuItemId: string;
  name: string;
  quantity: number;
  unitPrice: number;
  totalPrice: number;
}

export default function OrdersScreen() {
  const router = useRouter();
  const params = useLocalSearchParams<{ refresh?: string }>();

  const [orders, setOrders] = useState<Order[]>([]);
  const [loading, setLoading] = useState(true);

  // ✅ Pull-to-refresh state
  const [refreshing, setRefreshing] = useState(false);

  const loadOrders = useCallback(async () => {
    try {
      const response = await api.get<Order[]>('/MobileBff/orders');
      setOrders(response.data || []);
    } catch (error: any) {
      console.error('Error loading orders:', error);
      setOrders([]);
    }
  }, []);

  // ✅ Initial load (show full-screen loader only once)
  useEffect(() => {
    (async () => {
      try {
        setLoading(true);
        await loadOrders();
      } finally {
        setLoading(false);
      }
    })();
  }, [loadOrders]);

  // ✅ Auto refresh whenever tab/screen becomes active (best UX)
  useFocusEffect(
    useCallback(() => {
      // don’t flash loader when user returns; just refresh silently
      loadOrders();
    }, [loadOrders])
  );

  // ✅ If you ever navigate with /orders?refresh=xyz it will reload
  useEffect(() => {
    if (params.refresh) {
      loadOrders();
    }
  }, [params.refresh, loadOrders]);

  // ✅ Pull-to-refresh handler
  const onRefresh = useCallback(async () => {
    try {
      setRefreshing(true);
      await loadOrders();
    } finally {
      setRefreshing(false);
    }
  }, [loadOrders]);

  // ✅ Newest first
  const sortedOrders = useMemo(() => {
    return [...orders].sort(
      (a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime()
    );
  }, [orders]);

  function getStatusColor(status: string): string {
    switch (status) {
      case 'Pending':
        return '#fff3cd';
      case 'Confirmed':
        return '#d1ecf1';
      case 'Preparing':
        return '#d4edda';
      case 'Ready':
        return '#cce5ff';
      case 'OutForDelivery':
        return '#e2e3e5';
      case 'Delivered':
        return '#d4edda';
      case 'Cancelled':
        return '#f8d7da';
      default:
        return '#e9ecef';
    }
  }

  if (loading) {
    return (
      <View style={styles.container}>
        <View style={styles.emptyContainer}>
          <ActivityIndicator size="large" color="#6200ee" />
          <Text style={styles.loadingText}>Loading orders...</Text>
        </View>
      </View>
    );
  }

  const EmptyState = (
    <View style={styles.emptyContainer}>
      <Ionicons name="receipt-outline" size={64} color="#ccc" />
      <Text style={styles.emptyText}>No orders yet</Text>
      <Text style={styles.emptySubtext}>Your order history will appear here</Text>

      <TouchableOpacity style={styles.browseButton} onPress={() => router.push('/(tabs)')}>
        <Text style={styles.browseButtonText}>Browse Restaurants</Text>
      </TouchableOpacity>

      <Text style={styles.pullHint}>Pull down to refresh</Text>
    </View>
  );

  return (
    <View style={styles.container}>
      {sortedOrders.length === 0 ? (
        <FlatList
          data={[]}
          renderItem={null as any}
          ListEmptyComponent={EmptyState}
          refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} />}
          contentContainerStyle={{ flexGrow: 1 }}
        />
      ) : (
        <FlatList
          data={sortedOrders}
          keyExtractor={(item) => item.orderId}
          refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} />}
          renderItem={({ item }) => (
            <TouchableOpacity
              style={styles.orderCard}
              onPress={() => router.push(`/orders/${item.orderId}`)}
              activeOpacity={0.85}
            >
              <View style={styles.orderHeader}>
                <Text style={styles.orderId}>Order #{item.orderId.substring(0, 8)}</Text>
                <Text style={styles.orderDate}>
                  {new Date(item.createdAt).toLocaleDateString('en-US', {
                    month: 'short',
                    day: 'numeric',
                    year: 'numeric',
                    hour: '2-digit',
                    minute: '2-digit',
                  })}
                </Text>
              </View>

              <View style={styles.orderItems}>
                {item.items.slice(0, 2).map((orderItem) => (
                  <Text key={orderItem.orderItemId} style={styles.orderItemText}>
                    {orderItem.name} x {orderItem.quantity}
                  </Text>
                ))}
                {item.items.length > 2 && (
                  <Text style={styles.orderItemText}>+{item.items.length - 2} more items</Text>
                )}
              </View>

              <View style={styles.orderFooter}>
                <View style={[styles.statusBadge, { backgroundColor: getStatusColor(item.status) }]}>
                  <Text style={styles.orderStatus}>{item.status}</Text>
                </View>
                <Text style={styles.orderTotal}>${item.total.toFixed(2)}</Text>
              </View>
            </TouchableOpacity>
          )}
        />
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: '#f5f5f5' },

  emptyContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    padding: 40,
  },

  emptyText: { fontSize: 20, fontWeight: '600', color: '#333', marginTop: 16 },
  emptySubtext: { fontSize: 14, color: '#666', marginTop: 8, textAlign: 'center' },

  loadingText: { marginTop: 10, fontSize: 16, color: '#666' },

  browseButton: {
    backgroundColor: '#6200ee',
    paddingHorizontal: 24,
    paddingVertical: 12,
    borderRadius: 8,
    marginTop: 20,
  },
  browseButtonText: { color: '#fff', fontSize: 14, fontWeight: '600' },

  pullHint: { marginTop: 14, fontSize: 12, color: '#999', textAlign: 'center' },

  orderId: { fontSize: 18, fontWeight: '600', color: '#333' },

  orderItems: { marginVertical: 8 },
  orderItemText: { fontSize: 14, color: '#666', marginBottom: 4 },

  statusBadge: { paddingHorizontal: 12, paddingVertical: 4, borderRadius: 12 },

  orderCard: {
    backgroundColor: '#fff',
    borderRadius: 12,
    padding: 16,
    margin: 16,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.1,
    shadowRadius: 4,
    elevation: 3,
  },

  orderHeader: { flexDirection: 'row', justifyContent: 'space-between', marginBottom: 12 },

  orderDate: { fontSize: 14, color: '#666' },

  orderFooter: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' },

  // keep your existing look (purple text)
  orderStatus: { fontSize: 14, color: '#6200ee', fontWeight: '500' },

  orderTotal: { fontSize: 18, fontWeight: 'bold', color: '#333' },
});
