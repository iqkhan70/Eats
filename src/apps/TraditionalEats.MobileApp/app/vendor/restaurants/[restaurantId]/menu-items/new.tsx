import React, { useState, useEffect } from 'react';
import {
  View,
  Text,
  StyleSheet,
  ScrollView,
  TextInput,
  TouchableOpacity,
  Alert,
  ActivityIndicator,
} from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useRouter, useLocalSearchParams } from 'expo-router';
import { api } from '../../../../../services/api';

interface Category {
  categoryId: string;
  name: string;
  description?: string;
}

export default function CreateMenuItemScreen() {
  const router = useRouter();
  const params = useLocalSearchParams<{ restaurantId: string }>();
  const restaurantId = params.restaurantId;

  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [categories, setCategories] = useState<Category[]>([]);
  const [formData, setFormData] = useState({
    categoryId: '',
    name: '',
    description: '',
    price: '',
    imageUrl: '',
  });

  useEffect(() => {
    if (restaurantId) {
      loadCategories();
    }
  }, [restaurantId]);

  const loadCategories = async () => {
    try {
      setLoading(true);
      const response = await api.get<Category[]>('/MobileBff/categories');
      setCategories(response.data || []);
    } catch (error: any) {
      console.error('Error loading categories:', error);
      Alert.alert('Error', 'Failed to load categories');
    } finally {
      setLoading(false);
    }
  };

  const handleSave = async () => {
    if (!formData.name.trim()) {
      Alert.alert('Validation Error', 'Menu item name is required');
      return;
    }

    if (!formData.categoryId) {
      Alert.alert('Validation Error', 'Please select a category');
      return;
    }

    const price = parseFloat(formData.price);
    if (isNaN(price) || price <= 0) {
      Alert.alert('Validation Error', 'Please enter a valid price');
      return;
    }

    try {
      setSaving(true);
      const request = {
        categoryId: formData.categoryId,
        name: formData.name.trim(),
        description: formData.description.trim() || null,
        price: price,
        imageUrl: formData.imageUrl.trim() || null,
        dietaryTags: null as string[] | null,
      };

      await api.post(`/MobileBff/restaurants/${restaurantId}/menu-items`, request);
      
      Alert.alert('Success', 'Menu item created successfully', [
        { 
          text: 'OK', 
          onPress: () => {
            router.back();
          }
        }
      ]);
    } catch (error: any) {
      console.error('Error creating menu item:', error);
      const errorMessage = error.response?.data?.error || error.message || 'Failed to create menu item';
      Alert.alert('Error', errorMessage);
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return (
      <View style={styles.centerContainer}>
        <ActivityIndicator size="large" color="#6200ee" />
        <Text style={styles.loadingText}>Loading categories...</Text>
      </View>
    );
  }

  return (
    <ScrollView style={styles.container}>
      <View style={styles.header}>
        <TouchableOpacity onPress={() => router.back()} style={styles.backButton}>
          <Ionicons name="arrow-back" size={24} color="#fff" />
        </TouchableOpacity>
        <Text style={styles.headerTitle}>Add Menu Item</Text>
        <View style={styles.placeholder} />
      </View>

      <View style={styles.form}>
        <View style={styles.formGroup}>
          <Text style={styles.label}>Category *</Text>
          <View style={styles.categoryContainer}>
            {categories.map((category) => (
              <TouchableOpacity
                key={category.categoryId}
                style={[
                  styles.categoryButton,
                  formData.categoryId === category.categoryId && styles.categoryButtonSelected,
                ]}
                onPress={() => setFormData({ ...formData, categoryId: category.categoryId })}
              >
                <Text
                  style={[
                    styles.categoryButtonText,
                    formData.categoryId === category.categoryId && styles.categoryButtonTextSelected,
                  ]}
                >
                  {category.name}
                </Text>
              </TouchableOpacity>
            ))}
          </View>
          {!formData.categoryId && (
            <Text style={styles.errorText}>Please select a category</Text>
          )}
        </View>

        <View style={styles.formGroup}>
          <Text style={styles.label}>Item Name *</Text>
          <TextInput
            style={styles.input}
            value={formData.name}
            onChangeText={(text) => setFormData({ ...formData, name: text })}
            placeholder="Enter menu item name"
            autoFocus
          />
          {!formData.name.trim() && (
            <Text style={styles.errorText}>Item name is required</Text>
          )}
        </View>

        <View style={styles.formGroup}>
          <Text style={styles.label}>Description</Text>
          <TextInput
            style={[styles.input, styles.textArea]}
            value={formData.description}
            onChangeText={(text) => setFormData({ ...formData, description: text })}
            placeholder="Enter description"
            multiline
            numberOfLines={4}
          />
        </View>

        <View style={styles.formGroup}>
          <Text style={styles.label}>Price *</Text>
          <View style={styles.priceContainer}>
            <Text style={styles.currencySymbol}>$</Text>
            <TextInput
              style={[styles.input, styles.priceInput]}
              value={formData.price}
              onChangeText={(text) => {
                // Allow only numbers and one decimal point
                const cleaned = text.replace(/[^0-9.]/g, '');
                if (cleaned.split('.').length <= 2) {
                  setFormData({ ...formData, price: cleaned });
                }
              }}
              placeholder="0.00"
              keyboardType="decimal-pad"
            />
          </View>
          {(!formData.price || parseFloat(formData.price) <= 0) && (
            <Text style={styles.errorText}>Please enter a valid price</Text>
          )}
        </View>

        <View style={styles.formGroup}>
          <Text style={styles.label}>Image URL</Text>
          <TextInput
            style={styles.input}
            value={formData.imageUrl}
            onChangeText={(text) => setFormData({ ...formData, imageUrl: text })}
            placeholder="Enter image URL"
            autoCapitalize="none"
          />
        </View>

        <TouchableOpacity
          style={[
            styles.saveButton,
            (saving || !formData.name.trim() || !formData.categoryId || !formData.price || parseFloat(formData.price) <= 0) &&
            styles.saveButtonDisabled,
          ]}
          onPress={handleSave}
          disabled={saving || !formData.name.trim() || !formData.categoryId || !formData.price || parseFloat(formData.price) <= 0}
        >
          {saving ? (
            <ActivityIndicator size="small" color="#fff" />
          ) : (
            <>
              <Ionicons name="checkmark" size={20} color="#fff" style={{ marginRight: 8 }} />
              <Text style={styles.saveButtonText}>Create Menu Item</Text>
            </>
          )}
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
  centerContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    backgroundColor: '#f5f5f5',
  },
  loadingText: {
    marginTop: 16,
    fontSize: 16,
    color: '#666',
  },
  header: {
    backgroundColor: '#6200ee',
    padding: 16,
    paddingTop: 60,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
  },
  backButton: {
    padding: 8,
  },
  headerTitle: {
    fontSize: 20,
    fontWeight: 'bold',
    color: '#fff',
    flex: 1,
    textAlign: 'center',
  },
  placeholder: {
    width: 40,
  },
  form: {
    padding: 16,
  },
  formGroup: {
    marginBottom: 20,
  },
  label: {
    fontSize: 14,
    fontWeight: '600',
    color: '#333',
    marginBottom: 8,
  },
  categoryContainer: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 8,
  },
  categoryButton: {
    paddingHorizontal: 16,
    paddingVertical: 10,
    borderRadius: 20,
    backgroundColor: '#fff',
    borderWidth: 2,
    borderColor: '#e0e0e0',
  },
  categoryButtonSelected: {
    backgroundColor: '#6200ee',
    borderColor: '#6200ee',
  },
  categoryButtonText: {
    fontSize: 14,
    color: '#333',
    fontWeight: '500',
  },
  categoryButtonTextSelected: {
    color: '#fff',
  },
  input: {
    backgroundColor: '#fff',
    borderWidth: 1,
    borderColor: '#e0e0e0',
    borderRadius: 8,
    padding: 12,
    fontSize: 16,
    color: '#333',
  },
  textArea: {
    height: 100,
    textAlignVertical: 'top',
  },
  priceContainer: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: '#fff',
    borderWidth: 1,
    borderColor: '#e0e0e0',
    borderRadius: 8,
    paddingLeft: 12,
  },
  currencySymbol: {
    fontSize: 16,
    fontWeight: '600',
    color: '#333',
    marginRight: 8,
  },
  priceInput: {
    flex: 1,
    borderWidth: 0,
    paddingLeft: 0,
  },
  errorText: {
    fontSize: 12,
    color: '#d32f2f',
    marginTop: 4,
  },
  saveButton: {
    backgroundColor: '#6200ee',
    padding: 16,
    borderRadius: 8,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    marginTop: 8,
  },
  saveButtonDisabled: {
    opacity: 0.6,
  },
  saveButtonText: {
    color: '#fff',
    fontSize: 16,
    fontWeight: '600',
  },
});
