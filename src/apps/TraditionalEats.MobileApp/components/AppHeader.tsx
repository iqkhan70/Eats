import React from 'react';
import { View, Text, TouchableOpacity, StyleSheet, Platform } from 'react-native';
import { LinearGradient } from 'expo-linear-gradient';
import { Ionicons } from '@expo/vector-icons';
import { useRouter } from 'expo-router';

// Orange-to-yellow gradient (Tailwind orange-500 → yellow-500)
const GRADIENT_COLORS = ['#f97316', '#eab308'] as const;

interface Props {
  title?: string;
  showBack?: boolean;
  onBack?: () => void;
  right?: React.ReactNode;
}

export default function AppHeader({ title, showBack = true, onBack, right }: Props) {
  const router = useRouter();

  const handleBack = () => {
    try {
      if (onBack) return onBack();
      if (router.canGoBack()) router.back();
      else router.replace('/(tabs)');
    } catch (e) {
      router.replace('/(tabs)');
    }
  };

  return (
    <LinearGradient
      colors={[...GRADIENT_COLORS]}
      start={{ x: 0, y: 0 }}
      end={{ x: 1, y: 0 }}
      style={styles.header}
    >
      {showBack ? (
        <TouchableOpacity
          onPress={handleBack}
          style={styles.backButton}
          hitSlop={{ top: 32, bottom: 32, left: 32, right: 32 }}
          activeOpacity={0.7}
          accessibilityLabel="Back"
        >
          <View style={styles.backCircle}>
            <Ionicons name={Platform.OS === 'ios' ? 'chevron-back' : 'arrow-back'} size={24} color="#fff" />
          </View>
        </TouchableOpacity>
      ) : (
        <View style={styles.backButton} />
      )}

      <Text style={styles.title} numberOfLines={1}>
        {title}
      </Text>

      <View style={styles.right}>{right}</View>
    </LinearGradient>
  );
}

const styles = StyleSheet.create({
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingHorizontal: 12,
    paddingVertical: 14,
    paddingTop: 48,
    borderBottomWidth: 0,
  },
  backButton: {
    width: 56,
    height: 56,
    alignItems: 'center',
    justifyContent: 'center',
    marginLeft: -8,
  },
  backCircle: {
    width: 44,
    height: 44,
    borderRadius: 22,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: 'rgba(255,255,255,0.2)',
  },
  title: {
    flex: 1,
    textAlign: 'center',
    fontSize: 18,
    fontWeight: '600',
    color: '#fff',
    marginHorizontal: 8,
  },
  right: { width: 56, alignItems: 'center', justifyContent: 'center', marginRight: -8 },
});
