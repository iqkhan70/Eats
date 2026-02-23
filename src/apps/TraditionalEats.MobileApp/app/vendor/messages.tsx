import React, {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
} from "react";
import {
  ActivityIndicator,
  FlatList,
  SafeAreaView,
  StyleSheet,
  Text,
  TouchableOpacity,
  View,
} from "react-native";
import { Ionicons } from "@expo/vector-icons";
import { useFocusEffect, useLocalSearchParams, useRouter } from "expo-router";
import AsyncStorage from "@react-native-async-storage/async-storage";
import { authService } from "../../services/auth";
import { api } from "../../services/api";
import AppHeader from "../../components/AppHeader";
import {
  getVendorInbox,
  getVendorConversationMessages,
  type VendorConversation,
} from "../../services/vendorChat";

export default function VendorMessagesScreen() {
  const router = useRouter();
  const params = useLocalSearchParams<{ restaurantId?: string }>();

  const SKEW_MS = 2 * 60 * 1000;
  const restaurantIdParam =
    typeof params.restaurantId === "string" ? params.restaurantId : "";

  const [loading, setLoading] = useState(true);
  const [conversations, setConversations] = useState<VendorConversation[]>([]);
  const [vendorNameById, setVendorNameById] = useState<Record<string, string>>(
    {},
  );
  const [myUserId, setMyUserId] = useState<string>("");
  const [lastSeenByConversationId, setLastSeenByConversationId] = useState<
    Record<string, number>
  >({});
  const [lastSenderRoleByConversationId, setLastSenderRoleByConversationId] =
    useState<Record<string, string>>({});

  const pollingRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const loadingRef = useRef(false);
  const hasLoadedOnceRef = useRef(false);

  const [scopedRestaurantId, setScopedRestaurantId] =
    useState<string>(restaurantIdParam);

  useEffect(() => {
    setScopedRestaurantId(restaurantIdParam);
  }, [restaurantIdParam]);

  const scopedConversations = useMemo(() => {
    if (!scopedRestaurantId) return conversations;
    return conversations.filter((c) => c.restaurantId === scopedRestaurantId);
  }, [conversations, scopedRestaurantId]);

  const visibleConversations = useMemo(() => {
    if (!myUserId) return scopedConversations;
    return scopedConversations.filter((c) => c.customerId !== myUserId);
  }, [myUserId, scopedConversations]);

  const getLastSeenStorageKey = useCallback(
    (conversationId: string) => {
      return myUserId
        ? `vendor_inbox_last_seen:${myUserId}:${conversationId}`
        : `vendor_inbox_last_seen:${conversationId}`;
    },
    [myUserId],
  );

  const getLegacyLastSeenStorageKey = useCallback((conversationId: string) => {
    return `vendor_inbox_last_seen:${conversationId}`;
  }, []);

  const getConversationActivityTs = useCallback((c: VendorConversation) => {
    const anyC = c as any;
    const last =
      typeof c.lastMessageAt === "string"
        ? c.lastMessageAt
        : typeof anyC?.LastMessageAt === "string"
          ? anyC.LastMessageAt
          : "";
    const updated =
      typeof c.updatedAt === "string"
        ? c.updatedAt
        : typeof anyC?.UpdatedAt === "string"
          ? anyC.UpdatedAt
          : "";

    const t = Math.max(
      last ? new Date(last).getTime() : 0,
      updated ? new Date(updated).getTime() : 0,
    );
    return Number.isFinite(t) ? t : 0;
  }, []);

  const loadLastSeen = useCallback(async () => {
    const entries = await Promise.all(
      (visibleConversations ?? []).map(async (c) => {
        try {
          const key = getLastSeenStorageKey(c.conversationId);
          const raw =
            (await AsyncStorage.getItem(key)) ??
            (await AsyncStorage.getItem(
              getLegacyLastSeenStorageKey(c.conversationId),
            ));
          const n = raw ? Number(raw) : 0;
          const now = Date.now();
          const cleaned =
            Number.isFinite(n) && n > 0
              ? n > now + 365 * 24 * 60 * 60 * 1000
                ? now
                : n
              : 0;
          return [c.conversationId, cleaned] as const;
        } catch {
          return [c.conversationId, 0] as const;
        }
      }),
    );

    setLastSeenByConversationId((prev) => {
      const next = { ...prev };
      for (const [id, ts] of entries) next[id] = ts;
      return next;
    });
  }, [
    getLastSeenStorageKey,
    getLegacyLastSeenStorageKey,
    visibleConversations,
  ]);

  const markConversationSeen = useCallback(
    async (conversationId: string, seenAt: number) => {
      const base = Number.isFinite(seenAt) && seenAt > 0 ? seenAt : 0;
      const nextSeenAt = Math.max(Date.now(), base);
      setLastSeenByConversationId((prev) => ({
        ...prev,
        [conversationId]: nextSeenAt,
      }));
      try {
        await AsyncStorage.setItem(
          getLastSeenStorageKey(conversationId),
          String(nextSeenAt),
        );
        await AsyncStorage.setItem(
          getLegacyLastSeenStorageKey(conversationId),
          String(nextSeenAt),
        );
      } catch {
        return;
      }
    },
    [getLastSeenStorageKey, getLegacyLastSeenStorageKey],
  );

  const restaurantIds = useMemo(() => {
    const ids = new Set<string>();
    for (const c of scopedConversations) {
      if (c.restaurantId) ids.add(c.restaurantId);
    }
    if (scopedRestaurantId) ids.add(scopedRestaurantId);
    return Array.from(ids);
  }, [scopedConversations, scopedRestaurantId]);

  const loadVendorNames = useCallback(
    async (ids: string[]) => {
      const missing = ids.filter((id) => !vendorNameById[id]);
      if (missing.length === 0) return;

      const results = await Promise.all(
        missing.map(async (restaurantId) => {
          try {
            const { data } = await api.get<{ name?: string; Name?: string }>(
              `/MobileBff/restaurants/${restaurantId}`,
            );
            const name =
              typeof data?.name === "string"
                ? data.name
                : typeof data?.Name === "string"
                  ? data.Name
                  : "";
            return { restaurantId, name };
          } catch {
            return { restaurantId, name: "" };
          }
        }),
      );

      setVendorNameById((prev) => {
        const next = { ...prev };
        for (const r of results) {
          if (r.name) next[r.restaurantId] = r.name;
        }
        return next;
      });
    },
    [vendorNameById],
  );

  const loadLastSenderRoles = useCallback(
    async (source: VendorConversation[]) => {
      try {
        const entries = await Promise.all(
          (source ?? []).map(async (c) => {
            try {
              const msgs = await getVendorConversationMessages(
                c.conversationId,
              );
              const last =
                Array.isArray(msgs) && msgs.length > 0
                  ? msgs[msgs.length - 1]
                  : null;
              const role =
                typeof last?.senderRole === "string" ? last.senderRole : "";
              return [c.conversationId, role] as const;
            } catch {
              return [c.conversationId, ""] as const;
            }
          }),
        );

        setLastSenderRoleByConversationId((prev) => {
          const next = { ...prev };
          for (const [id, role] of entries) next[id] = role;
          return next;
        });
      } catch {
        return;
      }
    },
    [],
  );

  const load = useCallback(
    async (silent = false) => {
      if (loadingRef.current) return;
      loadingRef.current = true;
      try {
        if (!silent) setLoading(true);
        const authenticated = await authService.isAuthenticated();
        if (!authenticated) {
          router.replace("/login");
          return;
        }
        const inbox = await getVendorInbox();
        setConversations(inbox);
        void loadLastSenderRoles(inbox);
      } catch (e) {
        console.error("Load vendor inbox:", e);
        if (!silent) setConversations([]);
      } finally {
        if (!silent) setLoading(false);
        else setLoading((prev) => (prev ? false : prev));
        loadingRef.current = false;
      }
    },
    [loadLastSenderRoles, router],
  );

  useEffect(() => {
    if (!myUserId) return;
    if (visibleConversations.length === 0) return;
    void loadLastSeen();
  }, [myUserId, visibleConversations.length, loadLastSeen]);

  useEffect(() => {
    let mounted = true;
    void (async () => {
      try {
        const id = await authService.getUserId();
        if (mounted) setMyUserId(typeof id === "string" ? id : "");
      } catch {
        if (mounted) setMyUserId("");
      }
    })();
    return () => {
      mounted = false;
    };
  }, []);

  useEffect(() => {
    if (visibleConversations.length === 0) return;
    void loadLastSeen();
  }, [visibleConversations, loadLastSeen]);

  useFocusEffect(
    useCallback(() => {
      const silent = hasLoadedOnceRef.current;
      hasLoadedOnceRef.current = true;
      void load(silent);

      if (pollingRef.current) clearInterval(pollingRef.current);
      pollingRef.current = setInterval(() => {
        void load(true);
      }, 5000);

      return () => {
        if (pollingRef.current) {
          clearInterval(pollingRef.current);
          pollingRef.current = null;
        }
      };
    }, [load]),
  );

  useEffect(() => {
    if (restaurantIds.length === 0) return;
    loadVendorNames(restaurantIds);
  }, [restaurantIds, loadVendorNames]);

  return (
    <SafeAreaView style={styles.safe}>
      <AppHeader
        title="Messages"
        right={
          <TouchableOpacity
            onPress={() => void load(false)}
            style={styles.backButton}
          >
            <Ionicons name="refresh" size={22} color="#333" />
          </TouchableOpacity>
        }
      />

      {!!scopedRestaurantId && (
        <View style={styles.scopeBar}>
          <Text style={styles.scopeText} numberOfLines={1}>
            Viewing Vendor:{" "}
            {vendorNameById[scopedRestaurantId] || scopedRestaurantId}
          </Text>
          <TouchableOpacity
            onPress={() => setScopedRestaurantId("")}
            style={styles.scopeClearBtn}
            accessibilityLabel="Clear vendor filter"
            hitSlop={{ top: 10, bottom: 10, left: 10, right: 10 }}
          >
            <Ionicons name="close" size={18} color="#333" />
          </TouchableOpacity>
        </View>
      )}

      {loading ? (
        <View style={styles.center}>
          <ActivityIndicator size="large" color="#6200ee" />
          <Text style={styles.loadingText}>Loading messages...</Text>
        </View>
      ) : scopedConversations.length === 0 ? (
        <View style={styles.center}>
          <Ionicons name="chatbubbles-outline" size={48} color="#bbb" />
          <Text style={styles.emptyText}>No messages yet</Text>
        </View>
      ) : (
        <FlatList
          data={visibleConversations}
          keyExtractor={(c) => c.conversationId}
          contentContainerStyle={styles.list}
          renderItem={({ item }) => (
            <TouchableOpacity
              style={styles.row}
              onPress={async () => {
                await markConversationSeen(
                  item.conversationId,
                  getConversationActivityTs(item),
                );
                router.push({
                  pathname: "/vendor/messages/[conversationId]",
                  params: {
                    conversationId: item.conversationId,
                    restaurantId: item.restaurantId,
                    vendorName: vendorNameById[item.restaurantId] || "",
                  },
                } as any);
              }}
            >
              <View style={styles.rowLeft}>
                <Ionicons
                  name="person-circle-outline"
                  size={34}
                  color="#6200ee"
                />
              </View>
              <View style={styles.rowBody}>
                <Text style={styles.primaryText} numberOfLines={1}>
                  {item.customerDisplayName || item.customerId || "Customer"}
                </Text>
                <Text style={styles.secondaryText} numberOfLines={1}>
                  Vendor:{" "}
                  {vendorNameById[item.restaurantId] || item.restaurantId}
                </Text>
              </View>
              {getConversationActivityTs(item) >
                (lastSeenByConversationId[item.conversationId] ?? 0) +
                  SKEW_MS &&
                (lastSenderRoleByConversationId[item.conversationId] || "")
                  .toLowerCase()
                  .trim() === "customer" && <View style={styles.unreadDot} />}
              <Ionicons name="chevron-forward" size={18} color="#777" />
            </TouchableOpacity>
          )}
        />
      )}
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: "#f5f5f5" },
  header: {
    flexDirection: "row",
    alignItems: "center",
    justifyContent: "space-between",
    paddingHorizontal: 8,
    paddingVertical: 12,
    backgroundColor: "#fff",
    borderBottomWidth: 1,
    borderBottomColor: "#eee",
  },
  scopeBar: {
    flexDirection: "row",
    alignItems: "center",
    justifyContent: "space-between",
    paddingHorizontal: 12,
    paddingVertical: 10,
    backgroundColor: "rgba(227, 242, 253, 0.9)",
    borderBottomWidth: 1,
    borderBottomColor: "rgba(25, 118, 210, 0.18)",
  },
  scopeText: {
    flex: 1,
    fontSize: 13,
    color: "#1976d2",
    fontWeight: "600",
    paddingRight: 8,
  },
  scopeClearBtn: {
    width: 28,
    height: 28,
    borderRadius: 14,
    alignItems: "center",
    justifyContent: "center",
    backgroundColor: "rgba(0,0,0,0.06)",
  },
  backButton: {
    width: 40,
    height: 40,
    alignItems: "center",
    justifyContent: "center",
  },
  title: {
    flex: 1,
    textAlign: "center",
    fontSize: 18,
    fontWeight: "600",
    color: "#333",
  },
  center: { flex: 1, alignItems: "center", justifyContent: "center" },
  loadingText: { marginTop: 12, color: "#666" },
  emptyText: { marginTop: 12, color: "#777", fontSize: 14 },
  list: { padding: 12 },
  row: {
    flexDirection: "row",
    alignItems: "center",
    backgroundColor: "#fff",
    borderRadius: 12,
    padding: 12,
    marginBottom: 10,
    borderWidth: 1,
    borderColor: "#eee",
  },
  rowLeft: { marginRight: 10 },
  rowBody: { flex: 1 },
  primaryText: { fontSize: 15, fontWeight: "600", color: "#333" },
  secondaryText: { fontSize: 12, color: "#666", marginTop: 2 },
  unreadDot: {
    width: 10,
    height: 10,
    borderRadius: 5,
    backgroundColor: "#6200ee",
    marginRight: 10,
  },
});
