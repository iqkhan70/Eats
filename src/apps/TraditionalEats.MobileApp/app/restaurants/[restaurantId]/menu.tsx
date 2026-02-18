import React, { useState, useEffect, useCallback, useRef, useLayoutEffect } from 'react';
import { View, Text, StyleSheet, ScrollView, TouchableOpacity, ActivityIndicator, Alert } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useRouter, useLocalSearchParams, useNavigation } from 'expo-router';
import { SafeAreaView } from 'react-native-safe-area-context';
import { api } from '../../../services/api';
import { cartService } from '../../../services/cart';
import ReviewDisplay, { Review } from '../../../components/ReviewDisplay';
import ReviewRating from '../../../components/ReviewRating';
import { authService } from '../../../services/auth';

interface MenuItem {
  menuItemId: string;
  restaurantId: string;
  categoryId: string;
  categoryName?: string;
  name: string;
  description?: string;
  price: number;
  imageUrl?: string;
  isAvailable: boolean;
  dietaryTags: string[];
}

interface Category {
  categoryId: string;
  name: string;
  description?: string;
}

export default function MenuScreen() {
  const router = useRouter();
  const navigation = useNavigation();
  const params = useLocalSearchParams<{
    restaurantId?: string;
    refreshedAt?: string;
    categoryId?: string;
  }>();

  const restaurantId = params.restaurantId as string;
  const refreshedAt = params.refreshedAt; // 👈 will change after save

  // Hide default header - we use custom header with SafeAreaView
  useLayoutEffect(() => {
    navigation.setOptions({
      headerShown: false,
    });
  }, [navigation]);

  const [menuItems, setMenuItems] = useState<MenuItem[]>([]);
  const [categories, setCategories] = useState<Category[]>([]);
  const [selectedCategoryId, setSelectedCategoryId] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [currentCartId, setCurrentCartId] = useState<string | null>(null);
  const [addingItemId, setAddingItemId] = useState<string | null>(null);
  const [reviews, setReviews] = useState<Review[]>([]);
  const [loadingReviews, setLoadingReviews] = useState(false);
  const [restaurantRating, setRestaurantRating] = useState<{
    averageRating: number;
    totalReviews: number;
  } | null>(null);
  const [activeTab, setActiveTab] = useState<'menu' | 'reviews'>('menu');
  const [userCanReview, setUserCanReview] = useState(false);

  const isAddingToCartRef = useRef(false);

  const loadMenu = useCallback(async () => {
    try {
      setLoading(true);

      const queryParams: any = {};
      if (selectedCategoryId) queryParams.categoryId = selectedCategoryId;

      // ✅ cache buster
      queryParams.__ts = Date.now();

      const response = await api.get<MenuItem[]>(
        `/MobileBff/restaurants/${restaurantId}/menu`,
        {
          params: queryParams,
          headers: {
            'Cache-Control': 'no-cache',
            Pragma: 'no-cache',
          },
        }
      );

      console.log("MENU FETCHED AT", new Date().toISOString());
console.log("FIRST ITEM", response.data?.[0]);

      const mappedItems = (response.data || []).map((item: any) => ({
        menuItemId: item.menuItemId || item.id,
        restaurantId: item.restaurantId,
        categoryId: item.categoryId,
        categoryName: item.categoryName,
        name: item.name,
        description: item.description,
        price: item.price,
        imageUrl: item.imageUrl,
        isAvailable: item.isAvailable ?? true,
        dietaryTags: item.dietaryTags || [],
      }));

      setMenuItems(mappedItems);
    } catch (error: any) {
      console.error('Error loading menu:', error);
      setMenuItems([]);
    } finally {
      setLoading(false);
    }
  }, [restaurantId, selectedCategoryId]);

  const loadCategories = useCallback(async () => {
    try {
      const response = await api.get<Category[]>('/MobileBff/categories', {
        params: { __ts: Date.now() },
        headers: {
          'Cache-Control': 'no-cache',
          Pragma: 'no-cache',
        },
      });
      setCategories(response.data || []);
    } catch (error: any) {
      console.error('Error loading categories:', error);
      setCategories([]);
    }
  }, []);

  const loadReviews = useCallback(async () => {
    try {
      setLoadingReviews(true);
      const response = await api.get<Review[]>(
        `/MobileBff/reviews/restaurant/${restaurantId}?skip=0&take=10`
      );
      setReviews(response.data || []);
    } catch (error: any) {
      console.error('Error loading reviews:', error);
      setReviews([]);
    } finally {
      setLoadingReviews(false);
    }
  }, [restaurantId]);

  const loadRestaurantRating = useCallback(async () => {
    try {
      const response = await api.get<{
        restaurantId: string;
        averageRating: number;
        totalReviews: number;
      }>(`/MobileBff/reviews/restaurant/${restaurantId}/rating`);
      setRestaurantRating({
        averageRating: response.data.averageRating,
        totalReviews: response.data.totalReviews,
      });
    } catch (error: any) {
      console.error('Error loading restaurant rating:', error);
    }
  }, [restaurantId]);

  // Check if user is logged in and can review
  useEffect(() => {
    const checkAuth = async () => {
      try {
        const token = await authService.getAccessToken();
        setUserCanReview(!!token);
      } catch {
        setUserCanReview(false);
      }
    };
    checkAuth();
  }, []);

  // Initial load + restaurant change
  useEffect(() => {
    if (!restaurantId) return;
    loadMenu();
    loadCategories();
    loadReviews();
    loadRestaurantRating();
    setCurrentCartId(null);
  }, [restaurantId, loadMenu, loadCategories, loadReviews, loadRestaurantRating]);

  // ✅ Force reload when we come back from edit (token changes)
  useEffect(() => {
    if (!restaurantId) return;
    if (refreshedAt) {
      loadMenu();
    }
  }, [refreshedAt, restaurantId, loadMenu]);

  const filterByCategory = (categoryId: string | null) => {
    setSelectedCategoryId(categoryId);
  };

  const handleBackPress = useCallback(() => {
    try {
      if (router.canGoBack()) {
        router.back();
      } else {
        // Fallback if there's no history
        router.replace('/(tabs)/restaurants');
      }
    } catch (error) {
      console.error('Error navigating back:', error);
      // Fallback: try to go back using replace if back fails
      router.replace('/(tabs)/restaurants');
    }
  }, [router]);

  const addToCart = async (item: MenuItem) => {
    if (!item.isAvailable) return;

    if (isAddingToCartRef.current) {
      console.log('Add to cart already in progress, ignoring duplicate call');
      return;
    }

    try {
      isAddingToCartRef.current = true;
      setAddingItemId(item.menuItemId);

      let cart = await cartService.getCart();
      let cartIdToUse: string | null = null;

      if (cart && cart.cartId) {
        cartIdToUse = cart.cartId;
        if (cart.restaurantId && cart.restaurantId !== restaurantId) {
          cartIdToUse = await cartService.createCart(restaurantId);
        }
      } else {
        cartIdToUse = await cartService.createCart(restaurantId);
      }

      if (!cartIdToUse || cartIdToUse.trim() === '') throw new Error('Failed to create or retrieve cart');
      if (!item.menuItemId || item.menuItemId.trim() === '') throw new Error('MenuItem ID is missing');

      await cartService.addItemToCart(cartIdToUse, item.menuItemId, item.name, item.price, 1);

      setCurrentCartId(cartIdToUse);
      // Use setTimeout to ensure Alert doesn't block navigation
      setTimeout(() => {
        Alert.alert('Success', `${item.name} added to cart`);
      }, 100);
    } catch (error: any) {
      console.error('Error adding to cart:', error);
      // Use setTimeout to ensure Alert doesn't block navigation
      setTimeout(() => {
        Alert.alert('Error', error.message || 'Failed to add item to cart');
      }, 100);
    } finally {
      await new Promise(resolve => setTimeout(resolve, 500));
      isAddingToCartRef.current = false;
      setAddingItemId(null);
    }
  };

  const menuItemsByCategory = menuItems.reduce((acc, item) => {
    const categoryName = item.categoryName || 'Other';
    if (!acc[categoryName]) acc[categoryName] = [];
    acc[categoryName].push(item);
    return acc;
  }, {} as Record<string, MenuItem[]>);

  if (loading) {
    return (
      <SafeAreaView style={styles.container} edges={['top']}>
        <View style={styles.header}>
          <TouchableOpacity 
            onPress={handleBackPress} 
            style={styles.backButton}
            hitSlop={{ top: 10, bottom: 10, left: 10, right: 10 }}
            activeOpacity={0.7}
          >
            <Ionicons name="chevron-back" size={28} color="#333" />
          </TouchableOpacity>
          <Text style={styles.title}>Menu</Text>
        </View>
        <View style={styles.loadingContainer}>
          <ActivityIndicator size="large" color="#6200ee" />
        </View>
      </SafeAreaView>
    );
  }

  return (
    <SafeAreaView style={styles.container} edges={['top']}>
      <View style={styles.header}>
        <TouchableOpacity 
          onPress={handleBackPress} 
          style={styles.backButton}
          hitSlop={{ top: 10, bottom: 10, left: 10, right: 10 }}
          activeOpacity={0.7}
        >
          <Ionicons name="chevron-back" size={28} color="#333" />
        </TouchableOpacity>
        <Text style={styles.title}>Menu</Text>
        <TouchableOpacity
          onPress={() => router.push(`/restaurants/${restaurantId}/chat`)}
          style={styles.chatButton}
          hitSlop={{ top: 10, bottom: 10, left: 10, right: 10 }}
          activeOpacity={0.7}
        >
          <Ionicons name="chatbubbles-outline" size={24} color="#333" />
        </TouchableOpacity>
      </View>

      {/* Tabs */}
      <View style={styles.tabsContainer}>
        <TouchableOpacity
          style={[styles.tab, activeTab === 'menu' && styles.tabActive]}
          onPress={() => setActiveTab('menu')}
        >
          <Ionicons 
            name="restaurant" 
            size={20} 
            color={activeTab === 'menu' ? '#6200ee' : '#666'} 
          />
          <Text style={[styles.tabText, activeTab === 'menu' && styles.tabTextActive]}>
            Menu
          </Text>
        </TouchableOpacity>
        <TouchableOpacity
          style={[styles.tab, activeTab === 'reviews' && styles.tabActive]}
          onPress={() => setActiveTab('reviews')}
        >
          <Ionicons 
            name="star" 
            size={20} 
            color={activeTab === 'reviews' ? '#6200ee' : '#666'} 
          />
          <Text style={[styles.tabText, activeTab === 'reviews' && styles.tabTextActive]}>
            Reviews
            {restaurantRating && restaurantRating.totalReviews > 0 && (
              <Text style={styles.tabBadge}> ({restaurantRating.totalReviews})</Text>
            )}
          </Text>
        </TouchableOpacity>
      </View>

      <ScrollView style={styles.scrollView}>
        {activeTab === 'menu' ? (
          <>
        {categories.length > 0 && (
          <ScrollView
            horizontal
            showsHorizontalScrollIndicator={false}
            style={styles.categoryFilter}
            contentContainerStyle={styles.categoryFilterContent}
          >
            <TouchableOpacity
              style={[styles.categoryChip, selectedCategoryId === null && styles.categoryChipActive]}
              onPress={() => filterByCategory(null)}
            >
              <Text style={[styles.categoryChipText, selectedCategoryId === null && styles.categoryChipTextActive]}>
                All
              </Text>
            </TouchableOpacity>

            {categories.map(category => (
              <TouchableOpacity
                key={category.categoryId}
                style={[styles.categoryChip, selectedCategoryId === category.categoryId && styles.categoryChipActive]}
                onPress={() => filterByCategory(category.categoryId)}
              >
                <Text
                  style={[
                    styles.categoryChipText,
                    selectedCategoryId === category.categoryId && styles.categoryChipTextActive,
                  ]}
                >
                  {category.name}
                </Text>
              </TouchableOpacity>
            ))}
          </ScrollView>
        )}

        {Object.entries(menuItemsByCategory).map(([categoryName, items]) => (
          <View key={categoryName} style={styles.categorySection}>
            <Text style={styles.categoryTitle}>{categoryName}</Text>

            {items.map(item => (
              <TouchableOpacity key={item.menuItemId} style={styles.menuItemCard} activeOpacity={0.8}>
                <View style={styles.menuItemContent}>
                  <Ionicons name="restaurant" size={40} color="#6200ee" style={styles.menuItemIcon} />
                  <View style={styles.menuItemDetails}>
                    <Text style={styles.menuItemName}>{item.name}</Text>
                    {item.description && <Text style={styles.menuItemDescription}>{item.description}</Text>}
                    <Text style={styles.menuItemPrice}>${item.price.toFixed(2)}</Text>
                  </View>

                  {item.isAvailable ? (
                    <TouchableOpacity
                      style={[styles.addButton, addingItemId === item.menuItemId && styles.addButtonDisabled]}
                      onPress={() => addToCart(item)}
                      disabled={addingItemId === item.menuItemId}
                    >
                      {addingItemId === item.menuItemId ? (
                        <ActivityIndicator size="small" color="#6200ee" />
                      ) : (
                        <Ionicons name="add-circle" size={32} color="#6200ee" />
                      )}
                    </TouchableOpacity>
                  ) : (
                    <View style={styles.unavailableBadge}>
                      <Text style={styles.unavailableText}>Unavailable</Text>
                    </View>
                  )}
                </View>
              </TouchableOpacity>
            ))}
          </View>
        ))}

        {menuItems.length === 0 && (
          <View style={styles.emptyContainer}>
            <Ionicons name="restaurant-outline" size={64} color="#ccc" />
            <Text style={styles.emptyText}>No menu items found</Text>
          </View>
        )}
          </>
        ) : (
          <>
            {/* Reviews Tab Content */}
            <View style={styles.reviewsSection}>
              <View style={styles.reviewsHeader}>
                <View style={styles.reviewsHeaderLeft}>
                  <Text style={styles.reviewsTitle}>Reviews</Text>
                  {restaurantRating && restaurantRating.totalReviews > 0 && (
                    <View style={styles.ratingContainer}>
                      <ReviewRating
                        value={Math.round(restaurantRating.averageRating)}
                        editable={false}
                        showValue={true}
                        size={24}
                      />
                      <Text style={styles.ratingText}>
                        {restaurantRating.averageRating.toFixed(1)} ({restaurantRating.totalReviews} reviews)
                      </Text>
                    </View>
                  )}
                </View>
                {userCanReview && (
                  <View style={styles.reviewInfo}>
                    <Ionicons name="information-circle-outline" size={16} color="#666" />
                    <Text style={styles.reviewInfoText}>
                      Write reviews from your order history
                    </Text>
                  </View>
                )}
              </View>
              

              {loadingReviews ? (
                <View style={styles.reviewsLoadingContainer}>
                  <ActivityIndicator size="large" color="#6200ee" />
                  <Text style={styles.loadingText}>Loading reviews...</Text>
                </View>
              ) : (
                <ReviewDisplay reviews={reviews} />
              )}
            </View>
          </>
        )}
      </ScrollView>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: '#f5f5f5' },
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    padding: 16,
    backgroundColor: '#fff',
    borderBottomWidth: 1,
    borderBottomColor: '#e0e0e0',
  },
  backButton: { marginRight: 12 },
  title: { flex: 1, fontSize: 24, fontWeight: 'bold', color: '#333', textAlign: 'center' },
  chatButton: { marginLeft: 12 },
  loadingContainer: { flex: 1, justifyContent: 'center', alignItems: 'center' },
  tabsContainer: {
    flexDirection: 'row',
    backgroundColor: '#fff',
    borderBottomWidth: 1,
    borderBottomColor: '#e0e0e0',
    paddingHorizontal: 16,
  },
  tab: {
    flex: 1,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    paddingVertical: 16,
    borderBottomWidth: 2,
    borderBottomColor: 'transparent',
    gap: 6,
  },
  tabActive: {
    borderBottomColor: '#6200ee',
  },
  tabText: {
    fontSize: 16,
    fontWeight: '500',
    color: '#666',
  },
  tabTextActive: {
    color: '#6200ee',
    fontWeight: '600',
  },
  tabBadge: {
    fontSize: 14,
    color: '#6200ee',
    fontWeight: '600',
  },
  scrollView: { flex: 1 },
  categoryFilter: { maxHeight: 50, marginVertical: 12 },
  categoryFilterContent: { paddingHorizontal: 16, gap: 8 },
  categoryChip: {
    paddingHorizontal: 16,
    paddingVertical: 8,
    borderRadius: 20,
    backgroundColor: '#fff',
    borderWidth: 1,
    borderColor: '#e0e0e0',
    marginRight: 8,
  },
  categoryChipActive: { backgroundColor: '#6200ee', borderColor: '#6200ee' },
  categoryChipText: { fontSize: 14, color: '#666', fontWeight: '500' },
  categoryChipTextActive: { color: '#fff' },
  categorySection: { marginBottom: 24, paddingHorizontal: 16 },
  categoryTitle: { fontSize: 20, fontWeight: 'bold', color: '#333', marginBottom: 12 },
  menuItemCard: {
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
  menuItemContent: { flexDirection: 'row', alignItems: 'flex-start' },
  menuItemIcon: { marginRight: 12 },
  menuItemDetails: { flex: 1 },
  menuItemName: { fontSize: 18, fontWeight: '600', color: '#333', marginBottom: 4 },
  menuItemDescription: { fontSize: 14, color: '#666', marginBottom: 8 },
  menuItemPrice: { fontSize: 18, fontWeight: 'bold', color: '#6200ee', marginTop: 4 },
  addButton: { marginLeft: 12 },
  addButtonDisabled: { opacity: 0.5 },
  unavailableBadge: {
    backgroundColor: '#ffebee',
    paddingHorizontal: 12,
    paddingVertical: 6,
    borderRadius: 6,
    marginLeft: 12,
  },
  unavailableText: { fontSize: 12, color: '#c62828', fontWeight: '500' },
  emptyContainer: { flex: 1, justifyContent: 'center', alignItems: 'center', paddingVertical: 64 },
  emptyText: { fontSize: 16, color: '#999', marginTop: 16 },
  reviewsSection: { marginTop: 24, paddingHorizontal: 16, paddingBottom: 24 },
  reviewsHeader: {
    marginBottom: 20,
  },
  reviewsHeaderLeft: {
    gap: 12,
  },
  reviewsTitle: { fontSize: 24, fontWeight: 'bold', color: '#333', marginBottom: 8 },
  ratingContainer: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
  },
  ratingText: { fontSize: 16, fontWeight: '500', color: '#333' },
  reviewsLoadingContainer: {
    padding: 40,
    alignItems: 'center',
    justifyContent: 'center',
  },
  loadingText: {
    marginTop: 12,
    fontSize: 14,
    color: '#666',
  },
  reviewInfo: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
    paddingHorizontal: 12,
    paddingVertical: 6,
    backgroundColor: '#f5f5f5',
    borderRadius: 8,
    marginTop: 8,
  },
  reviewInfoText: {
    fontSize: 12,
    color: '#666',
    fontStyle: 'italic',
  },
});
