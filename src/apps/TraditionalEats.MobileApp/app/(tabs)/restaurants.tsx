import React, { useEffect, useMemo, useState, useCallback } from "react";
import {
  View,
  Text,
  StyleSheet,
  FlatList,
  TouchableOpacity,
  Image,
  KeyboardAvoidingView,
  Platform,
  Keyboard,
} from "react-native";
import { Ionicons } from "@expo/vector-icons";
import { BlurView } from "expo-blur";
import { useRouter, useLocalSearchParams } from "expo-router";
import { useSafeAreaInsets } from "react-native-safe-area-context";
import { api } from "../../services/api";
import BottomSearchBar from "../../components/BottomSearchBar";

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

function haversineMiles(
  lat1: number,
  lon1: number,
  lat2: number,
  lon2: number,
): number {
  const R = 3959;
  const dLat = ((lat2 - lat1) * Math.PI) / 180;
  const dLon = ((lon2 - lon1) * Math.PI) / 180;
  const a =
    Math.sin(dLat / 2) * Math.sin(dLat / 2) +
    Math.cos((lat1 * Math.PI) / 180) *
      Math.cos((lat2 * Math.PI) / 180) *
      Math.sin(dLon / 2) *
      Math.sin(dLon / 2);
  const c = 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
  return R * c;
}

/** Show "Same ZIP" when coords are invalid (0,0) or distance is unreasonably large. */
function getDisplayDistance(
  lat: number | null | undefined,
  lon: number | null | undefined,
  centerLat: number,
  centerLon: number,
): string {
  if (lat == null || lon == null) return "Same ZIP";
  if (Math.abs(lat) < 0.01 && Math.abs(lon) < 0.01) return "Same ZIP";
  const mi = haversineMiles(centerLat, centerLon, lat, lon);
  if (mi > 200) return "Same ZIP";
  return `${mi.toFixed(1)} mi away`;
}

export default function RestaurantsScreen() {
  const router = useRouter();
  const insets = useSafeAreaInsets();
  const params = useLocalSearchParams();

  const [restaurants, setRestaurants] = useState<Restaurant[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);

  // Search
  const [searchText, setSearchText] = useState("");
  const [debouncedSearch, setDebouncedSearch] = useState("");

  // Active filters (from query params)
  const activeCategory =
    typeof params.category === "string" ? params.category : "";
  const activeLocation =
    typeof params.location === "string" ? params.location : "";
  const zipParam = typeof params.zip === "string" ? params.zip : undefined;
  const centerLat =
    typeof params.latitude === "string"
      ? parseFloat(params.latitude)
      : undefined;
  const centerLon =
    typeof params.longitude === "string"
      ? parseFloat(params.longitude)
      : undefined;
  const radiusMiles =
    typeof params.radiusMiles === "string"
      ? parseFloat(params.radiusMiles)
      : undefined;
  const hasFilters = !!(
    activeCategory ||
    activeLocation ||
    (centerLat != null && centerLon != null)
  );

  // Debounce search (300ms)
  useEffect(() => {
    const t = setTimeout(() => setDebouncedSearch(searchText.trim()), 300);
    return () => clearTimeout(t);
  }, [searchText]);

  // Handle search from BottomSearchBar
  const handleSearch = useCallback((query: string) => {
    setSearchText(query);
    setDebouncedSearch(query.trim());
  }, []);

  const loadSuggestions = useCallback(
    async (query: string): Promise<string[]> => {
      if (!query || query.length < 2) {
        return [];
      }
      try {
        const response = await api.get<string[]>(
          "/MobileBff/search-suggestions",
          {
            params: { query, maxResults: 10 },
          },
        );
        return response.data;
      } catch (error: any) {
        console.error("Error loading suggestions:", error);
        return [];
      }
    },
    [],
  );

  useEffect(() => {
    loadRestaurants();
    // If user changes filters (location/category), usually it’s nicer to clear the search
    setSearchText("");
    setDebouncedSearch("");
  }, [
    params.location,
    params.category,
    params.zip,
    params.latitude,
    params.longitude,
    params.radiusMiles,
  ]);

  const clearFilters = () => {
    setSearchText("");
    setDebouncedSearch("");
    Keyboard.dismiss();
    router.replace("/restaurants"); // ✅ back to ALL restaurants (no params)
  };

  const loadRestaurants = async () => {
    try {
      setLoading(true);
      setLoadError(null);

      const queryParams: Record<string, string | number> = {};
      if (params.location) queryParams.location = params.location;
      if (params.category) queryParams.cuisineType = params.category;
      if (zipParam) queryParams.zip = zipParam;
      if (centerLat != null && !Number.isNaN(centerLat))
        queryParams.latitude = centerLat;
      if (centerLon != null && !Number.isNaN(centerLon))
        queryParams.longitude = centerLon;
      if (radiusMiles != null && !Number.isNaN(radiusMiles))
        queryParams.radiusMiles = radiusMiles;

      const response = await api.get<Restaurant[]>("/MobileBff/restaurants", {
        params: queryParams,
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

      setRestaurants(mappedRestaurants);
    } catch (error: any) {
      console.error("Error loading vendors:", error);
      setRestaurants([]);
      const status = error?.response?.status;
      const bodyMessage =
        error?.response?.data?.message ?? error?.response?.data?.error;
      const friendlyMessage =
        status === 404 || bodyMessage?.toLowerCase().includes("not found")
          ? "No vendors found for this area. Try another location or browse all vendors."
          : (bodyMessage ?? error?.message ?? "Request failed");
      setLoadError(
        `Could not load vendors. ${friendlyMessage}${status && status !== 404 ? ` Make sure the Mobile BFF is running (port 5102). On a real device, set your computer's IP in config/app.config.ts.` : ""}`,
      );
    } finally {
      setLoading(false);
    }
  };

  const filteredRestaurants = useMemo(() => {
    if (!debouncedSearch) return restaurants;

    const q = debouncedSearch.toLowerCase();

    return restaurants.filter((r) => {
      const haystack =
        `${r.name} ${r.cuisineType ?? ""} ${r.address ?? ""}`.toLowerCase();
      return haystack.includes(q);
    });
  }, [restaurants, debouncedSearch]);

  const renderRestaurant = ({ item }: { item: Restaurant }) => (
    <TouchableOpacity
      style={styles.restaurantCard}
      onPress={() => router.push(`/restaurants/${item.restaurantId}/menu`)}
      activeOpacity={0.85}
    >
      {item.imageUrl ? (
        <Image source={{ uri: item.imageUrl }} style={styles.restaurantImage} />
      ) : (
        <View style={styles.restaurantImagePlaceholder}>
          <Ionicons name="restaurant" size={40} color="#6200ee" />
        </View>
      )}

      <View style={styles.restaurantInfo}>
        <Text style={styles.restaurantName}>{item.name}</Text>
        {!!item.cuisineType && (
          <Text style={styles.cuisineType}>{item.cuisineType}</Text>
        )}
        <Text style={styles.address} numberOfLines={2}>
          {item.address}
        </Text>
        {centerLat != null && centerLon != null && (
          <Text style={styles.distanceText}>
            {getDisplayDistance(
              item.latitude,
              item.longitude,
              centerLat,
              centerLon,
            )}
          </Text>
        )}

        <View style={styles.ratingContainer}>
          <Ionicons name="star" size={16} color="#FFD700" />
          <Text style={styles.rating}>{item.rating ?? "-"}</Text>
          <Text style={styles.reviewCount}>
            ({item.reviewCount ?? 0} reviews)
          </Text>
        </View>
      </View>

      <Ionicons name="chevron-forward" size={20} color="#666" />
    </TouchableOpacity>
  );

  const ListHeader = (
    <View style={styles.searchHeader}>
      {/* ✅ Active filters row with Clear */}
      {hasFilters && (
        <View style={styles.filtersRow}>
          <Ionicons name="funnel-outline" size={16} color="#666" />
          <Text style={styles.filtersText} numberOfLines={1}>
            {activeCategory ? `Category: ${activeCategory}` : ""}
            {activeCategory && activeLocation ? " • " : ""}
            {activeLocation ? `Location: ${activeLocation}` : ""}
            {activeCategory && centerLat != null ? " • " : ""}
            {centerLat != null && radiusMiles != null
              ? `Within ${Math.round(radiusMiles)} mi`
              : ""}
          </Text>

          <TouchableOpacity
            onPress={clearFilters}
            style={styles.clearFiltersBtn}
          >
            <Text style={styles.clearFiltersBtnText}>Clear</Text>
          </TouchableOpacity>
        </View>
      )}

      {!!debouncedSearch && (
        <Text style={styles.resultsText}>
          Showing {filteredRestaurants.length} result
          {filteredRestaurants.length === 1 ? "" : "s"}
        </Text>
      )}
    </View>
  );

  if (loading) {
    return (
      <View style={styles.centerContainer}>
        <Text>Loading vendors...</Text>
      </View>
    );
  }

  if (loadError) {
    return (
      <View style={styles.centerContainer}>
        <Text style={styles.errorText}>{loadError}</Text>
      </View>
    );
  }

  return (
    <KeyboardAvoidingView
      style={styles.container}
      behavior={Platform.OS === "ios" ? "padding" : "height"}
      keyboardVerticalOffset={Platform.OS === "ios" ? 0 : 0}
    >
      {/* Search Query Indicator - Outside FlatList for better visibility */}
      {debouncedSearch && debouncedSearch.trim() && (
        <View style={[styles.searchIndicator, { marginTop: insets.top + 10, paddingTop: 12 }]}>
          <Text style={styles.searchIndicatorText}>
            Searching: "{debouncedSearch}"
          </Text>
          <TouchableOpacity
            onPress={() => {
              setSearchText("");
              setDebouncedSearch("");
            }}
            style={styles.clearSearchIcon}
          >
            <Ionicons name="close-circle" size={20} color="#666" />
          </TouchableOpacity>
        </View>
      )}

      <FlatList
        data={filteredRestaurants}
        renderItem={renderRestaurant}
        keyExtractor={(item) => item.restaurantId}
        contentContainerStyle={styles.listContent}
        ListHeaderComponent={ListHeader}
        stickyHeaderIndices={hasFilters ? [0] : []}
        keyboardDismissMode="on-drag"
        keyboardShouldPersistTaps="handled"
        ListEmptyComponent={
          <View style={styles.centerContainer}>
            <Text style={styles.emptyText}>
              {debouncedSearch ? "No matches found" : "No vendors found"}
            </Text>
          </View>
        }
      />
      <BottomSearchBar
        onSearch={handleSearch}
        onClear={() => {
          setSearchText("");
          setDebouncedSearch("");
        }}
        placeholder="Search vendors..."
        emptyStateTitle="Search vendors"
        emptyStateSubtitle="Find vendors by name, cuisine, or location"
        loadSuggestions={loadSuggestions}
        onSuggestionSelect={handleSearch}
      />
    </KeyboardAvoidingView>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: "#f5f5f5" },

  listContent: {
    padding: 16,
    paddingTop: 12,
    paddingBottom: 24,
  },

  // Sticky header wrapper
  searchHeader: {
    backgroundColor: "#f5f5f5",
    paddingHorizontal: 16,
    paddingTop: 12,
    paddingBottom: 10,
    borderBottomWidth: 1,
    borderBottomColor: "#e9e9e9",
  },

  resultsText: {
    marginTop: 8,
    fontSize: 12,
    color: "#777",
  },
  searchIndicator: {
    flexDirection: "row",
    alignItems: "center",
    justifyContent: "space-between",
    backgroundColor: "rgba(227, 242, 253, 0.8)",
    paddingHorizontal: 16,
    paddingVertical: 10,
    borderBottomWidth: 1,
    borderBottomColor: "rgba(25, 118, 210, 0.2)",
  },
  searchIndicatorText: {
    fontSize: 14,
    color: "#1976d2",
    fontWeight: "500",
  },
  clearSearchIcon: {
    padding: 4,
  },

  // ✅ Active filters row styles
  filtersRow: {
    marginTop: 10,
    flexDirection: "row",
    alignItems: "center",
    backgroundColor: "#fff",
    borderRadius: 10,
    paddingHorizontal: 12,
    paddingVertical: 10,
    borderWidth: 1,
    borderColor: "#e0e0e0",
  },
  filtersText: {
    flex: 1,
    marginLeft: 8,
    fontSize: 12,
    color: "#555",
  },
  clearFiltersBtn: {
    backgroundColor: "#6200ee",
    paddingHorizontal: 12,
    paddingVertical: 6,
    borderRadius: 8,
  },
  clearFiltersBtnText: {
    color: "#fff",
    fontSize: 12,
    fontWeight: "600",
  },

  restaurantCard: {
    flexDirection: "row",
    backgroundColor: "#fff",
    borderRadius: 12,
    padding: 12,
    marginBottom: 12,
    shadowColor: "#000",
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.1,
    shadowRadius: 4,
    elevation: 3,
    alignItems: "center",
  },

  restaurantImage: { width: 80, height: 80, borderRadius: 8, marginRight: 12 },

  restaurantImagePlaceholder: {
    width: 80,
    height: 80,
    borderRadius: 8,
    backgroundColor: "#f0f0f0",
    justifyContent: "center",
    alignItems: "center",
    marginRight: 12,
  },

  restaurantInfo: { flex: 1 },

  restaurantName: {
    fontSize: 18,
    fontWeight: "600",
    color: "#333",
    marginBottom: 4,
  },

  cuisineType: { fontSize: 14, color: "#666", marginBottom: 4 },

  address: { fontSize: 12, color: "#999", marginBottom: 4 },
  distanceText: { fontSize: 12, color: "#6200ee", marginBottom: 8 },

  ratingContainer: { flexDirection: "row", alignItems: "center" },

  rating: { fontSize: 14, fontWeight: "600", color: "#333", marginLeft: 4 },

  reviewCount: { fontSize: 12, color: "#666", marginLeft: 4 },

  centerContainer: {
    flex: 1,
    justifyContent: "center",
    alignItems: "center",
    padding: 40,
  },

  emptyText: { fontSize: 16, color: "#666" },
  errorText: {
    fontSize: 14,
    color: "#c00",
    textAlign: "center",
    paddingHorizontal: 24,
  },
});
