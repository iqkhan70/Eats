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

export default function AdminSettingsScreen() {
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
          <Ionicons name="chevron-back" size={28} color="#fff" />
        </TouchableOpacity>
        <Text style={styles.headerTitle}>System Settings</Text>
        <View style={styles.placeholder} />
      </View>

      <View style={styles.content}>
        <View style={styles.infoCard}>
          <Ionicons name="settings-outline" size={48} color="#f57c00" />
          <Text style={styles.infoTitle}>System Configuration</Text>
          <Text style={styles.infoText}>
            System-wide settings and configuration options will be available here.
          </Text>
        </View>

        <View style={styles.settingsList}>
          <View style={styles.settingItem}>
            <Ionicons name="time-outline" size={24} color="#666" />
            <View style={styles.settingContent}>
              <Text style={styles.settingLabel}>Business Hours</Text>
              <Text style={styles.settingDescription}>Configure platform operating hours</Text>
            </View>
            <Ionicons name="chevron-forward" size={20} color="#ccc" />
          </View>

          <View style={styles.settingItem}>
            <Ionicons name="cash-outline" size={24} color="#666" />
            <View style={styles.settingContent}>
              <Text style={styles.settingLabel}>Payment Settings</Text>
              <Text style={styles.settingDescription}>Manage payment gateways and fees</Text>
            </View>
            <Ionicons name="chevron-forward" size={20} color="#ccc" />
          </View>

          <View style={styles.settingItem}>
            <Ionicons name="notifications-outline" size={24} color="#666" />
            <View style={styles.settingContent}>
              <Text style={styles.settingLabel}>Notification Settings</Text>
              <Text style={styles.settingDescription}>Configure system notifications</Text>
            </View>
            <Ionicons name="chevron-forward" size={20} color="#ccc" />
          </View>

          <View style={styles.settingItem}>
            <Ionicons name="shield-outline" size={24} color="#666" />
            <View style={styles.settingContent}>
              <Text style={styles.settingLabel}>Security Settings</Text>
              <Text style={styles.settingDescription}>Manage security and access controls</Text>
            </View>
            <Ionicons name="chevron-forward" size={20} color="#ccc" />
          </View>
        </View>

        <View style={styles.noteCard}>
          <Ionicons name="construct-outline" size={24} color="#f57c00" />
          <Text style={styles.noteText}>
            System settings functionality will be available in a future update.
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
    backgroundColor: '#f57c00',
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
  },
  settingsList: {
    marginBottom: 16,
  },
  settingItem: {
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
  settingContent: {
    flex: 1,
    marginLeft: 12,
  },
  settingLabel: {
    fontSize: 16,
    fontWeight: '600',
    color: '#333',
    marginBottom: 4,
  },
  settingDescription: {
    fontSize: 12,
    color: '#666',
  },
  noteCard: {
    backgroundColor: '#fff3cd',
    borderRadius: 12,
    padding: 16,
    flexDirection: 'row',
    alignItems: 'flex-start',
    gap: 12,
    borderLeftWidth: 4,
    borderLeftColor: '#f57c00',
  },
  noteText: {
    flex: 1,
    fontSize: 14,
    color: '#856404',
    lineHeight: 20,
  },
});
