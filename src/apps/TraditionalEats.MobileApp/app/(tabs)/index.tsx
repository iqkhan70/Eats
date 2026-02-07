import React, { useState, useCallback } from "react";
import {
  View,
  Text,
  StyleSheet,
  ScrollView,
  TouchableOpacity,
  KeyboardAvoidingView,
  Platform,
  Keyboard,
} from "react-native";
import Slider from "@react-native-community/slider";
import { Ionicons } from "@expo/vector-icons";
import { BlurView } from "expo-blur";
import { useRouter } from "expo-router";
import { useSafeAreaInsets } from "react-native-safe-area-context";
import { api } from "../../services/api";
import BottomSearchBar from "../../components/BottomSearchBar";

const ZIP_REGEX = /^\s*(\d{5})(?:-\d{4})?\s*$/;

export default function HomeScreen() {
  const router = useRouter();
  const insets = useSafeAreaInsets();

  const [showAllCategories, setShowAllCategories] = useState(false);
  const [distanceMiles, setDistanceMiles] = useState(25);

  const initialCategoryCount = 6;

  const navigateToRestaurants = async (
    location?: string,
    category?: string,
  ) => {
    Keyboard.dismiss();

    const loc = (location ?? "").trim();
    const qs: string[] = [];
    if (category?.trim())
      qs.push(`category=${encodeURIComponent(category.trim())}`);

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

  const handleSearch = async (query: string) => {
    Keyboard.dismiss();
    await navigateToRestaurants(query);
  };

  const handleSuggestionSelect = (suggestion: string) => {
    handleSearch(suggestion);
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
          <Text style={styles.title}>Welcome to TraditionalEats</Text>
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
          <Text style={styles.sectionTitle}>Nearby Restaurants</Text>

          <TouchableOpacity
            style={styles.restaurantCard}
            onPress={() => navigateToRestaurants()}
            activeOpacity={0.85}
          >
            <View style={styles.restaurantInfo}>
              <Ionicons name="restaurant" size={24} color="#6200ee" />
              <View style={styles.restaurantDetails}>
                <Text style={styles.restaurantName}>Traditional Kitchen</Text>
                <Text style={styles.restaurantAddress}>123 Main St</Text>
                <View style={styles.ratingContainer}>
                  <Ionicons name="star" size={16} color="#FFD700" />
                  <Text style={styles.rating}>4.5</Text>
                  <Text style={styles.reviewCount}>(120 reviews)</Text>
                </View>
              </View>
            </View>
            <Ionicons name="chevron-forward" size={20} color="#666" />
          </TouchableOpacity>
        </View>
      </ScrollView>

      {/* Bottom Search Bar */}
      <BottomSearchBar
        onSearch={handleSearch}
        placeholder="Search for restaurants, cuisine, or location..."
        emptyStateTitle="Search for restaurants"
        emptyStateSubtitle="Enter an address, ZIP code, or restaurant name"
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
  sectionTitle: {
    fontSize: 20,
    fontWeight: "bold",
    color: "#333",
    marginBottom: 12,
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
  restaurantDetails: { marginLeft: 12, flex: 1 },
  restaurantName: {
    fontSize: 16,
    fontWeight: "600",
    color: "#333",
    marginBottom: 4,
  },
  restaurantAddress: { fontSize: 14, color: "#666", marginBottom: 4 },

  ratingContainer: { flexDirection: "row", alignItems: "center" },
  rating: { fontSize: 14, fontWeight: "600", color: "#333", marginLeft: 4 },
  reviewCount: { fontSize: 12, color: "#666", marginLeft: 4 },

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
