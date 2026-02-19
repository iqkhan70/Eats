import React, { useEffect, useMemo, useState } from "react";
import {
  ActivityIndicator,
  ScrollView,
  StyleSheet,
  Text,
  TouchableOpacity,
  View,
} from "react-native";
import { SafeAreaView } from "react-native-safe-area-context";
import { Ionicons } from "@expo/vector-icons";
import { useLocalSearchParams, useRouter } from "expo-router";
import { api } from "../../../services/api";
import AppHeader from "../../../components/AppHeader";

interface OrderItem {
  orderItemId: string;
  name: string;
  quantity: number;
  unitPrice: number;
  totalPrice: number;
}

interface StatusHistory {
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
  subtotal?: number;
  tax?: number;
  deliveryFee?: number;
  serviceFee?: number;
  total: number;
  items: OrderItem[];
  statusHistory?: StatusHistory[];
}

function getStatusColor(status: string): string {
  switch (status) {
    case "Pending":
      return "#FFA726";
    case "Confirmed":
      return "#29B6F6";
    case "Preparing":
      return "#42A5F5";
    case "Ready":
      return "#66BB6A";
    case "OutForDelivery":
      return "#AB47BC";
    case "Delivered":
      return "#26A69A";
    case "Completed":
      return "#78909C";
    case "Cancelled":
      return "#EF5350";
    default:
      return "#78909C";
  }
}

export default function VendorOrderDetailsScreen() {
  const router = useRouter();
  const params = useLocalSearchParams<{ orderId?: string; restaurantId?: string }>();

  const orderId = typeof params.orderId === "string" ? params.orderId : "";
  const restaurantId =
    typeof params.restaurantId === "string" ? params.restaurantId : "";

  const [loading, setLoading] = useState(true);
  const [order, setOrder] = useState<Order | null>(null);
  const [error, setError] = useState<string | null>(null);

  const title = useMemo(() => {
    if (!orderId) return "Order Details";
    return `Order #${orderId.slice(0, 8)}`;
  }, [orderId]);

  useEffect(() => {
    const load = async () => {
      if (!orderId || !restaurantId) {
        setError("Missing order context. Please open the order from the vendor orders list.");
        setLoading(false);
        return;
      }

      try {
        setLoading(true);
        setError(null);
        const res = await api.get<Order>(
          `/MobileBff/vendor/restaurants/${restaurantId}/orders/${orderId}`,
        );
        setOrder(res.data ?? null);
      } catch (e: any) {
        const status = e?.response?.status;
        const msg =
          e?.response?.data?.message ||
          e?.response?.data?.error ||
          e?.message ||
          "Failed to load order.";
        setError(status ? `${msg} (HTTP ${status})` : String(msg));
        setOrder(null);
      } finally {
        setLoading(false);
      }
    };

    void load();
  }, [orderId, restaurantId]);

  return (
    <SafeAreaView style={styles.container} edges={["top"]}>
      <AppHeader
        title={title}
        showBack
        onBack={() => router.back()}
        right={
          <TouchableOpacity
            onPress={() =>
              router.push({
                pathname: "/orders/chat/[orderId]",
                params: { orderId },
              } as any)
            }
            disabled={!orderId}
            style={[styles.headerIconBtn, !orderId && styles.headerIconBtnDisabled]}
            accessibilityLabel="Open order chat"
            activeOpacity={0.8}
          >
            <Ionicons name="chatbubbles-outline" size={22} color="#fff" />
          </TouchableOpacity>
        }
      />

      {loading ? (
        <View style={styles.center}>
          <ActivityIndicator size="large" color="#007AFF" />
          <Text style={styles.muted}>Loading...</Text>
        </View>
      ) : error ? (
        <View style={styles.center}>
          <Text style={styles.errorTitle}>Could not load order</Text>
          <Text style={styles.errorBody}>{error}</Text>
          <TouchableOpacity onPress={() => router.back()} style={styles.primaryBtn}>
            <Text style={styles.primaryBtnText}>Go Back</Text>
          </TouchableOpacity>
        </View>
      ) : !order ? (
        <View style={styles.center}>
          <Text style={styles.errorTitle}>Order not found</Text>
          <TouchableOpacity onPress={() => router.back()} style={styles.primaryBtn}>
            <Text style={styles.primaryBtnText}>Go Back</Text>
          </TouchableOpacity>
        </View>
      ) : (
        <ScrollView style={styles.scroll} contentContainerStyle={styles.content}>
          <View style={styles.card}>
            <View style={styles.rowBetween}>
              <Text style={styles.h2}>Status</Text>
              <View
                style={[
                  styles.badge,
                  { backgroundColor: getStatusColor(order.status) },
                ]}
              >
                <Text style={styles.badgeText}>{order.status}</Text>
              </View>
            </View>
            <Text style={styles.muted}>
              Created: {new Date(order.createdAt).toLocaleString()}
            </Text>
          </View>

          <View style={styles.card}>
            <Text style={styles.h2}>Items</Text>
            {order.items?.length ? (
              order.items.map((it) => (
                <View key={it.orderItemId} style={styles.itemRow}>
                  <Text style={styles.itemName}>
                    {it.quantity}x {it.name}
                  </Text>
                  <Text style={styles.itemPrice}>${it.totalPrice.toFixed(2)}</Text>
                </View>
              ))
            ) : (
              <Text style={styles.muted}>No items.</Text>
            )}
            <View style={styles.divider} />
            <View style={styles.itemRow}>
              <Text style={styles.totalLabel}>Total</Text>
              <Text style={styles.totalValue}>${order.total.toFixed(2)}</Text>
            </View>
          </View>

          {(order.deliveryAddress || order.specialInstructions) && (
            <View style={styles.card}>
              <Text style={styles.h2}>Customer Details</Text>
              {!!order.deliveryAddress && (
                <Text style={styles.body}>Address: {order.deliveryAddress}</Text>
              )}
              {!!order.specialInstructions && (
                <Text style={styles.body}>
                  Instructions: {order.specialInstructions}
                </Text>
              )}
            </View>
          )}

          {!!order.statusHistory?.length && (
            <View style={styles.card}>
              <Text style={styles.h2}>Status History</Text>
              {order.statusHistory.map((h) => (
                <View key={h.id} style={styles.historyRow}>
                  <Text style={styles.body}>
                    {new Date(h.changedAt).toLocaleString()} — {h.status}
                  </Text>
                  {!!h.notes && <Text style={styles.muted}>{h.notes}</Text>}
                </View>
              ))}
            </View>
          )}
        </ScrollView>
      )}
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: "#F5F5F5" },
  scroll: { flex: 1 },
  content: { padding: 16, paddingBottom: 24, gap: 12 },

  headerIconBtn: {
    width: 44,
    height: 44,
    borderRadius: 22,
    alignItems: "center",
    justifyContent: "center",
    backgroundColor: "rgba(255,255,255,0.2)",
  },
  headerIconBtnDisabled: { opacity: 0.4 },

  center: {
    flex: 1,
    justifyContent: "center",
    alignItems: "center",
    padding: 16,
    gap: 12,
  },
  muted: { color: "#666" },

  card: {
    backgroundColor: "#fff",
    borderRadius: 12,
    padding: 14,
    borderWidth: 1,
    borderColor: "#E0E0E0",
  },
  rowBetween: { flexDirection: "row", alignItems: "center", justifyContent: "space-between" },
  h2: { fontSize: 16, fontWeight: "700", color: "#333" },
  body: { fontSize: 13, color: "#333", marginTop: 8 },

  badge: {
    paddingHorizontal: 10,
    paddingVertical: 6,
    borderRadius: 999,
  },
  badgeText: { color: "#fff", fontWeight: "700", fontSize: 12 },

  itemRow: {
    flexDirection: "row",
    justifyContent: "space-between",
    alignItems: "center",
    marginTop: 10,
    gap: 10,
  },
  itemName: { flex: 1, color: "#333", fontWeight: "600" },
  itemPrice: { color: "#333", fontWeight: "700" },
  divider: { height: 1, backgroundColor: "#EEE", marginTop: 12 },
  totalLabel: { color: "#333", fontWeight: "800" },
  totalValue: { color: "#007AFF", fontWeight: "800" },

  historyRow: { marginTop: 10, gap: 4 },

  errorTitle: { fontSize: 16, fontWeight: "800", color: "#d32f2f", textAlign: "center" },
  errorBody: { color: "#444", textAlign: "center" },

  primaryBtn: {
    marginTop: 10,
    backgroundColor: "#007AFF",
    paddingHorizontal: 14,
    paddingVertical: 10,
    borderRadius: 10,
  },
  primaryBtnText: { color: "#fff", fontWeight: "800" },
});

