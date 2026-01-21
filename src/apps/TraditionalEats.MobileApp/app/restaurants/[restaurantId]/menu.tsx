import React, { useState, useEffect } from 'react';
import { View, Text, StyleSheet, ScrollView, TouchableOpacity, ActivityIndicator, Alert } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useRouter, useLocalSearchParams } from 'expo-router';
import { api } from '../../../services/api';
import { cartService } from '../../../services/cart';

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
  const params = useLocalSearchParams();
  const restaurantId = params.restaurantId as string;
  
  const [menuItems, setMenuItems] = useState<MenuItem[]>([]);
  const [categories, setCategories] = useState<Category[]>([]);
  const [selectedCategoryId, setSelectedCategoryId] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [currentCartId, setCurrentCartId] = useState<string | null>(null);
  const [addingItemId, setAddingItemId] = useState<string | null>(null);

  useEffect(() => {
    loadMenu();
    loadCategories();
  }, [restaurantId, selectedCategoryId]);

  const loadMenu = async () => {
    try {
      setLoading(true);
      const queryParams: any = {};
      if (selectedCategoryId) queryParams.categoryId = selectedCategoryId;
      
      const response = await api.get<MenuItem[]>(`/MobileBff/restaurants/${restaurantId}/menu`, { params: queryParams });
      
      const mappedItems = response.data.map((item: any) => ({
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
  };

  const loadCategories = async () => {
    try {
      const response = await api.get<Category[]>('/MobileBff/categories');
      setCategories(response.data || []);
    } catch (error: any) {
      console.error('Error loading categories:', error);
      setCategories([]);
    }
  };

  const filterByCategory = (categoryId: string | null) => {
    setSelectedCategoryId(categoryId);
  };

  const addToCart = async (item: MenuItem) => {
    if (!item.isAvailable) return;

    try {
      setAddingItemId(item.menuItemId);

      // Get or create cart
      if (!currentCartId) {
        const cart = await cartService.getCart();
        if (cart) {
          setCurrentCartId(cart.cartId);
          // If cart is for a different restaurant, create a new one
          if (cart.restaurantId && cart.restaurantId !== restaurantId) {
            const newCartId = await cartService.createCart(restaurantId);
            setCurrentCartId(newCartId);
          }
        } else {
          const newCartId = await cartService.createCart(restaurantId);
          setCurrentCartId(newCartId);
        }
      }

      await cartService.addItemToCart(currentCartId!, item.menuItemId, item.name, item.price, 1);
      Alert.alert('Success', `${item.name} added to cart`);
    } catch (error: any) {
      console.error('Error adding to cart:', error);
      Alert.alert('Error', 'Failed to add item to cart');
    } finally {
      setAddingItemId(null);
    }
  };

  // Group menu items by category
  const menuItemsByCategory = menuItems.reduce((acc, item) => {
    const categoryName = item.categoryName || 'Other';
    if (!acc[categoryName]) {
      acc[categoryName] = [];
    }
    acc[categoryName].push(item);
    return acc;
  }, {} as Record<string, MenuItem[]>);

  if (loading) {
    return (
      <View style={styles.container}>
        <View style={styles.header}>
          <TouchableOpacity onPress={() => router.back()} style={styles.backButton}>
            <Ionicons name="arrow-back" size={24} color="#333" />
          </TouchableOpacity>
          <Text style={styles.title}>Menu</Text>
        </View>
        <View style={styles.loadingContainer}>
          <ActivityIndicator size="large" color="#6200ee" />
        </View>
      </View>
    );
  }

  return (
    <View style={styles.container}>
      <View style={styles.header}>
        <TouchableOpacity onPress={() => router.back()} style={styles.backButton}>
          <Ionicons name="arrow-back" size={24} color="#333" />
        </TouchableOpacity>
        <Text style={styles.title}>Menu</Text>
      </View>

      <ScrollView style={styles.scrollView}>
        {/* Category Filter */}
        {categories.length > 0 && (
          <ScrollView 
            horizontal 
            showsHorizontalScrollIndicator={false}
            style={styles.categoryFilter}
            contentContainerStyle={styles.categoryFilterContent}
          >
            <TouchableOpacity
              style={[
                styles.categoryChip,
                selectedCategoryId === null && styles.categoryChipActive
              ]}
              onPress={() => filterByCategory(null)}
            >
              <Text style={[
                styles.categoryChipText,
                selectedCategoryId === null && styles.categoryChipTextActive
              ]}>All</Text>
            </TouchableOpacity>
            {categories.map((category) => (
              <TouchableOpacity
                key={category.categoryId}
                style={[
                  styles.categoryChip,
                  selectedCategoryId === category.categoryId && styles.categoryChipActive
                ]}
                onPress={() => filterByCategory(category.categoryId)}
              >
                <Text style={[
                  styles.categoryChipText,
                  selectedCategoryId === category.categoryId && styles.categoryChipTextActive
                ]}>{category.name}</Text>
              </TouchableOpacity>
            ))}
          </ScrollView>
        )}

        {/* Menu Items by Category */}
        {Object.entries(menuItemsByCategory).map(([categoryName, items]) => (
          <View key={categoryName} style={styles.categorySection}>
            <Text style={styles.categoryTitle}>{categoryName}</Text>
            {items.map((item) => (
              <TouchableOpacity
                key={item.menuItemId}
                style={styles.menuItemCard}
                activeOpacity={0.8}
              >
                <View style={styles.menuItemContent}>
                  <Ionicons name="restaurant" size={40} color="#6200ee" style={styles.menuItemIcon} />
                  <View style={styles.menuItemDetails}>
                    <Text style={styles.menuItemName}>{item.name}</Text>
                    {item.description && (
                      <Text style={styles.menuItemDescription}>{item.description}</Text>
                    )}
                    {item.dietaryTags.length > 0 && (
                      <View style={styles.dietaryTags}>
                        {item.dietaryTags.map((tag, index) => (
                          <View key={index} style={styles.dietaryTag}>
                            <Text style={styles.dietaryTagText}>{tag}</Text>
                          </View>
                        ))}
                      </View>
                    )}
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
      </ScrollView>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#f5f5f5',
  },
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    padding: 16,
    backgroundColor: '#fff',
    borderBottomWidth: 1,
    borderBottomColor: '#e0e0e0',
  },
  backButton: {
    marginRight: 12,
  },
  title: {
    fontSize: 24,
    fontWeight: 'bold',
    color: '#333',
  },
  loadingContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
  },
  scrollView: {
    flex: 1,
  },
  categoryFilter: {
    maxHeight: 50,
    marginVertical: 12,
  },
  categoryFilterContent: {
    paddingHorizontal: 16,
    gap: 8,
  },
  categoryChip: {
    paddingHorizontal: 16,
    paddingVertical: 8,
    borderRadius: 20,
    backgroundColor: '#fff',
    borderWidth: 1,
    borderColor: '#e0e0e0',
    marginRight: 8,
  },
  categoryChipActive: {
    backgroundColor: '#6200ee',
    borderColor: '#6200ee',
  },
  categoryChipText: {
    fontSize: 14,
    color: '#666',
    fontWeight: '500',
  },
  categoryChipTextActive: {
    color: '#fff',
  },
  categorySection: {
    marginBottom: 24,
    paddingHorizontal: 16,
  },
  categoryTitle: {
    fontSize: 20,
    fontWeight: 'bold',
    color: '#333',
    marginBottom: 12,
  },
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
  menuItemContent: {
    flexDirection: 'row',
    alignItems: 'flex-start',
  },
  menuItemIcon: {
    marginRight: 12,
  },
  menuItemDetails: {
    flex: 1,
  },
  menuItemName: {
    fontSize: 18,
    fontWeight: '600',
    color: '#333',
    marginBottom: 4,
  },
  menuItemDescription: {
    fontSize: 14,
    color: '#666',
    marginBottom: 8,
  },
  dietaryTags: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    marginBottom: 8,
    gap: 4,
  },
  dietaryTag: {
    backgroundColor: '#e3f2fd',
    paddingHorizontal: 8,
    paddingVertical: 4,
    borderRadius: 4,
    marginRight: 4,
  },
  dietaryTagText: {
    fontSize: 12,
    color: '#1976d2',
  },
  menuItemPrice: {
    fontSize: 18,
    fontWeight: 'bold',
    color: '#6200ee',
    marginTop: 4,
  },
  addButton: {
    marginLeft: 12,
  },
  addButtonDisabled: {
    opacity: 0.5,
  },
  unavailableBadge: {
    backgroundColor: '#ffebee',
    paddingHorizontal: 12,
    paddingVertical: 6,
    borderRadius: 6,
    marginLeft: 12,
  },
  unavailableText: {
    fontSize: 12,
    color: '#c62828',
    fontWeight: '500',
  },
  emptyContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    paddingVertical: 64,
  },
  emptyText: {
    fontSize: 16,
    color: '#999',
    marginTop: 16,
  },
});
