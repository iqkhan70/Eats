import React, { useState, useRef, useEffect } from 'react';
import { View, Text, StyleSheet, ScrollView, TouchableOpacity, TextInput } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useRouter } from 'expo-router';

export default function HomeScreen() {
  const router = useRouter();
  const searchInputRef = useRef<TextInput>(null);
  const [searchLocation, setSearchLocation] = useState('');
  const [showAllCategories, setShowAllCategories] = useState(false);
  const initialCategoryCount = 6;

  useEffect(() => {
    // Focus the search input when component mounts
    const timer = setTimeout(() => {
      searchInputRef.current?.focus();
    }, 100);
    return () => clearTimeout(timer);
  }, []);

  const categories = [
    { id: 1, name: 'Traditional', icon: 'restaurant' },
    { id: 2, name: 'Fast Food', icon: 'fast-food' },
    { id: 3, name: 'Desserts', icon: 'ice-cream' },
    { id: 4, name: 'Beverages', icon: 'cafe' },
    { id: 5, name: 'Vegetarian', icon: 'leaf' },
    { id: 6, name: 'Vegan', icon: 'flower' },
    { id: 7, name: 'Seafood', icon: 'fish' },
    { id: 8, name: 'BBQ', icon: 'flame' },
    { id: 9, name: 'Italian', icon: 'pizza' },
    { id: 10, name: 'Asian', icon: 'restaurant' },
  ];

  const displayedCategories = showAllCategories 
    ? categories 
    : categories.slice(0, initialCategoryCount);

  return (
    <ScrollView style={styles.container}>
      <View style={styles.header}>
        <Text style={styles.title}>Welcome to TraditionalEats</Text>
        <Text style={styles.subtitle}>Discover authentic traditional food</Text>
      </View>

      <View style={styles.searchContainer}>
        <Ionicons name="location" size={20} color="#666" style={styles.searchIcon} />
        <TextInput
          ref={searchInputRef}
          style={styles.searchInput}
          placeholder="Enter your location"
          value={searchLocation}
          onChangeText={setSearchLocation}
          autoFocus={true}
        />
        <TouchableOpacity 
          style={styles.searchButton}
          onPress={() => router.push(`/restaurants?location=${encodeURIComponent(searchLocation)}`)}
        >
          <Ionicons name="search" size={20} color="#fff" />
        </TouchableOpacity>
      </View>

      <View style={styles.section}>
        <Text style={styles.sectionTitle}>Popular Categories</Text>
        <View style={styles.categoryGrid}>
          {displayedCategories.map((category) => (
            <TouchableOpacity
              key={category.id}
              style={styles.categoryCard}
              onPress={() => router.push(`/restaurants?category=${encodeURIComponent(category.name)}`)}
            >
              <Ionicons name={category.icon as any} size={40} color="#6200ee" />
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
              {showAllCategories ? 'Show Less' : `Show All (${categories.length})`}
            </Text>
            <Ionicons 
              name={showAllCategories ? 'chevron-up' : 'chevron-down'} 
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
          onPress={() => router.push('/restaurants')}
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
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#f5f5f5',
  },
  header: {
    padding: 20,
    backgroundColor: '#fff',
    borderBottomWidth: 1,
    borderBottomColor: '#e0e0e0',
  },
  title: {
    fontSize: 24,
    fontWeight: 'bold',
    color: '#333',
    marginBottom: 4,
  },
  subtitle: {
    fontSize: 14,
    color: '#666',
  },
  searchContainer: {
    flexDirection: 'row',
    alignItems: 'center',
    margin: 16,
    paddingHorizontal: 12,
    backgroundColor: '#fff',
    borderRadius: 8,
    borderWidth: 1,
    borderColor: '#e0e0e0',
  },
  searchIcon: {
    marginRight: 8,
  },
  searchInput: {
    flex: 1,
    height: 44,
    fontSize: 16,
  },
  searchButton: {
    backgroundColor: '#6200ee',
    padding: 10,
    borderRadius: 6,
  },
  section: {
    marginTop: 20,
    paddingHorizontal: 16,
  },
  sectionTitle: {
    fontSize: 20,
    fontWeight: 'bold',
    color: '#333',
    marginBottom: 12,
  },
  categoryGrid: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    justifyContent: 'space-between',
  },
  categoryCard: {
    width: '48%',
    backgroundColor: '#fff',
    borderRadius: 12,
    padding: 20,
    alignItems: 'center',
    marginBottom: 12,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.1,
    shadowRadius: 4,
    elevation: 3,
  },
  categoryName: {
    marginTop: 8,
    fontSize: 14,
    fontWeight: '600',
    color: '#333',
  },
  restaurantCard: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    backgroundColor: '#fff',
    borderRadius: 12,
    padding: 16,
    marginBottom: 12,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.1,
    shadowRadius: 4,
    elevation: 3,
  },
  restaurantInfo: {
    flexDirection: 'row',
    alignItems: 'center',
    flex: 1,
  },
  restaurantDetails: {
    marginLeft: 12,
    flex: 1,
  },
  restaurantName: {
    fontSize: 16,
    fontWeight: '600',
    color: '#333',
    marginBottom: 4,
  },
  restaurantAddress: {
    fontSize: 14,
    color: '#666',
    marginBottom: 4,
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
  showMoreButton: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    marginTop: 8,
    paddingVertical: 12,
  },
  showMoreText: {
    fontSize: 14,
    fontWeight: '600',
    color: '#6200ee',
    marginRight: 4,
  },
});
