import React, { useEffect, useMemo, useState, useCallback } from "react";
import {
  View,
  Text,
  StyleSheet,
  FlatList,
  TouchableOpacity,
  Image,
  ScrollView,
  KeyboardAvoidingView,
  Platform,
  Keyboard,
  Modal,
} from "react-native";
import { Ionicons } from "@expo/vector-icons";
import { BlurView } from "expo-blur";
import { useRouter, useLocalSearchParams, useFocusEffect } from "expo-router";
import { useSafeAreaInsets } from "react-native-safe-area-context";
import { api } from "../../services/api";
import BottomSearchBar from "../../components/BottomSearchBar";
import { APP_CONFIG } from "../../config/api.config";

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

const getRestaurantImageUrl = (imageUrl?: string) => {
  if (!imageUrl) return "";
  if (imageUrl.startsWith("http://") || imageUrl.startsWith("https://"))
    return imageUrl;
  const base = APP_CONFIG.API_BASE_URL.replace(/\/$/, "");
  return `${base}/MobileBff/menu-image?path=${encodeURIComponent(imageUrl)}`;
};

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
  const [menuCategories, setMenuCategories] = useState<MenuCategory[]>([]);
  const [fullSizeImageRestaurant, setFullSizeImageRestaurant] =
    useState<Restaurant | null>(null);
  const [failedImageUrls, setFailedImageUrls] = useState<Set<string>>(new Set());

  // Search
  const [searchText, setSearchText] = useState("");
  const [debouncedSearch, setDebouncedSearch] = useState("");

  // Active filters (from query params)
  const activeCategory =
    typeof params.category === "string" ? params.category : "";
  const activeLocation =
    typeof params.location === "string" ? params.location : "";
  const activeMenuCategoryId =
    typeof params.menuCategoryId === "string" ? params.menuCategoryId : "";
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
    activeMenuCategoryId ||
    (centerLat != null && centerLon != null)
  );

  // Debounce search (300ms)
  useEffect(() => {
    const t = setTimeout(() => setDebouncedSearch(searchText.trim()), 300);
    return () => clearTimeout(t);
  }, [searchText]);

  const setMenuCategoryFilter = useCallback(
    (categoryId: string) => {
      setSearchText("");
      setDebouncedSearch("");
      Keyboard.dismiss();

      const nextParams: any = { ...params };
      if (!categoryId) {
        delete nextParams.menuCategoryId;
      } else {
        nextParams.menuCategoryId = categoryId;
      }

      router.replace({ pathname: "/restaurants", params: nextParams } as any);
    },
    [params, router],
  );

  // Handle search from BottomSearchBar
  const handleSearch = useCallback(
    (query: string) => {
      const trimmed = (query ?? "").trim();
      if (!trimmed) {
        setSearchText("");
        setDebouncedSearch("");
        return;
      }

      // If user typed an exact menu category name (e.g., "Jewelry"),
      // treat it as a category filter rather than a vendor-name search.
      const normalized = trimmed.toLowerCase();
      const exactCategory = menuCategories.find(
        (c) => (c.name ?? "").trim().toLowerCase() === normalized,
      );
      if (exactCategory?.categoryId) {
        setMenuCategoryFilter(exactCategory.categoryId);
        return;
      }

      setSearchText(query);
      setDebouncedSearch(trimmed);
    },
    [menuCategories, setMenuCategoryFilter],
  );

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

        // unique + cap
        return Array.from(new Set(merged)).slice(0, 10);
      } catch (error: any) {
        console.error("Error loading suggestions:", error);
        return [];
      }
    },
    [menuCategories],
  );

  useEffect(() => {
    loadRestaurants();
    loadMenuCategories();
    // If user changes filters (location/category), usually it’s nicer to clear the search
    setSearchText("");
    setDebouncedSearch("");
  }, [
    params.location,
    params.category,
    params.menuCategoryId,
    params.zip,
    params.latitude,
    params.longitude,
    params.radiusMiles,
  ]);

  const loadMenuCategories = useCallback(async () => {
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
    } catch (e) {
      setMenuCategories([]);
    }
  }, []);

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
      if (activeLocation) queryParams.location = activeLocation;
      if (activeCategory) queryParams.cuisineType = activeCategory;
      if (activeMenuCategoryId) queryParams.menuCategoryId = activeMenuCategoryId;
      if (zipParam) queryParams.zip = zipParam;
      if (centerLat != null && !Number.isNaN(centerLat))
        queryParams.latitude = centerLat;
      if (centerLon != null && !Number.isNaN(centerLon))
        queryParams.longitude = centerLon;
      if (radiusMiles != null && !Number.isNaN(radiusMiles))
        queryParams.radiusMiles = radiusMiles;

      const response = await api.get<Restaurant[]>("/MobileBff/restaurants", {
        params: { ...queryParams, __ts: Date.now() },
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
        `Could not load vendors. ${friendlyMessage}${status && status !== 404 ? ` Make sure the Mobile BFF is running (port 5102). On a real device, set your computer's IP in config/api.config.ts.` : ""}`,
      );
    } finally {
      setLoading(false);
    }
  };

  // Refresh when tab gains focus (e.g. after vendor adds image and returns)
  useFocusEffect(
    useCallback(() => {
      loadRestaurants();
      loadMenuCategories();
    }, []),
  );

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
      <TouchableOpacity
        onPress={() => setFullSizeImageRestaurant(item)}
        activeOpacity={0.9}
        style={styles.restaurantImagePlaceholder}
      >
        <Ionicons name="restaurant" size={40} color="#6200ee" style={{ position: "absolute" }} />
        {item.imageUrl && !failedImageUrls.has(item.imageUrl) && (
          <Image
            source={{ uri: getRestaurantImageUrl(item.imageUrl) }}
            style={[StyleSheet.absoluteFillObject, { borderRadius: 8 }]}
            resizeMode="cover"
            onError={() => setFailedImageUrls((prev) => new Set(prev).add(item.imageUrl!))}
          />
        )}
      </TouchableOpacity>

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
            {(activeCategory || activeLocation) && activeMenuCategoryId
              ? " • "
              : ""}
            {activeMenuCategoryId
              ? `Category: ${
                  menuCategories.find((c) => c.categoryId === activeMenuCategoryId)
                    ?.name ?? "Selected"
                }`
              : ""}
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

      {/* Menu category chips */}
      {menuCategories.length > 0 && (
        <View style={styles.menuCategoryRow}>
          <Text style={styles.menuCategoryLabel}>Categories</Text>
          <ScrollView
            horizontal
            showsHorizontalScrollIndicator={false}
            contentContainerStyle={styles.menuCategoryChips}
          >
            <TouchableOpacity
              style={[
                styles.menuChip,
                !activeMenuCategoryId && styles.menuChipActive,
              ]}
              onPress={() => setMenuCategoryFilter("")}
            >
              <Text
                style={[
                  styles.menuChipText,
                  !activeMenuCategoryId && styles.menuChipTextActive,
                ]}
              >
                All
              </Text>
            </TouchableOpacity>

            {menuCategories.map((c) => (
              <TouchableOpacity
                key={c.categoryId}
                style={[
                  styles.menuChip,
                  activeMenuCategoryId === c.categoryId && styles.menuChipActive,
                ]}
                onPress={() => setMenuCategoryFilter(c.categoryId)}
              >
                <Text
                  style={[
                    styles.menuChipText,
                    activeMenuCategoryId === c.categoryId &&
                      styles.menuChipTextActive,
                  ]}
                >
                  {c.name}
                </Text>
              </TouchableOpacity>
            ))}
          </ScrollView>
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
        collapsedText={debouncedSearch}
        placeholder="Search vendors..."
        emptyStateTitle="Search vendors"
        emptyStateSubtitle="Find vendors by name, cuisine, category, or location"
        loadSuggestions={loadSuggestions}
        onSuggestionSelect={handleSearch}
      />

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

  menuCategoryRow: {
    marginTop: 12,
  },
  menuCategoryLabel: {
    fontSize: 12,
    color: "#666",
    marginBottom: 8,
    fontWeight: "600",
  },
  menuCategoryChips: {
    gap: 8,
    paddingRight: 12,
  },
  menuChip: {
    paddingHorizontal: 12,
    paddingVertical: 8,
    borderRadius: 16,
    backgroundColor: "#fff",
    borderWidth: 1,
    borderColor: "#e0e0e0",
  },
  menuChipActive: {
    backgroundColor: "#6200ee",
    borderColor: "#6200ee",
  },
  menuChipText: {
    fontSize: 12,
    color: "#444",
    fontWeight: "600",
  },
  menuChipTextActive: {
    color: "#fff",
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
    overflow: "hidden",
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
