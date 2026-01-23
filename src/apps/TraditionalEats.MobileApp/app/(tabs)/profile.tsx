import React, { useState, useEffect } from 'react';
import { View, Text, StyleSheet, ScrollView, TouchableOpacity, Alert } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useRouter } from 'expo-router';
import { authService } from '../../services/auth';

export default function ProfileScreen() {
  const router = useRouter();
  const [isAuthenticated, setIsAuthenticated] = useState(false);
  const [userEmail, setUserEmail] = useState<string | null>(null);
  const [isVendor, setIsVendor] = useState(false);
  const [isAdmin, setIsAdmin] = useState(false);

  useEffect(() => {
    checkAuthStatus();
  }, []);

  const checkAuthStatus = async () => {
    const authenticated = await authService.isAuthenticated();
    setIsAuthenticated(authenticated);
    if (authenticated) {
      // Check user roles
      setIsVendor(await authService.isVendor());
      setIsAdmin(await authService.isAdmin());
      // You can fetch user info here if needed
      // For now, we'll just show a placeholder
    }
  };

  const handleLogout = async () => {
    Alert.alert(
      'Sign Out',
      'Are you sure you want to sign out?',
      [
        { text: 'Cancel', style: 'cancel' },
        {
          text: 'Sign Out',
          style: 'destructive',
          onPress: async () => {
            try {
              await authService.logout();
              setIsAuthenticated(false);
              setUserEmail(null);
              Alert.alert('Success', 'You have been signed out');
            } catch (error: any) {
              Alert.alert('Error', 'Failed to sign out');
            }
          },
        },
      ]
    );
  };

  const menuItems = [
    { icon: 'person-outline', label: 'Personal Information', route: '/profile/personal' },
    { icon: 'location-outline', label: 'Addresses', route: '/profile/addresses' },
    { icon: 'card-outline', label: 'Payment Methods', route: '/profile/payments' },
    { icon: 'notifications-outline', label: 'Notifications', route: '/profile/notifications' },
    { icon: 'help-circle-outline', label: 'Help & Support', route: '/profile/support' },
    { icon: 'settings-outline', label: 'Settings', route: '/profile/settings' },
  ];

  return (
    <ScrollView style={styles.container}>
      <View style={styles.header}>
        <View style={styles.avatarContainer}>
          <Ionicons name="person" size={48} color="#fff" />
        </View>
        {isAuthenticated ? (
          <>
            <Text style={styles.userName}>Welcome</Text>
            <Text style={styles.userEmail}>{userEmail || 'User'}</Text>
          </>
        ) : (
          <>
            <Text style={styles.userName}>Guest</Text>
            <Text style={styles.userEmail}>Sign in to access your account</Text>
          </>
        )}
      </View>

      {isAuthenticated ? (
        <>
          {(isVendor || isAdmin) && (
            <View style={styles.menuSection}>
              <Text style={styles.sectionTitle}>Business</Text>
              {isVendor && (
                <TouchableOpacity
                  style={styles.menuItem}
                  onPress={() => router.push('/vendor')}
                >
                  <View style={styles.menuItemLeft}>
                    <Ionicons name="restaurant" size={24} color="#6200ee" />
                    <Text style={styles.menuItemLabel}>Vendor Dashboard</Text>
                  </View>
                  <Ionicons name="chevron-forward" size={20} color="#666" />
                </TouchableOpacity>
              )}
              {isAdmin && (
                <TouchableOpacity
                  style={styles.menuItem}
                  onPress={() => router.push('/admin')}
                >
                  <View style={styles.menuItemLeft}>
                    <Ionicons name="shield-checkmark" size={24} color="#d32f2f" />
                    <Text style={[styles.menuItemLabel, { color: '#d32f2f' }]}>Admin Dashboard</Text>
                  </View>
                  <Ionicons name="chevron-forward" size={20} color="#666" />
                </TouchableOpacity>
              )}
            </View>
          )}
          
          <View style={styles.menuSection}>
            <Text style={styles.sectionTitle}>Account</Text>
            {menuItems.map((item, index) => (
              <TouchableOpacity
                key={index}
                style={styles.menuItem}
                onPress={() => router.push(item.route as any)}
              >
                <View style={styles.menuItemLeft}>
                  <Ionicons name={item.icon as any} size={24} color="#333" />
                  <Text style={styles.menuItemLabel}>{item.label}</Text>
                </View>
                <Ionicons name="chevron-forward" size={20} color="#666" />
              </TouchableOpacity>
            ))}
          </View>

          <TouchableOpacity 
            style={styles.logoutButton}
            onPress={handleLogout}
          >
            <Text style={styles.logoutButtonText}>Sign Out</Text>
          </TouchableOpacity>
        </>
      ) : (
        <View style={styles.menuSection}>
          <TouchableOpacity 
            style={styles.loginButton}
            onPress={() => router.push('/login')}
          >
            <Ionicons name="log-in-outline" size={24} color="#fff" style={{ marginRight: 8 }} />
            <Text style={styles.loginButtonText}>Sign In</Text>
          </TouchableOpacity>
          <TouchableOpacity 
            style={styles.registerButton}
            onPress={() => router.push('/register')}
          >
            <Text style={styles.registerButtonText}>Don't have an account? Sign Up</Text>
          </TouchableOpacity>
        </View>
      )}
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#f5f5f5',
  },
  header: {
    backgroundColor: '#6200ee',
    padding: 32,
    alignItems: 'center',
    paddingTop: 60,
  },
  avatarContainer: {
    width: 100,
    height: 100,
    borderRadius: 50,
    backgroundColor: 'rgba(255, 255, 255, 0.2)',
    justifyContent: 'center',
    alignItems: 'center',
    marginBottom: 16,
  },
  userName: {
    fontSize: 24,
    fontWeight: 'bold',
    color: '#fff',
    marginBottom: 4,
  },
  userEmail: {
    fontSize: 14,
    color: 'rgba(255, 255, 255, 0.8)',
  },
  menuSection: {
    backgroundColor: '#fff',
    marginTop: 16,
    paddingVertical: 8,
  },
  menuItem: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    padding: 16,
    borderBottomWidth: 1,
    borderBottomColor: '#f0f0f0',
  },
  menuItemLeft: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  menuItemLabel: {
    fontSize: 16,
    color: '#333',
    marginLeft: 16,
  },
  sectionTitle: {
    fontSize: 14,
    fontWeight: '600',
    color: '#666',
    marginBottom: 8,
    marginTop: 8,
    paddingHorizontal: 16,
    textTransform: 'uppercase',
  },
  logoutButton: {
    margin: 16,
    padding: 16,
    backgroundColor: '#fff',
    borderRadius: 8,
    alignItems: 'center',
    borderWidth: 1,
    borderColor: '#e0e0e0',
  },
  logoutButtonText: {
    fontSize: 16,
    fontWeight: '600',
    color: '#d32f2f',
  },
  loginButton: {
    margin: 16,
    padding: 16,
    backgroundColor: '#6200ee',
    borderRadius: 8,
    alignItems: 'center',
    flexDirection: 'row',
    justifyContent: 'center',
  },
  loginButtonText: {
    fontSize: 16,
    fontWeight: '600',
    color: '#fff',
  },
  registerButton: {
    margin: 16,
    marginTop: 0,
    padding: 16,
    alignItems: 'center',
  },
  registerButtonText: {
    fontSize: 14,
    color: '#666',
  },
});
