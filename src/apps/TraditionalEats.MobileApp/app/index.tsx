import React, { useEffect, useState, useCallback } from 'react';
import { View, Text, StyleSheet, TouchableOpacity, Image } from 'react-native';
import { useRouter, useFocusEffect } from 'expo-router';
import { authService } from '../services/auth';

export default function WelcomeScreen() {
  const router = useRouter();
  const [isAuthenticated, setIsAuthenticated] = useState(false);
  const [authChecked, setAuthChecked] = useState(false);

  const checkAuth = async () => {
    const authenticated = await authService.isAuthenticated();
    setIsAuthenticated(authenticated);
    setAuthChecked(true);
  };

  useEffect(() => {
    checkAuth();
  }, []);

  useFocusEffect(
    useCallback(() => {
      checkAuth();
    }, []),
  );

  return (
    <View style={styles.container}>
      <View style={styles.content}>
        <Image source={require('../assets/logo.png')} style={styles.logo} />
        <Text style={styles.title}>Kram</Text>
        <Text style={styles.subtitle}>
          Discover authentic traditional food from local vendors
        </Text>
      </View>
      
      <View style={styles.buttonContainer}>
        <TouchableOpacity 
          style={styles.primaryButton}
          onPress={() => router.push('/(tabs)')}
        >
          <Text style={styles.primaryButtonText}>Get Started</Text>
        </TouchableOpacity>
        
        {authChecked && !isAuthenticated && (
          <TouchableOpacity 
            style={styles.secondaryButton}
            onPress={() => router.push('/login')}
          >
            <Text style={styles.secondaryButtonText}>Sign In</Text>
          </TouchableOpacity>
        )}
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#fff',
    justifyContent: 'space-between',
    padding: 20,
  },
  content: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
  },
  logo: {
    width: 120,
    height: 120,
    resizeMode: 'contain',
    marginBottom: 20,
  },
  title: {
    fontSize: 32,
    fontWeight: 'bold',
    color: '#333',
    marginTop: 20,
    marginBottom: 10,
  },
  subtitle: {
    fontSize: 16,
    color: '#666',
    textAlign: 'center',
    paddingHorizontal: 40,
  },
  buttonContainer: {
    paddingBottom: 40,
  },
  primaryButton: {
    backgroundColor: '#6200ee',
    padding: 16,
    borderRadius: 8,
    alignItems: 'center',
    marginBottom: 12,
  },
  primaryButtonText: {
    color: '#fff',
    fontSize: 16,
    fontWeight: '600',
  },
  secondaryButton: {
    padding: 16,
    borderRadius: 8,
    alignItems: 'center',
    borderWidth: 1,
    borderColor: '#6200ee',
  },
  secondaryButtonText: {
    color: '#6200ee',
    fontSize: 16,
    fontWeight: '600',
  },
});
