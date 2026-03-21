import { Stack } from "expo-router";
import { RestaurantModeProvider } from "../../contexts/RestaurantModeContext";

export default function VendorLayout() {
  return (
    <RestaurantModeProvider>
      <Stack screenOptions={{ headerShown: false }} />
    </RestaurantModeProvider>
  );
}
