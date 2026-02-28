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
  Keyboard,
} from "react-native";
import { useRouter, useFocusEffect, useLocalSearchParams } from "expo-router";
import { Ionicons } from "@expo/vector-icons";
import { BlurView } from "expo-blur";
import { cartService, Cart, CartItem } from "../services/cart";
import { authService } from "../services/auth";
import * as WebBrowser from "expo-web-browser";
import { api } from "../services/api";
import AppHeader from "../components/AppHeader";

// Chat-driven custom requests are not real catalog menu items.
// We send Guid.Empty + isCustomRequest and let MobileBff normalize it.
const EMPTY_MENU_ITEM_ID = "00000000-0000-0000-0000-000000000000";

export default function CartScreen() {
  const router = useRouter();
  const params = useLocalSearchParams<{
    customOrderAmount?: string;
    customOrderDescription?: string;
    restaurantId?: string;
  }>();

  const [cart, setCart] = useState<Cart | null>(null);
  const [loading, setLoading] = useState(true);
  const [placingOrder, setPlacingOrder] = useState(false);
  const [customOrderCreated, setCustomOrderCreated] = useState(false);
  const customOrderCreatingRef = React.useRef(false);

  const [deliveryAddress, setDeliveryAddress] = useState(
    "Delivery is not available yet, might be available later based on customer needs. Pickup only!",
  );
  const [specialInstructions, setSpecialInstructions] = useState("");

  const [isAuthenticated, setIsAuthenticated] = useState(false);
  const [paymentReady, setPaymentReady] = useState(true); // Default to true, check when cart loads
  const [checkingPayment, setCheckingPayment] = useState(false);

  // ✅ Pull-to-refresh state
  const [refreshing, setRefreshing] = useState(false);

  // Reload cart when screen comes into focus (e.g., after login)
  useFocusEffect(
    useCallback(() => {
      loadCart();
      checkAuthStatus();
    }, []),
  );

  // Handle custom order from payment request
  React.useEffect(() => {
    const handleCustomOrder = async () => {
      if (!params.customOrderAmount || customOrderCreated) return;
      if (customOrderCreatingRef.current) return;

      const targetRestaurantId =
        typeof params.restaurantId === "string" && params.restaurantId.trim()
          ? params.restaurantId.trim()
          : undefined;

      customOrderCreatingRef.current = true;
      try {
        const amount = parseFloat(params.customOrderAmount);
        if (!isNaN(amount) && amount > 0) {
          // Ensure a cart exists for the target restaurant (if provided)
          let currentCart = cart;

          if (!currentCart) {
            try {
              const cartId = await cartService.createCart(targetRestaurantId);
              const cartData = await cartService.getCart();
              if (cartData) {
                setCart(cartData);
                currentCart = cartData;
              } else {
                throw new Error(`Cart created (${cartId}) but could not be loaded`);
              }
            } catch (error: any) {
              console.error("Failed to create cart for custom order:", error);
              Alert.alert("Error", "Failed to start an order for this payment request.");
              return;
            }
          } else if (
            targetRestaurantId &&
            currentCart.restaurantId &&
            currentCart.restaurantId !== targetRestaurantId
          ) {
            // Cart contains items from another vendor; ask user before replacing.
            Alert.alert(
              "Replace current cart?",
              "Your cart has items from another vendor. To accept this payment request, we need to start a new cart for this vendor.",
              [
                { text: "Cancel", style: "cancel" },
                {
                  text: "Replace",
                  style: "destructive",
                  onPress: async () => {
                    try {
                      await cartService.clearCart(currentCart!.cartId);
                      await cartService.createCart(targetRestaurantId);
                      const cartData = await cartService.getCart();
                      setCart(cartData);
                      if (cartData) {
                        await createCustomOrderItem(
                          cartData.cartId,
                          amount,
                          params.customOrderDescription,
                        );
                        if (!specialInstructions.trim() && params.customOrderDescription?.trim()) {
                          setSpecialInstructions(params.customOrderDescription.trim());
                        }
                        setCustomOrderCreated(true);
                      }
                    } catch (e: any) {
                      console.error("Failed to replace cart for custom order:", e);
                      Alert.alert("Error", "Failed to start a new cart for this payment request.");
                    }
                  },
                },
              ],
            );
            return;
          }

          try {
            await createCustomOrderItem(
              currentCart.cartId,
              amount,
              params.customOrderDescription,
            );
            if (!specialInstructions.trim() && params.customOrderDescription?.trim()) {
              setSpecialInstructions(params.customOrderDescription.trim());
            }
            setCustomOrderCreated(true);
          } catch (error: any) {
            console.error("Failed to add custom order item:", error);
            Alert.alert("Error", "Failed to add custom order to cart");
          }
        }
      } finally {
        customOrderCreatingRef.current = false;
      }
    };

    handleCustomOrder();
  }, [
    params.customOrderAmount,
    params.customOrderDescription,
    params.restaurantId,
    customOrderCreated,
    cart,
    specialInstructions,
  ]);

  const checkAuthStatus = async () => {
    const authenticated = await authService.isAuthenticated();
    setIsAuthenticated(authenticated);
  };

  const createCustomOrderItem = async (
    cartId: string,
    amount: number,
    description?: string,
  ) => {
    try {
      const itemName = `Custom Order${description ? ` - ${description}` : ""}`;

      await cartService.addItemToCart(
        cartId,
        EMPTY_MENU_ITEM_ID,
        itemName,
        amount,
        1,
        true,
      );

      // Reload cart to show new item
      await loadCart();
    } catch (error: any) {
      console.error("Failed to add custom order item:", error);
      throw error;
    }
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
        setPaymentReady(true); // Reset when cart is empty
      } else {
        setCart(cartData);
        // Check payment readiness if cart has a restaurant
        if (cartData.restaurantId) {
          await checkPaymentReadiness(cartData.restaurantId);
        } else {
          setPaymentReady(true);
        }
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
        setPaymentReady(true);
      } else {
        Alert.alert(
          "Error",
          error.response?.data?.error || "Failed to load cart",
        );
        setCart(null);
        setPaymentReady(true);
      }
    } finally {
      setLoading(false);
    }
  };

  const checkPaymentReadiness = async (restaurantId: string) => {
    try {
      setCheckingPayment(true);
      const response = await api.get<{
        restaurantId: string;
        paymentReady: boolean;
      }>(`/MobileBff/payments/restaurant/${restaurantId}/payment-ready`);
      setPaymentReady(response.data.paymentReady ?? true);
    } catch (error: any) {
      console.error("Error checking payment readiness:", error);
      // Fail open - allow orders if check fails
      setPaymentReady(true);
    } finally {
      setCheckingPayment(false);
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

      const paymentRedirectUrl = "kram://payment-done";
      const result = await cartService.placeOrder(
        cart.cartId,
        deliveryAddress,
        specialInstructions.trim() || undefined,
        `${paymentRedirectUrl}?status=success`,
        `${paymentRedirectUrl}?status=cancelled`,
      );

      // Clear cart state immediately after successful order placement
      setCart(null);
      setDeliveryAddress(
        "Delivery is not available yet, might be available later based on customer needs. Pickup only!",
      );
      setSpecialInstructions("");

      if (result.checkoutUrl) {
        // Open Stripe Checkout in in-app browser (stays within app)
        const authResult = await WebBrowser.openAuthSessionAsync(
          result.checkoutUrl,
          paymentRedirectUrl,
        );
        // Browser closed - navigate to orders (whether success or cancelled)
        router.push(`/(tabs)/orders?refresh=${Date.now()}`);
        if (authResult?.type === "success" && authResult.url?.includes("status=success")) {
          // Payment completed - optional: show brief success feedback
        }
        return;
      }

      const message = result.error
        ? `Order #${result.orderId.substring(0, 8)} placed. ${result.error}`
        : `Order placed! Order ID: ${result.orderId.substring(0, 8)}`;
      Alert.alert(result.error ? "Order placed" : "Success", message, [
        {
          text: "OK",
          onPress: () => router.push(`/(tabs)/orders?refresh=${Date.now()}`),
        },
      ]);
    } catch (error: any) {
      // Handle BadRequest (400) - vendor not set up for payments
      if (error.response?.status === 400 || error.message) {
        const errorMessage =
          error.message ||
          error.response?.data?.error ||
          error.response?.data?.message ||
          "This restaurant is not set up to accept payments yet. Please contact the restaurant directly.";
        Alert.alert("Cannot Place Order", errorMessage, [{ text: "OK" }]);
        return;
      }
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
        <AppHeader title="Cart" />

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
            <Text style={styles.browseButtonText}>Browse Vendors</Text>
          </TouchableOpacity>

          <Text style={styles.pullHint}>Pull down to refresh</Text>
        </ScrollView>
      </View>
    );
  }

  return (
    <View style={styles.container}>
      <AppHeader title="Shopping Cart" />

      <ScrollView
        style={styles.content}
        refreshControl={
          <RefreshControl refreshing={refreshing} onRefresh={onRefresh} />
        }
      >
        {cart.items.map((item) => (
          <View key={item.cartItemId} style={styles.cartItemWrapper}>
            <BlurView intensity={80} tint="light" style={styles.cartItem}>
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
                  <Ionicons name="remove" size={20} color="#fff" />
                </TouchableOpacity>

                <Text style={styles.quantityText}>{item.quantity}</Text>

                <TouchableOpacity
                  style={styles.quantityButton}
                  onPress={() => increaseQuantity(item)}
                >
                  <Ionicons name="add" size={20} color="#fff" />
                </TouchableOpacity>
              </View>

              <Text style={styles.itemTotal}>
                ${item.totalPrice.toFixed(2)}
              </Text>

              <TouchableOpacity
                style={styles.removeButton}
                onPress={() => removeItem(item)}
              >
                <Ionicons name="trash-outline" size={20} color="#c62828" />
              </TouchableOpacity>
            </BlurView>
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

        {/* Payment readiness banner - show if restaurant cannot accept payments */}
        {cart.restaurantId && !paymentReady && cart.items.length > 0 && (
          <View style={styles.paymentWarning}>
            <Ionicons name="warning" size={24} color="#000" />
            <View style={styles.paymentWarningContent}>
              <Text style={styles.paymentWarningTitle}>
                ⚠️ Cannot Place Order
              </Text>
              <Text style={styles.paymentWarningText}>
                This restaurant is not set up to accept payments yet. Please
                contact the restaurant directly or try again later.
              </Text>
            </View>
          </View>
        )}

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
            (placingOrder ||
              !deliveryAddress.trim() ||
              !isAuthenticated ||
              !paymentReady) &&
              styles.placeOrderButtonDisabled,
          ]}
          onPress={placeOrder}
          disabled={
            placingOrder ||
            !deliveryAddress.trim() ||
            !isAuthenticated ||
            !paymentReady
          }
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
  container: { flex: 1, backgroundColor: "transparent" },

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

  cartItemWrapper: {
    marginBottom: 10,
    borderRadius: 16,
    overflow: "hidden",
  },
  cartItem: {
    flexDirection: "row",
    backgroundColor: "rgba(255, 255, 255, 0.7)",
    borderRadius: 16,
    padding: 12,
    alignItems: "center",
    borderWidth: 1,
    borderColor: "rgba(255, 255, 255, 0.3)",
  },

  itemInfo: { flex: 1, marginRight: 12 },
  itemName: { fontSize: 16, fontWeight: "600", color: "#333", marginBottom: 4 },
  itemPrice: { fontSize: 12, color: "#666" },

  quantityControls: {
    flexDirection: "row",
    alignItems: "center",
    marginRight: 12,
  },
  quantityButton: {
    padding: 8,
    backgroundColor: "#f97316",
    borderRadius: 8,
  },
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
    backgroundColor: "rgba(255, 255, 255, 0.7)",
    borderRadius: 16,
    padding: 16,
    marginBottom: 16,
    borderWidth: 1,
    borderColor: "rgba(255, 255, 255, 0.3)",
    overflow: "hidden",
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
    backgroundColor: "#f97316",
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
    backgroundColor: "#f97316",
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
    backgroundColor: "#f97316",
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

  paymentWarning: {
    backgroundColor: "#ffc107",
    borderRadius: 12,
    padding: 16,
    marginBottom: 16,
    flexDirection: "row",
    alignItems: "flex-start",
    gap: 12,
  },
  paymentWarningContent: {
    flex: 1,
  },
  paymentWarningTitle: {
    fontSize: 16,
    fontWeight: "bold",
    color: "#000",
    marginBottom: 4,
  },
  paymentWarningText: {
    fontSize: 14,
    color: "#000",
    lineHeight: 20,
  },
});
