import React, { useCallback, useEffect, useRef, useState } from "react";
import {
  ActivityIndicator,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  TouchableOpacity,
  View,
  Modal,
  Alert,
  KeyboardAvoidingView,
  Platform,
} from "react-native";
import { Ionicons } from "@expo/vector-icons";
import { useRouter } from "expo-router";
import {
  connectVendorChatHub,
  disconnectVendorChatHub,
  getVendorConversationMessages,
  joinVendorConversation,
  leaveVendorConversation,
  sendVendorMessage,
  type VendorChatMessage,
} from "../services/vendorChat";
import { createPaymentRequestMetadata } from "../types/paymentRequest";

function isAuthError(message: string): boolean {
  const lower = (message || "").toLowerCase();
  return (
    lower.includes("sign in") ||
    lower.includes("unauthorized") ||
    lower.includes("401") ||
    lower.includes("expired") ||
    lower.includes("token")
  );
}

function getSenderLabel(msg: VendorChatMessage): string {
  if (msg.senderDisplayName?.trim()) return msg.senderDisplayName.trim();
  switch (msg.senderRole) {
    case "Customer":
      return "You";
    case "Vendor":
      return "Vendor";
    case "Admin":
      return "Admin";
    default:
      return msg.senderRole || "Unknown";
  }
}

function formatTime(iso: string): string {
  try {
    const d = new Date(iso);
    return d.toLocaleTimeString("en-US", { hour: "numeric", minute: "2-digit" });
  } catch {
    return "";
  }
}

export default function VendorChat({
  conversationId,
}: {
  conversationId: string;
}) {
  const router = useRouter();
  const [messages, setMessages] = useState<VendorChatMessage[]>([]);
  const [loading, setLoading] = useState(true);
  const [connected, setConnected] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [input, setInput] = useState("");
  const [sending, setSending] = useState(false);
  const [showPaymentModal, setShowPaymentModal] = useState(false);
  const [paymentAmount, setPaymentAmount] = useState("");
  const [paymentDescription, setPaymentDescription] = useState("");
  const [sendingPaymentRequest, setSendingPaymentRequest] = useState(false);

  const scrollRef = useRef<ScrollView | null>(null);
  const hasRedirectedRef = useRef(false);

  const loadMessages = useCallback(async () => {
    try {
      setLoading(true);
      const list = await getVendorConversationMessages(conversationId);
      setMessages(list);
    } catch (e: any) {
      if (e?.response?.status === 401 && !hasRedirectedRef.current) {
        hasRedirectedRef.current = true;
        router.replace("/login");
        return;
      }
      console.error("Load vendor chat messages:", e);
    } finally {
      setLoading(false);
    }
  }, [conversationId, router]);

  useEffect(() => {
    loadMessages();
  }, [loadMessages]);

  const scrollToBottom = useCallback((animated = true) => {
    requestAnimationFrame(() => {
      scrollRef.current?.scrollToEnd({ animated });
    });
  }, []);

  useEffect(() => {
    const t = setTimeout(() => scrollToBottom(true), 50);
    return () => clearTimeout(t);
  }, [messages.length, scrollToBottom]);

  useEffect(() => {
    let mounted = true;

    const onMessage = (msg: VendorChatMessage) => {
      if (mounted && msg.conversationId === conversationId) {
        setMessages((prev) => [...prev, msg]);
      }
    };

    const onError = (err: string) => {
      if (!mounted) return;
      if (isAuthError(err) && !hasRedirectedRef.current) {
        hasRedirectedRef.current = true;
        router.replace("/login");
        return;
      }
      setError(err);
      setConnected(false);
    };

    const onState = (isConnected: boolean) => {
      if (mounted) setConnected(isConnected);
    };

    connectVendorChatHub(onMessage, onError, onState)
      .then((ok) => {
        if (ok && mounted) {
          setError(null);
          joinVendorConversation(conversationId).catch((e) => {
            const msg = String(e?.message ?? e);
            if (isAuthError(msg) && !hasRedirectedRef.current) {
              hasRedirectedRef.current = true;
              router.replace("/login");
              return;
            }
            console.warn("Vendor chat join:", msg);
          });
        }
      })
      .catch((e) => {
        if (mounted) console.warn("Vendor chat connect:", e?.message ?? e);
      });

    return () => {
      mounted = false;
      leaveVendorConversation(conversationId).catch(() => {});
      disconnectVendorChatHub().catch(() => {});
    };
  }, [conversationId, router]);

  const handleSendPaymentRequest = async () => {
    if (!paymentAmount.trim()) {
      Alert.alert("Error", "Please enter an amount");
      return;
    }

    const amount = parseFloat(paymentAmount);
    if (isNaN(amount) || amount <= 0) {
      Alert.alert("Error", "Please enter a valid amount");
      return;
    }

    setSendingPaymentRequest(true);
    try {
      const metadata = createPaymentRequestMetadata(amount, paymentDescription);
      const message = `Payment request: $${amount.toFixed(2)}${paymentDescription ? ` for ${paymentDescription}` : ""}`;
      
      await sendVendorMessage(
        conversationId,
        message,
        JSON.stringify(metadata)
      );

      // Reset form
      setPaymentAmount("");
      setPaymentDescription("");
      setShowPaymentModal(false);
      scrollToBottom(true);
    } catch (e: any) {
      const msg = e?.message || "Failed to send payment request";
      Alert.alert("Error", msg);
    } finally {
      setSendingPaymentRequest(false);
    }
  };

  const handleSend = async () => {
    const text = input.trim();
    if (!text || !connected || sending) return;

    setSending(true);
    try {
      await sendVendorMessage(conversationId, text);
      setInput("");
      scrollToBottom(true);
    } catch (e: any) {
      const msg = e?.message || "Failed to send";
      if (isAuthError(msg) && !hasRedirectedRef.current) {
        hasRedirectedRef.current = true;
        router.replace("/login");
        return;
      }
      setError(msg);
    } finally {
      setSending(false);
    }
  };

  return (
    <View style={styles.card}>
      <View style={styles.header}>
        <Ionicons name="chatbubbles" size={22} color="#333" />
        <Text style={styles.title}>Vendor Chat</Text>
        <View style={styles.badge}>
          <View
            style={[
              styles.dot,
              connected ? styles.dotConnected : styles.dotDisconnected,
            ]}
          />
          <Text style={styles.badgeText}>
            {connected ? "Connected" : "Disconnected"}
          </Text>
        </View>
      </View>

      {error ? (
        <Text style={styles.errorText} numberOfLines={2}>
          {error}
        </Text>
      ) : null}

      <View style={styles.messagesContainer}>
        {loading ? (
          <View style={styles.loadingBox}>
            <ActivityIndicator size="small" color="#6200ee" />
            <Text style={styles.loadingText}>Loading messages...</Text>
          </View>
        ) : messages.length === 0 ? (
          <View style={styles.emptyBox}>
            <Ionicons name="chatbubble-outline" size={40} color="#999" />
            <Text style={styles.emptyText}>
              No messages yet. Ask the vendor a question!
            </Text>
          </View>
        ) : (
          <ScrollView
            ref={scrollRef}
            style={styles.messagesScroll}
            contentContainerStyle={styles.messagesContent}
            keyboardShouldPersistTaps="handled"
            showsVerticalScrollIndicator
            onContentSizeChange={() => scrollToBottom(false)}
          >
            {messages.map((msg) => {
              const isYou = msg.senderRole === "Customer";
              return (
                <View
                  key={msg.messageId || `${msg.sentAt}-${msg.message?.slice(0, 10)}`}
                  style={[
                    styles.messageBubble,
                    isYou ? styles.messageBubbleOwn : styles.messageBubbleOther,
                  ]}
                >
                  <View style={styles.messageMeta}>
                    <Text style={styles.messageSender}>{getSenderLabel(msg)}</Text>
                    <Text style={styles.messageTime}>{formatTime(msg.sentAt)}</Text>
                  </View>
                  <Text style={styles.messageBody}>{msg.message}</Text>
                </View>
              );
            })}
          </ScrollView>
        )}
      </View>

      <View style={styles.inputRow}>
        <TouchableOpacity
          style={[styles.paymentButton, !connected && styles.sendButtonDisabled]}
          onPress={() => setShowPaymentModal(true)}
          disabled={!connected}
        >
          <Ionicons name="cash" size={20} color="#fff" />
        </TouchableOpacity>

        <TextInput
          style={styles.input}
          placeholder={
            connected ? "Type your message..." : "Sign in and connect to chat..."
          }
          placeholderTextColor="#999"
          value={input}
          onChangeText={setInput}
          editable={true}
          multiline
          maxLength={2000}
          onFocus={() => setTimeout(() => scrollToBottom(true), 60)}
        />
        <TouchableOpacity
          style={[
            styles.sendButton,
            (!connected || !input.trim() || sending) && styles.sendButtonDisabled,
          ]}
          onPress={handleSend}
          disabled={!connected || !input.trim() || sending}
        >
          {sending ? (
            <ActivityIndicator size="small" color="#fff" />
          ) : (
            <Ionicons name="send" size={20} color="#fff" />
          )}
        </TouchableOpacity>
      </View>

      {/* Payment Request Modal */}
      <Modal
        visible={showPaymentModal}
        transparent
        animationType="slide"
        onRequestClose={() => setShowPaymentModal(false)}
      >
        <KeyboardAvoidingView
          behavior={Platform.OS === "ios" ? "padding" : "height"}
          style={styles.modalOverlay}
        >
          <ScrollView
            style={styles.modalScrollView}
            contentContainerStyle={styles.modalScrollContent}
            keyboardShouldPersistTaps="handled"
            showsVerticalScrollIndicator={true}
          >
            <View style={styles.modalContent}>
              <View style={styles.modalHeader}>
                <Text style={styles.modalTitle}>Request Custom Payment</Text>
                <TouchableOpacity onPress={() => setShowPaymentModal(false)}>
                  <Ionicons name="close" size={24} color="#333" />
                </TouchableOpacity>
              </View>

              <Text style={styles.modalLabel}>Amount ($)</Text>
              <TextInput
                style={styles.modalInput}
                placeholder="e.g., 25.00"
                keyboardType="decimal-pad"
                value={paymentAmount}
                onChangeText={setPaymentAmount}
                editable={!sendingPaymentRequest}
              />

              <Text style={styles.modalLabel}>Description (optional)</Text>
              <TextInput
                style={[styles.modalInput, styles.modalInputLarge]}
                placeholder="e.g., Custom dessert platter"
                value={paymentDescription}
                onChangeText={setPaymentDescription}
                multiline
                maxLength={200}
                editable={!sendingPaymentRequest}
              />

              <TouchableOpacity
                style={[
                  styles.modalButton,
                  (!paymentAmount.trim() || sendingPaymentRequest) && styles.modalButtonDisabled,
                ]}
                onPress={handleSendPaymentRequest}
                disabled={!paymentAmount.trim() || sendingPaymentRequest}
              >
                {sendingPaymentRequest ? (
                  <ActivityIndicator size="small" color="#fff" />
                ) : (
                  <Text style={styles.modalButtonText}>Send Payment Request</Text>
                )}
              </TouchableOpacity>
            </View>
          </ScrollView>
        </KeyboardAvoidingView>
      </Modal>
    </View>
  );
}

const styles = StyleSheet.create({
  card: {
    flex: 1,
    backgroundColor: "#fff",
    borderRadius: 12,
    padding: 16,
    shadowColor: "#000",
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.1,
    shadowRadius: 4,
    elevation: 3,
  },
  header: { flexDirection: "row", alignItems: "center", marginBottom: 12 },
  title: { fontSize: 18, fontWeight: "600", color: "#333", marginLeft: 8 },
  badge: { flexDirection: "row", alignItems: "center", marginLeft: "auto" },
  dot: { width: 8, height: 8, borderRadius: 4, marginRight: 6 },
  dotConnected: { backgroundColor: "#28a745" },
  dotDisconnected: { backgroundColor: "#6c757d" },
  badgeText: { fontSize: 12, color: "#666" },
  errorText: { fontSize: 12, color: "#dc3545", marginBottom: 8 },
  loadingBox: {
    flexDirection: "row",
    alignItems: "center",
    justifyContent: "center",
    padding: 24,
    gap: 8,
  },
  loadingText: { fontSize: 14, color: "#666" },
  emptyBox: { alignItems: "center", padding: 24 },
  emptyText: { fontSize: 14, color: "#999", marginTop: 8, textAlign: "center" },
  messagesContainer: { flex: 1, minHeight: 0 },
  messagesScroll: { flex: 1 },
  messagesContent: { paddingVertical: 8 },
  messageBubble: {
    padding: 10,
    borderRadius: 10,
    marginBottom: 8,
    maxWidth: "85%",
  },
  messageBubbleOwn: { alignSelf: "flex-end", backgroundColor: "#e8e0f7" },
  messageBubbleOther: { alignSelf: "flex-start", backgroundColor: "#f0f0f0" },
  messageMeta: { flexDirection: "row", alignItems: "center", marginBottom: 4 },
  messageSender: { fontSize: 12, fontWeight: "600", color: "#555" },
  messageTime: { fontSize: 11, color: "#888", marginLeft: 8 },
  messageBody: { fontSize: 14, color: "#333" },

  inputRow: { flexDirection: "row", alignItems: "flex-end", marginTop: 12, gap: 8, paddingHorizontal: 12 },
  paymentButton: {
    width: 44,
    height: 44,
    borderRadius: 8,
    backgroundColor: "#28a745",
    alignItems: "center",
    justifyContent: "center",
  },
  input: {
    flex: 1,
    borderWidth: 1,
    borderColor: "#ddd",
    borderRadius: 8,
    paddingHorizontal: 12,
    paddingVertical: 10,
    fontSize: 14,
    color: "#333",
    minHeight: 44,
    maxHeight: 100,
  },
  sendButton: {
    width: 44,
    height: 44,
    borderRadius: 8,
    backgroundColor: "#6200ee",
    alignItems: "center",
    justifyContent: "center",
  },
  sendButtonDisabled: { backgroundColor: "#ccc" },

  // Modal styles
  modalOverlay: {
    flex: 1,
    backgroundColor: "rgba(0, 0, 0, 0.5)",
    justifyContent: "flex-end",
  },
  modalScrollView: {
    flex: 1,
  },
  modalScrollContent: {
    flexGrow: 1,
    justifyContent: "flex-end",
    paddingBottom: Platform.OS === "ios" ? 40 : 20,
  },
  modalContent: {
    backgroundColor: "#fff",
    borderTopLeftRadius: 12,
    borderTopRightRadius: 12,
    padding: 16,
    paddingBottom: 24,
    minHeight: "auto",
  },
  modalHeader: {
    flexDirection: "row",
    justifyContent: "space-between",
    alignItems: "center",
    marginBottom: 16,
  },
  modalTitle: {
    fontSize: 18,
    fontWeight: "700",
    color: "#333",
  },
  modalLabel: {
    fontSize: 13,
    fontWeight: "600",
    color: "#555",
    marginBottom: 6,
  },
  modalInput: {
    borderWidth: 1,
    borderColor: "#ddd",
    borderRadius: 8,
    paddingHorizontal: 12,
    paddingVertical: 10,
    fontSize: 14,
    color: "#333",
    marginBottom: 14,
  },
  modalInputLarge: {
    minHeight: 80,
    textAlignVertical: "top",
  },
  modalButton: {
    backgroundColor: "#0097a7",
    borderRadius: 8,
    paddingVertical: 12,
    alignItems: "center",
    marginTop: 8,
  },
  modalButtonDisabled: { backgroundColor: "#ccc" },
  modalButtonText: {
    fontSize: 14,
    fontWeight: "600",
    color: "#fff",
  },
});

