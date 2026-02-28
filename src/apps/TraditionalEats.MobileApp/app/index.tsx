import React, { useEffect } from 'react';
import { View, ActivityIndicator, StyleSheet } from 'react-native';
import { useRouter } from 'expo-router';
import { authService } from '../services/auth';

export default function IndexScreen() {
  const router = useRouter();

  useEffect(() => {
    const redirect = async () => {
      const authenticated = await authService.isAuthenticated();
      if (authenticated) {
        router.replace('/(tabs)');
      } else {
        router.replace('/login');
      }
    };
    redirect();
  }, []);

  return (
    <View style={styles.container}>
      <ActivityIndicator size="large" color="#6200ee" />
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#fff',
    justifyContent: 'center',
    alignItems: 'center',
  },
});
