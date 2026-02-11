import React, { useState, useRef, useEffect, useCallback } from "react";
import {
  View,
  Text,
  StyleSheet,
  TextInput,
  TouchableOpacity,
  Animated,
  Modal,
  Pressable,
  Keyboard,
  Platform,
  Dimensions,
  ScrollView,
} from "react-native";
import { Ionicons } from "@expo/vector-icons";
import { useSafeAreaInsets } from "react-native-safe-area-context";
import { BlurView } from "expo-blur";

const { height: SCREEN_HEIGHT } = Dimensions.get("window");

interface BottomSearchBarProps {
  onSearch: (query: string) => void;
  placeholder?: string;
  emptyStateTitle?: string;
  emptyStateSubtitle?: string;
  loadSuggestions?: (query: string) => Promise<string[]>;
  onSuggestionSelect?: (suggestion: string) => void;
  onClear?: () => void;
  initialValue?: string;
}

export default function BottomSearchBar({
  onSearch,
  placeholder = "Search for vendors, cuisine, or location...",
  emptyStateTitle = "Search for vendors",
  emptyStateSubtitle = "Enter an address, ZIP code, or vendor name",
  loadSuggestions,
  onSuggestionSelect,
  onClear,
  initialValue = "",
}: BottomSearchBarProps) {
  const insets = useSafeAreaInsets();
  const [isExpanded, setIsExpanded] = useState(false);
  const [searchText, setSearchText] = useState(initialValue);
  const [suggestions, setSuggestions] = useState<string[]>([]);
  const [isLoadingSuggestions, setIsLoadingSuggestions] = useState(false);

  const searchInputRef = useRef<TextInput>(null);
  const debounceTimerRef = useRef<NodeJS.Timeout | null>(null);
  
  // Animation values
  const expandAnimation = useRef(new Animated.Value(0)).current;
  const backdropOpacity = useRef(new Animated.Value(0)).current;
  const modalTranslateY = useRef(new Animated.Value(SCREEN_HEIGHT)).current;

  // Reset expanded state when component unmounts to prevent stuck modals
  useEffect(() => {
    return () => {
      setIsExpanded(false);
      if (debounceTimerRef.current) {
        clearTimeout(debounceTimerRef.current);
      }
    };
  }, []);

  useEffect(() => {
    if (isExpanded) {
      // Expand animation
      Animated.parallel([
        Animated.timing(expandAnimation, {
          toValue: 1,
          duration: 300,
          useNativeDriver: false,
        }),
        Animated.timing(backdropOpacity, {
          toValue: 1,
          duration: 300,
          useNativeDriver: true,
        }),
        Animated.spring(modalTranslateY, {
          toValue: 0,
          tension: 65,
          friction: 11,
          useNativeDriver: true,
        }),
      ]).start();

      // Focus input after animation
      setTimeout(() => {
        searchInputRef.current?.focus();
      }, 200);
    } else {
      // Collapse animation
      Animated.parallel([
        Animated.timing(expandAnimation, {
          toValue: 0,
          duration: 250,
          useNativeDriver: false,
        }),
        Animated.timing(backdropOpacity, {
          toValue: 0,
          duration: 250,
          useNativeDriver: true,
        }),
        Animated.timing(modalTranslateY, {
          toValue: SCREEN_HEIGHT,
          duration: 250,
          useNativeDriver: true,
        }),
      ]).start();

      Keyboard.dismiss();
    }
  }, [isExpanded]);

  const handleExpand = () => {
    setIsExpanded(true);
  };

  const handleCollapse = () => {
    setIsExpanded(false);
    setSearchText("");
    setSuggestions([]);
    // Stop any running animations and immediately reset to collapsed state
    expandAnimation.stopAnimation(() => {
      expandAnimation.setValue(0);
    });
    backdropOpacity.stopAnimation(() => {
      backdropOpacity.setValue(0);
    });
    modalTranslateY.stopAnimation(() => {
      modalTranslateY.setValue(SCREEN_HEIGHT);
    });
    // Also set immediately to ensure visibility
    expandAnimation.setValue(0);
    backdropOpacity.setValue(0);
    modalTranslateY.setValue(SCREEN_HEIGHT);
  };

  const handleSearchChange = useCallback(
    (text: string) => {
      setSearchText(text);

      if (loadSuggestions && text.length >= 2) {
        // Clear previous timer
        if (debounceTimerRef.current) {
          clearTimeout(debounceTimerRef.current);
        }

        // Debounce API call
        debounceTimerRef.current = setTimeout(async () => {
          setIsLoadingSuggestions(true);
          try {
            const results = await loadSuggestions(text);
            setSuggestions(results);
          } catch (error) {
            console.error("Error loading suggestions:", error);
            setSuggestions([]);
          } finally {
            setIsLoadingSuggestions(false);
          }
        }, 300);
      } else {
        setSuggestions([]);
      }
    },
    [loadSuggestions],
  );

  const handleSuggestionPress = (suggestion: string) => {
    setSearchText(suggestion);
    setSuggestions([]);
    if (onSuggestionSelect) {
      // If onSuggestionSelect is provided, use it and let it handle the search
      // Don't call onSearch with raw suggestion to avoid overwriting extracted search term
      onSuggestionSelect(suggestion);
      handleCollapse();
    } else {
      // If no onSuggestionSelect, use onSearch directly
      handleCollapse();
      onSearch(suggestion);
    }
  };

  const handleSearch = () => {
    if (searchText.trim()) {
      handleCollapse();
      onSearch(searchText.trim());
    }
  };

  const pillHeight = expandAnimation.interpolate({
    inputRange: [0, 1],
    outputRange: [56, 0],
  });

  const pillOpacity = expandAnimation.interpolate({
    inputRange: [0, 1],
    outputRange: [1, 0],
  });

  return (
    <>
      {/* Floating Pill Search Bar */}
      <Animated.View
        style={[
          styles.pillContainer,
          {
            bottom: insets.bottom + 70, // Position above tab bar (tab bar is ~49px + padding)
            opacity: pillOpacity,
            height: pillHeight,
          },
        ]}
        pointerEvents={isExpanded ? "none" : "box-none"}
      >
        <Pressable 
          onPress={handleExpand} 
          style={styles.pill}
          hitSlop={{ top: 10, bottom: 10, left: 10, right: 10 }}
        >
          <Ionicons name="search" size={22} color="#667eea" />
          <Text style={styles.pillText}>{placeholder}</Text>
        </Pressable>
      </Animated.View>

      {/* Full Screen Modal */}
      <Modal
        visible={isExpanded}
        transparent
        animationType="none"
        onRequestClose={handleCollapse}
      >
        <View style={styles.modalContainer}>
          {/* Backdrop with blur effect */}
          <Animated.View
            style={[
              StyleSheet.absoluteFill,
              {
                opacity: backdropOpacity,
              },
            ]}
          >
            <BlurView
              intensity={20}
              tint="dark"
              style={StyleSheet.absoluteFill}
            >
              <Pressable
                style={StyleSheet.absoluteFill}
                onPress={handleCollapse}
              />
            </BlurView>
          </Animated.View>

          {/* Search Modal Content */}
          <Animated.View
            style={[
              styles.modalContent,
              {
                transform: [{ translateY: modalTranslateY }],
                paddingTop: insets.top + 20,
                paddingBottom: insets.bottom + 20,
              },
            ]}
          >
            {/* Search Bar */}
            <View style={styles.searchBarContainer}>
              <View style={styles.searchBar}>
                <Ionicons
                  name="search"
                  size={22}
                  color="#667eea"
                  style={styles.searchIcon}
                />
                <TextInput
                  ref={searchInputRef}
                  style={styles.searchInput}
                  placeholder={placeholder}
                  placeholderTextColor="#999"
                  value={searchText}
                  onChangeText={handleSearchChange}
                  returnKeyType="search"
                  onSubmitEditing={handleSearch}
                  autoFocus
                />
                {searchText.length > 0 && (
                  <TouchableOpacity
                    onPress={() => {
                      setSearchText("");
                      setSuggestions([]);
                      // Clear the search query if onClear is provided
                      if (onClear) {
                        onClear();
                      }
                    }}
                    style={styles.clearButton}
                  >
                    <Ionicons name="close-circle" size={20} color="#666" />
                  </TouchableOpacity>
                )}
                <TouchableOpacity
                  onPress={handleCollapse}
                  style={styles.cancelButton}
                >
                  <Text style={styles.cancelText}>Cancel</Text>
                </TouchableOpacity>
              </View>
            </View>

            {/* Suggestions List */}
            <ScrollView
              style={styles.suggestionsContainer}
              keyboardShouldPersistTaps="handled"
              showsVerticalScrollIndicator={false}
            >
              {isLoadingSuggestions && (
                <View style={styles.loadingContainer}>
                  <Text style={styles.loadingText}>Searching...</Text>
                </View>
              )}

              {!isLoadingSuggestions && suggestions.length > 0 && (
                <View style={styles.suggestionsList}>
                  <Text style={styles.suggestionsHeader}>Suggestions</Text>
                  {suggestions.map((item, index) => (
                    <TouchableOpacity
                      key={`${item}-${index}`}
                      style={styles.suggestionItem}
                      onPress={() => handleSuggestionPress(item)}
                      activeOpacity={0.7}
                    >
                      <Ionicons
                        name="location-outline"
                        size={20}
                        color="#6200ee"
                        style={styles.suggestionIcon}
                      />
                      <Text style={styles.suggestionText}>{item}</Text>
                      <Ionicons name="chevron-forward" size={18} color="#ccc" />
                    </TouchableOpacity>
                  ))}
                </View>
              )}

              {!isLoadingSuggestions &&
                searchText.length >= 2 &&
                suggestions.length === 0 && (
                  <View style={styles.emptyContainer}>
                    <Ionicons name="search-outline" size={48} color="#ccc" />
                    <Text style={styles.emptyText}>No results found</Text>
                    <Text style={styles.emptySubtext}>
                      Try a different search term
                    </Text>
                  </View>
                )}

              {searchText.length === 0 && (
                <View style={styles.emptyContainer}>
                  <Ionicons name="location-outline" size={48} color="#6200ee" />
                  <Text style={styles.emptyText}>{emptyStateTitle}</Text>
                  <Text style={styles.emptySubtext}>
                    {emptyStateSubtitle}
                  </Text>
                </View>
              )}
            </ScrollView>
          </Animated.View>
        </View>
      </Modal>
    </>
  );
}

const styles = StyleSheet.create({
  pillContainer: {
    position: "absolute",
    left: 16,
    right: 16,
    zIndex: 999, // Lower than modal but high enough to be above content
    overflow: "visible", // Allow touches to pass through empty areas
  },
  pill: {
    flexDirection: "row",
    alignItems: "center",
    backgroundColor: "#fff",
    borderRadius: 28,
    paddingHorizontal: 20,
    paddingVertical: 16,
    shadowColor: "#667eea",
    shadowOffset: { width: 0, height: 4 },
    shadowOpacity: 0.25,
    shadowRadius: 12,
    elevation: 8,
    borderWidth: 2,
    borderColor: "#667eea",
  },
  pillText: {
    marginLeft: 12,
    fontSize: 16,
    color: "#333",
    flex: 1,
    fontWeight: "500",
  },
  modalContainer: {
    flex: 1,
  },
  backdrop: {
    ...StyleSheet.absoluteFillObject,
  },
  modalContent: {
    flex: 1,
    backgroundColor: "#fff",
    borderTopLeftRadius: 20,
    borderTopRightRadius: 20,
    overflow: "hidden",
  },
  searchBarContainer: {
    paddingHorizontal: 16,
    paddingBottom: 16,
    borderBottomWidth: 1,
    borderBottomColor: "#e0e0e0",
  },
  searchBar: {
    flexDirection: "row",
    alignItems: "center",
    backgroundColor: "#fff",
    borderRadius: 12,
    paddingHorizontal: 16,
    paddingVertical: 12,
    borderWidth: 2,
    borderColor: "#667eea",
    shadowColor: "#667eea",
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.15,
    shadowRadius: 8,
    elevation: 4,
  },
  searchIcon: {
    marginRight: 12,
  },
  searchInput: {
    flex: 1,
    fontSize: 16,
    color: "#333",
    padding: 0,
  },
  clearButton: {
    padding: 4,
    marginRight: 8,
  },
  cancelButton: {
    paddingLeft: 12,
  },
  cancelText: {
    fontSize: 16,
    color: "#6200ee",
    fontWeight: "500",
  },
  suggestionsContainer: {
    flex: 1,
  },
  loadingContainer: {
    padding: 32,
    alignItems: "center",
  },
  loadingText: {
    fontSize: 14,
    color: "#666",
  },
  suggestionsList: {
    paddingTop: 8,
  },
  suggestionsHeader: {
    fontSize: 12,
    fontWeight: "600",
    color: "#999",
    textTransform: "uppercase",
    letterSpacing: 0.5,
    paddingHorizontal: 16,
    paddingVertical: 8,
  },
  suggestionItem: {
    flexDirection: "row",
    alignItems: "center",
    paddingHorizontal: 16,
    paddingVertical: 14,
    borderBottomWidth: 1,
    borderBottomColor: "#f0f0f0",
  },
  suggestionIcon: {
    marginRight: 12,
  },
  suggestionText: {
    flex: 1,
    fontSize: 16,
    color: "#333",
  },
  emptyContainer: {
    flex: 1,
    alignItems: "center",
    justifyContent: "center",
    padding: 48,
  },
  emptyText: {
    fontSize: 18,
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
});
