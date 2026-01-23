import React, { useState, useEffect } from 'react';
import {
  View,
  Text,
  StyleSheet,
  ScrollView,
  TouchableOpacity,
  Alert,
  ActivityIndicator,
} from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useRouter } from 'expo-router';
import { authService } from '../../services/auth';

export default function AdminOrdersScreen() {
  const router = useRouter();
  const [isAdmin, setIsAdmin] = useState(false);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    checkAuth();
  }, []);

  const checkAuth = async () => {
    const admin = await authService.isAdmin();
    setIsAdmin(admin);
    
    if (!admin) {
      Alert.alert('Access Denied', 'You must be an admin to access this page.');
      router.back();
    }
    
    setLoading(false);
  };

  if (loading) {
    return (
      <View style={styles.centerContainer}>
        <ActivityIndicator size="large" color="#6200ee" />
        <Text style={styles.loadingText}>Loading...</Text>
      </View>
    );
  }

  return (
    <ScrollView style={styles.container}>
      <View style={styles.header}>
        <TouchableOpacity onPress={() => router.back()} style={styles.backButton}>
          <Ionicons name="arrow-back" size={24} color="#fff" />
        </TouchableOpacity>
        <Text style={styles.headerTitle}>All Orders</Text>
        <View style={styles.placeholder} />
      </View>

      <View style={styles.content}>
        <View style={styles.infoCard}>
          <Ionicons name="receipt-outline" size={48} color="#388e3c" />
          <Text style={styles.infoTitle}>Order Management</Text>
          <Text style={styles.infoText}>
            View and manage all orders across the platform. You can access individual orders from the Orders tab.
          </Text>
          
          <TouchableOpacity
            style={styles.navigateButton}
            onPress={() => router.push('/(tabs)/orders')}
          >
            <Ionicons name="arrow-forward" size={20} color="#fff" />
            <Text style={styles.navigateButtonText}>Go to Orders</Text>
          </TouchableOpacity>
        </View>

        <View style={styles.noteCard}>
          <Ionicons name="information-circle-outline" size={24} color="#1976d2" />
          <Text style={styles.noteText}>
            Admin-specific order management features (filtering, bulk actions, analytics) will be available soon.
          </Text>
        </View>
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
    backgroundColor: '#388e3c',
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
  content: {
    padding: 16,
  },
  infoCard: {
    backgroundColor: '#fff',
    borderRadius: 12,
    padding: 20,
    alignItems: 'center',
    marginBottom: 16,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.1,
    shadowRadius: 4,
    elevation: 3,
  },
  infoTitle: {
    fontSize: 20,
    fontWeight: 'bold',
    color: '#333',
    marginTop: 12,
    marginBottom: 8,
  },
  infoText: {
    fontSize: 14,
    color: '#666',
    textAlign: 'center',
    marginBottom: 20,
  },
  navigateButton: {
    backgroundColor: '#388e3c',
    paddingHorizontal: 24,
    paddingVertical: 12,
    borderRadius: 8,
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
  },
  navigateButtonText: {
    color: '#fff',
    fontSize: 16,
    fontWeight: '600',
  },
  noteCard: {
    backgroundColor: '#e3f2fd',
    borderRadius: 12,
    padding: 16,
    flexDirection: 'row',
    alignItems: 'flex-start',
    gap: 12,
    borderLeftWidth: 4,
    borderLeftColor: '#1976d2',
  },
  noteText: {
    flex: 1,
    fontSize: 14,
    color: '#1565c0',
    lineHeight: 20,
  },
});
