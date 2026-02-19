# Cart Screen Enhancement for Custom Payment Requests

This file shows the changes needed to the Cart screen to handle custom order parameters from payment requests.

## Required Changes to `app/cart.tsx`

### 1. Update Imports
Add this import to detect custom order params:
```typescript
import { useLocalSearchParams } from "expo-router";
```

### 2. Add to Component
Inside the CartScreen component, add near the top:
```typescript
const params = useLocalSearchParams<{
  customOrderAmount?: string;
  customOrderDescription?: string;
}>();

const [customOrderCreated, setCustomOrderCreated] = useState(false);
```

### 3. Add useEffect to Handle Custom Orders
```typescript
useEffect(() => {
  // If custom order params are present and cart hasn't been created yet
  if (params.customOrderAmount && !customOrderCreated && cart) {
    const amount = parseFloat(params.customOrderAmount);
    if (!isNaN(amount) && amount > 0) {
      createCustomOrderItem(amount, params.customOrderDescription);
      setCustomOrderCreated(true);
    }
  }
}, [params.customOrderAmount, params.customOrderDescription, customOrderCreated, cart]);

// Add this function
const createCustomOrderItem = async (amount: number, description?: string) => {
  if (!cart) return;
  
  try {
    // Create a custom cart item with amount as the price
    const itemName = `Custom Order${description ? ` - ${description}` : ""}`;
    
    await cartService.addItemToCart(
      cart.cartId,
      // Use a special null menuItemId to indicate custom item
      "00000000-0000-0000-0000-000000000000", // or null, depending on service
      itemName,
      amount,
      1,
      { customItem: "true" } // Mark as custom item
    );
    
    // Reload cart to show new item
    await loadCart();
  } catch (error: any) {
    console.error("Failed to add custom order item:", error);
    Alert.alert("Error", "Failed to add custom order to cart");
  }
};
```

### 4. Optional: Display Custom Item Badge
In the order items render section, add a badge for custom items:
```typescript
{order.items.map((item, index) => {
  const isCustom = item.modifiersJson?.includes?.("customItem");
  return (
    <View key={item.orderItemId}>
      <View style={styles.orderItem}>
        {isCustom && (
          <View style={styles.customBadge}>
            <Text style={styles.customBadgeText}>CUSTOM</Text>
          </View>
        )}
        {/* ... rest of item rendering ... */}
      </View>
    </View>
  );
})}
```

### 5. Add Styles for Custom Item Badge
```typescript
const styles = StyleSheet.create({
  // ... existing styles ...
  
  customBadge: {
    backgroundColor: "#e3f2fd",
    borderLeftWidth: 4,
    borderLeftColor: "#0097a7",
    paddingVertical: 4,
    paddingHorizontal: 8,
    marginBottom: 8,
    borderRadius: 4,
  },
  customBadgeText: {
    fontSize: 10,
    fontWeight: "700",
    color: "#0097a7",
    letterSpacing: 0.5,
  },
});
```

## Alternative: Simpler Implementation

If you want to keep it minimal, just add this to the component:

```typescript
// At the top with other state
const params = useLocalSearchParams<{
  customOrderAmount?: string;
  customOrderDescription?: string;
}>();

// In loadCart() or after cart loads, check for params
useEffect(() => {
  if (params.customOrderAmount && cart?.cartId) {
    const amount = parseFloat(params.customOrderAmount);
    if (!isNaN(amount) && amount > 0) {
      // Add custom item to cart
      const itemName = `Custom Order${params.customOrderDescription ? ` - ${params.customOrderDescription}` : ""}`;
      cartService.addItemToCart(
        cart.cartId,
        null, // or special UUID for custom items
        itemName,
        amount,
        1
      ).then(() => loadCart());
    }
  }
}, [params.customOrderAmount]);
```

## Testing

1. **Vendor side:** 
   - Open vendor chat
   - Tap green cash button
   - Enter $15.00 and "Custom appetizer platter"
   - Tap "Send Payment Request"

2. **Customer side:**
   - Open order chat
   - See payment request bubble with $15.00
   - Tap "Accept Payment"
   - Should route to cart with custom item pre-filled
   - Cart should show:
     - Item: "Custom Order - Custom appetizer platter"
     - Price: $15.00
     - Quantity: 1
   - Customer adds notes if desired
   - Customer places order normally
   - Order created with custom $15.00 item

## Notes

- The `customOrderAmount` and `customOrderDescription` are passed as route params
- Cart screen detects them and auto-adds a custom item
- The custom item has `menuItemId = null` to indicate it's not from standard menu
- Price is set to the requested amount
- Customer still controls final notes and can modify before checkout
- Standard payment flow applies after custom item is in cart
