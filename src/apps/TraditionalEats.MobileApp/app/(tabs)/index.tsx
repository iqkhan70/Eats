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
  Modal,
} from "react-native";
import Slider from "@react-native-community/slider";
import { Ionicons } from "@expo/vector-icons";
import { BlurView } from "expo-blur";
import { LinearGradient } from "expo-linear-gradient";
import { useLocalSearchParams, useRouter, useFocusEffect } from "expo-router";
import { StatusBar } from "expo-status-bar";
import { useSafeAreaInsets } from "react-native-safe-area-context";
import * as Location from "expo-location";
import { api } from "../../services/api";
import BottomSearchBar from "../../components/BottomSearchBar";
import { APP_CONFIG } from "../../config/api.config";

const ZIP_REGEX = /^\s*(\d{5})(?:-\d{4})?\s*$/;

const hasActiveDeal = (r: Restaurant): boolean => {
  if (!r.activeDealTitle?.trim()) return false;
  if (r.activeDealEndTime) {
    const end = new Date(r.activeDealEndTime).getTime();
    if (end < Date.now()) return false;
  }
  return true;
};

const getDealBadgeText = (r: Restaurant): string => {
  if (r.activeDealDiscountPercent != null)
    return `${r.activeDealDiscountPercent}% off`;
  return r.activeDealTitle?.trim() ?? "Deal";
};

const getRestaurantImageUrl = (imageUrl?: string) => {
  if (!imageUrl) return "";
  if (imageUrl.startsWith("http://") || imageUrl.startsWith("https://"))
    return imageUrl;
  const base = APP_CONFIG.API_BASE_URL.replace(/\/$/, "");
  return `${base}/MobileBff/menu-image?path=${encodeURIComponent(imageUrl)}`;
};

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
  activeDealTitle?: string | null;
  activeDealDiscountPercent?: number | null;
  activeDealEndTime?: string | null;
}

interface MenuCategory {
  categoryId: string;
  name: string;
}

export default function HomeScreen() {
  const router = useRouter();
  const params = useLocalSearchParams();
  const insets = useSafeAreaInsets();

  const [distanceMiles, setDistanceMiles] = useState(25);
  const [userLocation, setUserLocation] = useState<{
    latitude: number;
    longitude: number;
  } | null>(null);
  const [nearbyRestaurants, setNearbyRestaurants] = useState<Restaurant[]>([]);
  const [loadingRestaurants, setLoadingRestaurants] = useState(false);
  const [locationPermissionDenied, setLocationPermissionDenied] =
    useState(false);
  const [menuCategories, setMenuCategories] = useState<MenuCategory[]>([]);
  const [fullSizeImageRestaurant, setFullSizeImageRestaurant] =
    useState<Restaurant | null>(null);
  const [failedImageUrls, setFailedImageUrls] = useState<Set<string>>(
    new Set(),
  );

  const navigateToRestaurants = async (
    location?: string,
    category?: string,
    menuCategoryId?: string,
  ) => {
    Keyboard.dismiss();

    const loc = (location ?? "").trim();
    const params: Record<string, string | number> = {};
    if (category?.trim()) params.category = category.trim();
    if (menuCategoryId?.trim()) params.menuCategoryId = menuCategoryId.trim();

    const zipMatch = loc.match(ZIP_REGEX);
    if (zipMatch && loc) {
      const zip = zipMatch[1];
      try {
        const { data } = await api.get<{ latitude: number; longitude: number }>(
          "/MobileBff/geocode-zip",
          { params: { zip } },
        );
        const miles = Math.round(Math.max(1, Math.min(100, distanceMiles)));
        params.latitude = data.latitude;
        params.longitude = data.longitude;
        params.radiusMiles = miles;
        params.zip = zip;
      } catch {
        params.location = loc;
        params.radiusMiles = Math.round(
          Math.max(1, Math.min(100, distanceMiles)),
        );
      }
    } else if (loc) {
      params.location = loc;
      params.radiusMiles = Math.round(
        Math.max(1, Math.min(100, distanceMiles)),
      );
    }

    router.push({ pathname: "/(tabs)/restaurants", params } as any);
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
        const apiSuggestions = Array.isArray(response.data)
          ? response.data
          : [];
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
      await navigateToRestaurants(
        undefined,
        undefined,
        exactCategory.categoryId,
      );
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
        // Expected when user denies, location services off, or emulator has no GPS
        setLocationPermissionDenied(true);
        if (__DEV__)
          console.warn(
            "Location unavailable:",
            (error as Error)?.message ?? error,
          );
      }
    })();
  }, []);

  // Fetch nearby restaurants when location or distance changes
  const fetchNearbyRestaurants = useCallback(async () => {
    if (!userLocation) return;
    try {
      setLoadingRestaurants(true);
      const response = await api.get<Restaurant[]>("/MobileBff/restaurants", {
        params: {
          latitude: userLocation.latitude,
          longitude: userLocation.longitude,
          radiusMiles: Math.round(distanceMiles),
          take: 10, // Limit to 10 for home page
          __ts: Date.now(),
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
        activeDealTitle: r.activeDealTitle ?? r.ActiveDealTitle,
        activeDealDiscountPercent: r.activeDealDiscountPercent ?? r.ActiveDealDiscountPercent,
        activeDealEndTime: r.activeDealEndTime ?? r.ActiveDealEndTime,
      }));

      setNearbyRestaurants(mappedRestaurants);
    } catch (error: any) {
      console.error("Error loading nearby vendors:", error);
      setNearbyRestaurants([]);
    } finally {
      setLoadingRestaurants(false);
    }
  }, [userLocation, distanceMiles]);

  useEffect(() => {
    fetchNearbyRestaurants();
  }, [fetchNearbyRestaurants]);

  // Refresh when tab gains focus (e.g. after vendor adds image and returns)
  useFocusEffect(
    useCallback(() => {
      fetchNearbyRestaurants();
    }, [fetchNearbyRestaurants]),
  );

  const navigateToRestaurantDetails = (restaurant?: Restaurant) => {
    if (restaurant) {
      // Navigate to the specific restaurant's menu page
      router.push(`/restaurants/${restaurant.restaurantId}/catalog`);
    } else {
      // "View All" button - navigate to restaurants list with filters
      if (userLocation) {
        router.push(
          `/(tabs)/restaurants?latitude=${userLocation.latitude}&longitude=${userLocation.longitude}&radiusMiles=${Math.round(distanceMiles)}`,
        );
      } else {
        navigateToRestaurants();
      }
    }
  };

  const vendorTypeCategories = [
    { id: 1, name: "Food", icon: "restaurant" },
    { id: 2, name: "Education", icon: "school" },
    { id: 3, name: "Home Care", icon: "home" },
    { id: 4, name: "Builds and Repairs", icon: "construct" },
    { id: 5, name: "Events", icon: "calendar" },
    { id: 6, name: "Other", icon: "ellipsis-horizontal" },
  ];

  const displayedCategories = vendorTypeCategories;

  return (
    <KeyboardAvoidingView
      style={styles.container}
      behavior={Platform.OS === "ios" ? "padding" : "height"}
    >
      <StatusBar style="dark" />
      <ScrollView
        style={styles.scrollView}
        contentContainerStyle={styles.scrollContent}
        keyboardDismissMode="on-drag"
        keyboardShouldPersistTaps="handled"
        showsVerticalScrollIndicator={false}
      >
        <LinearGradient
          colors={["#f97316", "#eab308"]}
          start={{ x: 0, y: 0 }}
          end={{ x: 1, y: 0 }}
          style={[styles.header, { paddingTop: insets.top + 20 }]}
        >
          <Text style={styles.title}>Welcome to Kram</Text>
          <Text style={styles.subtitle}>Discover your local vendors</Text>
        </LinearGradient>

        {/* Distance slider: 0–100 miles */}
        <BlurView intensity={80} tint="light" style={styles.distanceRow}>
          <View style={styles.distanceLabelRow}>
            <Ionicons name="location" size={18} color="#f97316" />
            <Text style={styles.distanceLabel}>
              Within {Math.round(distanceMiles)} miles
            </Text>
          </View>
          <Slider
            style={styles.slider}
            minimumValue={1}
            maximumValue={100}
            step={1}
            value={distanceMiles}
            onValueChange={setDistanceMiles}
            minimumTrackTintColor="#f97316"
            maximumTrackTintColor="#e0e0e0"
            thumbTintColor="#f97316"
          />
        </BlurView>

        {/* Content */}
        <View style={styles.section}>
          <Text style={styles.sectionTitle}>What are you looking for?</Text>

          <View style={styles.categoryGrid}>
            {displayedCategories.map((category) => (
              <TouchableOpacity
                key={category.id}
                style={styles.categoryCardWrapper}
                onPress={() => navigateToRestaurants(undefined, category.name)}
                activeOpacity={0.85}
              >
                <BlurView
                  intensity={80}
                  tint="light"
                  style={styles.categoryCard}
                >
                  <Ionicons
                    name={category.icon as any}
                    size={36}
                    color="#f97316"
                  />
                  <Text
                    style={styles.categoryName}
                    numberOfLines={1}
                    adjustsFontSizeToFit
                  >
                    {category.name}
                  </Text>
                </BlurView>
              </TouchableOpacity>
            ))}
          </View>
        </View>

        {/* Today's Deals banner - only when there are active deals */}
        {userLocation &&
          !loadingRestaurants &&
          (() => {
            const deals = nearbyRestaurants.filter(hasActiveDeal).slice(0, 5);
            if (deals.length === 0) return null;
            return (
              <View style={styles.section}>
                <Text style={styles.sectionTitle}>Today's Deals</Text>
                <Text style={styles.dealsSubtitle}>
                  Restaurant discounts. Some items have their own deal – you get
                  the best discount per item.
                </Text>
                <View style={styles.dealsBanner}>
                  {deals.map((r, i) => (
                    <TouchableOpacity
                      key={r.restaurantId}
                      style={[
                        styles.dealItem,
                        i < deals.length - 1 && styles.dealItemBorder,
                      ]}
                      onPress={() => navigateToRestaurantDetails(r)}
                      activeOpacity={0.85}
                    >
                      <Ionicons name="flame" size={16} color="#f97316" />
                      <Text style={styles.dealText} numberOfLines={1}>
                        {getDealBadgeText(r)} – {r.name}
                      </Text>
                    </TouchableOpacity>
                  ))}
                </View>
              </View>
            );
          })()}

        <View style={styles.section}>
          <View style={styles.sectionHeader}>
            <Text style={styles.sectionTitle}>Nearby Vendors</Text>
            {userLocation && nearbyRestaurants.length > 0 && (
              <TouchableOpacity
                onPress={() => navigateToRestaurantDetails()}
                style={styles.viewAllButton}
              >
                <Text style={styles.viewAllText}>View All</Text>
                <Ionicons name="chevron-forward" size={16} color="#f97316" />
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
              <ActivityIndicator size="small" color="#f97316" />
              <Text style={styles.locationPromptText}>
                Getting your location...
              </Text>
            </View>
          )}

          {loadingRestaurants && userLocation && (
            <View style={styles.loadingContainer}>
              <ActivityIndicator size="small" color="#f97316" />
              <Text style={styles.loadingText}>Loading vendors...</Text>
            </View>
          )}

          {!loadingRestaurants &&
            userLocation &&
            nearbyRestaurants.length === 0 && (
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
                  <TouchableOpacity
                    onPress={() => {
                      if (
                        restaurant.imageUrl &&
                        !failedImageUrls.has(restaurant.imageUrl)
                      )
                        setFullSizeImageRestaurant(restaurant);
                    }}
                    activeOpacity={0.9}
                    style={styles.restaurantImagePlaceholder}
                  >
                    <Ionicons
                      name="restaurant"
                      size={24}
                      color="#f97316"
                      style={{ position: "absolute" }}
                    />
                    {restaurant.imageUrl &&
                      !failedImageUrls.has(restaurant.imageUrl) && (
                        <Image
                          source={{
                            uri: getRestaurantImageUrl(restaurant.imageUrl),
                          }}
                          style={[
                            StyleSheet.absoluteFillObject,
                            { borderRadius: 8 },
                          ]}
                          resizeMode="cover"
                          onError={() =>
                            setFailedImageUrls((prev) =>
                              new Set(prev).add(restaurant.imageUrl!),
                            )
                          }
                        />
                      )}
                  </TouchableOpacity>
                  <View style={styles.restaurantDetails}>
                    <View style={styles.restaurantNameRow}>
                      <Text style={styles.restaurantName}>{restaurant.name}</Text>
                      {hasActiveDeal(restaurant) && (
                        <View style={styles.dealBadge}>
                          <Ionicons name="flame" size={12} color="#fff" />
                          <Text style={styles.dealBadgeText}>
                            {getDealBadgeText(restaurant)}
                          </Text>
                        </View>
                      )}
                    </View>
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

      <Modal
        visible={!!fullSizeImageRestaurant}
        transparent
        animationType="fade"
        onRequestClose={() => setFullSizeImageRestaurant(null)}
      >
        <TouchableOpacity
          style={styles.modalOverlay}
          activeOpacity={1}
          onPress={() => setFullSizeImageRestaurant(null)}
        >
          <View style={styles.modalContent}>
            {fullSizeImageRestaurant && (
              <>
                <Text style={styles.modalTitle}>
                  {fullSizeImageRestaurant.name}
                </Text>
                <Image
                  source={{
                    uri: getRestaurantImageUrl(
                      fullSizeImageRestaurant.imageUrl,
                    ),
                  }}
                  style={styles.fullSizeImage}
                  resizeMode="contain"
                />
                <Text style={styles.modalCloseHint}>Tap outside to close</Text>
              </>
            )}
          </View>
        </TouchableOpacity>
      </Modal>

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
  container: { flex: 1, backgroundColor: "#f5f5f5" },
  scrollView: { flex: 1 },
  scrollContent: { paddingBottom: 100 },

  header: {
    padding: 20,
    paddingBottom: 24,
  },
  title: { fontSize: 24, fontWeight: "bold", color: "#fff", marginBottom: 4 },
  subtitle: { fontSize: 14, color: "rgba(255, 255, 255, 0.9)" },

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
  distanceLabelRow: {
    flexDirection: "row",
    alignItems: "center",
    gap: 6,
    marginBottom: 4,
  },
  distanceLabel: { fontSize: 14, color: "#555" },
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
    color: "#f97316",
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
    overflow: "hidden",
  },
  restaurantDetails: { marginLeft: 12, flex: 1 },
  restaurantNameRow: {
    flexDirection: "row",
    alignItems: "center",
    flexWrap: "wrap",
    gap: 8,
    marginBottom: 4,
  },
  restaurantName: {
    fontSize: 16,
    fontWeight: "600",
    color: "#333",
  },
  dealBadge: {
    flexDirection: "row",
    alignItems: "center",
    backgroundColor: "#f97316",
    paddingHorizontal: 8,
    paddingVertical: 2,
    borderRadius: 8,
    gap: 4,
  },
  dealBadgeText: {
    fontSize: 11,
    fontWeight: "600",
    color: "#fff",
  },
  dealsSubtitle: {
    fontSize: 12,
    color: "#666",
    marginBottom: 8,
    lineHeight: 16,
  },
  dealsBanner: {
    backgroundColor: "rgba(249, 115, 22, 0.12)",
    borderRadius: 12,
    padding: 12,
    borderWidth: 1,
    borderColor: "rgba(249, 115, 22, 0.3)",
    marginTop: 8,
  },
  dealItem: {
    flexDirection: "row",
    alignItems: "center",
    paddingVertical: 8,
    gap: 8,
  },
  dealItemBorder: {
    borderBottomWidth: 1,
    borderBottomColor: "rgba(249, 115, 22, 0.2)",
  },
  dealText: {
    fontSize: 14,
    color: "#333",
    flex: 1,
  },
  restaurantAddress: { fontSize: 14, color: "#666", marginBottom: 4 },
  cuisineType: {
    fontSize: 12,
    color: "#f97316",
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
    color: "#f97316",
    marginRight: 4,
  },

  modalOverlay: {
    flex: 1,
    backgroundColor: "rgba(0,0,0,0.85)",
    justifyContent: "center",
    alignItems: "center",
  },
  modalContent: {
    padding: 20,
    alignItems: "center",
  },
  modalTitle: {
    fontSize: 18,
    fontWeight: "600",
    color: "#fff",
    marginBottom: 16,
  },
  fullSizeImage: {
    width: 320,
    height: 320,
    borderRadius: 8,
  },
  modalCloseHint: {
    fontSize: 12,
    color: "#999",
    marginTop: 12,
  },
});
