import React, { useCallback, useEffect, useMemo, useState } from "react";
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
import { useFocusEffect, useRouter } from "expo-router";
import AsyncStorage from "@react-native-async-storage/async-storage";
import { authService } from "../services/auth";
import { api } from "../services/api";
import AppHeader from "../components/AppHeader";
import {
  getMyVendorConversations,
  getVendorConversationMessages,
  type VendorConversation,
} from "../services/vendorChat";

export default function MessagesScreen() {
  const router = useRouter();

  const SKEW_MS = 2 * 60 * 1000;

  const [loading, setLoading] = useState(true);
  const [conversations, setConversations] = useState<VendorConversation[]>([]);
  const [vendorNameById, setVendorNameById] = useState<Record<string, string>>(
    {},
  );
  const [ownRestaurantIds, setOwnRestaurantIds] = useState<Set<string>>(
    () => new Set<string>(),
  );
  const [myUserId, setMyUserId] = useState<string>("");
  const [lastSeenByConversationId, setLastSeenByConversationId] = useState<
    Record<string, number>
  >({});
  const [lastSenderRoleByConversationId, setLastSenderRoleByConversationId] =
    useState<Record<string, string>>({});

  const getLastSeenStorageKey = useCallback(
    (conversationId: string) => {
      return myUserId
        ? `vendor_chat_last_seen:${myUserId}:${conversationId}`
        : `vendor_chat_last_seen:${conversationId}`;
    },
    [myUserId],
  );

  const getLegacyLastSeenStorageKey = useCallback((conversationId: string) => {
    return `vendor_chat_last_seen:${conversationId}`;
  }, []);

  const restaurantIds = useMemo(() => {
    const ids = new Set<string>();
    for (const c of conversations) {
      if (c.restaurantId) ids.add(c.restaurantId);
    }
    return Array.from(ids);
  }, [conversations]);

  const sortedConversations = useMemo(() => {
    const list = [...conversations].filter(
      (c) => !ownRestaurantIds.has(c.restaurantId),
    );
    const ts = (iso?: string) => {
      if (!iso) return 0;
      const t = new Date(iso).getTime();
      return Number.isFinite(t) ? t : 0;
    };
    return list.sort((a, b) => {
      const aAny = a as any;
      const bAny = b as any;
      const aLast =
        typeof a.lastMessageAt === "string"
          ? a.lastMessageAt
          : typeof aAny?.LastMessageAt === "string"
            ? aAny.LastMessageAt
            : "";
      const aUpdated =
        typeof a.updatedAt === "string"
          ? a.updatedAt
          : typeof aAny?.UpdatedAt === "string"
            ? aAny.UpdatedAt
            : "";
      const bLast =
        typeof b.lastMessageAt === "string"
          ? b.lastMessageAt
          : typeof bAny?.LastMessageAt === "string"
            ? bAny.LastMessageAt
            : "";
      const bUpdated =
        typeof b.updatedAt === "string"
          ? b.updatedAt
          : typeof bAny?.UpdatedAt === "string"
            ? bAny.UpdatedAt
            : "";

      const aTs = Math.max(ts(aLast), ts(aUpdated));
      const bTs = Math.max(ts(bLast), ts(bUpdated));
      return bTs - aTs;
    });
  }, [conversations, ownRestaurantIds]);

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

  const loadLastSeen = useCallback(
    async (source: VendorConversation[]) => {
      const entries = await Promise.all(
        (source ?? []).map(async (c) => {
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
    },
    [getLastSeenStorageKey, getLegacyLastSeenStorageKey],
  );

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

  const load = useCallback(async () => {
    try {
      setLoading(true);
      const authenticated = await authService.isAuthenticated();
      if (!authenticated) {
        router.replace("/login");
        return;
      }
      const inbox = await getMyVendorConversations();
      setConversations(inbox);
      await loadLastSeen(inbox);

      void (async () => {
        try {
          const entries = await Promise.all(
            (inbox ?? []).map(async (c) => {
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
      })();
    } catch (e) {
      console.error("Load inbox:", e);
      setConversations([]);
    } finally {
      setLoading(false);
    }
  }, [loadLastSeen, router]);

  useEffect(() => {
    load();
  }, [load]);

  useEffect(() => {
    if (!myUserId) return;
    if (conversations.length === 0) return;
    void loadLastSeen(conversations);
  }, [myUserId, conversations, loadLastSeen]);

  useEffect(() => {
    let mounted = true;
    void (async () => {
      try {
        try {
          const id = await authService.getUserId();
          if (mounted) setMyUserId(typeof id === "string" ? id : "");
        } catch {
          if (mounted) setMyUserId("");
        }

        const authenticated = await authService.isAuthenticated();
        if (!mounted) return;
        if (!authenticated) {
          setOwnRestaurantIds(new Set());
          return;
        }

        const vendor = await authService.isVendor();
        if (!mounted) return;
        if (!vendor) {
          setOwnRestaurantIds(new Set());
          return;
        }

        try {
          const res = await api.get<{ restaurantId: string }[]>(
            "/MobileBff/vendor/my-restaurants",
          );
          const ids = new Set<string>();
          const data = res.data ?? [];
          if (Array.isArray(data)) {
            for (const r of data) {
              if (r?.restaurantId) ids.add(r.restaurantId);
            }
          }
          setOwnRestaurantIds(ids);
        } catch {
          setOwnRestaurantIds(new Set());
        }
      } catch {
        if (mounted) setOwnRestaurantIds(new Set());
      }
    })();

    return () => {
      mounted = false;
    };
  }, []);

  useEffect(() => {
    if (conversations.length === 0) return;
    void loadLastSeen(conversations);
  }, [conversations, loadLastSeen]);

  useFocusEffect(
    useCallback(() => {
      void load();
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
        showBack
        onBack={() => {
          try {
            if (router.canGoBack()) router.back();
            else router.replace("/(tabs)/orders");
          } catch {
            router.replace("/(tabs)/orders");
          }
        }}
        right={
          <TouchableOpacity onPress={load} style={styles.headerBtn}>
            <Ionicons name="refresh" size={22} color="#333" />
          </TouchableOpacity>
        }
      />

      {loading ? (
        <View style={styles.center}>
          <ActivityIndicator size="large" color="#6200ee" />
          <Text style={styles.loadingText}>Loading messages...</Text>
        </View>
      ) : conversations.length === 0 ? (
        <View style={styles.center}>
          <Ionicons name="chatbubbles-outline" size={48} color="#bbb" />
          <Text style={styles.emptyText}>No messages yet</Text>
        </View>
      ) : (
        <FlatList
          data={sortedConversations}
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
                  pathname: "/messages/[conversationId]",
                  params: {
                    conversationId: item.conversationId,
                    restaurantId: item.restaurantId,
                    vendorName: vendorNameById[item.restaurantId] || "",
                  },
                } as any);
              }}
            >
              <View style={styles.rowLeft}>
                <Ionicons name="storefront-outline" size={28} color="#6200ee" />
              </View>
              <View style={styles.rowBody}>
                <Text style={styles.primaryText} numberOfLines={1}>
                  {vendorNameById[item.restaurantId] || item.restaurantId}
                </Text>
                <Text style={styles.secondaryText} numberOfLines={1}>
                  Customer: {item.customerDisplayName || item.customerId}
                </Text>
              </View>
              {getConversationActivityTs(item) >
                (lastSeenByConversationId[item.conversationId] ?? 0) +
                  SKEW_MS &&
                (lastSenderRoleByConversationId[item.conversationId] || "")
                  .toLowerCase()
                  .trim() !== "customer" && <View style={styles.unreadDot} />}
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
  headerBtn: {
    width: 40,
    height: 40,
    alignItems: "center",
    justifyContent: "center",
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
