import React, { useState, useEffect } from 'react';
import { View, Text, StyleSheet, ScrollView, ActivityIndicator, TouchableOpacity } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useRouter, useLocalSearchParams } from 'expo-router';
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
  deliveryAddress?: string;
  specialInstructions?: string;
  createdAt: string;
  estimatedDeliveryAt?: string;
  deliveredAt?: string;
  items: OrderItem[];
  statusHistory?: StatusHistory[];
}

interface OrderItem {
  orderItemId: string;
  menuItemId: string;
  name: string;
  description?: string;
  quantity: number;
  unitPrice: number;
  totalPrice: number;
  modifiersJson?: string;
}

interface StatusHistory {
  id: string;
  orderId: string;
  status: string;
  notes?: string;
  changedAt: string;
}

export default function OrderDetailsScreen() {
  const router = useRouter();
  const params = useLocalSearchParams<{ orderId: string }>();
  const [order, setOrder] = useState<Order | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    if (params.orderId) {
      loadOrder();
    }
  }, [params.orderId]);

  const loadOrder = async () => {
    try {
      setLoading(true);
      const response = await api.get<Order>(`/MobileBff/orders/${params.orderId}`);
      setOrder(response.data);
    } catch (error: any) {
      console.error('Error loading order:', error);
      setOrder(null);
    } finally {
      setLoading(false);
    }
  };

  const getStatusColor = (status: string): string => {
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
  };

  const getStatusTextColor = (status: string): string => {
    switch (status) {
      case 'Pending':
        return '#856404';
      case 'Confirmed':
        return '#0c5460';
      case 'Preparing':
        return '#155724';
      case 'Ready':
        return '#004085';
      case 'OutForDelivery':
        return '#383d41';
      case 'Delivered':
        return '#155724';
      case 'Cancelled':
        return '#721c24';
      default:
        return '#495057';
    }
  };

  const parseModifiers = (modifiersJson?: string): string[] => {
    if (!modifiersJson) return [];
    try {
      return JSON.parse(modifiersJson);
    } catch {
      return [];
    }
  };

  if (loading) {
    return (
      <View style={styles.container}>
        <View style={styles.loadingContainer}>
          <ActivityIndicator size="large" color="#6200ee" />
          <Text style={styles.loadingText}>Loading order details...</Text>
        </View>
      </View>
    );
  }

  if (!order) {
    return (
      <View style={styles.container}>
        <View style={styles.emptyContainer}>
          <Ionicons name="alert-circle-outline" size={64} color="#ccc" />
          <Text style={styles.emptyText}>Order Not Found</Text>
          <Text style={styles.emptySubtext}>
            The order you're looking for doesn't exist or you don't have permission to view it.
          </Text>
          <TouchableOpacity
            style={styles.backButton}
            onPress={() => router.back()}
          >
            <Text style={styles.backButtonText}>Back to Orders</Text>
          </TouchableOpacity>
        </View>
      </View>
    );
  }

  const sortedStatusHistory = order.statusHistory
    ? [...order.statusHistory].sort((a, b) => 
        new Date(a.changedAt).getTime() - new Date(b.changedAt).getTime()
      )
    : [];

  return (
    <ScrollView style={styles.container} contentContainerStyle={styles.contentContainer}>
      {/* Header */}
      <View style={styles.header}>
        <TouchableOpacity onPress={() => router.back()} style={styles.backIcon}>
          <Ionicons name="arrow-back" size={24} color="#333" />
        </TouchableOpacity>
        <Text style={styles.headerTitle}>Order Details</Text>
        <View style={styles.backIcon} />
      </View>

      {/* Order Info Card */}
      <View style={styles.card}>
        <View style={styles.orderHeader}>
          <View style={styles.orderInfo}>
            <Text style={styles.orderId}>Order #{order.orderId.substring(0, 8)}</Text>
            <Text style={styles.orderDate}>
              Placed on {new Date(order.createdAt).toLocaleDateString('en-US', {
                month: 'short',
                day: 'numeric',
                year: 'numeric',
                hour: '2-digit',
                minute: '2-digit',
              })}
            </Text>
          </View>
          <View style={[styles.statusBadge, { backgroundColor: getStatusColor(order.status) }]}>
            <Text style={[styles.statusText, { color: getStatusTextColor(order.status) }]}>
              {order.status}
            </Text>
          </View>
        </View>

        {order.estimatedDeliveryAt && (
          <View style={styles.infoRow}>
            <Ionicons name="time-outline" size={20} color="#666" />
            <Text style={styles.infoText}>
              Estimated delivery: {new Date(order.estimatedDeliveryAt).toLocaleDateString('en-US', {
                month: 'short',
                day: 'numeric',
                hour: '2-digit',
                minute: '2-digit',
              })}
            </Text>
          </View>
        )}

        {order.specialInstructions && (
          <View style={[styles.infoRow, styles.specialInstructionsRow]}>
            <Ionicons name="information-circle-outline" size={20} color="#ffc107" />
            <View style={styles.specialInstructionsContent}>
              <Text style={styles.specialInstructionsLabel}>Special Instructions</Text>
              <Text style={[styles.infoText, styles.specialInstructionsText]}>{order.specialInstructions}</Text>
            </View>
          </View>
        )}

        {order.deliveryAddress && (
          <View style={styles.infoRow}>
            <Ionicons name="location-outline" size={20} color="#666" />
            <Text style={styles.infoText}>{order.deliveryAddress}</Text>
          </View>
        )}
      </View>

      {/* Status History Timeline */}
      {sortedStatusHistory.length > 0 && (
        <View style={styles.card}>
          <Text style={styles.cardTitle}>Order Status</Text>
          <View style={styles.timeline}>
            {sortedStatusHistory.map((statusEntry, index) => (
              <View key={statusEntry.id} style={styles.timelineItem}>
                <View style={styles.timelineLine}>
                  <View style={[styles.timelineDot, { backgroundColor: '#6200ee' }]} />
                  {index < sortedStatusHistory.length - 1 && <View style={styles.timelineConnector} />}
                </View>
                <View style={styles.timelineContent}>
                  <Text style={styles.timelineStatus}>{statusEntry.status}</Text>
                  <Text style={styles.timelineDate}>
                    {new Date(statusEntry.changedAt).toLocaleDateString('en-US', {
                      month: 'short',
                      day: 'numeric',
                      hour: '2-digit',
                      minute: '2-digit',
                    })}
                  </Text>
                  {statusEntry.notes && (
                    <Text style={styles.timelineNotes}>{statusEntry.notes}</Text>
                  )}
                </View>
              </View>
            ))}
          </View>
        </View>
      )}

      {/* Order Items */}
      <View style={styles.card}>
        <Text style={styles.cardTitle}>Order Items</Text>
        {order.items.map((item, index) => {
          const modifiers = parseModifiers(item.modifiersJson);
          return (
            <View key={item.orderItemId}>
              <View style={styles.orderItem}>
                <View style={styles.orderItemInfo}>
                  <Text style={styles.orderItemName}>{item.name}</Text>
                  {item.description && (
                    <Text style={styles.orderItemDescription}>{item.description}</Text>
                  )}
                  <Text style={styles.orderItemQuantity}>
                    Quantity: {item.quantity} × ${item.unitPrice.toFixed(2)}
                  </Text>
                  {modifiers.length > 0 && (
                    <View style={styles.modifiersContainer}>
                      {modifiers.map((modifier, modIndex) => (
                        <View key={modIndex} style={styles.modifierBadge}>
                          <Text style={styles.modifierText}>{modifier}</Text>
                        </View>
                      ))}
                    </View>
                  )}
                </View>
                <Text style={styles.orderItemPrice}>${item.totalPrice.toFixed(2)}</Text>
              </View>
              {index < order.items.length - 1 && <View style={styles.divider} />}
            </View>
          );
        })}
      </View>

      {/* Order Summary */}
      <View style={styles.card}>
        <Text style={styles.cardTitle}>Order Summary</Text>
        <View style={styles.summaryRow}>
          <Text style={styles.summaryLabel}>Subtotal</Text>
          <Text style={styles.summaryValue}>${order.subtotal.toFixed(2)}</Text>
        </View>
        <View style={styles.summaryRow}>
          <Text style={styles.summaryLabel}>Tax</Text>
          <Text style={styles.summaryValue}>${order.tax.toFixed(2)}</Text>
        </View>
        <View style={styles.summaryRow}>
          <Text style={styles.summaryLabel}>Delivery Fee</Text>
          <Text style={styles.summaryValue}>${order.deliveryFee.toFixed(2)}</Text>
        </View>
        <View style={styles.summaryDivider} />
        <View style={styles.summaryRow}>
          <Text style={styles.summaryTotalLabel}>Total</Text>
          <Text style={styles.summaryTotalValue}>${order.total.toFixed(2)}</Text>
        </View>
      </View>
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#f5f5f5',
  },
  contentContainer: {
    padding: 16,
  },
  loadingContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    padding: 40,
  },
  loadingText: {
    marginTop: 10,
    fontSize: 16,
    color: '#666',
  },
  emptyContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    padding: 40,
  },
  emptyText: {
    fontSize: 20,
    fontWeight: '600',
    color: '#333',
    marginTop: 16,
  },
  emptySubtext: {
    fontSize: 14,
    color: '#666',
    marginTop: 8,
    textAlign: 'center',
  },
  backButton: {
    backgroundColor: '#6200ee',
    paddingHorizontal: 24,
    paddingVertical: 12,
    borderRadius: 8,
    marginTop: 20,
  },
  backButtonText: {
    color: '#fff',
    fontSize: 14,
    fontWeight: '600',
  },
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    marginBottom: 16,
  },
  backIcon: {
    width: 40,
    height: 40,
    justifyContent: 'center',
    alignItems: 'center',
  },
  headerTitle: {
    fontSize: 24,
    fontWeight: 'bold',
    color: '#333',
  },
  card: {
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
  orderHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'flex-start',
    marginBottom: 12,
  },
  orderInfo: {
    flex: 1,
  },
  orderId: {
    fontSize: 20,
    fontWeight: 'bold',
    color: '#333',
    marginBottom: 4,
  },
  orderDate: {
    fontSize: 14,
    color: '#666',
  },
  statusBadge: {
    paddingHorizontal: 12,
    paddingVertical: 6,
    borderRadius: 12,
  },
  statusText: {
    fontSize: 14,
    fontWeight: '500',
  },
  specialInstructionsRow: {
    backgroundColor: '#fff3cd',
    padding: 12,
    borderRadius: 8,
    borderLeftWidth: 4,
    borderLeftColor: '#ffc107',
    marginBottom: 8,
  },
  specialInstructionsContent: {
    flex: 1,
    marginLeft: 12,
  },
  specialInstructionsLabel: {
    fontSize: 12,
    fontWeight: '600',
    color: '#856404',
    marginBottom: 4,
  },
  specialInstructionsText: {
    fontStyle: 'italic',
    color: '#856404',
  },
  infoRow: {
    flexDirection: 'row',
    alignItems: 'center',
    marginTop: 8,
    gap: 8,
  },
  infoText: {
    fontSize: 14,
    color: '#666',
    flex: 1,
  },
  cardTitle: {
    fontSize: 18,
    fontWeight: 'bold',
    color: '#333',
    marginBottom: 12,
  },
  timeline: {
    marginTop: 8,
  },
  timelineItem: {
    flexDirection: 'row',
    marginBottom: 16,
  },
  timelineLine: {
    alignItems: 'center',
    marginRight: 12,
    minWidth: 24,
  },
  timelineDot: {
    width: 12,
    height: 12,
    borderRadius: 6,
    marginTop: 4,
  },
  timelineConnector: {
    width: 2,
    flex: 1,
    backgroundColor: '#ddd',
    marginTop: 4,
    minHeight: 40,
  },
  timelineContent: {
    flex: 1,
  },
  timelineStatus: {
    fontSize: 16,
    fontWeight: '500',
    color: '#333',
    marginBottom: 4,
  },
  timelineDate: {
    fontSize: 14,
    color: '#666',
    marginBottom: 2,
  },
  timelineNotes: {
    fontSize: 14,
    color: '#666',
    fontStyle: 'italic',
  },
  orderItem: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'flex-start',
    marginBottom: 12,
  },
  orderItemInfo: {
    flex: 1,
    marginRight: 12,
  },
  orderItemName: {
    fontSize: 16,
    fontWeight: '500',
    color: '#333',
    marginBottom: 4,
  },
  orderItemDescription: {
    fontSize: 14,
    color: '#666',
    marginBottom: 4,
  },
  orderItemQuantity: {
    fontSize: 14,
    color: '#666',
    marginBottom: 4,
  },
  modifiersContainer: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 6,
    marginTop: 4,
  },
  modifierBadge: {
    backgroundColor: '#f0f0f0',
    paddingHorizontal: 8,
    paddingVertical: 4,
    borderRadius: 4,
  },
  modifierText: {
    fontSize: 12,
    color: '#666',
  },
  orderItemPrice: {
    fontSize: 16,
    fontWeight: 'bold',
    color: '#333',
  },
  divider: {
    height: 1,
    backgroundColor: '#eee',
    marginVertical: 12,
  },
  summaryRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    marginBottom: 8,
  },
  summaryLabel: {
    fontSize: 16,
    color: '#666',
  },
  summaryValue: {
    fontSize: 16,
    color: '#333',
  },
  summaryDivider: {
    height: 1,
    backgroundColor: '#eee',
    marginVertical: 12,
  },
  summaryTotalLabel: {
    fontSize: 18,
    fontWeight: 'bold',
    color: '#333',
  },
  summaryTotalValue: {
    fontSize: 18,
    fontWeight: 'bold',
    color: '#6200ee',
  },
});
