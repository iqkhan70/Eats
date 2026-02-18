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

export default function AdminDashboardScreen() {
  const router = useRouter();
  const [isAuthenticated, setIsAuthenticated] = useState(false);
  const [isAdmin, setIsAdmin] = useState(false);
  const [loading, setLoading] = useState(true);
  const [userRoles, setUserRoles] = useState<string[]>([]);

  useEffect(() => {
    checkAuthAndLoad();
  }, []);

  const checkAuthAndLoad = async () => {
    const authenticated = await authService.isAuthenticated();
    setIsAuthenticated(authenticated);
    
    if (authenticated) {
      const roles = await authService.getUserRoles();
      setUserRoles(roles);
      
      const admin = await authService.isAdmin();
      setIsAdmin(admin);
      
      if (!admin) {
        Alert.alert('Access Denied', 'You must be an admin to access this page.');
        router.back();
      }
    } else {
      Alert.alert('Authentication Required', 'Please log in to access the admin dashboard.');
      router.push('/login');
    }
    
    setLoading(false);
  };

  const adminMenuItems = [
    {
      icon: 'restaurant-outline',
      label: 'Manage Vendors',
      description: 'View and manage all vendors',
      route: '/admin/restaurants',
      color: '#6200ee',
    },
    {
      icon: 'pricetags-outline',
      label: 'Manage Categories',
      description: 'Add and organize platform categories',
      route: '/admin/categories',
      color: '#0097a7',
    },
    {
      icon: 'people-outline',
      label: 'Manage Users',
      description: 'View and manage user accounts',
      route: '/admin/users',
      color: '#1976d2',
    },
    {
      icon: 'document-text-outline',
      label: 'Document Management',
      description: 'View and manage all vendor documents',
      route: '/admin/documents',
      color: '#7b1fa2',
    },
    {
      icon: 'receipt-outline',
      label: 'View Orders',
      description: 'Monitor all orders across the platform',
      route: '/admin/orders',
      color: '#388e3c',
    },
    {
      icon: 'settings-outline',
      label: 'System Settings',
      description: 'Configure system-wide settings',
      route: '/admin/settings',
      color: '#f57c00',
    },
  ];

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
          <Ionicons name="chevron-back" size={28} color="#fff" />
        </TouchableOpacity>
        <Text style={styles.headerTitle}>Admin Dashboard</Text>
        <View style={styles.placeholder} />
      </View>

      <View style={styles.content}>
        <View style={styles.infoCard}>
          <Ionicons name="shield-checkmark" size={32} color="#6200ee" />
          <Text style={styles.infoTitle}>Administrator Access</Text>
          <Text style={styles.infoText}>You have full system access</Text>
          {userRoles.length > 0 && (
            <View style={styles.rolesContainer}>
              <Text style={styles.rolesLabel}>Your Roles:</Text>
              {userRoles.map((role, index) => (
                <View key={index} style={styles.roleBadge}>
                  <Text style={styles.roleText}>{role}</Text>
                </View>
              ))}
            </View>
          )}
        </View>

        <View style={styles.menuSection}>
          <Text style={styles.sectionTitle}>Admin Functions</Text>
          {adminMenuItems.map((item, index) => (
            <TouchableOpacity
              key={index}
              style={styles.menuItem}
              onPress={() => {
                router.push(item.route as any);
              }}
            >
              <View style={[styles.menuIconContainer, { backgroundColor: `${item.color}15` }]}>
                <Ionicons name={item.icon as any} size={24} color={item.color} />
              </View>
              <View style={styles.menuItemContent}>
                <Text style={styles.menuItemLabel}>{item.label}</Text>
                <Text style={styles.menuItemDescription}>{item.description}</Text>
              </View>
              <Ionicons name="chevron-forward" size={20} color="#ccc" />
            </TouchableOpacity>
          ))}
        </View>

        <View style={styles.statsSection}>
          <Text style={styles.sectionTitle}>Quick Stats</Text>
          <View style={styles.statsGrid}>
            <View style={styles.statCard}>
              <Ionicons name="restaurant" size={24} color="#6200ee" />
              <Text style={styles.statValue}>-</Text>
              <Text style={styles.statLabel}>Vendors</Text>
            </View>
            <View style={styles.statCard}>
              <Ionicons name="people" size={24} color="#1976d2" />
              <Text style={styles.statValue}>-</Text>
              <Text style={styles.statLabel}>Users</Text>
            </View>
            <View style={styles.statCard}>
              <Ionicons name="receipt-outline" size={24} color="#388e3c" />
              <Text style={styles.statValue}>-</Text>
              <Text style={styles.statLabel}>Orders</Text>
            </View>
            <View style={styles.statCard}>
              <Ionicons name="time" size={24} color="#f57c00" />
              <Text style={styles.statValue}>-</Text>
              <Text style={styles.statLabel}>Pending</Text>
            </View>
          </View>
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
  },
  infoText: {
    fontSize: 14,
    color: '#666',
    marginTop: 4,
  },
  rolesContainer: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    justifyContent: 'center',
    marginTop: 12,
    gap: 8,
  },
  rolesLabel: {
    fontSize: 12,
    color: '#666',
    width: '100%',
    textAlign: 'center',
    marginBottom: 4,
  },
  roleBadge: {
    backgroundColor: '#6200ee',
    paddingHorizontal: 12,
    paddingVertical: 4,
    borderRadius: 12,
  },
  roleText: {
    color: '#fff',
    fontSize: 12,
    fontWeight: '600',
  },
  menuSection: {
    marginBottom: 24,
  },
  sectionTitle: {
    fontSize: 18,
    fontWeight: 'bold',
    color: '#333',
    marginBottom: 12,
  },
  menuItem: {
    backgroundColor: '#fff',
    borderRadius: 12,
    padding: 16,
    marginBottom: 12,
    flexDirection: 'row',
    alignItems: 'center',
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.1,
    shadowRadius: 4,
    elevation: 3,
  },
  menuIconContainer: {
    width: 48,
    height: 48,
    borderRadius: 24,
    justifyContent: 'center',
    alignItems: 'center',
    marginRight: 12,
  },
  menuItemContent: {
    flex: 1,
  },
  menuItemLabel: {
    fontSize: 16,
    fontWeight: '600',
    color: '#333',
    marginBottom: 4,
  },
  menuItemDescription: {
    fontSize: 12,
    color: '#666',
  },
  statsSection: {
    marginBottom: 24,
  },
  statsGrid: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 12,
  },
  statCard: {
    backgroundColor: '#fff',
    borderRadius: 12,
    padding: 16,
    alignItems: 'center',
    width: '47%',
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.1,
    shadowRadius: 4,
    elevation: 3,
  },
  statValue: {
    fontSize: 24,
    fontWeight: 'bold',
    color: '#333',
    marginTop: 8,
  },
  statLabel: {
    fontSize: 12,
    color: '#666',
    marginTop: 4,
  },
});
