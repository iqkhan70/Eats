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
import { useRouter } from "expo-router";
import { authService } from "../services/auth";
import { api } from "../services/api";
import AppHeader from "../components/AppHeader";
import {
  getMyVendorConversations,
  type VendorConversation,
} from "../services/vendorChat";

export default function MessagesScreen() {
  const router = useRouter();

  const [loading, setLoading] = useState(true);
  const [conversations, setConversations] = useState<VendorConversation[]>([]);
  const [vendorNameById, setVendorNameById] = useState<Record<string, string>>(
    {},
  );

  const restaurantIds = useMemo(() => {
    const ids = new Set<string>();
    for (const c of conversations) {
      if (c.restaurantId) ids.add(c.restaurantId);
    }
    return Array.from(ids);
  }, [conversations]);

  const sortedConversations = useMemo(() => {
    const list = [...conversations];
    const ts = (iso?: string) => {
      if (!iso) return 0;
      const t = new Date(iso).getTime();
      return Number.isFinite(t) ? t : 0;
    };
    return list.sort((a, b) => {
      const aTs = Math.max(ts(a.lastMessageAt), ts(a.updatedAt));
      const bTs = Math.max(ts(b.lastMessageAt), ts(b.updatedAt));
      return bTs - aTs;
    });
  }, [conversations]);

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
      const list = await getMyVendorConversations();
      setConversations(list);
    } catch (e) {
      console.error("Load messages inbox:", e);
      setConversations([]);
    } finally {
      setLoading(false);
    }
  }, [router]);

  useEffect(() => {
    load();
  }, [load]);

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
              onPress={() =>
                router.push({
                  pathname: "/messages/[conversationId]",
                  params: {
                    conversationId: item.conversationId,
                    restaurantId: item.restaurantId,
                    vendorName: vendorNameById[item.restaurantId] || "",
                  },
                } as any)
              }
            >
              <View style={styles.rowLeft}>
                <Ionicons name="storefront-outline" size={28} color="#6200ee" />
              </View>
              <View style={styles.rowBody}>
                <Text style={styles.primaryText} numberOfLines={1}>
                  {vendorNameById[item.restaurantId] ||
                    item.restaurantId ||
                    "Vendor"}
                </Text>
                <Text style={styles.secondaryText} numberOfLines={1}>
                  Tap to open chat
                </Text>
              </View>
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
});
