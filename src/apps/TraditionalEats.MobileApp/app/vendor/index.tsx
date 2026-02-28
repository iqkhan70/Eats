import React, { useMemo, useState, useEffect } from "react";
import {
  View,
  Text,
  StyleSheet,
  ScrollView,
  TouchableOpacity,
  Alert,
  ActivityIndicator,
  RefreshControl,
  Linking,
} from "react-native";
import { LinearGradient } from "expo-linear-gradient";
import { Ionicons } from "@expo/vector-icons";
import { useRouter, useFocusEffect } from "expo-router";
import { api } from "../../services/api";
import { authService } from "../../services/auth";
import BottomSearchBar from "../../components/BottomSearchBar";

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
}

export default function VendorDashboardScreen() {
  const router = useRouter();
  const [restaurants, setRestaurants] = useState<Restaurant[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [isAuthenticated, setIsAuthenticated] = useState(false);
  const [isVendor, setIsVendor] = useState(false);
  const [searchText, setSearchText] = useState("");
  const [searchTextDebounced, setSearchTextDebounced] = useState("");
  const [stripeOnboardingStatus, setStripeOnboardingStatus] = useState<
    string | null
  >(null);
  const [stripeConnecting, setStripeConnecting] = useState(false);

  const filteredRestaurants = useMemo(() => {
    const q = searchTextDebounced.trim().toLowerCase();
    if (!q) return restaurants;
    return restaurants.filter((r) => (r.name ?? "").toLowerCase().includes(q));
  }, [restaurants, searchTextDebounced]);

  useEffect(() => {
    const t = setTimeout(() => setSearchTextDebounced(searchText), 150);
    return () => clearTimeout(t);
  }, [searchText]);

  useEffect(() => {
    checkAuthAndLoad();
  }, []);

  // Reload restaurants and sync Stripe status when screen gains focus (e.g. after creating a restaurant or returning from Stripe onboarding)
  useFocusEffect(
    React.useCallback(() => {
      if (isAuthenticated && isVendor) {
        loadRestaurants();
        // Sync onboarding status from Stripe so we show Complete when user just finished in browser (webhook may be missed)
        api
          .post<{ status?: string }>(
            "/MobileBff/payments/vendor/refresh-onboarding-status",
          )
          .then((res) =>
            setStripeOnboardingStatus(res.data?.status ?? "Pending"),
          )
          .catch(() => loadStripeOnboardingStatus());
      }
    }, [isAuthenticated, isVendor]),
  );

  const checkAuthAndLoad = async () => {
    const authenticated = await authService.isAuthenticated();
    setIsAuthenticated(authenticated);

    if (authenticated) {
      const vendor = await authService.isVendor();
      setIsVendor(vendor);

      if (vendor) {
        await Promise.all([loadRestaurants(), loadStripeOnboardingStatus()]);
      } else {
        Alert.alert(
          "Access Denied",
          "You must be a vendor to access this page.",
        );
        router.back();
      }
    } else {
      Alert.alert(
        "Authentication Required",
        "Please log in to access the vendor dashboard.",
      );
      router.push("/login");
    }
  };

  const loadRestaurants = async () => {
    try {
      setLoading(true);
      const response = await api.get<Restaurant[]>(
        "/MobileBff/vendor/my-restaurants",
        { params: { __ts: Date.now() } },
      );
      setRestaurants(response.data || []);
    } catch (error: any) {
      // Don't log expected errors (axios interceptor handles logging)
      if (error.response?.status === 401) {
        Alert.alert("Session Expired", "Please log in again.");
        await authService.logout();
        router.push("/login");
      } else if (error.response?.status === 403) {
        Alert.alert(
          "Access Denied",
          "You do not have permission to access this page.",
        );
        router.back();
      } else {
        // Only show alert for unexpected errors
        const errorMessage =
          error.response?.data?.error ||
          error.message ||
          "Failed to load vendors. Please try again.";
        Alert.alert("Error", errorMessage);
      }
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  };

  const onRefresh = async () => {
    setRefreshing(true);
    try {
      await loadRestaurants();
      try {
        const res = await api.post<{ status?: string }>(
          "/MobileBff/payments/vendor/refresh-onboarding-status",
        );
        setStripeOnboardingStatus(res.data?.status ?? "Pending");
      } catch {
        await loadStripeOnboardingStatus();
      }
    } finally {
      setRefreshing(false);
    }
  };

  const loadStripeOnboardingStatus = async () => {
    try {
      const response = await api.get<{ status?: string }>(
        "/MobileBff/payments/vendor/onboarding-status",
      );
      setStripeOnboardingStatus(response.data?.status ?? "Pending");
    } catch {
      setStripeOnboardingStatus("Pending");
    }
  };

  const connectStripe = async () => {
    try {
      setStripeConnecting(true);
      const response = await api.post<{ url?: string }>(
        "/MobileBff/payments/vendor/connect-link",
      );
      const url = response.data?.url;
      if (url) {
        await Linking.openURL(url);
      } else {
        Alert.alert("Error", "Could not start Stripe setup.");
      }
    } catch (error: any) {
      const msg =
        error.response?.data?.error ||
        error.message ||
        "Failed to start Stripe setup.";
      Alert.alert("Error", msg);
    } finally {
      setStripeConnecting(false);
    }
  };

  const handleCreateRestaurant = () => {
    router.push("/vendor/create-restaurant");
  };

  const handleEditRestaurant = (restaurantId: string) => {
    router.push(`/vendor/restaurants/${restaurantId}/edit`);
  };

  const handleManageMenu = (restaurantId: string) => {
    router.push(`/vendor/restaurants/${restaurantId}/menu`);
  };

  const handleDeleteRestaurant = (restaurant: Restaurant) => {
    Alert.alert(
      "Delete Vendor",
      `Are you sure you want to delete "${restaurant.name}"?`,
      [
        { text: "Cancel", style: "cancel" },
        {
          text: "Delete",
          style: "destructive",
          onPress: async () => {
            try {
              await api.delete(
                `/MobileBff/vendor/restaurants/${restaurant.restaurantId}`,
              );
              Alert.alert("Success", "Vendor deleted successfully.");
              await loadRestaurants();
            } catch (error: any) {
              // Don't log expected errors (axios interceptor handles logging)
              const errorMessage =
                error.response?.data?.error ||
                error.message ||
                "Failed to delete vendor. Please try again.";
              Alert.alert("Error", errorMessage);
            }
          },
        },
      ],
    );
  };

  if (loading && restaurants.length === 0) {
    return (
      <View style={styles.centerContainer}>
        <ActivityIndicator size="large" color="#6200ee" />
        <Text style={styles.loadingText}>Loading vendors...</Text>
      </View>
    );
  }

  return (
    <View style={styles.container}>
      <ScrollView
        style={styles.container}
        refreshControl={
          <RefreshControl refreshing={refreshing} onRefresh={onRefresh} />
        }
        contentContainerStyle={styles.scrollContent}
      >
        <LinearGradient
          colors={['#f97316', '#eab308']}
          start={{ x: 0, y: 0 }}
          end={{ x: 1, y: 0 }}
          style={styles.header}
        >
          <TouchableOpacity
            onPress={() => router.back()}
            style={styles.backButton}
          >
            <Ionicons name="chevron-back" size={28} color="#fff" />
          </TouchableOpacity>
          <View style={styles.headerActions}>
            <TouchableOpacity
              onPress={() => router.push("/vendor/documents")}
              style={styles.iconButton}
            >
              <Ionicons name="document-text-outline" size={20} color="#fff" />
            </TouchableOpacity>
            <TouchableOpacity
              onPress={() => router.push("/vendor/orders")}
              style={styles.iconButton}
            >
              <Ionicons name="receipt-outline" size={20} color="#fff" />
            </TouchableOpacity>
            <TouchableOpacity
              onPress={() => router.push("/vendor/create-restaurant")}
              style={styles.iconButton}
            >
              <Ionicons name="add" size={20} color="#fff" />
            </TouchableOpacity>
          </View>
        </LinearGradient>

        {stripeOnboardingStatus && stripeOnboardingStatus !== "Complete" && (
          <View style={styles.stripeBanner}>
            <View style={styles.stripeBannerContent}>
              <Text style={styles.stripeBannerTitle}>
                Stripe setup incomplete
              </Text>
              <Text style={styles.stripeBannerText}>
                Complete the short Stripe Connect flow so you can accept paid
                orders. In test mode use Stripe's test data—no real bank details
                needed until you go live.
              </Text>
            </View>
            <TouchableOpacity
              style={[
                styles.stripeButton,
                stripeConnecting && styles.stripeButtonDisabled,
              ]}
              onPress={connectStripe}
              disabled={stripeConnecting}
            >
              {stripeConnecting ? (
                <ActivityIndicator size="small" color="#fff" />
              ) : (
                <Text style={styles.stripeButtonText}>Finish Stripe setup</Text>
              )}
            </TouchableOpacity>
          </View>
        )}

        {restaurants.length === 0 ? (
          <View style={styles.emptyContainer}>
            <Ionicons name="restaurant-outline" size={64} color="#ccc" />
            <Text style={styles.emptyText}>No vendors yet</Text>
            <Text style={styles.emptySubtext}>
              Create your first vendor to get started
            </Text>
            <TouchableOpacity
              style={styles.createButton}
              onPress={handleCreateRestaurant}
            >
              <Text style={styles.createButtonText}>Create Vendor</Text>
            </TouchableOpacity>
          </View>
        ) : (
          <View style={styles.restaurantsList}>
            {filteredRestaurants.map((restaurant) => (
              <View key={restaurant.restaurantId} style={styles.restaurantCard}>
                <View style={styles.restaurantHeader}>
                  <Text style={styles.restaurantName}>{restaurant.name}</Text>
                  <View
                    style={[
                      styles.statusBadge,
                      restaurant.isActive
                        ? styles.activeBadge
                        : styles.inactiveBadge,
                    ]}
                  >
                    <Text style={styles.statusText}>
                      {restaurant.isActive ? "Active" : "Inactive"}
                    </Text>
                  </View>
                </View>

                {restaurant.description && (
                  <Text style={styles.restaurantDescription} numberOfLines={2}>
                    {restaurant.description}
                  </Text>
                )}

                {restaurant.cuisineType && (
                  <Text style={styles.cuisineType}>
                    {restaurant.cuisineType}
                  </Text>
                )}

                <View style={styles.actionButtons}>
                  <TouchableOpacity
                    style={[styles.actionButton, styles.editButton]}
                    onPress={() =>
                      handleEditRestaurant(restaurant.restaurantId)
                    }
                  >
                    <Ionicons name="create-outline" size={18} color="#6200ee" />
                    <Text style={styles.editButtonText}>Edit</Text>
                  </TouchableOpacity>

                  <TouchableOpacity
                    style={[styles.actionButton, styles.menuButton]}
                    onPress={() => handleManageMenu(restaurant.restaurantId)}
                  >
                    <Ionicons
                      name="restaurant-outline"
                      size={18}
                      color="#fff"
                    />
                    <Text style={styles.menuButtonText}>Menu</Text>
                  </TouchableOpacity>

                  <TouchableOpacity
                    style={[styles.actionButton, styles.ordersButton]}
                    onPress={() =>
                      router.push(
                        `/vendor/orders?restaurantId=${restaurant.restaurantId}`,
                      )
                    }
                  >
                    <Ionicons name="receipt-outline" size={18} color="#fff" />
                    <Text style={styles.ordersButtonText}>Orders</Text>
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

        <TouchableOpacity style={styles.fab} onPress={handleCreateRestaurant}>
          <Ionicons name="add" size={28} color="#fff" />
        </TouchableOpacity>
      </ScrollView>

      <BottomSearchBar
        bottomOffset={160}
        onSearch={(query) => {
          setSearchText(query.trim());
          setSearchTextDebounced(query.trim());
        }}
        onClear={() => {
          setSearchText("");
          setSearchTextDebounced("");
        }}
        placeholder="Search vendors..."
        emptyStateTitle="Search vendors"
        emptyStateSubtitle="Search by vendor name"
        loadSuggestions={async (query) => {
          const q = query.trim().toLowerCase();
          if (!q) return [];
          const matches = restaurants
            .map((r) => r?.name ?? "")
            .filter((name) => name.trim().length > 0)
            .filter((name) => name.toLowerCase().includes(q))
            .sort((a, b) => a.localeCompare(b));
          return matches.slice(0, 10);
        }}
        onSuggestionSelect={(suggestion) => {
          setSearchText(suggestion);
          setSearchTextDebounced(suggestion);
        }}
        initialValue={searchText}
      />
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: "#f5f5f5",
  },
  scrollContent: {
    paddingBottom: 240,
  },
  centerContainer: {
    flex: 1,
    justifyContent: "center",
    alignItems: "center",
    backgroundColor: "#f5f5f5",
  },
  loadingText: {
    marginTop: 16,
    fontSize: 16,
    color: "#666",
  },
  header: {
    padding: 16,
    paddingTop: 60,
    flexDirection: "row",
    alignItems: "center",
    justifyContent: "space-between",
  },
  backButton: {
    padding: 8,
  },
  headerTitle: {
    fontSize: 20,
    fontWeight: "bold",
    color: "#fff",
    flex: 1,
    textAlign: "center",
  },
  headerButtons: {
    flexDirection: "row",
    gap: 8,
  },
  headerActions: {
    flexDirection: "row",
    gap: 12,
    alignItems: "center",
  },
  iconButton: {
    padding: 8,
  },
  addButton: {
    padding: 8,
  },
  stripeBanner: {
    backgroundColor: "#fff3cd",
    margin: 16,
    marginBottom: 0,
    padding: 16,
    borderRadius: 12,
    borderLeftWidth: 4,
    borderLeftColor: "#ffc107",
  },
  stripeBannerContent: {
    marginBottom: 12,
  },
  stripeBannerTitle: {
    fontSize: 16,
    fontWeight: "bold",
    color: "#000",
  },
  stripeBannerText: {
    fontSize: 14,
    color: "#333",
    marginTop: 4,
  },
  stripeButton: {
    backgroundColor: "#000",
    paddingVertical: 10,
    paddingHorizontal: 16,
    borderRadius: 8,
    alignItems: "center",
  },
  stripeButtonDisabled: {
    opacity: 0.6,
  },
  stripeButtonText: {
    color: "#fff",
    fontSize: 14,
    fontWeight: "600",
  },
  emptyContainer: {
    flex: 1,
    justifyContent: "center",
    alignItems: "center",
    padding: 32,
    minHeight: 400,
  },
  emptyText: {
    fontSize: 20,
    fontWeight: "bold",
    color: "#333",
    marginTop: 16,
  },
  emptySubtext: {
    fontSize: 14,
    color: "#666",
    marginTop: 8,
    textAlign: "center",
  },
  createButton: {
    marginTop: 24,
    paddingHorizontal: 24,
    paddingVertical: 12,
    backgroundColor: "#6200ee",
    borderRadius: 8,
  },
  createButtonText: {
    color: "#fff",
    fontSize: 16,
    fontWeight: "600",
  },
  restaurantsList: {
    padding: 16,
  },
  restaurantCard: {
    backgroundColor: "#fff",
    borderRadius: 12,
    marginBottom: 16,
    overflow: "hidden",
    shadowColor: "#000",
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.1,
    shadowRadius: 4,
    elevation: 3,
  },
  restaurantHeader: {
    flexDirection: "row",
    justifyContent: "space-between",
    alignItems: "center",
    marginBottom: 8,
    padding: 16,
    paddingBottom: 0,
  },
  restaurantName: {
    fontSize: 18,
    fontWeight: "bold",
    color: "#333",
    flex: 1,
  },
  statusBadge: {
    paddingHorizontal: 8,
    paddingVertical: 4,
    borderRadius: 12,
  },
  activeBadge: {
    backgroundColor: "#4caf50",
  },
  inactiveBadge: {
    backgroundColor: "#ccc",
  },
  statusText: {
    fontSize: 12,
    fontWeight: "600",
    color: "#fff",
  },
  restaurantDescription: {
    fontSize: 14,
    color: "#666",
    marginBottom: 8,
    paddingHorizontal: 16,
  },
  cuisineType: {
    fontSize: 12,
    color: "#999",
    marginBottom: 12,
    paddingHorizontal: 16,
  },
  actionButtons: {
    flexDirection: "row",
    gap: 8,
    marginTop: 8,
    padding: 16,
  },
  actionButton: {
    flex: 1,
    flexDirection: "row",
    alignItems: "center",
    justifyContent: "center",
    paddingVertical: 10,
    paddingHorizontal: 12,
    borderRadius: 8,
    gap: 4,
  },
  editButton: {
    backgroundColor: "#f3e5f5",
    borderWidth: 1,
    borderColor: "#6200ee",
  },
  editButtonText: {
    color: "#6200ee",
    fontSize: 14,
    fontWeight: "600",
  },
  menuButton: {
    backgroundColor: "#6200ee",
  },
  menuButtonText: {
    color: "#fff",
    fontSize: 14,
    fontWeight: "600",
  },
  deleteButton: {
    backgroundColor: "#ffebee",
    borderWidth: 1,
    borderColor: "#d32f2f",
  },
  deleteButtonText: {
    color: "#d32f2f",
    fontSize: 14,
    fontWeight: "600",
  },
  ordersButton: {
    backgroundColor: "#4caf50",
  },
  ordersButtonText: {
    color: "#fff",
    fontSize: 14,
    fontWeight: "600",
  },
  fab: {
    position: "absolute",
    right: 16,
    bottom: 16,
    width: 56,
    height: 56,
    borderRadius: 28,
    backgroundColor: "#6200ee",
    justifyContent: "center",
    alignItems: "center",
    shadowColor: "#000",
    shadowOffset: { width: 0, height: 4 },
    shadowOpacity: 0.3,
    shadowRadius: 4,
    elevation: 5,
  },
});
