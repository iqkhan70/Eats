import { Stack } from 'expo-router';
import { StatusBar } from 'expo-status-bar';

export default function RootLayout() {
  return (
    <>
      <Stack
        screenOptions={{
          headerStyle: {
            backgroundColor: '#6200ee',
          },
          headerTintColor: '#fff',
          headerTitleStyle: {
            fontWeight: 'bold',
          },
        }}
      >
        <Stack.Screen 
          name="(tabs)" 
          options={{ headerShown: false }} 
        />
        <Stack.Screen 
          name="index" 
          options={{ title: 'TraditionalEats' }} 
        />
        <Stack.Screen 
          name="login" 
          options={{ title: 'Sign In' }} 
        />
        <Stack.Screen 
          name="register" 
          options={{ title: 'Sign Up', presentation: 'modal' }} 
        />
      </Stack>
      <StatusBar style="light" />
    </>
  );
}
