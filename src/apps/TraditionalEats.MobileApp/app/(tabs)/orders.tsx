import React, {
  useCallback,
  useEffect,
  useMemo,
  useState,
  useRef,
} from "react";
import {
  View,
  Text,
  StyleSheet,
  FlatList,
  ActivityIndicator,
  TouchableOpacity,
  RefreshControl,
  Keyboard,
} from "react-native";
import { Ionicons } from "@expo/vector-icons";
import { useRouter, useLocalSearchParams, useFocusEffect } from "expo-router";
import { api } from "../../services/api";
import { authService } from "../../services/auth";
import BottomSearchBar from "../../components/BottomSearchBar";

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

type OrderFilter = "all" | "active" | "past";

export default function OrdersScreen() {
  const router = useRouter();
  const params = useLocalSearchParams<{ refresh?: string }>();

  const [orders, setOrders] = useState<Order[]>([]);
  const [loading, setLoading] = useState(true);
  const [isAuthenticated, setIsAuthenticated] = useState(false);
  const [checkingAuth, setCheckingAuth] = useState(true);
  const [filter, setFilter] = useState<OrderFilter>("all");
  const [searchQuery, setSearchQuery] = useState("");

  // ✅ Pull-to-refresh state
  const [refreshing, setRefreshing] = useState(false);

  // Track if we've already redirected to prevent double redirects
  const hasRedirectedRef = useRef(false);
  const isNavigatingRef = useRef(false);

  // Check authentication status (only on mount)
  useEffect(() => {
    let isMounted = true;

    (async () => {
      try {
        const authenticated = await authService.isAuthenticated();

        if (!isMounted) return;

        setIsAuthenticated(authenticated);

        if (
          !authenticated &&
          !hasRedirectedRef.current &&
          !isNavigatingRef.current
        ) {
          // Redirect to login if not authenticated (only once)
          hasRedirectedRef.current = true;
          isNavigatingRef.current = true;
          // Use replace to avoid stacking - login is no longer a modal
          router.replace("/login");
          return;
        }
      } catch (error) {
        console.error("Error checking authentication:", error);
        if (!isMounted) return;

        if (!hasRedirectedRef.current && !isNavigatingRef.current) {
          hasRedirectedRef.current = true;
          isNavigatingRef.current = true;
          router.replace("/login");
        }
      } finally {
        if (isMounted) {
          setCheckingAuth(false);
        }
      }
    })();

    return () => {
      isMounted = false;
    };
  }, [router]);

  const loadOrders = useCallback(async () => {
    // Don't load orders if not authenticated
    if (!isAuthenticated) {
      return;
    }

    try {
      const response = await api.get<Order[]>("/MobileBff/orders");
      setOrders(response.data || []);
    } catch (error: any) {
      console.error("Error loading orders:", error);

      // If 401 Unauthorized, redirect to login (only if not already redirected)
      if (error.response?.status === 401) {
        setIsAuthenticated(false);
        if (!hasRedirectedRef.current && !isNavigatingRef.current) {
          hasRedirectedRef.current = true;
          isNavigatingRef.current = true;
          router.replace("/login");
        }
        return;
      }

      setOrders([]);
    }
  }, [isAuthenticated, router]);

  // ✅ Initial load (show full-screen loader only once)
  useEffect(() => {
    // Only load orders if authenticated and auth check is complete
    if (!checkingAuth && isAuthenticated) {
      (async () => {
        try {
          setLoading(true);
          await loadOrders();
        } finally {
          setLoading(false);
        }
      })();
    }
  }, [loadOrders, checkingAuth, isAuthenticated]);

  // ✅ Auto refresh whenever tab/screen becomes active (best UX)
  useFocusEffect(
    useCallback(() => {
      // Only check auth and refresh if we haven't already redirected
      if (hasRedirectedRef.current || isNavigatingRef.current) {
        return;
      }

      // Check auth status when screen comes into focus
      (async () => {
        try {
          const authenticated = await authService.isAuthenticated();
          setIsAuthenticated(authenticated);

          if (!authenticated) {
            if (!hasRedirectedRef.current && !isNavigatingRef.current) {
              hasRedirectedRef.current = true;
              isNavigatingRef.current = true;
              router.replace("/login");
            }
            return;
          }

          // Only refresh if authenticated
          if (authenticated) {
            loadOrders();
          }
        } catch (error) {
          console.error("Error checking authentication on focus:", error);
          if (!hasRedirectedRef.current && !isNavigatingRef.current) {
            hasRedirectedRef.current = true;
            isNavigatingRef.current = true;
            router.replace("/login");
          }
        }
      })();
    }, [loadOrders, router]),
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

  // ✅ Filter and sort orders
  const filteredAndSortedOrders = useMemo(() => {
    let filtered = [...orders];

    // Apply filter
    if (filter === "past") {
      // Past orders: Delivered, Cancelled, Refunded
      filtered = filtered.filter((order) =>
        ["Delivered", "Cancelled", "Refunded"].includes(order.status),
      );
    } else if (filter === "active") {
      // Active orders: everything except past orders
      filtered = filtered.filter(
        (order) =>
          !["Delivered", "Cancelled", "Refunded"].includes(order.status),
      );
    }
    // 'all' shows everything, no filtering needed

    // Apply search query
    if (searchQuery.trim()) {
      const query = searchQuery.toLowerCase().trim();
      filtered = filtered.filter((order) => {
        // Search by order ID (full ID and first 8 chars)
        const orderId = (order.orderId || "").toLowerCase();
        const orderIdShort = order.orderId ? order.orderId.substring(0, 8).toLowerCase() : "";
        // Search by status
        const status = (order.status || "").toLowerCase();
        // Search by delivery address
        const address = (order.deliveryAddress || "").toLowerCase();
        // Search by item names
        const itemNames = (order.items || [])
          .map((item) => (item.name || "").toLowerCase())
          .join(" ");
        
        // Check if query matches any field
        // For order ID: check both full ID and first 8 characters
        const matchesOrderId = 
          (orderId && orderId.includes(query)) || 
          (orderIdShort && orderIdShort.includes(query));
        // For status: exact match or contains
        const matchesStatus = status === query || (status && status.includes(query));
        // For address: contains
        const matchesAddress = address && address.includes(query);
        // For items: contains
        const matchesItems = itemNames && itemNames.includes(query);
        
        return matchesOrderId || matchesStatus || matchesAddress || matchesItems;
      });
    }

    // Sort by newest first
    return filtered.sort(
      (a, b) =>
        new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime(),
    );
  }, [orders, filter, searchQuery]);

  function getStatusColor(status: string): string {
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
        return "#d4edda";
      case "Cancelled":
        return "#f8d7da";
      default:
        return "#e9ecef";
    }
  }

  // Don't render anything if we've redirected (prevents double rendering)
  if (hasRedirectedRef.current || isNavigatingRef.current) {
    return null;
  }

  // Show loading while checking authentication
  if (checkingAuth || loading) {
    return (
      <View style={styles.container}>
        <View style={styles.emptyContainer}>
          <ActivityIndicator size="large" color="#6200ee" />
          <Text style={styles.loadingText}>
            {checkingAuth ? "Checking authentication..." : "Loading orders..."}
          </Text>
        </View>
      </View>
    );
  }

  // Don't render anything if not authenticated (should redirect)
  if (!isAuthenticated) {
    return null;
  }

  const EmptyState = (
    <View style={styles.emptyContainer}>
      <Ionicons name="receipt-outline" size={64} color="#ccc" />
      <Text style={styles.emptyText}>No orders yet</Text>
      <Text style={styles.emptySubtext}>
        Your order history will appear here
      </Text>

      <TouchableOpacity
        style={styles.browseButton}
        onPress={() => router.push("/(tabs)")}
      >
        <Text style={styles.browseButtonText}>Browse Restaurants</Text>
      </TouchableOpacity>

      <Text style={styles.pullHint}>Pull down to refresh</Text>
    </View>
  );

  return (
    <View style={styles.container}>
      {/* Search Query Indicator */}
      {searchQuery.trim() && (
        <View style={styles.searchIndicator}>
          <Text style={styles.searchIndicatorText}>
            Searching: "{searchQuery}"
          </Text>
          <TouchableOpacity
            onPress={() => setSearchQuery("")}
            style={styles.clearSearchIcon}
          >
            <Ionicons name="close-circle" size={20} color="#666" />
          </TouchableOpacity>
        </View>
      )}

      {/* Filter Tabs */}
      <View style={styles.filterContainer}>
        <TouchableOpacity
          style={[styles.filterTab, filter === "all" && styles.filterTabActive]}
          onPress={() => setFilter("all")}
        >
          <Text
            style={[
              styles.filterTabText,
              filter === "all" && styles.filterTabTextActive,
            ]}
          >
            All
          </Text>
        </TouchableOpacity>
        <TouchableOpacity
          style={[
            styles.filterTab,
            filter === "active" && styles.filterTabActive,
          ]}
          onPress={() => setFilter("active")}
        >
          <Text
            style={[
              styles.filterTabText,
              filter === "active" && styles.filterTabTextActive,
            ]}
          >
            Active
          </Text>
        </TouchableOpacity>
        <TouchableOpacity
          style={[
            styles.filterTab,
            filter === "past" && styles.filterTabActive,
          ]}
          onPress={() => setFilter("past")}
        >
          <Text
            style={[
              styles.filterTabText,
              filter === "past" && styles.filterTabTextActive,
            ]}
          >
            Past Orders
          </Text>
        </TouchableOpacity>
      </View>

      {filteredAndSortedOrders.length === 0 ? (
        <FlatList
          data={[]}
          renderItem={null as any}
          ListEmptyComponent={EmptyState}
          refreshControl={
            <RefreshControl refreshing={refreshing} onRefresh={onRefresh} />
          }
          contentContainerStyle={{ flexGrow: 1 }}
        />
      ) : (
        <FlatList
          data={filteredAndSortedOrders}
          keyExtractor={(item) => item.orderId}
          contentContainerStyle={{ paddingBottom: 100 }}
          refreshControl={
            <RefreshControl refreshing={refreshing} onRefresh={onRefresh} />
          }
          renderItem={({ item }) => (
            <TouchableOpacity
              style={styles.orderCard}
              onPress={() => router.push(`/orders/${item.orderId}`)}
              activeOpacity={0.85}
            >
              <View style={styles.orderHeader}>
                <Text style={styles.orderId}>
                  Order #{item.orderId.substring(0, 8)}
                </Text>
                <Text style={styles.orderDate}>
                  {new Date(item.createdAt).toLocaleDateString("en-US", {
                    month: "short",
                    day: "numeric",
                    year: "numeric",
                    hour: "2-digit",
                    minute: "2-digit",
                  })}
                </Text>
              </View>

              <View style={styles.orderItems}>
                {item.items.slice(0, 2).map((orderItem) => (
                  <Text
                    key={orderItem.orderItemId}
                    style={styles.orderItemText}
                  >
                    {orderItem.name} x {orderItem.quantity}
                  </Text>
                ))}
                {item.items.length > 2 && (
                  <Text style={styles.orderItemText}>
                    +{item.items.length - 2} more items
                  </Text>
                )}
              </View>

              <View style={styles.orderFooter}>
                <View
                  style={[
                    styles.statusBadge,
                    { backgroundColor: getStatusColor(item.status) },
                  ]}
                >
                  <Text style={styles.orderStatus}>{item.status}</Text>
                </View>
                <Text style={styles.orderTotal}>${item.total.toFixed(2)}</Text>
              </View>
            </TouchableOpacity>
          )}
        />
      )}
      <BottomSearchBar
        onSearch={(query) => {
          Keyboard.dismiss();
          setSearchQuery(query);
        }}
        onClear={() => {
          setSearchQuery("");
        }}
        placeholder="Search orders..."
        emptyStateTitle="Search orders"
        emptyStateSubtitle="Search by order ID, status, items, or address"
        loadSuggestions={async (query) => {
          // Return order IDs, statuses, and item names as suggestions
          const suggestions: string[] = [];
          const queryLower = query.toLowerCase();

          // Get unique statuses
          const statuses = Array.from(new Set(orders.map((o) => o.status)))
            .filter((s) => s.toLowerCase().includes(queryLower))
            .slice(0, 3);
          suggestions.push(...statuses);

          // Get order IDs
          orders.forEach((order) => {
            const orderId = order.orderId.substring(0, 8);
            if (
              orderId.toLowerCase().includes(queryLower) &&
              suggestions.length < 10
            ) {
              suggestions.push(`Order #${orderId}`);
            }
          });

          // Get item names
          orders.forEach((order) => {
            order.items.forEach((item) => {
              if (
                item.name.toLowerCase().includes(queryLower) &&
                !suggestions.includes(item.name) &&
                suggestions.length < 10
              ) {
                suggestions.push(item.name);
              }
            });
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
          // Set search query immediately - this will trigger filtering
          setSearchQuery(searchTerm);
        }}
      />
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: "#f5f5f5" },

  emptyContainer: {
    flex: 1,
    justifyContent: "center",
    alignItems: "center",
    padding: 40,
  },

  emptyText: { fontSize: 20, fontWeight: "600", color: "#333", marginTop: 16 },
  emptySubtext: {
    fontSize: 14,
    color: "#666",
    marginTop: 8,
    textAlign: "center",
  },

  loadingText: { marginTop: 10, fontSize: 16, color: "#666" },


  pullHint: { marginTop: 14, fontSize: 12, color: "#999", textAlign: "center" },
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
  clearSearchButton: {
    backgroundColor: "#6200ee",
    paddingHorizontal: 24,
    paddingVertical: 12,
    borderRadius: 8,
    marginTop: 20,
  },
  clearSearchButtonText: { color: "#fff", fontSize: 14, fontWeight: "600" },

  orderId: { fontSize: 18, fontWeight: "600", color: "#333" },

  orderItems: { marginVertical: 8 },
  orderItemText: { fontSize: 14, color: "#666", marginBottom: 4 },

  statusBadge: { paddingHorizontal: 12, paddingVertical: 4, borderRadius: 12 },

  orderCard: {
    backgroundColor: "#fff",
    borderRadius: 12,
    padding: 16,
    margin: 16,
    shadowColor: "#000",
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.1,
    shadowRadius: 4,
    elevation: 3,
  },

  orderHeader: {
    flexDirection: "row",
    justifyContent: "space-between",
    marginBottom: 12,
  },

  orderDate: { fontSize: 14, color: "#666" },

  orderFooter: {
    flexDirection: "row",
    justifyContent: "space-between",
    alignItems: "center",
  },

  // keep your existing look (purple text)
  orderStatus: { fontSize: 14, color: "#6200ee", fontWeight: "500" },

  orderTotal: { fontSize: 18, fontWeight: "bold", color: "#333" },

  filterContainer: {
    flexDirection: "row",
    backgroundColor: "#fff",
    paddingHorizontal: 16,
    paddingVertical: 8,
    borderBottomWidth: 1,
    borderBottomColor: "#e0e0e0",
    gap: 8,
  },
  filterTab: {
    flex: 1,
    paddingVertical: 10,
    paddingHorizontal: 16,
    borderRadius: 8,
    alignItems: "center",
    backgroundColor: "#f5f5f5",
  },
  filterTabActive: {
    backgroundColor: "#6200ee",
  },
  filterTabText: {
    fontSize: 14,
    fontWeight: "500",
    color: "#666",
  },
  filterTabTextActive: {
    color: "#fff",
    fontWeight: "600",
  },
});
