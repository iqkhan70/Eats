import React, { useEffect, useMemo, useState } from 'react';
import {
  View,
  Text,
  StyleSheet,
  FlatList,
  TouchableOpacity,
  Image,
  TextInput,
  KeyboardAvoidingView,
  Platform,
  Keyboard,
  TouchableWithoutFeedback,
} from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useRouter, useLocalSearchParams } from 'expo-router';
import { api } from '../../services/api';

interface Restaurant {
  restaurantId: string;
  name: string;
  cuisineType?: string;
  address: string;
  rating?: number;
  reviewCount?: number;
  imageUrl?: string;
}

export default function RestaurantsScreen() {
  const router = useRouter();
  const params = useLocalSearchParams();

  const [restaurants, setRestaurants] = useState<Restaurant[]>([]);
  const [loading, setLoading] = useState(true);

  // Search
  const [searchText, setSearchText] = useState('');
  const [debouncedSearch, setDebouncedSearch] = useState('');

  // Active filters (from query params)
  const activeCategory = typeof params.category === 'string' ? params.category : '';
  const activeLocation = typeof params.location === 'string' ? params.location : '';
  const hasFilters = !!(activeCategory || activeLocation);

  // Debounce search (300ms)
  useEffect(() => {
    const t = setTimeout(() => setDebouncedSearch(searchText.trim()), 300);
    return () => clearTimeout(t);
  }, [searchText]);

  useEffect(() => {
    loadRestaurants();
    // If user changes filters (location/category), usually it’s nicer to clear the search
    setSearchText('');
    setDebouncedSearch('');
  }, [params.location, params.category]);

  const clearFilters = () => {
    setSearchText('');
    setDebouncedSearch('');
    Keyboard.dismiss();
    router.replace('/restaurants'); // ✅ back to ALL restaurants (no params)
  };

  const loadRestaurants = async () => {
    try {
      setLoading(true);

      const queryParams: any = {};
      if (params.location) queryParams.location = params.location;
      if (params.category) queryParams.cuisineType = params.category;

      const response = await api.get<Restaurant[]>('/MobileBff/restaurants', { params: queryParams });

      const mappedRestaurants = response.data.map((r: any) => ({
        restaurantId: r.restaurantId || r.id,
        name: r.name,
        cuisineType: r.cuisineType,
        address: r.address,
        rating: r.rating,
        reviewCount: r.reviewCount,
        imageUrl: r.imageUrl,
      }));

      setRestaurants(mappedRestaurants);
    } catch (error) {
      console.error('Error loading restaurants:', error);
      setRestaurants([]);
    } finally {
      setLoading(false);
    }
  };

  const filteredRestaurants = useMemo(() => {
    if (!debouncedSearch) return restaurants;

    const q = debouncedSearch.toLowerCase();

    return restaurants.filter((r) => {
      const haystack = `${r.name} ${r.cuisineType ?? ''} ${r.address ?? ''}`.toLowerCase();
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
          <Ionicons name="restaurant" size={40} color="#ccc" />
        </View>
      )}

      <View style={styles.restaurantInfo}>
        <Text style={styles.restaurantName}>{item.name}</Text>
        {!!item.cuisineType && <Text style={styles.cuisineType}>{item.cuisineType}</Text>}
        <Text style={styles.address} numberOfLines={2}>
          {item.address}
        </Text>

        <View style={styles.ratingContainer}>
          <Ionicons name="star" size={16} color="#FFD700" />
          <Text style={styles.rating}>{item.rating ?? '-'}</Text>
          <Text style={styles.reviewCount}>({item.reviewCount ?? 0} reviews)</Text>
        </View>
      </View>

      <Ionicons name="chevron-forward" size={20} color="#666" />
    </TouchableOpacity>
  );

  const ListHeader = (
    <View style={styles.searchHeader}>
      <View style={styles.searchContainer}>
        <Ionicons name="search" size={18} color="#666" style={styles.searchIcon} />
        <TextInput
          value={searchText}
          onChangeText={setSearchText}
          placeholder="Search restaurants (name, cuisine, address)…"
          placeholderTextColor="#999"
          style={styles.searchInput}
          autoCapitalize="none"
          autoCorrect={false}
          returnKeyType="search"
          onSubmitEditing={() => Keyboard.dismiss()} // ✅ keyboard down on submit
        />
        {!!searchText && (
          <TouchableOpacity
            onPress={() => setSearchText('')}
            style={styles.clearBtn}
            hitSlop={{ top: 8, bottom: 8, left: 8, right: 8 }}
          >
            <Ionicons name="close-circle" size={18} color="#666" />
          </TouchableOpacity>
        )}
      </View>

      {/* ✅ Active filters row with Clear */}
      {hasFilters && (
        <View style={styles.filtersRow}>
          <Ionicons name="funnel-outline" size={16} color="#666" />
          <Text style={styles.filtersText} numberOfLines={1}>
            {activeCategory ? `Category: ${activeCategory}` : ''}
            {activeCategory && activeLocation ? ' • ' : ''}
            {activeLocation ? `Location: ${activeLocation}` : ''}
          </Text>

          <TouchableOpacity onPress={clearFilters} style={styles.clearFiltersBtn}>
            <Text style={styles.clearFiltersBtnText}>Clear</Text>
          </TouchableOpacity>
        </View>
      )}

      {!!debouncedSearch && (
        <Text style={styles.resultsText}>
          Showing {filteredRestaurants.length} result{filteredRestaurants.length === 1 ? '' : 's'}
        </Text>
      )}
    </View>
  );

  if (loading) {
    return (
      <View style={styles.centerContainer}>
        <Text>Loading restaurants...</Text>
      </View>
    );
  }

  return (
    <TouchableWithoutFeedback onPress={Keyboard.dismiss} accessible={false}>
      <KeyboardAvoidingView
        style={styles.container}
        behavior={Platform.OS === 'ios' ? 'padding' : 'height'}
        keyboardVerticalOffset={Platform.OS === 'ios' ? 0 : 0}
      >
        <FlatList
          data={filteredRestaurants}
          renderItem={renderRestaurant}
          keyExtractor={(item) => item.restaurantId}
          contentContainerStyle={styles.listContent}
          ListHeaderComponent={ListHeader}
          stickyHeaderIndices={[0]}
          keyboardDismissMode="on-drag" // ✅ drag list -> keyboard down
          keyboardShouldPersistTaps="handled"
          ListEmptyComponent={
            <View style={styles.centerContainer}>
              <Text style={styles.emptyText}>
                {debouncedSearch ? 'No matches found' : 'No restaurants found'}
              </Text>
            </View>
          }
        />
      </KeyboardAvoidingView>
    </TouchableWithoutFeedback>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: '#f5f5f5' },

  listContent: {
    padding: 16,
    paddingTop: 12,
    paddingBottom: 24,
  },

  // Sticky header wrapper
  searchHeader: {
    backgroundColor: '#f5f5f5',
    paddingHorizontal: 16,
    paddingTop: 12,
    paddingBottom: 10,
    borderBottomWidth: 1,
    borderBottomColor: '#e9e9e9',
  },

  searchContainer: {
    flexDirection: 'row',
    alignItems: 'center',
  },

  searchIcon: {
    position: 'absolute',
    left: 12,
    zIndex: 2,
  },

  searchInput: {
    flex: 1,
    backgroundColor: '#fff',
    borderRadius: 10,
    paddingLeft: 36,
    paddingRight: 36,
    paddingVertical: 10,
    borderWidth: 1,
    borderColor: '#e0e0e0',
    fontSize: 14,
    color: '#333',
  },

  clearBtn: {
    position: 'absolute',
    right: 10,
  },

  resultsText: {
    marginTop: 8,
    fontSize: 12,
    color: '#777',
  },

  // ✅ Active filters row styles
  filtersRow: {
    marginTop: 10,
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: '#fff',
    borderRadius: 10,
    paddingHorizontal: 12,
    paddingVertical: 10,
    borderWidth: 1,
    borderColor: '#e0e0e0',
  },
  filtersText: {
    flex: 1,
    marginLeft: 8,
    fontSize: 12,
    color: '#555',
  },
  clearFiltersBtn: {
    backgroundColor: '#6200ee',
    paddingHorizontal: 12,
    paddingVertical: 6,
    borderRadius: 8,
  },
  clearFiltersBtnText: {
    color: '#fff',
    fontSize: 12,
    fontWeight: '600',
  },

  restaurantCard: {
    flexDirection: 'row',
    backgroundColor: '#fff',
    borderRadius: 12,
    padding: 12,
    marginBottom: 12,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.1,
    shadowRadius: 4,
    elevation: 3,
    alignItems: 'center',
  },

  restaurantImage: { width: 80, height: 80, borderRadius: 8, marginRight: 12 },

  restaurantImagePlaceholder: {
    width: 80,
    height: 80,
    borderRadius: 8,
    backgroundColor: '#f0f0f0',
    justifyContent: 'center',
    alignItems: 'center',
    marginRight: 12,
  },

  restaurantInfo: { flex: 1 },

  restaurantName: { fontSize: 18, fontWeight: '600', color: '#333', marginBottom: 4 },

  cuisineType: { fontSize: 14, color: '#666', marginBottom: 4 },

  address: { fontSize: 12, color: '#999', marginBottom: 8 },

  ratingContainer: { flexDirection: 'row', alignItems: 'center' },

  rating: { fontSize: 14, fontWeight: '600', color: '#333', marginLeft: 4 },

  reviewCount: { fontSize: 12, color: '#666', marginLeft: 4 },

  centerContainer: { flex: 1, justifyContent: 'center', alignItems: 'center', padding: 40 },

  emptyText: { fontSize: 16, color: '#666' },
});
