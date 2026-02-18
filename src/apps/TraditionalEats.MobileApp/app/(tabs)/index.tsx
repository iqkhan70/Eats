import React, { useState, useCallback, useEffect } from "react";
import {
  View,
  Text,
  StyleSheet,
  ScrollView,
  TouchableOpacity,
  KeyboardAvoidingView,
  Platform,
  Keyboard,
  ActivityIndicator,
  Alert,
  Image,
} from "react-native";
import Slider from "@react-native-community/slider";
import { Ionicons } from "@expo/vector-icons";
import { BlurView } from "expo-blur";
import { useLocalSearchParams, useRouter } from "expo-router";
import { useSafeAreaInsets } from "react-native-safe-area-context";
import * as Location from "expo-location";
import { api } from "../../services/api";
import BottomSearchBar from "../../components/BottomSearchBar";

const ZIP_REGEX = /^\s*(\d{5})(?:-\d{4})?\s*$/;

interface Restaurant {
  restaurantId: string;
  name: string;
  cuisineType?: string;
  address: string;
  rating?: number;
  reviewCount?: number;
  imageUrl?: string;
  latitude?: number;
  longitude?: number;
}

interface MenuCategory {
  categoryId: string;
  name: string;
}

export default function HomeScreen() {
  const router = useRouter();
  const params = useLocalSearchParams();
  const insets = useSafeAreaInsets();

  const [showAllCategories, setShowAllCategories] = useState(false);
  const [distanceMiles, setDistanceMiles] = useState(25);
  const [userLocation, setUserLocation] = useState<{ latitude: number; longitude: number } | null>(null);
  const [nearbyRestaurants, setNearbyRestaurants] = useState<Restaurant[]>([]);
  const [loadingRestaurants, setLoadingRestaurants] = useState(false);
  const [locationPermissionDenied, setLocationPermissionDenied] = useState(false);
  const [menuCategories, setMenuCategories] = useState<MenuCategory[]>([]);

  const initialCategoryCount = 6;

  const navigateToRestaurants = async (
    location?: string,
    category?: string,
    menuCategoryId?: string,
  ) => {
    Keyboard.dismiss();

    const loc = (location ?? "").trim();
    const qs: string[] = [];
    if (category?.trim())
      qs.push(`category=${encodeURIComponent(category.trim())}`);
    if (menuCategoryId?.trim())
      qs.push(`menuCategoryId=${encodeURIComponent(menuCategoryId.trim())}`);

    const zipMatch = loc.match(ZIP_REGEX);
    if (zipMatch && loc) {
      const zip = zipMatch[1];
      try {
        const { data } = await api.get<{ latitude: number; longitude: number }>(
          "/MobileBff/geocode-zip",
          { params: { zip } },
        );
        const miles = Math.round(Math.max(1, Math.min(100, distanceMiles)));
        qs.push(`latitude=${data.latitude}`);
        qs.push(`longitude=${data.longitude}`);
        qs.push(`radiusMiles=${miles}`);
        qs.push(`zip=${encodeURIComponent(zip)}`);
      } catch {
        qs.push(`location=${encodeURIComponent(loc)}`);
      }
    } else if (loc) {
      qs.push(`location=${encodeURIComponent(loc)}`);
      qs.push(
        `radiusMiles=${Math.round(Math.max(1, Math.min(100, distanceMiles)))}`,
      );
    }

    const query = qs.length ? `?${qs.join("&")}` : "";
    router.push(`/(tabs)/restaurants${query}`);
  };

  const loadSuggestions = useCallback(
    async (query: string): Promise<string[]> => {
      if (!query || query.length < 2) {
        return [];
      }

      try {
        const q = query.trim().toLowerCase();
        const categorySuggestions =
          q && menuCategories.length
            ? menuCategories
                .filter((c) => (c.name ?? "").toLowerCase().includes(q))
                .map((c) => c.name)
            : [];

        const response = await api.get<string[]>(
          "/MobileBff/search-suggestions",
          {
            params: { query, maxResults: 10 },
          },
        );
        const apiSuggestions = Array.isArray(response.data) ? response.data : [];
        const merged = [...categorySuggestions, ...apiSuggestions]
          .map((s) => (typeof s === "string" ? s.trim() : ""))
          .filter(Boolean);

        return Array.from(new Set(merged)).slice(0, 10);
      } catch (error: any) {
        console.error("Error loading suggestions:", error);
        return [];
      }
    },
    [menuCategories],
  );

  const handleSearch = async (query: string) => {
    Keyboard.dismiss();
    const trimmed = (query ?? "").trim();
    if (!trimmed) {
      await navigateToRestaurants();
      return;
    }

    const normalized = trimmed.toLowerCase();
    const exactCategory = menuCategories.find(
      (c) => (c.name ?? "").trim().toLowerCase() === normalized,
    );
    if (exactCategory?.categoryId) {
      await navigateToRestaurants(undefined, undefined, exactCategory.categoryId);
      return;
    }

    await navigateToRestaurants(query);
  };

  const handleSuggestionSelect = (suggestion: string) => {
    handleSearch(suggestion);
  };

  useEffect(() => {
    (async () => {
      try {
        const res = await api.get<MenuCategory[]>("/MobileBff/categories", {
          params: { __ts: Date.now() },
          headers: { "Cache-Control": "no-cache", Pragma: "no-cache" },
        });
        const list = Array.isArray(res.data) ? res.data : [];
        setMenuCategories(
          list.map((c: any) => ({
            categoryId: c.categoryId ?? c.id,
            name: c.name,
          })),
        );
      } catch {
        setMenuCategories([]);
      }
    })();
  }, []);

  // Get user's current location
  useEffect(() => {
    (async () => {
      try {
        const { status } = await Location.requestForegroundPermissionsAsync();
        if (status !== "granted") {
          setLocationPermissionDenied(true);
          console.log("Location permission denied");
          return;
        }

        const location = await Location.getCurrentPositionAsync({
          accuracy: Location.Accuracy.Balanced,
        });
        setUserLocation({
          latitude: location.coords.latitude,
          longitude: location.coords.longitude,
        });
      } catch (error) {
        console.error("Error getting location:", error);
        setLocationPermissionDenied(true);
      }
    })();
  }, []);

  // Fetch nearby restaurants when location or distance changes
  useEffect(() => {
    if (!userLocation) return;

    const fetchNearbyRestaurants = async () => {
      try {
        setLoadingRestaurants(true);
        const response = await api.get<Restaurant[]>("/MobileBff/restaurants", {
          params: {
            latitude: userLocation.latitude,
            longitude: userLocation.longitude,
            radiusMiles: Math.round(distanceMiles),
            take: 10, // Limit to 10 for home page
          },
        });

        const data = response.data;
        const list = Array.isArray(data) ? data : [];
        const mappedRestaurants = list.map((r: any) => ({
          restaurantId: r.restaurantId || r.id,
          name: r.name,
          cuisineType: r.cuisineType,
          address: r.address,
          rating: r.rating,
          reviewCount: r.reviewCount,
          imageUrl: r.imageUrl,
          latitude: r.latitude,
          longitude: r.longitude,
        }));

        setNearbyRestaurants(mappedRestaurants);
      } catch (error: any) {
        console.error("Error loading nearby vendors:", error);
        setNearbyRestaurants([]);
      } finally {
        setLoadingRestaurants(false);
      }
    };

    fetchNearbyRestaurants();
  }, [userLocation, distanceMiles]);

  const navigateToRestaurantDetails = (restaurant?: Restaurant) => {
    if (restaurant) {
      // Navigate to the specific restaurant's menu page
      router.push(`/restaurants/${restaurant.restaurantId}/menu`);
    } else {
      // "View All" button - navigate to restaurants list with filters
      if (userLocation) {
        router.push(`/(tabs)/restaurants?latitude=${userLocation.latitude}&longitude=${userLocation.longitude}&radiusMiles=${Math.round(distanceMiles)}`);
      } else {
        navigateToRestaurants();
      }
    }
  };

  const categories = [
    { id: 1, name: "Traditional", icon: "restaurant" },
    { id: 2, name: "Fast Food", icon: "fast-food" },
    { id: 3, name: "Desserts", icon: "ice-cream" },
    { id: 4, name: "Beverages", icon: "cafe" },
    { id: 5, name: "Vegetarian", icon: "leaf" },
    { id: 6, name: "Vegan", icon: "flower" },
    { id: 7, name: "Seafood", icon: "fish" },
    { id: 8, name: "BBQ", icon: "flame" },
    { id: 9, name: "Italian", icon: "pizza" },
    { id: 10, name: "Asian", icon: "restaurant" },
  ];

  const displayedCategories = showAllCategories
    ? categories
    : categories.slice(0, initialCategoryCount);

  return (
    <KeyboardAvoidingView
      style={styles.container}
      behavior={Platform.OS === "ios" ? "padding" : "height"}
    >
      <ScrollView
        style={styles.scrollView}
        contentContainerStyle={styles.scrollContent}
        keyboardDismissMode="on-drag"
        keyboardShouldPersistTaps="handled"
        showsVerticalScrollIndicator={false}
      >
        <View style={[styles.header, { paddingTop: insets.top + 20 }]}>
          <Text style={styles.title}>Welcome to Kram</Text>
          <Text style={styles.subtitle}>
            Discover authentic traditional food
          </Text>
        </View>

        {/* Distance slider: 0–100 miles */}
        <BlurView intensity={80} tint="light" style={styles.distanceRow}>
          <Text style={styles.distanceLabel}>
            Within {Math.round(distanceMiles)} miles
          </Text>
          <Slider
            style={styles.slider}
            minimumValue={1}
            maximumValue={100}
            step={1}
            value={distanceMiles}
            onValueChange={setDistanceMiles}
            minimumTrackTintColor="#6200ee"
            maximumTrackTintColor="#e0e0e0"
            thumbTintColor="#6200ee"
          />
        </BlurView>

        {/* Content */}
        <View style={styles.section}>
          <Text style={styles.sectionTitle}>Popular Categories</Text>

          <View style={styles.categoryGrid}>
            {displayedCategories.map((category) => (
              <TouchableOpacity
                key={category.id}
                style={styles.categoryCardWrapper}
                onPress={() => navigateToRestaurants(undefined, category.name)}
                activeOpacity={0.85}
              >
                <BlurView intensity={80} tint="light" style={styles.categoryCard}>
                  <Ionicons
                    name={category.icon as any}
                    size={36}
                    color="#6200ee"
                  />
                  <Text style={styles.categoryName}>{category.name}</Text>
                </BlurView>
              </TouchableOpacity>
            ))}
          </View>

          {categories.length > initialCategoryCount && (
            <TouchableOpacity
              style={styles.showMoreButton}
              onPress={() => setShowAllCategories(!showAllCategories)}
            >
              <Text style={styles.showMoreText}>
                {showAllCategories
                  ? "Show Less"
                  : `Show All (${categories.length})`}
              </Text>
              <Ionicons
                name={showAllCategories ? "chevron-up" : "chevron-down"}
                size={20}
                color="#6200ee"
              />
            </TouchableOpacity>
          )}
        </View>

        <View style={styles.section}>
          <View style={styles.sectionHeader}>
            <Text style={styles.sectionTitle}>Nearby Vendors</Text>
            {userLocation && nearbyRestaurants.length > 0 && (
              <TouchableOpacity
                onPress={() => navigateToRestaurantDetails()}
                style={styles.viewAllButton}
              >
                <Text style={styles.viewAllText}>View All</Text>
                <Ionicons name="chevron-forward" size={16} color="#6200ee" />
              </TouchableOpacity>
            )}
          </View>

          {locationPermissionDenied && (
            <View style={styles.locationPrompt}>
              <Ionicons name="location-outline" size={24} color="#666" />
              <Text style={styles.locationPromptText}>
                Enable location to see nearby vendors
              </Text>
            </View>
          )}

          {!locationPermissionDenied && !userLocation && (
            <View style={styles.locationPrompt}>
              <ActivityIndicator size="small" color="#6200ee" />
              <Text style={styles.locationPromptText}>
                Getting your location...
              </Text>
            </View>
          )}

          {loadingRestaurants && userLocation && (
            <View style={styles.loadingContainer}>
              <ActivityIndicator size="small" color="#6200ee" />
              <Text style={styles.loadingText}>Loading vendors...</Text>
            </View>
          )}

          {!loadingRestaurants && userLocation && nearbyRestaurants.length === 0 && (
            <View style={styles.emptyContainer}>
              <Ionicons name="restaurant-outline" size={48} color="#ccc" />
              <Text style={styles.emptyText}>No vendors found</Text>
              <Text style={styles.emptySubtext}>
                Try increasing the distance range
              </Text>
            </View>
          )}

          {!loadingRestaurants &&
            userLocation &&
            nearbyRestaurants.length > 0 &&
            nearbyRestaurants.map((restaurant) => (
              <TouchableOpacity
                key={restaurant.restaurantId}
                style={styles.restaurantCard}
                onPress={() => navigateToRestaurantDetails(restaurant)}
                activeOpacity={0.85}
              >
                <View style={styles.restaurantInfo}>
                  {restaurant.imageUrl ? (
                    <Image
                      source={{ uri: restaurant.imageUrl }}
                      style={styles.restaurantImage}
                    />
                  ) : (
                    <View style={styles.restaurantImagePlaceholder}>
                      <Ionicons name="restaurant" size={24} color="#6200ee" />
                    </View>
                  )}
                  <View style={styles.restaurantDetails}>
                    <Text style={styles.restaurantName}>{restaurant.name}</Text>
                    <Text style={styles.restaurantAddress}>
                      {restaurant.address}
                    </Text>
                    {restaurant.cuisineType && (
                      <Text style={styles.cuisineType}>
                        {restaurant.cuisineType}
                      </Text>
                    )}
                    <View style={styles.ratingContainer}>
                      {restaurant.rating && (
                        <>
                          <Ionicons name="star" size={16} color="#FFD700" />
                          <Text style={styles.rating}>
                            {restaurant.rating.toFixed(1)}
                          </Text>
                          {restaurant.reviewCount && (
                            <Text style={styles.reviewCount}>
                              ({restaurant.reviewCount} reviews)
                            </Text>
                          )}
                        </>
                      )}
                    </View>
                  </View>
                </View>
                <Ionicons name="chevron-forward" size={20} color="#666" />
              </TouchableOpacity>
            ))}
        </View>
      </ScrollView>

      {/* Bottom Search Bar */}
      <BottomSearchBar
        onSearch={handleSearch}
        placeholder="Search for vendors, cuisine, or location..."
        collapsedText={
          typeof params.location === "string" && params.location.trim()
            ? params.location.trim()
            : undefined
        }
        emptyStateTitle="Search for vendors"
        emptyStateSubtitle="Enter a vendor name, cuisine, category, address, or ZIP code"
        loadSuggestions={loadSuggestions}
        onSuggestionSelect={handleSuggestionSelect}
      />
    </KeyboardAvoidingView>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: "transparent" },
  scrollView: { flex: 1 },
  scrollContent: { paddingBottom: 100 },

  header: {
    padding: 20,
    backgroundColor: "transparent",
  },
  title: { fontSize: 24, fontWeight: "bold", color: "#333", marginBottom: 4 },
  subtitle: { fontSize: 14, color: "#666" },

  distanceRow: {
    marginTop: 20,
    marginBottom: 8,
    paddingHorizontal: 20,
    paddingVertical: 12,
    backgroundColor: "rgba(255, 255, 255, 0.7)",
    marginHorizontal: 16,
    borderRadius: 12,
    borderWidth: 1,
    borderColor: "rgba(255, 255, 255, 0.3)",
    overflow: "hidden",
  },
  distanceLabel: { fontSize: 14, color: "#555", marginBottom: 4 },
  slider: { width: "100%", height: 32 },

  section: { marginTop: 20, paddingHorizontal: 16 },
  sectionHeader: {
    flexDirection: "row",
    justifyContent: "space-between",
    alignItems: "center",
    marginBottom: 12,
  },
  sectionTitle: {
    fontSize: 20,
    fontWeight: "bold",
    color: "#333",
  },
  viewAllButton: {
    flexDirection: "row",
    alignItems: "center",
    paddingVertical: 4,
    paddingHorizontal: 8,
  },
  viewAllText: {
    fontSize: 14,
    fontWeight: "600",
    color: "#6200ee",
    marginRight: 4,
  },

  categoryGrid: {
    flexDirection: "row",
    flexWrap: "wrap",
    justifyContent: "space-between",
    gap: 12,
  },
  categoryCardWrapper: {
    width: "31%",
    marginBottom: 12,
    borderRadius: 16,
    overflow: "hidden",
  },
  categoryCard: {
    width: "100%",
    backgroundColor: "rgba(255, 255, 255, 0.7)",
    borderRadius: 16,
    padding: 16,
    alignItems: "center",
    justifyContent: "center",
    minHeight: 100,
    borderWidth: 1,
    borderColor: "rgba(255, 255, 255, 0.3)",
  },
  categoryName: {
    marginTop: 8,
    fontSize: 12,
    fontWeight: "600",
    color: "#333",
    textAlign: "center",
  },

  restaurantCard: {
    flexDirection: "row",
    alignItems: "center",
    justifyContent: "space-between",
    backgroundColor: "rgba(255, 255, 255, 0.7)",
    borderRadius: 12,
    padding: 16,
    borderWidth: 1,
    borderColor: "rgba(255, 255, 255, 0.3)",
    marginBottom: 12,
    shadowColor: "#000",
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.1,
    shadowRadius: 4,
    elevation: 3,
  },
  restaurantInfo: { flexDirection: "row", alignItems: "center", flex: 1 },
  restaurantImage: {
    width: 60,
    height: 60,
    borderRadius: 8,
    backgroundColor: "#f0f0f0",
  },
  restaurantImagePlaceholder: {
    width: 60,
    height: 60,
    borderRadius: 8,
    backgroundColor: "#f0f0f0",
    alignItems: "center",
    justifyContent: "center",
  },
  restaurantDetails: { marginLeft: 12, flex: 1 },
  restaurantName: {
    fontSize: 16,
    fontWeight: "600",
    color: "#333",
    marginBottom: 4,
  },
  restaurantAddress: { fontSize: 14, color: "#666", marginBottom: 4 },
  cuisineType: {
    fontSize: 12,
    color: "#6200ee",
    fontWeight: "500",
    marginBottom: 4,
  },
  ratingContainer: { flexDirection: "row", alignItems: "center" },
  rating: { fontSize: 14, fontWeight: "600", color: "#333", marginLeft: 4 },
  reviewCount: { fontSize: 12, color: "#666", marginLeft: 4 },
  locationPrompt: {
    flexDirection: "row",
    alignItems: "center",
    justifyContent: "center",
    padding: 24,
    backgroundColor: "rgba(255, 255, 255, 0.7)",
    borderRadius: 12,
    borderWidth: 1,
    borderColor: "rgba(255, 255, 255, 0.3)",
    marginBottom: 12,
  },
  locationPromptText: {
    marginLeft: 12,
    fontSize: 14,
    color: "#666",
  },
  loadingContainer: {
    flexDirection: "row",
    alignItems: "center",
    justifyContent: "center",
    padding: 24,
  },
  loadingText: {
    marginLeft: 12,
    fontSize: 14,
    color: "#666",
  },
  emptyContainer: {
    alignItems: "center",
    justifyContent: "center",
    padding: 32,
    backgroundColor: "rgba(255, 255, 255, 0.7)",
    borderRadius: 12,
    borderWidth: 1,
    borderColor: "rgba(255, 255, 255, 0.3)",
  },
  emptyText: {
    fontSize: 16,
    fontWeight: "600",
    color: "#333",
    marginTop: 12,
  },
  emptySubtext: {
    fontSize: 14,
    color: "#666",
    marginTop: 4,
    textAlign: "center",
  },

  showMoreButton: {
    flexDirection: "row",
    alignItems: "center",
    justifyContent: "center",
    marginTop: 8,
    paddingVertical: 12,
  },
  showMoreText: {
    fontSize: 14,
    fontWeight: "600",
    color: "#6200ee",
    marginRight: 4,
  },
});
