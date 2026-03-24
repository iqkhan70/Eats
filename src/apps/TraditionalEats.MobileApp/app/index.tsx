import React, { useCallback, useRef } from "react";
import { View, ActivityIndicator, StyleSheet } from "react-native";
import { useRouter } from "expo-router";
import { StatusBar } from "expo-status-bar";

/**
 * Entry `/` → main tabs. Use onLayout (not useEffect) so router.replace runs after
 * this screen is laid out inside the Stack — avoids "navigate before mounting Root Layout".
 * Apple 5.1.1(v): open browse experience without forcing login first.
 */
export default function IndexScreen() {
  const router = useRouter();
  const didRedirect = useRef(false);

  const onRootLayout = useCallback(() => {
    if (didRedirect.current) return;
    didRedirect.current = true;
    // Defer one tick so the root Stack has finished wiring the navigation container.
    setTimeout(() => {
      router.replace("/(tabs)");
    }, 0);
  }, [router]);

  return (
    <View style={styles.container} onLayout={onRootLayout}>
      <StatusBar style="dark" />
      <ActivityIndicator size="large" color="#6200ee" />
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: "#fff",
    justifyContent: "center",
    alignItems: "center",
  },
});
