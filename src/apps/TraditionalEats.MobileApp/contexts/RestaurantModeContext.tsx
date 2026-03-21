/**
 * Restaurant Mode: When vendor enables "Accepting orders", they receive
 * full-screen new order alerts with Accept/Reject, countdown, and keep screen awake.
 */
import React, {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useRef,
  useState,
} from "react";
import AsyncStorage from "@react-native-async-storage/async-storage";
import {
  connectVendorChatHub,
  addVendorMessageListener,
  joinVendorRestaurant,
  leaveVendorRestaurant,
  type VendorChatMessage,
} from "../services/vendorChat";
import { authService } from "../services/auth";
import { api } from "../services/api";
import {
  isOrderPlaced,
  parseOrderPlaced,
  type OrderPlacedMetadata,
} from "../types/orderPlaced";
import NewOrderModal from "../components/NewOrderModal";

const ACCEPTING_ORDERS_KEY = "vendor_accepting_orders";

interface Restaurant {
  restaurantId: string;
  name: string;
}

interface RestaurantModeContextValue {
  acceptingOrders: boolean;
  setAcceptingOrders: (value: boolean) => void;
  pendingOrders: OrderPlacedMetadata[];
  dismissPendingOrder: () => void;
  respondToOrder: (orderId: string, accept: boolean) => Promise<void>;
  restaurantNameById: Record<string, string>;
}

const RestaurantModeContext = createContext<RestaurantModeContextValue | null>(
  null,
);

export function useRestaurantMode() {
  const ctx = useContext(RestaurantModeContext);
  return ctx;
}

export function RestaurantModeProvider({
  children,
}: {
  children: React.ReactNode;
}) {
  const [acceptingOrders, setAcceptingOrdersState] = useState(false);
  const [pendingOrders, setPendingOrders] = useState<OrderPlacedMetadata[]>([]);
  const [restaurants, setRestaurants] = useState<Restaurant[]>([]);
  const [restaurantNameById, setRestaurantNameById] = useState<
    Record<string, string>
  >({});
  const connectedRef = useRef(false);
  const joinedRestaurantsRef = useRef<Set<string>>(new Set());
  const acceptingOrdersRef = useRef(acceptingOrders);
  const [isVendor, setIsVendor] = useState(false);

  acceptingOrdersRef.current = acceptingOrders;

  const loadRestaurants = useCallback(async () => {
    try {
      const res = await api.get<Restaurant[]>(
        "/MobileBff/vendor/my-restaurants",
        { params: { __ts: Date.now() } },
      );
      const list = res.data ?? [];
      setRestaurants(list);
      const map: Record<string, string> = {};
      for (const r of list) {
        if (r.restaurantId) map[r.restaurantId] = r.name ?? "";
      }
      setRestaurantNameById(map);
      return list;
    } catch {
      return [];
    }
  }, []);

  const loadAcceptingState = useCallback(async () => {
    try {
      const raw = await AsyncStorage.getItem(ACCEPTING_ORDERS_KEY);
      const value = raw === "true";
      setAcceptingOrdersState(value);
      return value;
    } catch {
      return false;
    }
  }, []);

  const setAcceptingOrders = useCallback(async (value: boolean) => {
    setAcceptingOrdersState(value);
    try {
      await AsyncStorage.setItem(ACCEPTING_ORDERS_KEY, String(value));
    } catch {
      // ignore
    }
  }, []);

  const dismissPendingOrder = useCallback(() => {
    setPendingOrders((prev) => prev.slice(1));
  }, []);

  const respondToOrder = useCallback(
    async (orderId: string, accept: boolean) => {
      try {
        await api.put(`/MobileBff/orders/${orderId}/status`, {
          status: accept ? "Preparing" : "Cancelled",
          notes: accept ? null : "Rejected by vendor",
        });
      } catch {
        // Error handled by caller
      } finally {
        setPendingOrders((prev) => prev.slice(1));
      }
    },
    [],
  );

  useEffect(() => {
    let mounted = true;
    void (async () => {
      const val = await loadAcceptingState();
      if (!mounted) return;
      const vendor = await authService.isVendor();
      if (!mounted) return;
      setIsVendor(vendor);
      if (vendor) {
        await loadRestaurants();
      }
    })();
    return () => {
      mounted = false;
    };
  }, [loadAcceptingState, loadRestaurants]);

  useEffect(() => {
    if (!isVendor) return;

    const onMessage = (msg: VendorChatMessage) => {
      if (!acceptingOrders) return;
      if (!msg.metadataJson || !isOrderPlaced(msg.metadataJson)) return;

      const order = parseOrderPlaced(msg.metadataJson);
      if (!order) return;

      setPendingOrders((prev) => {
        const orderIdStr = String(order.orderId ?? "").toLowerCase();
        if (orderIdStr && prev.some((o) => String(o.orderId ?? "").toLowerCase() === orderIdStr)) return prev;
        return [...prev, order];
      });
      try {
        const Haptics = require("expo-haptics");
        Haptics.notificationAsync(Haptics.NotificationFeedbackType.Success);
      } catch {
        // Haptics may not be available
      }
    };

    const unsub = addVendorMessageListener(onMessage);

    const onError = (err: string) => {
      console.warn("RestaurantMode SignalR:", err);
    };

    const onState = (connected: boolean) => {
      connectedRef.current = connected;
      if (!connected && acceptingOrdersRef.current) {
        connectVendorChatHub(() => {}, onError, onState).catch(() => {});
      }
    };

    connectVendorChatHub(() => {}, onError, onState).catch(() => {});

    return () => {
      unsub();
      // Don't disconnect - connection may be shared with VendorChat
    };
  }, [acceptingOrders, isVendor]);

  useEffect(() => {
    if (!acceptingOrders || restaurants.length === 0) {
      if (acceptingOrders === false) {
        try { require("expo-keep-awake").deactivateKeepAwake(); } catch {}
        for (const rid of joinedRestaurantsRef.current) {
          leaveVendorRestaurant(rid).catch(() => {});
        }
        joinedRestaurantsRef.current.clear();
      }
      return;
    }

    try { require("expo-keep-awake").activateKeepAwakeAsync().catch(() => {}); } catch {}

    const joinAll = async () => {
      for (const r of restaurants) {
        if (!r.restaurantId) continue;
        try {
          await joinVendorRestaurant(r.restaurantId);
          joinedRestaurantsRef.current.add(r.restaurantId);
        } catch {
          // join failed, will retry on reconnect
        }
      }
    };

    void joinAll();

    return () => {
      try { require("expo-keep-awake").deactivateKeepAwake(); } catch {}
      for (const rid of joinedRestaurantsRef.current) {
        leaveVendorRestaurant(rid).catch(() => {});
      }
      joinedRestaurantsRef.current.clear();
    };
  }, [acceptingOrders, restaurants]);

  const currentOrder = pendingOrders[0] ?? null;
  const restaurantId = currentOrder?.restaurantId ?? "";
  const restaurantName = restaurantId
    ? restaurantNameById[restaurantId] ?? ""
    : "";

  const value: RestaurantModeContextValue = {
    acceptingOrders,
    setAcceptingOrders,
    pendingOrders,
    dismissPendingOrder,
    respondToOrder,
    restaurantNameById,
  };

  return (
    <RestaurantModeContext.Provider value={value}>
      {children}
      <NewOrderModal
        visible={pendingOrders.length > 0}
        order={currentOrder}
        queueLength={pendingOrders.length}
        restaurantId={restaurantId}
        restaurantName={restaurantName}
        onAccept={(orderId) => respondToOrder(orderId, true)}
        onReject={(orderId) => respondToOrder(orderId, false)}
        onDismiss={dismissPendingOrder}
      />
    </RestaurantModeContext.Provider>
  );
}
