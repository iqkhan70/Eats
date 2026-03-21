/**
 * Full-screen modal for new order alerts in Restaurant Mode.
 * Shows order summary, Accept/Reject buttons, and optional countdown.
 */
import React, { useEffect, useState } from "react";
import {
  View,
  Text,
  StyleSheet,
  Modal,
  TouchableOpacity,
  ActivityIndicator,
  Alert,
} from "react-native";
import { Ionicons } from "@expo/vector-icons";
import { useRouter } from "expo-router";
import type { OrderPlacedMetadata } from "../types/orderPlaced";

const COUNTDOWN_SECONDS = 30;

interface NewOrderModalProps {
  visible: boolean;
  order: OrderPlacedMetadata | null;
  queueLength?: number;
  restaurantId: string;
  restaurantName: string;
  onAccept: (orderId: string) => Promise<void>;
  onReject: (orderId: string) => Promise<void>;
  onDismiss: () => void;
}

export default function NewOrderModal({
  visible,
  order,
  queueLength = 1,
  restaurantId,
  restaurantName,
  onAccept,
  onReject,
  onDismiss,
}: NewOrderModalProps) {
  const router = useRouter();
  const [countdown, setCountdown] = useState(COUNTDOWN_SECONDS);
  const [responding, setResponding] = useState(false);

  useEffect(() => {
    if (!visible || !order) {
      setCountdown(COUNTDOWN_SECONDS);
      return;
    }
    const interval = setInterval(() => {
      setCountdown((prev) => {
        if (prev <= 1) {
          clearInterval(interval);
          return 0;
        }
        return prev - 1;
      });
    }, 1000);
    return () => clearInterval(interval);
  }, [visible, order]);

  const handleAccept = async () => {
    if (!order || responding) return;
    const oid = order.orderId;
    setResponding(true);
    try {
      await onAccept(oid);
      // Don't call onDismiss - respondToOrder already removes from queue
      router.push({
        pathname: "/vendor/orders/[orderId]",
        params: {
          orderId: oid,
          restaurantId,
          restaurantName,
        },
      } as never);
    } catch (e) {
      Alert.alert("Error", "Failed to accept order. Please try again.");
    } finally {
      setResponding(false);
    }
  };

  const handleReject = async () => {
    if (!order || responding) return;
    Alert.alert(
      "Reject order?",
      "The customer will be notified that the order was declined.",
      [
        { text: "Cancel", style: "cancel" },
        {
          text: "Reject",
          style: "destructive",
          onPress: async () => {
            setResponding(true);
            try {
              await onReject(order.orderId);
              // Don't call onDismiss - respondToOrder already removes from queue
            } catch (e) {
              Alert.alert("Error", "Failed to reject order. Please try again.");
            } finally {
              setResponding(false);
            }
          },
        },
      ],
    );
  };

  const orderIdShort = order?.orderId?.toString().slice(0, 8) ?? "?";

  return (
    <Modal
      visible={visible}
      animationType="slide"
      transparent={false}
      onRequestClose={onDismiss}
    >
      <View style={styles.container}>
        <View style={styles.header}>
          <View style={styles.badge}>
            <Ionicons name="notifications" size={28} color="#fff" />
            <Text style={styles.badgeText}>NEW ORDER</Text>
          </View>
          {restaurantName ? (
            <Text style={styles.restaurantName}>{restaurantName}</Text>
          ) : null}
          {queueLength > 1 ? (
            <Text style={styles.queueHint}>
              {queueLength} orders waiting
            </Text>
          ) : null}
        </View>

        <View style={styles.content}>
          <Text style={styles.orderId}>Order #{orderIdShort}</Text>

          <View style={styles.itemsSection}>
            <Text style={styles.sectionTitle}>Items</Text>
            {(order?.items ?? []).map((item, idx) => (
              <Text key={idx} style={styles.itemLine}>
                {item.quantity}x {item.name}
                {item.modifiers?.length
                  ? ` (${item.modifiers.join(", ")})`
                  : ""}{" "}
                – ${item.totalPrice.toFixed(2)}
              </Text>
            ))}
          </View>

          {(order?.serviceFee ?? 0) > 0 ? (
            <Text style={styles.fee}>Service fee: ${order.serviceFee.toFixed(2)}</Text>
          ) : null}

          <Text style={styles.total}>Total: ${(order?.total ?? 0).toFixed(2)}</Text>

          {order?.deliveryAddress ? (
            <View style={styles.addressSection}>
              <Ionicons name="location" size={18} color="#666" />
              <Text style={styles.address} numberOfLines={2}>
                {order?.deliveryAddress ?? ""}
              </Text>
            </View>
          ) : null}

          {countdown > 0 && (
            <Text style={styles.countdown}>
              Respond in {countdown}s
            </Text>
          )}
        </View>

        <View style={styles.actions}>
          <TouchableOpacity
            style={[styles.button, styles.rejectButton]}
            onPress={handleReject}
            disabled={!order || responding}
          >
            {responding ? (
              <ActivityIndicator size="small" color="#fff" />
            ) : (
              <>
                <Ionicons name="close-circle" size={24} color="#fff" />
                <Text style={styles.buttonText}>Reject</Text>
              </>
            )}
          </TouchableOpacity>

          <TouchableOpacity
            style={[styles.button, styles.acceptButton]}
            onPress={handleAccept}
            disabled={!order || responding}
          >
            {responding ? (
              <ActivityIndicator size="small" color="#fff" />
            ) : (
              <>
                <Ionicons name="checkmark-circle" size={24} color="#fff" />
                <Text style={styles.buttonText}>Accept</Text>
              </>
            )}
          </TouchableOpacity>
        </View>
      </View>
    </Modal>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: "#fff",
    paddingTop: 60,
    paddingHorizontal: 20,
  },
  header: {
    alignItems: "center",
    marginBottom: 24,
  },
  badge: {
    flexDirection: "row",
    alignItems: "center",
    backgroundColor: "#f97316",
    paddingHorizontal: 16,
    paddingVertical: 10,
    borderRadius: 24,
    gap: 8,
  },
  badgeText: {
    fontSize: 16,
    fontWeight: "800",
    color: "#fff",
  },
  restaurantName: {
    marginTop: 8,
    fontSize: 14,
    color: "#666",
  },
  queueHint: {
    marginTop: 6,
    fontSize: 13,
    color: "#f97316",
    fontWeight: "600",
  },
  content: {
    flex: 1,
  },
  orderId: {
    fontSize: 22,
    fontWeight: "700",
    color: "#333",
    marginBottom: 16,
  },
  itemsSection: {
    marginBottom: 12,
  },
  sectionTitle: {
    fontSize: 14,
    fontWeight: "600",
    color: "#666",
    marginBottom: 8,
  },
  itemLine: {
    fontSize: 15,
    color: "#333",
    marginBottom: 4,
  },
  fee: {
    fontSize: 13,
    color: "#666",
    marginBottom: 4,
  },
  total: {
    fontSize: 20,
    fontWeight: "700",
    color: "#f97316",
    marginBottom: 16,
  },
  addressSection: {
    flexDirection: "row",
    alignItems: "flex-start",
    gap: 8,
    backgroundColor: "#f5f5f5",
    padding: 12,
    borderRadius: 8,
  },
  address: {
    flex: 1,
    fontSize: 14,
    color: "#555",
  },
  countdown: {
    marginTop: 20,
    fontSize: 14,
    color: "#f97316",
    fontWeight: "600",
    textAlign: "center",
  },
  actions: {
    flexDirection: "row",
    gap: 16,
    paddingBottom: 40,
  },
  button: {
    flex: 1,
    flexDirection: "row",
    alignItems: "center",
    justifyContent: "center",
    paddingVertical: 16,
    borderRadius: 12,
    gap: 8,
  },
  rejectButton: {
    backgroundColor: "#dc3545",
  },
  acceptButton: {
    backgroundColor: "#28a745",
  },
  buttonText: {
    fontSize: 16,
    fontWeight: "700",
    color: "#fff",
  },
});
