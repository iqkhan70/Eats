import React, { useState, useRef, useEffect, useCallback } from "react";
import {
  View,
  Text,
  StyleSheet,
  ScrollView,
  TouchableOpacity,
  TextInput,
  KeyboardAvoidingView,
  Platform,
  Keyboard,
  TouchableWithoutFeedback,
  Pressable,
} from "react-native";
import Slider from "@react-native-community/slider";
import { Ionicons } from "@expo/vector-icons";
import { useRouter } from "expo-router";
import { api } from "../../services/api";

const ZIP_REGEX = /^\s*(\d{5})(?:-\d{4})?\s*$/;

export default function HomeScreen() {
  const router = useRouter();
  const searchInputRef = useRef<TextInput>(null);

  const [searchLocation, setSearchLocation] = useState("");
  const [suggestions, setSuggestions] = useState<string[]>([]);
  const [showSuggestions, setShowSuggestions] = useState(false);
  const [showAllCategories, setShowAllCategories] = useState(false);
  const [distanceMiles, setDistanceMiles] = useState(25);
  const [geocodeLoading, setGeocodeLoading] = useState(false);

  const initialCategoryCount = 6;

  const debounceTimerRef = useRef<NodeJS.Timeout | null>(null);
  const blurTimeoutRef = useRef<NodeJS.Timeout | null>(null);

  useEffect(() => {
    // Focus the search input when component mounts
    const timer = setTimeout(() => {
      searchInputRef.current?.focus();
    }, 100);
    return () => clearTimeout(timer);
  }, []);

  const loadSuggestions = useCallback(async (query: string) => {
    if (!query || query.length < 2) {
      setSuggestions([]);
      setShowSuggestions(false);
      return;
    }

    // Clear previous timer
    if (debounceTimerRef.current) {
      clearTimeout(debounceTimerRef.current);
    }

    // Debounce API call
    debounceTimerRef.current = setTimeout(async () => {
      try {
        const response = await api.get<string[]>(
          "/MobileBff/search-suggestions",
          {
            params: { query, maxResults: 10 },
          },
        );

        setSuggestions(response.data);
        setShowSuggestions(response.data.length > 0);
      } catch (error: any) {
        console.error("Error loading suggestions:", error);
        setSuggestions([]);
        setShowSuggestions(false);
      }
    }, 300);
  }, []);

  const handleSearchChange = (text: string) => {
    setSearchLocation(text);
    loadSuggestions(text);
  };

  const navigateToRestaurants = async (
    location?: string,
    category?: string,
  ) => {
    setShowSuggestions(false);
    setSuggestions([]);
    Keyboard.dismiss();

    const loc = (location ?? searchLocation ?? "").trim();
    const qs: string[] = [];
    if (category?.trim())
      qs.push(`category=${encodeURIComponent(category.trim())}`);

    const zipMatch = loc.match(ZIP_REGEX);
    if (zipMatch && loc) {
      const zip = zipMatch[1];
      setGeocodeLoading(true);
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
      } finally {
        setGeocodeLoading(false);
      }
    } else if (loc) {
      qs.push(`location=${encodeURIComponent(loc)}`);
      qs.push(
        `radiusMiles=${Math.round(Math.max(1, Math.min(100, distanceMiles)))}`,
      );
    }

    const query = qs.length ? `?${qs.join("&")}` : "";
    router.push(`/restaurants${query}`);
  };

  const handleSuggestionSelect = (suggestion: string) => {
    // Cancel any pending blur timeout
    if (blurTimeoutRef.current) {
      clearTimeout(blurTimeoutRef.current);
      blurTimeoutRef.current = null;
    }

    setSearchLocation(suggestion);
    setShowSuggestions(false);
    setSuggestions([]);

    // Blur to dismiss keyboard (optional, Keyboard.dismiss also works)
    searchInputRef.current?.blur();
    Keyboard.dismiss();

    navigateToRestaurants(suggestion);
  };

  const hideSuggestionsAndKeyboard = () => {
    setShowSuggestions(false);
    Keyboard.dismiss();
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
    <TouchableWithoutFeedback
      onPress={hideSuggestionsAndKeyboard}
      accessible={false}
    >
      <KeyboardAvoidingView
        style={styles.container}
        behavior={Platform.OS === "ios" ? "padding" : "height"}
      >
        <View style={styles.header}>
          <Text style={styles.title}>Welcome to TraditionalEats</Text>
          <Text style={styles.subtitle}>
            Discover authentic traditional food
          </Text>
        </View>

        {/* Search + Suggestions */}
        <View style={styles.searchWrapper}>
          <View style={styles.searchContainer}>
            <Ionicons
              name="location"
              size={20}
              color="#666"
              style={styles.searchIcon}
            />

            <TextInput
              ref={searchInputRef}
              style={styles.searchInput}
              placeholder="Address or ZIP code"
              value={searchLocation}
              onChangeText={handleSearchChange}
              onFocus={() => {
                if (suggestions.length > 0) setShowSuggestions(true);
              }}
              // Keep onBlur empty to avoid the “double tap” issue
              onBlur={() => {}}
              autoFocus
              returnKeyType="search"
              onSubmitEditing={() => navigateToRestaurants(searchLocation)}
            />

            <TouchableOpacity
              style={[
                styles.searchButton,
                geocodeLoading && styles.searchButtonDisabled,
              ]}
              onPress={() => navigateToRestaurants(searchLocation)}
              disabled={geocodeLoading}
            >
              <Ionicons name="search" size={20} color="#fff" />
            </TouchableOpacity>
          </View>

          {/* Suggestions dropdown: wrap in Pressable to stop outside tap handler */}
          {showSuggestions && suggestions.length > 0 && (
            <Pressable
              style={styles.suggestionsContainer}
              onPress={(e) => {
                // Prevent tap from bubbling to the outer TouchableWithoutFeedback
                e.stopPropagation?.();
              }}
            >
              {suggestions.map((item, index) => (
                <TouchableOpacity
                  key={`${item}-${index}`}
                  style={styles.suggestionItem}
                  onPress={() => handleSuggestionSelect(item)}
                  activeOpacity={0.7}
                >
                  <Ionicons
                    name="location-outline"
                    size={16}
                    color="#666"
                    style={styles.suggestionIcon}
                  />
                  <Text style={styles.suggestionText}>{item}</Text>
                </TouchableOpacity>
              ))}
            </Pressable>
          )}

          {/* Distance slider: 0–100 miles */}
          <View style={styles.distanceRow}>
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
          </View>
        </View>

        {/* Content */}
        <ScrollView
          style={styles.scrollView}
          keyboardDismissMode="on-drag" // ✅ scroll -> keyboard down
          keyboardShouldPersistTaps="handled"
        >
          <View style={styles.section}>
            <Text style={styles.sectionTitle}>Popular Categories</Text>

            <View style={styles.categoryGrid}>
              {displayedCategories.map((category) => (
                <TouchableOpacity
                  key={category.id}
                  style={styles.categoryCard}
                  onPress={() =>
                    navigateToRestaurants(undefined, category.name)
                  }
                  activeOpacity={0.85}
                >
                  <Ionicons
                    name={category.icon as any}
                    size={40}
                    color="#6200ee"
                  />
                  <Text style={styles.categoryName}>{category.name}</Text>
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

          <View style={{ height: 24 }} />
        </ScrollView>
      </KeyboardAvoidingView>
    </TouchableWithoutFeedback>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: "#f5f5f5" },
  scrollView: { flex: 1 },

  header: {
    padding: 20,
    backgroundColor: "#fff",
    borderBottomWidth: 1,
    borderBottomColor: "#e0e0e0",
  },
  title: { fontSize: 24, fontWeight: "bold", color: "#333", marginBottom: 4 },
  subtitle: { fontSize: 14, color: "#666" },

  searchWrapper: {
    margin: 16,
    marginBottom: 0,
    position: "relative",
    zIndex: 1000,
    backgroundColor: "#f5f5f5",
  },
  searchContainer: {
    flexDirection: "row",
    alignItems: "center",
    paddingHorizontal: 12,
    backgroundColor: "#fff",
    borderRadius: 8,
    borderWidth: 1,
    borderColor: "#e0e0e0",
  },
  searchIcon: { marginRight: 8 },
  searchInput: { flex: 1, height: 44, fontSize: 16 },
  searchButton: { backgroundColor: "#6200ee", padding: 10, borderRadius: 6 },
  searchButtonDisabled: { opacity: 0.6 },

  distanceRow: { marginTop: 12, paddingHorizontal: 4 },
  distanceLabel: { fontSize: 14, color: "#555", marginBottom: 4 },
  slider: { width: "100%", height: 32 },

  suggestionsContainer: {
    position: "absolute",
    top: "100%",
    left: 0,
    right: 0,
    marginTop: 4,
    backgroundColor: "#fff",
    borderRadius: 8,
    borderWidth: 1,
    borderColor: "#e0e0e0",
    maxHeight: 200,
    shadowColor: "#000",
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.1,
    shadowRadius: 4,
    elevation: 10,
    zIndex: 1001,
  },
  suggestionItem: {
    flexDirection: "row",
    alignItems: "center",
    padding: 12,
    borderBottomWidth: 1,
    borderBottomColor: "#f0f0f0",
  },
  suggestionIcon: { marginRight: 8 },
  suggestionText: { fontSize: 14, color: "#333", flex: 1 },

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
  },
  categoryCard: {
    width: "48%",
    backgroundColor: "#fff",
    borderRadius: 12,
    padding: 20,
    alignItems: "center",
    marginBottom: 12,
    shadowColor: "#000",
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.1,
    shadowRadius: 4,
    elevation: 3,
  },
  categoryName: {
    marginTop: 8,
    fontSize: 14,
    fontWeight: "600",
    color: "#333",
  },

  restaurantCard: {
    flexDirection: "row",
    alignItems: "center",
    justifyContent: "space-between",
    backgroundColor: "#fff",
    borderRadius: 12,
    padding: 16,
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
