import { View, Text, StyleSheet, FlatList, TouchableOpacity, Image } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useRouter, useLocalSearchParams } from 'expo-router';
import React, { useState, useEffect } from 'react';
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

  useEffect(() => {
    loadRestaurants();
  }, [params.location, params.category]);

  const loadRestaurants = async () => {
    try {
      setLoading(true);
      
      // Build query params
      const queryParams: any = {};
      if (params.location) queryParams.location = params.location;
      if (params.category) queryParams.cuisineType = params.category;
      
      const response = await api.get<Restaurant[]>('/MobileBff/restaurants', { params: queryParams });
      
      // Map backend DTO to frontend format
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
      setRestaurants([]); // Set to empty on error
    } finally {
      setLoading(false);
    }
  };

  const renderRestaurant = ({ item }: { item: Restaurant }) => (
    <TouchableOpacity
      style={styles.restaurantCard}
      onPress={() => router.push(`/restaurants/${item.restaurantId}/menu`)}
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
        <Text style={styles.cuisineType}>{item.cuisineType}</Text>
        <Text style={styles.address}>{item.address}</Text>
        <View style={styles.ratingContainer}>
          <Ionicons name="star" size={16} color="#FFD700" />
          <Text style={styles.rating}>{item.rating}</Text>
          <Text style={styles.reviewCount}>({item.reviewCount} reviews)</Text>
        </View>
      </View>
      <Ionicons name="chevron-forward" size={20} color="#666" />
    </TouchableOpacity>
  );

  if (loading) {
    return (
      <View style={styles.centerContainer}>
        <Text>Loading restaurants...</Text>
      </View>
    );
  }

  return (
    <View style={styles.container}>
      <FlatList
        data={restaurants}
        renderItem={renderRestaurant}
        keyExtractor={(item) => item.restaurantId}
        contentContainerStyle={styles.listContent}
        ListEmptyComponent={
          <View style={styles.centerContainer}>
            <Text style={styles.emptyText}>No restaurants found</Text>
          </View>
        }
      />
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#f5f5f5',
  },
  listContent: {
    padding: 16,
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
  },
  restaurantImage: {
    width: 80,
    height: 80,
    borderRadius: 8,
    marginRight: 12,
  },
  restaurantImagePlaceholder: {
    width: 80,
    height: 80,
    borderRadius: 8,
    backgroundColor: '#f0f0f0',
    justifyContent: 'center',
    alignItems: 'center',
    marginRight: 12,
  },
  restaurantInfo: {
    flex: 1,
  },
  restaurantName: {
    fontSize: 18,
    fontWeight: '600',
    color: '#333',
    marginBottom: 4,
  },
  cuisineType: {
    fontSize: 14,
    color: '#666',
    marginBottom: 4,
  },
  address: {
    fontSize: 12,
    color: '#999',
    marginBottom: 8,
  },
  ratingContainer: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  rating: {
    fontSize: 14,
    fontWeight: '600',
    color: '#333',
    marginLeft: 4,
  },
  reviewCount: {
    fontSize: 12,
    color: '#666',
    marginLeft: 4,
  },
  centerContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    padding: 40,
  },
  emptyText: {
    fontSize: 16,
    color: '#666',
  },
});
