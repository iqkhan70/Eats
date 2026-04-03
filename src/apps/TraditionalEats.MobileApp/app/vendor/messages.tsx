import React, {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
} from "react";
import {
  ActivityIndicator,
  Alert,
  FlatList,
  KeyboardAvoidingView,
  Modal,
  Platform,
  SafeAreaView,
  StyleSheet,
  Text,
  TextInput,
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
  broadcastVendorMessage,
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

  const [broadcastOpen, setBroadcastOpen] = useState(false);
  const [broadcastText, setBroadcastText] = useState("");
  const [broadcasting, setBroadcasting] = useState(false);
  /** Restaurants you own or staff (same list as vendor dashboard). Used to hide broadcast for customer-only threads. */
  const [managedRestaurantIds, setManagedRestaurantIds] = useState<string[]>(
    [],
  );
  const [isAdminUser, setIsAdminUser] = useState(false);

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

  /** One restaurant in inbox → allow deal blast without explicit filter. */
  const singleRestaurantFromInbox = useMemo(() => {
    const ids = [...new Set(conversations.map((c) => c.restaurantId))];
    return ids.length === 1 ? ids[0] : "";
  }, [conversations]);

  const broadcastRestaurantId =
    scopedRestaurantId || singleRestaurantFromInbox;

  const canShowBroadcast = useMemo(() => {
    if (!broadcastRestaurantId) return false;
    if (isAdminUser) return true;
    const bid = broadcastRestaurantId.toLowerCase();
    return managedRestaurantIds.some((id) => id.toLowerCase() === bid);
  }, [broadcastRestaurantId, managedRestaurantIds, isAdminUser]);

  const fetchManagedRestaurants = useCallback(async () => {
    try {
      const [admin, res] = await Promise.all([
        authService.isAdmin(),
        api.get<unknown[]>("/MobileBff/vendor/my-restaurants"),
      ]);
      setIsAdminUser(!!admin);
      const rows = Array.isArray(res.data) ? res.data : [];
      const ids: string[] = [];
      for (const row of rows) {
        if (row && typeof row === "object") {
          const o = row as Record<string, unknown>;
          const id = o.restaurantId ?? o.RestaurantId;
          if (typeof id === "string" && id.length > 0) ids.push(id);
        }
      }
      setManagedRestaurantIds(ids);
    } catch {
      setIsAdminUser(false);
      setManagedRestaurantIds([]);
    }
  }, []);

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

  const triedVendorIdsRef = useRef<Set<string>>(new Set());

  const loadVendorNames = useCallback(
    async (ids: string[]) => {
      const missing = ids.filter(
        (id) => !vendorNameById[id] && !triedVendorIdsRef.current.has(id),
      );
      if (missing.length === 0) return;

      for (const id of missing) triedVendorIdsRef.current.add(id);

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
            return { restaurantId, name: "Unknown" };
          }
        }),
      );

      setVendorNameById((prev) => {
        const next = { ...prev };
        for (const r of results) {
          next[r.restaurantId] = r.name || "Unknown";
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
        await fetchManagedRestaurants();
      } catch (e) {
        console.error("Load vendor inbox:", e);
        if (!silent) setConversations([]);
      } finally {
        if (!silent) setLoading(false);
        else setLoading((prev) => (prev ? false : prev));
        loadingRef.current = false;
      }
    },
    [fetchManagedRestaurants, loadLastSenderRoles, router],
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

      {canShowBroadcast && (
        <View style={styles.dealBar}>
          <TouchableOpacity
            onPress={() => setBroadcastOpen(true)}
            style={styles.broadcastPill}
            accessibilityLabel="Broadcast a deal to customers who have chatted"
          >
            <Ionicons name="megaphone-outline" size={16} color="#1565c0" />
            <Text style={styles.broadcastPillText}>Broadcast deal</Text>
          </TouchableOpacity>
          <Text style={styles.dealBarHint} numberOfLines={2}>
            Sends one message to every chat for{" "}
            {vendorNameById[broadcastRestaurantId] || "this vendor"}.
          </Text>
        </View>
      )}

      <Modal
        visible={broadcastOpen}
        animationType="slide"
        transparent
        onRequestClose={() => !broadcasting && setBroadcastOpen(false)}
      >
        <KeyboardAvoidingView
          style={styles.modalBackdrop}
          behavior={Platform.OS === "ios" ? "padding" : undefined}
        >
          <View style={styles.modalCard}>
            <Text style={styles.modalTitle}>Message all chat customers</Text>
            <Text style={styles.modalHint}>
              Same note goes to everyone who already has a thread with this
              vendor (e.g. % off items). Max 1500 characters.
            </Text>
            <TextInput
              style={styles.modalInput}
              multiline
              maxLength={1500}
              placeholder="e.g. 25% off all samosas until 8pm tonight!"
              value={broadcastText}
              onChangeText={setBroadcastText}
              editable={!broadcasting}
            />
            <View style={styles.modalActions}>
              <TouchableOpacity
                style={styles.modalBtnSecondary}
                onPress={() => !broadcasting && setBroadcastOpen(false)}
                disabled={broadcasting}
              >
                <Text style={styles.modalBtnSecondaryText}>Cancel</Text>
              </TouchableOpacity>
              <TouchableOpacity
                style={styles.modalBtnPrimary}
                onPress={async () => {
                  const t = broadcastText.trim();
                  if (!t || !broadcastRestaurantId) return;
                  try {
                    setBroadcasting(true);
                    const r = await broadcastVendorMessage(
                      broadcastRestaurantId,
                      t,
                    );
                    setBroadcastOpen(false);
                    setBroadcastText("");
                    Alert.alert(
                      "Sent",
                      r.threadCount === 0
                        ? "No customer chats yet for this vendor — nothing was sent."
                        : `Delivered to ${r.sentCount} chat${r.sentCount === 1 ? "" : "s"}.`,
                    );
                  } catch (e: any) {
                    Alert.alert(
                      "Could not send",
                      e?.response?.data?.message ||
                        e?.message ||
                        "Try again later.",
                    );
                  } finally {
                    setBroadcasting(false);
                  }
                }}
                disabled={broadcasting || !broadcastText.trim()}
              >
                <Text style={styles.modalBtnPrimaryText}>
                  {broadcasting ? "Sending…" : "Send"}
                </Text>
              </TouchableOpacity>
            </View>
          </View>
        </KeyboardAvoidingView>
      </Modal>

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
  dealBar: {
    flexDirection: "row",
    alignItems: "center",
    paddingHorizontal: 12,
    paddingVertical: 8,
    backgroundColor: "#fff",
    borderBottomWidth: 1,
    borderBottomColor: "#eee",
    gap: 10,
  },
  dealBarHint: {
    flex: 1,
    fontSize: 12,
    color: "#666",
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
  broadcastPill: {
    flexDirection: "row",
    alignItems: "center",
    gap: 4,
    paddingHorizontal: 10,
    paddingVertical: 6,
    borderRadius: 16,
    backgroundColor: "rgba(21, 101, 192, 0.12)",
    marginRight: 6,
  },
  broadcastPillText: {
    fontSize: 13,
    fontWeight: "700",
    color: "#1565c0",
  },
  modalBackdrop: {
    flex: 1,
    backgroundColor: "rgba(0,0,0,0.45)",
    justifyContent: "center",
    padding: 20,
  },
  modalCard: {
    backgroundColor: "#fff",
    borderRadius: 14,
    padding: 18,
  },
  modalTitle: {
    fontSize: 17,
    fontWeight: "700",
    color: "#222",
    marginBottom: 8,
  },
  modalHint: {
    fontSize: 13,
    color: "#666",
    marginBottom: 12,
    lineHeight: 18,
  },
  modalInput: {
    borderWidth: 1,
    borderColor: "#ddd",
    borderRadius: 10,
    padding: 12,
    minHeight: 100,
    textAlignVertical: "top",
    fontSize: 15,
    marginBottom: 16,
  },
  modalActions: {
    flexDirection: "row",
    justifyContent: "flex-end",
    gap: 12,
  },
  modalBtnSecondary: {
    paddingVertical: 10,
    paddingHorizontal: 16,
  },
  modalBtnSecondaryText: { fontSize: 16, color: "#666" },
  modalBtnPrimary: {
    backgroundColor: "#6200ee",
    paddingVertical: 10,
    paddingHorizontal: 20,
    borderRadius: 10,
  },
  modalBtnPrimaryText: { fontSize: 16, fontWeight: "600", color: "#fff" },
});
