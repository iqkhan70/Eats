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
import { useLocalSearchParams, useRouter } from "expo-router";
import { authService } from "../../services/auth";
import { api } from "../../services/api";
import AppHeader from "../../components/AppHeader";
import {
  getVendorInbox,
  type VendorConversation,
} from "../../services/vendorChat";

export default function VendorMessagesScreen() {
  const router = useRouter();
  const params = useLocalSearchParams<{ restaurantId?: string }>();
  const restaurantIdParam =
    typeof params.restaurantId === "string" ? params.restaurantId : "";

  const [loading, setLoading] = useState(true);
  const [conversations, setConversations] = useState<VendorConversation[]>([]);
  const [vendorNameById, setVendorNameById] = useState<Record<string, string>>(
    {},
  );

  const [scopedRestaurantId, setScopedRestaurantId] =
    useState<string>(restaurantIdParam);

  useEffect(() => {
    setScopedRestaurantId(restaurantIdParam);
  }, [restaurantIdParam]);

  const scopedConversations = useMemo(() => {
    if (!scopedRestaurantId) return conversations;
    return conversations.filter((c) => c.restaurantId === scopedRestaurantId);
  }, [conversations, scopedRestaurantId]);

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

  const load = useCallback(async () => {
    try {
      setLoading(true);
      const authenticated = await authService.isAuthenticated();
      if (!authenticated) {
        router.replace("/login");
        return;
      }
      const inbox = await getVendorInbox();
      setConversations(inbox);
    } catch (e) {
      console.error("Load vendor inbox:", e);
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
        right={
          <TouchableOpacity onPress={load} style={styles.backButton}>
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
          data={scopedConversations}
          keyExtractor={(c) => c.conversationId}
          contentContainerStyle={styles.list}
          renderItem={({ item }) => (
            <TouchableOpacity
              style={styles.row}
              onPress={() =>
                router.push({
                  pathname: "/vendor/messages/[conversationId]",
                  params: {
                    conversationId: item.conversationId,
                    restaurantId: item.restaurantId,
                    vendorName: vendorNameById[item.restaurantId] || "",
                  },
                } as any)
              }
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
});
