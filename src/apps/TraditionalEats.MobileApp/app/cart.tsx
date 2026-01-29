import React, { useState, useCallback } from "react";
import {
  View,
  Text,
  StyleSheet,
  ScrollView,
  TouchableOpacity,
  ActivityIndicator,
  Alert,
  TextInput,
  RefreshControl,
} from "react-native";
import { useRouter, useFocusEffect } from "expo-router";
import { Ionicons } from "@expo/vector-icons";
import { cartService, Cart, CartItem } from "../services/cart";
import { authService } from "../services/auth";

export default function CartScreen() {
  const router = useRouter();

  const [cart, setCart] = useState<Cart | null>(null);
  const [loading, setLoading] = useState(true);
  const [placingOrder, setPlacingOrder] = useState(false);

  const [deliveryAddress, setDeliveryAddress] = useState(
    "Delivery is not available yet, might be available later based on customer needs. Pickup only!",
  );
  const [specialInstructions, setSpecialInstructions] = useState("");

  const [isAuthenticated, setIsAuthenticated] = useState(false);

  // ✅ Pull-to-refresh state
  const [refreshing, setRefreshing] = useState(false);

  // Reload cart when screen comes into focus (e.g., after login)
  useFocusEffect(
    useCallback(() => {
      loadCart();
      checkAuthStatus();
    }, []),
  );

  const checkAuthStatus = async () => {
    const authenticated = await authService.isAuthenticated();
    setIsAuthenticated(authenticated);
  };

  const loadCart = async () => {
    try {
      setLoading(true);
      const cartData = await cartService.getCart();

      // Ensure items array exists
      if (cartData && !cartData.items) {
        cartData.items = [];
      }

      // If cart is null or has no items, set to null
      if (!cartData || !cartData.items || cartData.items.length === 0) {
        setCart(null);
      } else {
        setCart(cartData);
      }
    } catch (error: any) {
      console.error("Error loading cart:", error);

      // Don't show alert for empty cart (404/204/400 with "not found"), only for actual errors
      if (
        error.response?.status === 404 ||
        error.response?.status === 204 ||
        (error.response?.status === 400 &&
          error.response?.data?.message?.includes("not found"))
      ) {
        setCart(null);
      } else {
        Alert.alert(
          "Error",
          error.response?.data?.error || "Failed to load cart",
        );
        setCart(null);
      }
    } finally {
      setLoading(false);
    }
  };

  // ✅ Pull-to-refresh handler
  const onRefresh = useCallback(async () => {
    try {
      setRefreshing(true);
      await Promise.all([loadCart(), checkAuthStatus()]);
    } finally {
      setRefreshing(false);
    }
  }, []);

  const increaseQuantity = async (item: CartItem) => {
    if (!cart) return;
    try {
      await cartService.updateCartItemQuantity(
        cart.cartId,
        item.cartItemId,
        item.quantity + 1,
      );
      await loadCart();
    } catch (error: any) {
      Alert.alert("Error", "Failed to update quantity");
    }
  };

  const decreaseQuantity = async (item: CartItem) => {
    if (!cart) return;
    try {
      if (item.quantity > 1) {
        await cartService.updateCartItemQuantity(
          cart.cartId,
          item.cartItemId,
          item.quantity - 1,
        );
      } else {
        await removeItem(item);
      }
      await loadCart();
    } catch (error: any) {
      Alert.alert("Error", "Failed to update quantity");
    }
  };

  const removeItem = async (item: CartItem) => {
    if (!cart) return;
    try {
      await cartService.removeCartItem(cart.cartId, item.cartItemId);
      await loadCart();
    } catch (error: any) {
      // If cart not found, just reload - the item is already gone
      if (
        error.response?.status === 400 &&
        error.response?.data?.message?.includes("not found")
      ) {
        await loadCart();
        return;
      }
      const errorMessage = error.message || "Failed to remove item";
      Alert.alert("Error", errorMessage);
    }
  };

  const clearCart = async () => {
    if (!cart) return;

    Alert.alert(
      "Clear Cart",
      "Are you sure you want to remove all items from your cart?",
      [
        { text: "Cancel", style: "cancel" },
        {
          text: "Clear",
          style: "destructive",
          onPress: async () => {
            try {
              await cartService.clearCart(cart.cartId);
              await loadCart();
              Alert.alert("Success", "Cart cleared");
            } catch (error: any) {
              // If cart not found, it's already cleared - just reload
              if (
                error.response?.status === 400 &&
                error.response?.data?.message?.includes("not found")
              ) {
                await loadCart();
                return;
              }
              const errorMessage = error.message || "Failed to clear cart";
              Alert.alert("Error", errorMessage);
            }
          },
        },
      ],
    );
  };

  const placeOrder = async () => {
    if (!cart || !deliveryAddress.trim()) {
      Alert.alert("Error", "Please enter a delivery address");
      return;
    }

    // Check if user is authenticated
    const authenticated = await authService.isAuthenticated();
    if (!authenticated) {
      Alert.alert(
        "Login Required",
        "You need to be logged in to place an order. Would you like to log in now?",
        [
          { text: "Cancel", style: "cancel" },
          {
            text: "Log In",
            onPress: () => router.push("/login"),
          },
        ],
      );
      return;
    }

    try {
      setPlacingOrder(true);

      const orderId = await cartService.placeOrder(
        cart.cartId,
        deliveryAddress,
        specialInstructions.trim() || undefined,
      );

      // Clear cart state immediately after successful order placement
      setCart(null);
      setDeliveryAddress(
        "Delivery is not available yet, might be available later based on customer needs. Pickup only!",
      );
      setSpecialInstructions("");

      Alert.alert(
        "Success",
        `Order placed! Order ID: ${orderId.substring(0, 8)}`,
        [
          {
            text: "OK",
            // ✅ Force Orders screen to "refresh" by changing the URL each time
            onPress: () => router.push(`/(tabs)/orders?refresh=${Date.now()}`),
          },
        ],
      );
    } catch (error: any) {
      if (error.response?.status === 401) {
        Alert.alert(
          "Authentication Required",
          "Your session has expired. Please log in again to place an order.",
          [
            { text: "Cancel", style: "cancel" },
            { text: "Log In", onPress: () => router.push("/login") },
          ],
        );
      } else {
        const errorMessage =
          error.response?.data?.message ||
          error.message ||
          "Failed to place order";
        Alert.alert("Error", errorMessage);
      }
    } finally {
      setPlacingOrder(false);
      // Optional: refresh cart again (in case backend changed anything)
      await loadCart();
    }
  };

  if (loading) {
    return (
      <View style={styles.centerContainer}>
        <ActivityIndicator size="large" color="#6200ee" />
        <Text style={styles.loadingText}>Loading cart...</Text>
      </View>
    );
  }

  // ✅ Empty cart: still allow pull-to-refresh by using ScrollView
  if (!cart || !cart.items || cart.items.length === 0) {
    return (
      <View style={styles.container}>
        <View style={styles.header}>
          <TouchableOpacity
            onPress={() => router.back()}
            style={styles.backButton}
          >
            <Ionicons name="arrow-back" size={24} color="#333" />
          </TouchableOpacity>
          <Text style={styles.headerTitle}>Cart</Text>
        </View>

        <ScrollView
          contentContainerStyle={styles.centerContainer}
          refreshControl={
            <RefreshControl refreshing={refreshing} onRefresh={onRefresh} />
          }
        >
          <Ionicons name="cart-outline" size={64} color="#ccc" />
          <Text style={styles.emptyText}>Your cart is empty</Text>
          <Text style={styles.emptySubtext}>
            Add items from the menu to get started
          </Text>

          <TouchableOpacity
            style={styles.browseButton}
            onPress={() => router.push("/(tabs)/restaurants")}
          >
            <Text style={styles.browseButtonText}>Browse Restaurants</Text>
          </TouchableOpacity>

          <Text style={styles.pullHint}>Pull down to refresh</Text>
        </ScrollView>
      </View>
    );
  }

  return (
    <View style={styles.container}>
      <View style={styles.header}>
        <TouchableOpacity
          onPress={() => router.back()}
          style={styles.backButton}
        >
          <Ionicons name="arrow-back" size={24} color="#333" />
        </TouchableOpacity>
        <Text style={styles.headerTitle}>Shopping Cart</Text>
      </View>

      <ScrollView
        style={styles.content}
        refreshControl={
          <RefreshControl refreshing={refreshing} onRefresh={onRefresh} />
        }
      >
        {cart.items.map((item) => (
          <View key={item.cartItemId} style={styles.cartItem}>
            <View style={styles.itemInfo}>
              <Text style={styles.itemName}>{item.name}</Text>
              <Text style={styles.itemPrice}>
                ${item.unitPrice.toFixed(2)} each
              </Text>
            </View>

            <View style={styles.quantityControls}>
              <TouchableOpacity
                style={styles.quantityButton}
                onPress={() => decreaseQuantity(item)}
              >
                <Ionicons name="remove" size={20} color="#6200ee" />
              </TouchableOpacity>

              <Text style={styles.quantityText}>{item.quantity}</Text>

              <TouchableOpacity
                style={styles.quantityButton}
                onPress={() => increaseQuantity(item)}
              >
                <Ionicons name="add" size={20} color="#6200ee" />
              </TouchableOpacity>
            </View>

            <Text style={styles.itemTotal}>${item.totalPrice.toFixed(2)}</Text>

            <TouchableOpacity
              style={styles.removeButton}
              onPress={() => removeItem(item)}
            >
              <Ionicons name="trash-outline" size={20} color="#c62828" />
            </TouchableOpacity>
          </View>
        ))}

        {(() => {
          const amountBeforeServiceFee =
            cart.subtotal + cart.tax + cart.deliveryFee;
          const serviceFee = Math.min(amountBeforeServiceFee * 0.02, 5);
          const totalWithServiceFee = amountBeforeServiceFee + serviceFee;
          return (
            <View style={styles.summary}>
              <View style={styles.summaryRow}>
                <Text style={styles.summaryLabel}>Subtotal</Text>
                <Text style={styles.summaryValue}>
                  ${cart.subtotal.toFixed(2)}
                </Text>
              </View>
              <View style={styles.summaryRow}>
                <Text style={styles.summaryLabel}>Tax</Text>
                <Text style={styles.summaryValue}>${cart.tax.toFixed(2)}</Text>
              </View>
              <View style={styles.summaryRow}>
                <Text style={styles.summaryLabel}>Delivery Fee</Text>
                <Text style={styles.summaryValue}>
                  ${cart.deliveryFee.toFixed(2)}
                </Text>
              </View>
              <View style={styles.summaryRow}>
                <Text style={styles.summaryLabel}>Service Fee</Text>
                <Text style={styles.summaryValue}>
                  ${serviceFee.toFixed(2)}
                </Text>
              </View>
              <View style={styles.divider} />
              <View style={styles.summaryRow}>
                <Text style={styles.totalLabel}>Total</Text>
                <Text style={styles.totalValue}>
                  ${totalWithServiceFee.toFixed(2)}
                </Text>
              </View>
            </View>
          );
        })()}

        {!isAuthenticated && (
          <View style={styles.authWarning}>
            <Ionicons name="alert-circle-outline" size={20} color="#ff9800" />
            <Text style={styles.authWarningText}>
              You need to be logged in to place an order
            </Text>
            <TouchableOpacity
              style={styles.loginButton}
              onPress={() => router.push("/login")}
            >
              <Text style={styles.loginButtonText}>Log In</Text>
            </TouchableOpacity>
          </View>
        )}

        {cart.items.length > 0 && (
          <TouchableOpacity style={styles.clearCartButton} onPress={clearCart}>
            <Ionicons name="trash-outline" size={20} color="#c62828" />
            <Text style={styles.clearCartText}>Clear Cart</Text>
          </TouchableOpacity>
        )}

        <View style={styles.deliverySection}>
          <Text style={styles.deliveryLabel}>
            Special Instructions (Optional)
          </Text>
          <TextInput
            style={styles.deliveryInput}
            placeholder="e.g., 'Cut with clean knife', 'I will pick it up around 3 PM CST', etc."
            value={specialInstructions}
            onChangeText={setSpecialInstructions}
            multiline
            numberOfLines={3}
          />
        </View>

        <View style={styles.deliverySection}>
          <Text style={styles.deliveryLabel}>Delivery Address</Text>
          <TextInput
            style={styles.deliveryInput}
            placeholder="Enter your delivery address"
            value={deliveryAddress}
            editable={false}
            onChangeText={setDeliveryAddress}
            multiline
          />
        </View>

        <TouchableOpacity
          style={[
            styles.placeOrderButton,
            (placingOrder || !deliveryAddress.trim() || !isAuthenticated) &&
              styles.placeOrderButtonDisabled,
          ]}
          onPress={placeOrder}
          disabled={placingOrder || !deliveryAddress.trim() || !isAuthenticated}
        >
          {placingOrder ? (
            <ActivityIndicator color="#fff" />
          ) : (
            <Text style={styles.placeOrderButtonText}>Place Order</Text>
          )}
        </TouchableOpacity>

        <Text style={styles.pullHint}>Pull down to refresh</Text>
      </ScrollView>
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: "#f5f5f5" },

  centerContainer: {
    flexGrow: 1,
    justifyContent: "center",
    alignItems: "center",
    padding: 20,
  },

  loadingText: { marginTop: 10, fontSize: 16, color: "#666" },

  header: {
    flexDirection: "row",
    alignItems: "center",
    padding: 16,
    paddingTop: 50,
    backgroundColor: "#fff",
    borderBottomWidth: 1,
    borderBottomColor: "#e0e0e0",
  },
  backButton: { marginRight: 16 },
  headerTitle: { fontSize: 20, fontWeight: "bold", color: "#333" },

  content: { flex: 1, padding: 16 },

  cartItem: {
    flexDirection: "row",
    backgroundColor: "#fff",
    borderRadius: 12,
    padding: 12,
    marginBottom: 10,
    alignItems: "center",
    shadowColor: "#000",
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.1,
    shadowRadius: 4,
    elevation: 3,
  },

  itemInfo: { flex: 1, marginRight: 12 },
  itemName: { fontSize: 16, fontWeight: "600", color: "#333", marginBottom: 4 },
  itemPrice: { fontSize: 12, color: "#666" },

  quantityControls: {
    flexDirection: "row",
    alignItems: "center",
    marginRight: 12,
  },
  quantityButton: { padding: 4 },
  quantityText: {
    fontSize: 16,
    fontWeight: "600",
    marginHorizontal: 12,
    minWidth: 30,
    textAlign: "center",
  },

  itemTotal: {
    fontSize: 16,
    fontWeight: "bold",
    color: "#6200ee",
    marginRight: 12,
    minWidth: 60,
    textAlign: "right",
  },
  removeButton: { padding: 4 },

  summary: {
    backgroundColor: "#fff",
    borderRadius: 12,
    padding: 16,
    marginTop: 10,
    marginBottom: 16,
  },
  summaryRow: {
    flexDirection: "row",
    justifyContent: "space-between",
    marginBottom: 8,
  },
  summaryLabel: { fontSize: 14, color: "#666" },
  summaryValue: { fontSize: 14, color: "#333" },
  divider: { height: 1, backgroundColor: "#e0e0e0", marginVertical: 8 },
  totalLabel: { fontSize: 18, fontWeight: "bold", color: "#333" },
  totalValue: { fontSize: 18, fontWeight: "bold", color: "#6200ee" },

  deliverySection: {
    backgroundColor: "#fff",
    borderRadius: 12,
    padding: 16,
    marginBottom: 16,
  },
  deliveryLabel: {
    fontSize: 14,
    fontWeight: "600",
    color: "#333",
    marginBottom: 8,
  },
  deliveryInput: {
    borderWidth: 1,
    borderColor: "#e0e0e0",
    borderRadius: 8,
    padding: 12,
    fontSize: 14,
    minHeight: 80,
    textAlignVertical: "top",
  },

  placeOrderButton: {
    backgroundColor: "#6200ee",
    padding: 16,
    borderRadius: 8,
    alignItems: "center",
    marginBottom: 20,
  },
  placeOrderButtonDisabled: { opacity: 0.6 },
  placeOrderButtonText: { color: "#fff", fontSize: 16, fontWeight: "600" },

  emptyText: { fontSize: 18, fontWeight: "600", color: "#333", marginTop: 16 },
  emptySubtext: {
    fontSize: 14,
    color: "#666",
    marginTop: 8,
    textAlign: "center",
  },

  browseButton: {
    backgroundColor: "#6200ee",
    paddingHorizontal: 24,
    paddingVertical: 12,
    borderRadius: 8,
    marginTop: 20,
  },
  browseButtonText: { color: "#fff", fontSize: 14, fontWeight: "600" },

  authWarning: {
    backgroundColor: "#fff3cd",
    borderRadius: 12,
    padding: 16,
    marginBottom: 16,
    flexDirection: "row",
    alignItems: "center",
    gap: 12,
  },
  authWarningText: { flex: 1, fontSize: 14, color: "#856404" },
  loginButton: {
    backgroundColor: "#6200ee",
    paddingHorizontal: 16,
    paddingVertical: 8,
    borderRadius: 6,
  },
  loginButtonText: { color: "#fff", fontSize: 14, fontWeight: "600" },

  clearCartButton: {
    flexDirection: "row",
    alignItems: "center",
    justifyContent: "center",
    backgroundColor: "#fff",
    borderWidth: 1,
    borderColor: "#c62828",
    borderRadius: 8,
    padding: 12,
    marginBottom: 16,
    gap: 8,
  },
  clearCartText: { color: "#c62828", fontSize: 14, fontWeight: "600" },

  pullHint: { marginTop: 8, fontSize: 12, color: "#999" },
});
