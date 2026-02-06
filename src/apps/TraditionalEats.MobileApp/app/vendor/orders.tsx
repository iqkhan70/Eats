import React, {
  useEffect,
  useMemo,
  useRef,
  useState,
  useCallback,
} from "react";
import {
  View,
  Text,
  StyleSheet,
  ScrollView,
  TouchableOpacity,
  ActivityIndicator,
  RefreshControl,
  Alert,
  KeyboardAvoidingView,
  Platform,
  Keyboard,
  TouchableWithoutFeedback,
} from "react-native";
import { useRouter, useLocalSearchParams } from "expo-router";
import axios from "axios";
import { authService } from "../../services/auth";
import { api } from "../../services/api";
import BottomSearchBar from "../../components/BottomSearchBar";

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
  serviceFee?: number;
  items: OrderItem[];
  statusHistory?: OrderStatusHistory[];
}

const statusOptions = [
  "Pending",
  "Preparing",
  "Ready",
  "Completed",
  "Cancelled",
] as const;
type OrderStatus = (typeof statusOptions)[number];

const allowedNextStatuses: Record<OrderStatus, OrderStatus[]> = {
  Pending: ["Preparing", "Cancelled"],
  Preparing: ["Ready", "Cancelled"],
  Ready: ["Completed", "Cancelled"],
  Completed: [],
  Cancelled: [],
};

const getStatusColor = (status: string): string => {
  switch (status) {
    case "Pending":
      return "#FFA726";
    case "Preparing":
      return "#42A5F5";
    case "Ready":
      return "#66BB6A";
    case "Completed":
      return "#78909C";
    case "Cancelled":
      return "#EF5350";
    default:
      return "#78909C";
  }
};

function safeJsonParse(input: unknown) {
  if (typeof input !== "string") return input;
  try {
    return JSON.parse(input);
  } catch {
    return input;
  }
}

/** Pretty-print for RN console (avoids truncation) */
function logJson(label: string, obj: any) {
  try {
    console.log(label + ":\n" + JSON.stringify(obj, null, 2));
  } catch {
    console.log(label + ":", obj);
  }
}

function extractAspNetValidationErrors(data: any): string | null {
  const errors = data?.errors;
  if (!errors || typeof errors !== "object") return null;

  const lines: string[] = [];
  for (const key of Object.keys(errors)) {
    const arr = errors[key];
    if (Array.isArray(arr)) {
      for (const msg of arr) lines.push(`${key}: ${msg}`);
    }
  }
  return lines.length ? lines.join("\n") : null;
}

function normalizeErrorMessage(err: unknown): string {
  if (axios.isAxiosError(err)) {
    const data = err.response?.data as any;

    const modelErrors = extractAspNetValidationErrors(data);
    if (modelErrors) return modelErrors;

    if (typeof data === "string" && data.trim()) return data;
    if (data?.message) return String(data.message);
    if (data?.error) return String(data.error);
    if (data?.title && data?.detail) return `${data.title}\n${data.detail}`;
    if (data?.title) return String(data.title);
    if (data?.detail) return String(data.detail);

    return `Request failed (${err.response?.status ?? "no status"})`;
  }

  if (err instanceof Error) return err.message;
  return "Unexpected error";
}

/**
 * IMPORTANT:
 * Your backend expects:
 *   public record UpdateOrderStatusRequest(string Status, string? Notes);
 * So status MUST be a STRING. No numeric fallback.
 */
async function putUpdateOrderStatus(
  orderId: string,
  status: OrderStatus,
  notes?: string | null,
) {
  const payload = {
    status, // string only
    notes: notes ?? null,
  };

  const url = `/MobileBff/orders/${orderId}/status`;

  logJson("Updating order status (PUT)", { url, payload });

  await api.put(url, payload, {
    headers: { "Content-Type": "application/json" },
  });
}

export default function VendorOrdersScreen() {
  const router = useRouter();
  const params = useLocalSearchParams<{ restaurantId?: string }>();

  const [isAuthenticated, setIsAuthenticated] = useState(false);
  const [isVendor, setIsVendor] = useState(false);

  const [restaurants, setRestaurants] = useState<Restaurant[]>([]);
  const [orders, setOrders] = useState<Order[]>([]);

  const [selectedRestaurantId, setSelectedRestaurantId] = useState<
    string | null
  >(params.restaurantId || null);

  const [loadingRestaurants, setLoadingRestaurants] = useState(true);
  const [loadingOrders, setLoadingOrders] = useState(false);
  const [refreshing, setRefreshing] = useState(false);
  const [updatingStatus, setUpdatingStatus] = useState<string | null>(null);

  // ✅ SEARCH
  const [searchText, setSearchText] = useState("");
  const [searchTextDebounced, setSearchTextDebounced] = useState("");

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

  // ✅ SEARCH: tiny debounce
  useEffect(() => {
    const t = setTimeout(() => {
      setSearchTextDebounced(searchText);
    }, 150);
    return () => clearTimeout(t);
  }, [searchText]);

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
      console.error("Auth check failed:", err);
      Alert.alert("Error", "Failed to validate authentication.");
    }
  };

  const activeRestaurants = useMemo(() => {
    return restaurants.filter((r) => r.isActive);
  }, [restaurants]);

  const loadRestaurants = async () => {
    try {
      setLoadingRestaurants(true);
      const response = await api.get("/MobileBff/vendor/my-restaurants");

      const data = response.data ?? [];
      if (!isMountedRef.current) return;

      setRestaurants(Array.isArray(data) ? data : []);
    } catch (err) {
      console.error("Error loading restaurants:", err);
      Alert.alert("Error", normalizeErrorMessage(err));
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
          `/MobileBff/vendor/restaurants/${selectedRestaurantId}/orders`,
        );
        const data = response.data ?? [];
        allOrders = Array.isArray(data) ? data : [];
      } else {
        for (const restaurant of activeRestaurants) {
          try {
            const response = await api.get(
              `/MobileBff/vendor/restaurants/${restaurant.restaurantId}/orders`,
            );
            const data = response.data ?? [];
            if (Array.isArray(data)) allOrders.push(...data);
          } catch (innerErr) {
            console.error(
              `Error loading orders for restaurant ${restaurant.restaurantId}:`,
              innerErr,
            );
          }
        }
      }

      allOrders.sort(
        (a, b) =>
          new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime(),
      );

      if (!isMountedRef.current) return;
      setOrders(allOrders);
    } catch (err) {
      console.error("Error loading orders:", err);
      Alert.alert("Error", normalizeErrorMessage(err));
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

        logJson("Error updating order status (axios)", {
          status: err.response?.status,
          responseData: err.response?.data,
          url: err.config?.url,
          method: err.config?.method,
          dataSent: parsedSent,
          headersSent: err.config?.headers,
        });
      } else {
        console.error("Error updating order status:", err);
      }

      Alert.alert("Update failed", normalizeErrorMessage(err));
    } finally {
      if (isMountedRef.current) setUpdatingStatus(null);
    }
  };

  const showStatusPicker = (order: Order) => {
    const current = order.status as OrderStatus;

    if (!statusOptions.includes(current)) {
      Alert.alert("Cannot update", `Unknown order status: ${order.status}`);
      return;
    }

    const nextList = allowedNextStatuses[current];
    if (!nextList || nextList.length === 0) {
      Alert.alert("Cannot update", `Order is already ${order.status}.`);
      return;
    }

    Alert.alert("Update status", `Current: "${order.status}"`, [
      ...nextList.map((s) => ({
        text: s,
        onPress: () => void updateOrderStatus(order.orderId, s),
      })),
      { text: "Cancel", style: "cancel" },
    ]);
  };

  // ✅ Map restaurantId -> restaurant name
  const restaurantNameById = useMemo(() => {
    const map = new Map<string, string>();
    for (const r of restaurants) map.set(r.restaurantId, r.name ?? "");
    return map;
  }, [restaurants]);

  // ✅ Filter restaurant chips by search
  const filteredRestaurantsForChips = useMemo(() => {
    const q = searchTextDebounced.trim().toLowerCase();
    if (!q) return restaurants;

    const filtered = restaurants.filter((r) =>
      (r.name ?? "").toLowerCase().includes(q),
    );

    // keep selected visible even if it doesn't match
    if (selectedRestaurantId) {
      const selected = restaurants.find(
        (r) => r.restaurantId === selectedRestaurantId,
      );
      if (
        selected &&
        !filtered.some((r) => r.restaurantId === selected.restaurantId)
      ) {
        return [selected, ...filtered];
      }
    }

    return filtered;
  }, [restaurants, searchTextDebounced, selectedRestaurantId]);

  // ✅ Filter orders (also matches restaurant name)
  const filteredOrders = useMemo(() => {
    const q = searchTextDebounced.trim().toLowerCase();
    if (!q) return orders;

    return orders.filter((o) => {
      const orderId = (o.orderId ?? "").toLowerCase();
      const orderIdShort = o.orderId ? o.orderId.substring(0, 8).toLowerCase() : "";
      const status = (o.status ?? "").toLowerCase();
      const delivery = (o.deliveryAddress ?? "").toLowerCase();
      const notes = (o.specialInstructions ?? "").toLowerCase();

      const items = (o.items ?? [])
        .map((i) => (i.name ?? "").toLowerCase())
        .join(" ");

      const restaurantName = (
        restaurantNameById.get(o.restaurantId) ?? ""
      ).toLowerCase();

      // Check if query matches any field
      const matchesOrderId = orderId.includes(q) || orderIdShort.includes(q);
      const matchesStatus = status === q || status.includes(q);
      const matchesDelivery = delivery.includes(q);
      const matchesNotes = notes.includes(q);
      const matchesItems = items.includes(q);
      const matchesRestaurant = restaurantName.includes(q);

      return (
        matchesOrderId ||
        matchesStatus ||
        matchesDelivery ||
        matchesNotes ||
        matchesItems ||
        matchesRestaurant
      );
    });
  }, [orders, searchTextDebounced, restaurantNameById]);

  if (!isAuthenticated || !isVendor) {
    return (
      <View style={styles.container}>
        <Text style={styles.errorText}>
          You must be a vendor to access this page.
        </Text>
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
        <Text style={styles.errorText}>
          You don&apos;t have any restaurants yet.
        </Text>
      </View>
    );
  }

  return (
    <KeyboardAvoidingView
      style={styles.screen}
      behavior={Platform.OS === "ios" ? "padding" : undefined}
      keyboardVerticalOffset={Platform.OS === "ios" ? 90 : 0}
    >
      <TouchableWithoutFeedback onPress={Keyboard.dismiss} accessible={false}>
        <View style={styles.container}>
          <View style={styles.header}>
            <TouchableOpacity
              onPress={() => router.back()}
              style={styles.backButton}
            >
              <Text style={styles.backButtonText}>← Back</Text>
            </TouchableOpacity>
            <Text style={styles.title}>Vendor Orders</Text>
          </View>

          <ScrollView
            style={styles.scrollView}
            refreshControl={
              <RefreshControl refreshing={refreshing} onRefresh={onRefresh} />
            }
            keyboardShouldPersistTaps="handled"
            keyboardDismissMode={
              Platform.OS === "ios" ? "interactive" : "on-drag"
            }
            contentInsetAdjustmentBehavior="automatic"
          >
            {/* Search Query Indicator */}
            {searchTextDebounced.trim() && (
              <View style={styles.searchIndicator}>
                <Text style={styles.searchIndicatorText}>
                  Searching: "{searchTextDebounced}"
                </Text>
                <TouchableOpacity
                  onPress={() => {
                    setSearchText("");
                    setSearchTextDebounced("");
                  }}
                  style={styles.clearSearchIcon}
                >
                  <Text style={styles.clearSearchIconText}>✕</Text>
                </TouchableOpacity>
              </View>
            )}

            <View style={styles.filterContainer}>
              <View style={styles.filterHeaderRow}>
                <Text style={styles.filterLabel}>Filter by Restaurant:</Text>
                {!!searchTextDebounced && (
                  <Text style={styles.resultsText}>
                    {filteredOrders.length} result
                    {filteredOrders.length === 1 ? "" : "s"}
                  </Text>
                )}
              </View>

              <ScrollView
                horizontal
                showsHorizontalScrollIndicator={false}
                style={styles.restaurantFilter}
                keyboardShouldPersistTaps="handled"
              >
                <TouchableOpacity
                  style={[
                    styles.filterChip,
                    !selectedRestaurantId && styles.filterChipActive,
                  ]}
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

                {filteredRestaurantsForChips.map((restaurant) => (
                  <TouchableOpacity
                    key={restaurant.restaurantId}
                    style={[
                      styles.filterChip,
                      selectedRestaurantId === restaurant.restaurantId &&
                        styles.filterChipActive,
                    ]}
                    onPress={() =>
                      setSelectedRestaurantId(restaurant.restaurantId)
                    }
                  >
                    <Text
                      style={[
                        styles.filterChipText,
                        selectedRestaurantId === restaurant.restaurantId &&
                          styles.filterChipTextActive,
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
            ) : filteredOrders.length === 0 ? (
              <View style={styles.emptyContainer}>
                <Text style={styles.emptyText}>
                  {searchTextDebounced
                    ? "No matching orders"
                    : "No orders found"}
                </Text>
              </View>
            ) : (
              filteredOrders.map((order) => (
                <TouchableOpacity
                  key={order.orderId}
                  style={styles.orderCard}
                  onPress={() => router.push(`/orders/${order.orderId}`)}
                  activeOpacity={0.85}
                >
                  <View style={styles.orderHeader}>
                    <View>
                      <Text style={styles.orderId}>
                        Order #{order.orderId.substring(0, 8)}
                      </Text>
                      <Text style={styles.orderDate}>
                        {new Date(order.createdAt).toLocaleString()}
                      </Text>
                    </View>

                    <View
                      style={[
                        styles.statusBadge,
                        { backgroundColor: getStatusColor(order.status) },
                      ]}
                    >
                      <Text style={styles.statusText}>{order.status}</Text>
                    </View>
                  </View>

                  <View style={styles.orderItems}>
                    {order.items.map((item) => (
                      <Text key={item.orderItemId} style={styles.orderItem}>
                        {item.quantity}x {item.name} - $
                        {item.totalPrice.toFixed(2)}
                      </Text>
                    ))}
                  </View>

                  {order.specialInstructions && (
                    <View style={styles.specialInstructionsContainer}>
                      <Text style={styles.specialInstructionsLabel}>
                        Special Instructions:
                      </Text>
                      <Text style={styles.specialInstructionsText}>
                        {order.specialInstructions}
                      </Text>
                    </View>
                  )}

                  {order.deliveryAddress && (
                    <Text style={styles.deliveryAddress}>
                      📍 {order.deliveryAddress}
                    </Text>
                  )}

                  <View style={styles.orderFooter}>
                    <Text style={styles.orderTotal}>
                      Total: ${order.total.toFixed(2)}
                    </Text>

                    <TouchableOpacity
                      style={[
                        styles.statusButton,
                        { backgroundColor: getStatusColor(order.status) },
                        updatingStatus === order.orderId &&
                          styles.statusButtonDisabled,
                      ]}
                      onPress={() => showStatusPicker(order)}
                      disabled={updatingStatus === order.orderId}
                      activeOpacity={0.8}
                    >
                      {updatingStatus === order.orderId ? (
                        <ActivityIndicator size="small" color="white" />
                      ) : (
                        <Text style={styles.statusButtonText}>
                          Update Status
                        </Text>
                      )}
                    </TouchableOpacity>
                  </View>
                </TouchableOpacity>
              ))
            )}
          </ScrollView>
        </View>
      </TouchableWithoutFeedback>
      <BottomSearchBar
        onSearch={(query) => {
          Keyboard.dismiss();
          setSearchText(query);
          setSearchTextDebounced(query);
        }}
        onClear={() => {
          setSearchText("");
          setSearchTextDebounced("");
        }}
        placeholder="Search orders or restaurants..."
        emptyStateTitle="Search orders"
        emptyStateSubtitle="Search by order ID, restaurant name, or status"
        loadSuggestions={async (query) => {
          // Return restaurant names and order IDs as suggestions
          const suggestions: string[] = [];
          restaurants.forEach((r) => {
            if (r.name.toLowerCase().includes(query.toLowerCase())) {
              suggestions.push(r.name);
            }
          });
          orders.forEach((o) => {
            const orderId = o.orderId.substring(0, 8);
            if (orderId.toLowerCase().includes(query.toLowerCase())) {
              suggestions.push(`Order #${orderId}`);
            }
          });
          return suggestions.slice(0, 10);
        }}
        onSuggestionSelect={(suggestion) => {
          Keyboard.dismiss();
          // Extract the actual search term from suggestion
          // Remove "Order #" prefix if present
          let searchTerm = suggestion;
          if (suggestion.startsWith("Order #")) {
            searchTerm = suggestion.replace("Order #", "").trim();
          }
          // Set both searchText and searchTextDebounced immediately for instant filtering
          setSearchText(searchTerm);
          setSearchTextDebounced(searchTerm);
        }}
      />
    </KeyboardAvoidingView>
  );
}

const styles = StyleSheet.create({
  screen: {
    flex: 1,
    backgroundColor: "#F5F5F5",
  },
  container: {
    flex: 1,
    backgroundColor: "#F5F5F5",
  },
  header: {
    flexDirection: "row",
    alignItems: "center",
    padding: 16,
    backgroundColor: "white",
    borderBottomWidth: 1,
    borderBottomColor: "#E0E0E0",
  },
  backButton: {
    marginRight: 16,
  },
  backButtonText: {
    fontSize: 16,
    color: "#007AFF",
  },
  title: {
    fontSize: 20,
    fontWeight: "bold",
  },
  scrollView: {
    flex: 1,
  },

  // ✅ SEARCH STYLES
  searchContainer: {
    margin: 16,
    marginBottom: 8,
    backgroundColor: "white",
    borderRadius: 10,
    borderWidth: 1,
    borderColor: "#E0E0E0",
    paddingHorizontal: 12,
    paddingVertical: 10,
    flexDirection: "row",
    alignItems: "center",
  },
  searchInput: {
    flex: 1,
    fontSize: 14,
    color: "#111",
    padding: 0,
  },
  clearButton: {
    marginLeft: 10,
    width: 28,
    height: 28,
    borderRadius: 14,
    alignItems: "center",
    justifyContent: "center",
    backgroundColor: "#F0F0F0",
  },
  clearButtonText: {
    fontSize: 14,
    color: "#444",
    fontWeight: "700",
    lineHeight: 14,
  },

  filterContainer: {
    backgroundColor: "white",
    padding: 16,
    borderBottomWidth: 1,
    borderBottomColor: "#E0E0E0",
    paddingTop: 12,
  },
  filterHeaderRow: {
    flexDirection: "row",
    alignItems: "baseline",
    justifyContent: "space-between",
    marginBottom: 8,
  },
  resultsText: {
    fontSize: 12,
    color: "#666",
  },
  filterLabel: {
    fontSize: 14,
    fontWeight: "600",
    color: "#333",
  },
  restaurantFilter: {
    flexDirection: "row",
  },
  filterChip: {
    paddingHorizontal: 16,
    paddingVertical: 8,
    borderRadius: 20,
    backgroundColor: "#F0F0F0",
    marginRight: 8,
  },
  filterChipActive: {
    backgroundColor: "#007AFF",
  },
  filterChipText: {
    fontSize: 14,
    color: "#333",
  },
  filterChipTextActive: {
    color: "white",
  },
  loadingContainer: {
    padding: 32,
    alignItems: "center",
  },
  loadingText: {
    marginTop: 8,
    color: "#666",
  },
  emptyContainer: {
    padding: 32,
    alignItems: "center",
  },
  emptyText: {
    fontSize: 16,
    color: "#666",
  },
  orderCard: {
    backgroundColor: "white",
    margin: 16,
    padding: 16,
    borderRadius: 8,
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
    marginBottom: 12,
  },
  orderId: {
    fontSize: 16,
    fontWeight: "bold",
    color: "#333",
  },
  orderDate: {
    fontSize: 12,
    color: "#666",
    marginTop: 4,
  },
  statusBadge: {
    paddingHorizontal: 12,
    paddingVertical: 6,
    borderRadius: 16,
  },
  statusText: {
    color: "white",
    fontSize: 12,
    fontWeight: "600",
  },
  orderItems: {
    marginBottom: 12,
  },
  orderItem: {
    fontSize: 14,
    color: "#333",
    marginBottom: 4,
  },
  specialInstructionsContainer: {
    marginBottom: 10,
    backgroundColor: "#FAFAFA",
    padding: 10,
    borderRadius: 8,
    borderWidth: 1,
    borderColor: "#EFEFEF",
  },
  specialInstructionsLabel: {
    fontSize: 12,
    fontWeight: "700",
    color: "#444",
    marginBottom: 4,
  },
  specialInstructionsText: {
    fontSize: 12,
    color: "#555",
  },
  deliveryAddress: {
    fontSize: 12,
    color: "#666",
    marginBottom: 12,
  },
  orderFooter: {
    flexDirection: "row",
    justifyContent: "space-between",
    alignItems: "center",
    paddingTop: 12,
    borderTopWidth: 1,
    borderTopColor: "#E0E0E0",
  },
  orderTotal: {
    fontSize: 16,
    fontWeight: "bold",
    color: "#333",
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
    color: "white",
    fontSize: 12,
    fontWeight: "600",
  },
  errorText: {
    fontSize: 16,
    color: "#666",
    textAlign: "center",
    marginTop: 32,
  },
  searchIndicator: {
    flexDirection: "row",
    alignItems: "center",
    justifyContent: "space-between",
    backgroundColor: "#e3f2fd",
    paddingHorizontal: 16,
    paddingVertical: 10,
    borderBottomWidth: 1,
    borderBottomColor: "#e0e0e0",
  },
  searchIndicatorText: {
    fontSize: 14,
    color: "#1976d2",
    fontWeight: "500",
  },
  clearSearchIcon: {
    padding: 4,
  },
  clearSearchIconText: {
    fontSize: 18,
    color: "#666",
    fontWeight: "bold",
  },
});
