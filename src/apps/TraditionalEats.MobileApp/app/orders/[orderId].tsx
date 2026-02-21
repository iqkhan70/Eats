import React, { useState, useEffect } from "react";
import {
  View,
  Text,
  StyleSheet,
  ScrollView,
  ActivityIndicator,
  TouchableOpacity,
  Alert,
  Linking,
} from "react-native";
import { Ionicons } from "@expo/vector-icons";
import { useRouter, useLocalSearchParams, useNavigation } from "expo-router";
import { useLayoutEffect } from "react";
import { SafeAreaView } from "react-native-safe-area-context";
import { api } from "../../services/api";
import { cartService } from "../../services/cart";
import ReviewForm from "../../components/ReviewForm";
import ReviewDisplay, { Review } from "../../components/ReviewDisplay";
import { authService } from "../../services/auth";
import AppHeader from "../../components/AppHeader";

interface RestaurantLight {
  restaurantId: string;
  name: string;
}

interface Order {
  orderId: string;
  customerId: string;
  restaurantId: string;
  subtotal: number;
  tax: number;
  deliveryFee: number;
  serviceFee?: number;
  total: number;
  status: string;
  paymentStatus?: string;
  stripePaymentIntentId?: string;
  paymentFailureReason?: string;
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
  const navigation = useNavigation();
  const params = useLocalSearchParams<{
    orderId: string;
    restaurantName?: string;
  }>();
  const [order, setOrder] = useState<Order | null>(null);
  const [loading, setLoading] = useState(true);
  const [reordering, setReordering] = useState(false);
  const [retryingPayment, setRetryingPayment] = useState(false);
  const [hasReview, setHasReview] = useState(false);
  const [existingReview, setExistingReview] = useState<Review | null>(null);
  const [showEditForm, setShowEditForm] = useState(false);
  const [activeTab, setActiveTab] = useState<"details" | "review">("details");
  const [restaurantName, setRestaurantName] = useState<string>(
    typeof params.restaurantName === "string" ? params.restaurantName : "",
  );

  // Hide default header - we use custom header with SafeAreaView
  useLayoutEffect(() => {
    navigation.setOptions({
      headerShown: false,
    });
  }, [navigation]);

  useEffect(() => {
    if (params.orderId) {
      loadOrder();
    }
  }, [params.orderId]);

  useEffect(() => {
    const passed =
      typeof params.restaurantName === "string" ? params.restaurantName : "";
    if (passed.trim()) {
      setRestaurantName(passed);
    }
  }, [params.restaurantName]);

  useEffect(() => {
    const rid = order?.restaurantId;
    if (!rid) return;
    if (restaurantName.trim()) return;

    void (async () => {
      try {
        const res = await api.get<RestaurantLight>(
          `/MobileBff/restaurants/${rid}`,
        );
        const name = (res.data as any)?.name;
        if (typeof name === "string" && name.trim()) setRestaurantName(name);
      } catch {
        return;
      }
    })();
  }, [order?.restaurantId, restaurantName]);

  useEffect(() => {
    if (
      order &&
      (order.status === "Delivered" || order.status === "Completed")
    ) {
      checkForExistingReview();
    } else {
      // Reset review state if order status changes
      setHasReview(false);
      setExistingReview(null);
    }
  }, [order?.orderId, order?.status]);

  const loadOrder = async () => {
    try {
      setLoading(true);
      const response = await api.get<Order>(
        `/MobileBff/orders/${params.orderId}`,
      );
      setOrder(response.data);
    } catch (error: any) {
      console.error("Error loading order:", error);
      setOrder(null);
    } finally {
      setLoading(false);
    }
  };

  const getStatusColor = (status: string): string => {
    switch (status) {
      case "Pending":
        return "#fff3cd";
      case "Confirmed":
        return "#d1ecf1";
      case "Preparing":
        return "#d4edda";
      case "Ready":
        return "#cce5ff";
      case "OutForDelivery":
        return "#e2e3e5";
      case "Delivered":
      case "Completed":
        return "#d4edda";
      case "Cancelled":
        return "#f8d7da";
      default:
        return "#e9ecef";
    }
  };

  const getStatusTextColor = (status: string): string => {
    switch (status) {
      case "Pending":
        return "#856404";
      case "Confirmed":
        return "#0c5460";
      case "Preparing":
        return "#155724";
      case "Ready":
        return "#004085";
      case "OutForDelivery":
        return "#383d41";
      case "Delivered":
      case "Completed":
        return "#155724";
      case "Cancelled":
        return "#721c24";
      default:
        return "#495057";
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

  const isPastOrder = (status: string): boolean => {
    return ["Delivered", "Completed", "Cancelled", "Refunded"].includes(status);
  };

  const handleReorder = async () => {
    if (!order) return;

    try {
      setReordering(true);

      // Get or create cart for this restaurant
      let cartId: string;
      try {
        const existingCart = await cartService.getCart();
        if (existingCart && existingCart.restaurantId === order.restaurantId) {
          // Use existing cart if it's for the same restaurant
          cartId = existingCart.cartId;
        } else {
          // Create new cart for this restaurant
          cartId = await cartService.createCart(order.restaurantId);
        }
      } catch (error: any) {
        // If no cart exists, create one
        cartId = await cartService.createCart(order.restaurantId);
      }

      // Add all items from the order to the cart
      for (const item of order.items) {
        await cartService.addItemToCart(
          cartId,
          item.menuItemId,
          item.name,
          item.unitPrice,
          item.quantity,
        );
      }

      Alert.alert(
        "Items Added to Cart",
        "All items from this order have been added to your cart.",
        [
          {
            text: "View Cart",
            onPress: () => router.push("/(tabs)/cart"),
            style: "default",
          },
          {
            text: "OK",
            style: "cancel",
          },
        ],
      );
    } catch (error: any) {
      console.error("Error reordering:", error);
      Alert.alert(
        "Error",
        error.response?.data?.error ||
          error.message ||
          "Failed to add items to cart. Please try again.",
        [{ text: "OK" }],
      );
    } finally {
      setReordering(false);
    }
  };

  const checkForExistingReview = async () => {
    if (!order) return;

    try {
      const token = await authService.getAccessToken();
      if (!token) {
        console.log("No auth token, skipping review check");
        setHasReview(false);
        setExistingReview(null);
        return;
      }

      const response = await api.get<Review[]>(
        "/MobileBff/reviews/me?skip=0&take=100",
        {
          headers: { Authorization: `Bearer ${token}` },
        },
      );

      if (response.data) {
        const review = response.data.find((r) => r.orderId === order.orderId);
        if (review) {
          console.log("Found existing review:", review.reviewId);
          setExistingReview(review);
          setHasReview(true);
        } else {
          console.log("No existing review found for order:", order.orderId);
          setHasReview(false);
          setExistingReview(null);
        }
      }
    } catch (error: any) {
      console.error("Error checking for existing review:", error);
      setHasReview(false);
      setExistingReview(null);
    }
  };

  const handleReviewSubmitted = async () => {
    setShowEditForm(false);
    await checkForExistingReview();
    Alert.alert("Success", "Review submitted successfully!");
  };

  const retryPayment = async () => {
    if (!order) return;

    try {
      setRetryingPayment(true);
      const res = await api.post<{ orderId: string; checkoutUrl?: string }>(
        `/MobileBff/orders/${order.orderId}/retry-payment`,
      );
      const checkoutUrl = (res.data as any)?.checkoutUrl;
      if (typeof checkoutUrl !== "string" || !checkoutUrl.trim()) {
        Alert.alert(
          "Payment",
          "Could not start payment retry. Please try again in a moment.",
        );
        return;
      }

      await Linking.openURL(checkoutUrl);
      Alert.alert(
        "Complete payment",
        "Complete your payment in the browser, then return to the app.",
        [{ text: "OK", onPress: () => loadOrder() }],
      );
    } catch (e: any) {
      const msg =
        e?.response?.data?.message ||
        e?.response?.data?.error ||
        e?.message ||
        "Failed to retry payment.";
      Alert.alert("Payment", String(msg));
    } finally {
      setRetryingPayment(false);
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
            The order you're looking for doesn't exist or you don't have
            permission to view it.
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
    ? [...order.statusHistory].sort(
        (a, b) =>
          new Date(a.changedAt).getTime() - new Date(b.changedAt).getTime(),
      )
    : [];

  const canReview =
    order && (order.status === "Delivered" || order.status === "Completed");

  return (
    <SafeAreaView style={styles.container} edges={["top"]}>
      <AppHeader title="Order Details" />

      {/* Tabs */}
      {canReview && (
        <View style={styles.tabsContainer}>
          <TouchableOpacity
            style={[styles.tab, activeTab === "details" && styles.tabActive]}
            onPress={() => setActiveTab("details")}
          >
            <Ionicons
              name="receipt-outline"
              size={20}
              color={activeTab === "details" ? "#6200ee" : "#666"}
            />
            <Text
              style={[
                styles.tabText,
                activeTab === "details" && styles.tabTextActive,
              ]}
            >
              Order Details
            </Text>
          </TouchableOpacity>
          <TouchableOpacity
            style={[styles.tab, activeTab === "review" && styles.tabActive]}
            onPress={() => setActiveTab("review")}
          >
            <Ionicons
              name="star"
              size={20}
              color={activeTab === "review" ? "#6200ee" : "#666"}
            />
            <Text
              style={[
                styles.tabText,
                activeTab === "review" && styles.tabTextActive,
              ]}
            >
              Review
              {hasReview && <Text style={styles.tabBadge}> ✓</Text>}
            </Text>
          </TouchableOpacity>
        </View>
      )}

      <ScrollView
        style={styles.scrollView}
        contentContainerStyle={styles.contentContainer}
      >
        {activeTab === "details" || !canReview ? (
          <>
            {/* Order Info Card */}
            <View style={styles.card}>
              <View style={styles.orderHeader}>
                <View style={styles.orderInfo}>
                  <Text style={styles.orderId}>
                    Order #{order.orderId.substring(0, 8)}
                  </Text>
                  {!!restaurantName.trim() && (
                    <Text style={styles.restaurantName}>{restaurantName}</Text>
                  )}
                  <Text style={styles.orderDate}>
                    Placed on{" "}
                    {new Date(order.createdAt).toLocaleDateString("en-US", {
                      month: "short",
                      day: "numeric",
                      year: "numeric",
                      hour: "2-digit",
                      minute: "2-digit",
                    })}
                  </Text>
                </View>
                <View
                  style={[
                    styles.statusBadge,
                    { backgroundColor: getStatusColor(order.status) },
                  ]}
                >
                  <Text
                    style={[
                      styles.statusText,
                      { color: getStatusTextColor(order.status) },
                    ]}
                  >
                    {order.status}
                  </Text>
                </View>
              </View>

              {!!order.paymentStatus && (
                <View style={styles.paymentRow}>
                  <Ionicons
                    name={
                      order.paymentStatus === "Succeeded"
                        ? "checkmark-circle"
                        : order.paymentStatus === "Failed"
                          ? "close-circle"
                          : "time"
                    }
                    size={18}
                    color={
                      order.paymentStatus === "Succeeded"
                        ? "#2e7d32"
                        : order.paymentStatus === "Failed"
                          ? "#c62828"
                          : "#666"
                    }
                  />
                  <Text style={styles.paymentText}>
                    Payment: {order.paymentStatus}
                  </Text>
                </View>
              )}

              {order.paymentStatus === "Failed" && (
                <View style={styles.paymentRetryContainer}>
                  {!!order.paymentFailureReason?.trim() && (
                    <Text style={styles.paymentFailureText}>
                      {order.paymentFailureReason}
                    </Text>
                  )}
                  <TouchableOpacity
                    style={[
                      styles.retryPaymentButton,
                      retryingPayment && styles.retryPaymentButtonDisabled,
                    ]}
                    onPress={retryPayment}
                    disabled={retryingPayment}
                  >
                    {retryingPayment ? (
                      <ActivityIndicator color="#fff" />
                    ) : (
                      <>
                        <Ionicons name="refresh" size={18} color="#fff" />
                        <Text style={styles.retryPaymentButtonText}>
                          Retry Payment
                        </Text>
                      </>
                    )}
                  </TouchableOpacity>
                </View>
              )}

              {order.estimatedDeliveryAt && (
                <View style={styles.infoRow}>
                  <Ionicons name="time-outline" size={20} color="#666" />
                  <Text style={styles.infoText}>
                    Estimated delivery:{" "}
                    {new Date(order.estimatedDeliveryAt).toLocaleDateString(
                      "en-US",
                      {
                        month: "short",
                        day: "numeric",
                        hour: "2-digit",
                        minute: "2-digit",
                      },
                    )}
                  </Text>
                </View>
              )}

              {order.specialInstructions && (
                <View style={[styles.infoRow, styles.specialInstructionsRow]}>
                  <Ionicons
                    name="information-circle-outline"
                    size={20}
                    color="#ffc107"
                  />
                  <View style={styles.specialInstructionsContent}>
                    <Text style={styles.specialInstructionsLabel}>
                      Special Instructions
                    </Text>
                    <Text
                      style={[styles.infoText, styles.specialInstructionsText]}
                    >
                      {order.specialInstructions}
                    </Text>
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
                        <View
                          style={[
                            styles.timelineDot,
                            { backgroundColor: "#6200ee" },
                          ]}
                        />
                        {index < sortedStatusHistory.length - 1 && (
                          <View style={styles.timelineConnector} />
                        )}
                      </View>
                      <View style={styles.timelineContent}>
                        <Text style={styles.timelineStatus}>
                          {statusEntry.status}
                        </Text>
                        <Text style={styles.timelineDate}>
                          {new Date(statusEntry.changedAt).toLocaleDateString(
                            "en-US",
                            {
                              month: "short",
                              day: "numeric",
                              hour: "2-digit",
                              minute: "2-digit",
                            },
                          )}
                        </Text>
                        {statusEntry.notes && (
                          <Text style={styles.timelineNotes}>
                            {statusEntry.notes}
                          </Text>
                        )}
                      </View>
                    </View>
                  ))}
                </View>
              </View>
            )}

            {/* Chat – opens on its own screen to save space */}
            <TouchableOpacity
              style={styles.chatCard}
              onPress={() => router.push(`/orders/chat/${params.orderId}`)}
              activeOpacity={0.7}
            >
              <Ionicons name="chatbubbles" size={24} color="#6200ee" />
              <View style={styles.chatCardText}>
                <Text style={styles.chatCardTitle}>Order Chat</Text>
                <Text style={styles.chatCardSubtitle}>
                  Message about this order
                </Text>
              </View>
              <Ionicons name="chevron-forward" size={22} color="#999" />
            </TouchableOpacity>

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
                          <Text style={styles.orderItemDescription}>
                            {item.description}
                          </Text>
                        )}
                        <Text style={styles.orderItemQuantity}>
                          Quantity: {item.quantity} × $
                          {item.unitPrice.toFixed(2)}
                        </Text>
                        {modifiers.length > 0 && (
                          <View style={styles.modifiersContainer}>
                            {modifiers.map((modifier, modIndex) => (
                              <View key={modIndex} style={styles.modifierBadge}>
                                <Text style={styles.modifierText}>
                                  {modifier}
                                </Text>
                              </View>
                            ))}
                          </View>
                        )}
                      </View>
                      <Text style={styles.orderItemPrice}>
                        ${item.totalPrice.toFixed(2)}
                      </Text>
                    </View>
                    {index < order.items.length - 1 && (
                      <View style={styles.divider} />
                    )}
                  </View>
                );
              })}
            </View>

            {/* Reorder Button - Only show for past orders */}
            {isPastOrder(order.status) && (
              <TouchableOpacity
                style={[
                  styles.reorderButton,
                  reordering && styles.reorderButtonDisabled,
                ]}
                onPress={handleReorder}
                disabled={reordering}
              >
                {reordering ? (
                  <ActivityIndicator color="#fff" />
                ) : (
                  <>
                    <Ionicons name="repeat" size={20} color="#fff" />
                    <Text style={styles.reorderButtonText}>Reorder</Text>
                  </>
                )}
              </TouchableOpacity>
            )}

            {/* Order Summary */}
            <View style={styles.card}>
              <Text style={styles.cardTitle}>Order Summary</Text>
              <View style={styles.summaryRow}>
                <Text style={styles.summaryLabel}>Subtotal</Text>
                <Text style={styles.summaryValue}>
                  ${order.subtotal.toFixed(2)}
                </Text>
              </View>
              <View style={styles.summaryRow}>
                <Text style={styles.summaryLabel}>Tax</Text>
                <Text style={styles.summaryValue}>${order.tax.toFixed(2)}</Text>
              </View>
              <View style={styles.summaryRow}>
                <Text style={styles.summaryLabel}>Delivery Fee</Text>
                <Text style={styles.summaryValue}>
                  ${order.deliveryFee.toFixed(2)}
                </Text>
              </View>
              <View style={styles.summaryRow}>
                <Text style={styles.summaryLabel}>Service Fee</Text>
                <Text style={styles.summaryValue}>
                  ${(order.serviceFee ?? 0).toFixed(2)}
                </Text>
              </View>
              <View style={styles.summaryDivider} />
              <View style={styles.summaryRow}>
                <Text style={styles.summaryTotalLabel}>Total</Text>
                <Text style={styles.summaryTotalValue}>
                  ${order.total.toFixed(2)}
                </Text>
              </View>
            </View>
          </>
        ) : (
          <>
            {/* Review Tab Content */}
            {!hasReview && (
              <View style={styles.card}>
                <Text style={styles.cardTitle}>Review Your Order</Text>
                <Text style={styles.reviewSubtitle}>
                  Share your experience and help others discover great food!
                </Text>
                <ReviewForm
                  orderId={order.orderId}
                  restaurantId={order.restaurantId}
                  onReviewSubmitted={handleReviewSubmitted}
                />
              </View>
            )}

            {hasReview && existingReview && (
              <View style={styles.card}>
                <View style={styles.reviewHeader}>
                  <Text style={styles.cardTitle}>Your Review</Text>
                  {!showEditForm && (
                    <TouchableOpacity
                      onPress={() => setShowEditForm(true)}
                      style={styles.editButton}
                    >
                      <Ionicons
                        name="create-outline"
                        size={18}
                        color="#6200ee"
                      />
                      <Text style={styles.editButtonText}>Edit</Text>
                    </TouchableOpacity>
                  )}
                </View>
                {!showEditForm ? (
                  <ReviewDisplay reviews={[existingReview]} />
                ) : (
                  <ReviewForm
                    orderId={order.orderId}
                    restaurantId={order.restaurantId}
                    existingReviewId={existingReview.reviewId}
                    onReviewSubmitted={handleReviewSubmitted}
                    onCancel={() => setShowEditForm(false)}
                  />
                )}
              </View>
            )}
          </>
        )}
      </ScrollView>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: "#f5f5f5",
  },
  scrollView: {
    flex: 1,
  },
  contentContainer: {
    padding: 16,
  },
  tabsContainer: {
    flexDirection: "row",
    backgroundColor: "#fff",
    borderBottomWidth: 1,
    borderBottomColor: "#e0e0e0",
    paddingHorizontal: 16,
  },
  tab: {
    flex: 1,
    flexDirection: "row",
    alignItems: "center",
    justifyContent: "center",
    paddingVertical: 16,
    borderBottomWidth: 2,
    borderBottomColor: "transparent",
    gap: 6,
  },
  tabActive: {
    borderBottomColor: "#6200ee",
  },
  tabText: {
    fontSize: 16,
    fontWeight: "500",
    color: "#666",
  },
  tabTextActive: {
    color: "#6200ee",
    fontWeight: "600",
  },
  tabBadge: {
    fontSize: 14,
    color: "#6200ee",
    fontWeight: "600",
  },
  loadingContainer: {
    flex: 1,
    justifyContent: "center",
    alignItems: "center",
    padding: 40,
  },
  loadingText: {
    marginTop: 10,
    fontSize: 16,
    color: "#666",
  },
  emptyContainer: {
    flex: 1,
    justifyContent: "center",
    alignItems: "center",
    padding: 40,
  },
  emptyText: {
    fontSize: 20,
    fontWeight: "600",
    color: "#333",
    marginTop: 16,
  },
  emptySubtext: {
    fontSize: 14,
    color: "#666",
    marginTop: 8,
    textAlign: "center",
  },
  backButton: {
    backgroundColor: "#6200ee",
    paddingHorizontal: 24,
    paddingVertical: 12,
    borderRadius: 8,
    marginTop: 20,
  },
  backButtonText: {
    color: "#fff",
    fontSize: 14,
    fontWeight: "600",
  },
  header: {
    flexDirection: "row",
    alignItems: "center",
    justifyContent: "space-between",
    padding: 16,
    backgroundColor: "#fff",
    borderBottomWidth: 1,
    borderBottomColor: "#e0e0e0",
  },
  backIcon: {
    width: 40,
    height: 40,
    justifyContent: "center",
    alignItems: "center",
  },
  headerTitle: {
    fontSize: 24,
    fontWeight: "bold",
    color: "#333",
  },
  card: {
    backgroundColor: "#fff",
    borderRadius: 12,
    padding: 16,
    marginBottom: 16,
    shadowColor: "#000",
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.1,
    shadowRadius: 4,
    elevation: 3,
  },
  orderHeader: {
    flexDirection: "row",
    justifyContent: "space-between",
    alignItems: "flex-start",
    marginBottom: 16,
  },
  paymentRow: {
    flexDirection: "row",
    alignItems: "center",
    gap: 8,
    marginTop: 6,
  },
  paymentText: {
    fontSize: 14,
    color: "#333",
    fontWeight: "600",
  },
  paymentRetryContainer: {
    marginTop: 10,
  },
  paymentFailureText: {
    color: "#c62828",
    fontSize: 13,
    marginBottom: 10,
  },
  retryPaymentButton: {
    backgroundColor: "#c62828",
    borderRadius: 10,
    paddingVertical: 12,
    paddingHorizontal: 14,
    flexDirection: "row",
    alignItems: "center",
    justifyContent: "center",
    gap: 8,
  },
  retryPaymentButtonDisabled: {
    opacity: 0.7,
  },
  retryPaymentButtonText: {
    color: "#fff",
    fontSize: 15,
    fontWeight: "700",
  },
  orderInfo: {
    flex: 1,
  },
  orderId: {
    fontSize: 20,
    fontWeight: "bold",
    color: "#333",
    marginBottom: 4,
  },
  restaurantName: {
    fontSize: 14,
    color: "#444",
    marginBottom: 6,
    fontWeight: "600",
  },
  orderDate: {
    fontSize: 14,
    color: "#666",
  },
  statusBadge: {
    paddingHorizontal: 12,
    paddingVertical: 6,
    borderRadius: 12,
  },
  statusText: {
    fontSize: 14,
    fontWeight: "500",
  },
  specialInstructionsRow: {
    backgroundColor: "#fff3cd",
    padding: 12,
    borderRadius: 8,
    borderLeftWidth: 4,
    borderLeftColor: "#ffc107",
    marginBottom: 8,
  },
  specialInstructionsContent: {
    flex: 1,
    marginLeft: 12,
  },
  specialInstructionsLabel: {
    fontSize: 12,
    fontWeight: "600",
    color: "#856404",
    marginBottom: 4,
  },
  specialInstructionsText: {
    fontStyle: "italic",
    color: "#856404",
  },
  infoRow: {
    flexDirection: "row",
    alignItems: "center",
    marginTop: 8,
    gap: 8,
  },
  infoText: {
    fontSize: 14,
    color: "#666",
    flex: 1,
  },
  cardTitle: {
    fontSize: 18,
    fontWeight: "bold",
    color: "#333",
    marginBottom: 12,
  },
  reviewSubtitle: {
    fontSize: 14,
    color: "#666",
    marginBottom: 16,
    fontStyle: "italic",
  },
  chatCard: {
    flexDirection: "row",
    alignItems: "center",
    backgroundColor: "#fff",
    borderRadius: 12,
    padding: 16,
    marginBottom: 16,
    shadowColor: "#000",
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.1,
    shadowRadius: 4,
    elevation: 3,
  },
  chatCardText: {
    flex: 1,
    marginLeft: 12,
  },
  chatCardTitle: {
    fontSize: 16,
    fontWeight: "600",
    color: "#333",
  },
  chatCardSubtitle: {
    fontSize: 13,
    color: "#666",
    marginTop: 2,
  },
  timeline: {
    marginTop: 8,
  },
  timelineItem: {
    flexDirection: "row",
    marginBottom: 16,
  },
  timelineLine: {
    alignItems: "center",
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
    backgroundColor: "#ddd",
    marginTop: 4,
    minHeight: 40,
  },
  timelineContent: {
    flex: 1,
  },
  timelineStatus: {
    fontSize: 16,
    fontWeight: "500",
    color: "#333",
    marginBottom: 4,
  },
  timelineDate: {
    fontSize: 14,
    color: "#666",
    marginBottom: 2,
  },
  timelineNotes: {
    fontSize: 14,
    color: "#666",
    fontStyle: "italic",
  },
  orderItem: {
    flexDirection: "row",
    justifyContent: "space-between",
    alignItems: "flex-start",
    marginBottom: 12,
  },
  orderItemInfo: {
    flex: 1,
    marginRight: 12,
  },
  orderItemName: {
    fontSize: 16,
    fontWeight: "500",
    color: "#333",
    marginBottom: 4,
  },
  orderItemDescription: {
    fontSize: 14,
    color: "#666",
    marginBottom: 4,
  },
  orderItemQuantity: {
    fontSize: 14,
    color: "#666",
    marginBottom: 4,
  },
  modifiersContainer: {
    flexDirection: "row",
    flexWrap: "wrap",
    gap: 6,
    marginTop: 4,
  },
  modifierBadge: {
    backgroundColor: "#f0f0f0",
    paddingHorizontal: 8,
    paddingVertical: 4,
    borderRadius: 4,
  },
  modifierText: {
    fontSize: 12,
    color: "#666",
  },
  orderItemPrice: {
    fontSize: 16,
    fontWeight: "bold",
    color: "#333",
  },
  divider: {
    height: 1,
    backgroundColor: "#eee",
    marginVertical: 12,
  },
  summaryRow: {
    flexDirection: "row",
    justifyContent: "space-between",
    marginBottom: 8,
  },
  summaryLabel: {
    fontSize: 16,
    color: "#666",
  },
  summaryValue: {
    fontSize: 16,
    color: "#333",
  },
  summaryDivider: {
    height: 1,
    backgroundColor: "#eee",
    marginVertical: 12,
  },
  summaryTotalLabel: {
    fontSize: 18,
    fontWeight: "bold",
    color: "#333",
  },
  summaryTotalValue: {
    fontSize: 18,
    fontWeight: "bold",
    color: "#6200ee",
  },
  reorderButton: {
    flexDirection: "row",
    alignItems: "center",
    justifyContent: "center",
    backgroundColor: "#6200ee",
    paddingVertical: 16,
    paddingHorizontal: 24,
    borderRadius: 12,
    marginBottom: 16,
    gap: 8,
  },
  reorderButtonDisabled: {
    opacity: 0.6,
  },
  reorderButtonText: {
    color: "#fff",
    fontSize: 16,
    fontWeight: "600",
  },
  reviewHeader: {
    flexDirection: "row",
    justifyContent: "space-between",
    alignItems: "center",
    marginBottom: 12,
  },
  editButton: {
    flexDirection: "row",
    alignItems: "center",
    gap: 4,
    paddingHorizontal: 12,
    paddingVertical: 6,
    borderRadius: 8,
    backgroundColor: "#f0f0f0",
  },
  editButtonText: {
    fontSize: 14,
    fontWeight: "500",
    color: "#6200ee",
  },
});
