import { Redirect, useLocalSearchParams } from 'expo-router';

/** Redirect /menu to /catalog for backward compatibility */
export default function MenuRedirect() {
  const { restaurantId } = useLocalSearchParams<{ restaurantId: string }>();
  if (!restaurantId) return null;
  return <Redirect href={`/restaurants/${restaurantId}/catalog`} />;
}
