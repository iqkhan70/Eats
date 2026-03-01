// components/OrderChat.tsx (full updated component with keyboard-safe layout)
import React, { useState, useEffect, useCallback, useRef } from "react";
import {
  View,
  Text,
  StyleSheet,
  TextInput,
  TouchableOpacity,
  ActivityIndicator,
  ScrollView,
} from "react-native";
import { useRouter } from "expo-router";
import { Ionicons } from "@expo/vector-icons";
import {
  getOrderChatMessages,
  connectChatHub,
  joinOrderChat,
  leaveOrderChat,
  sendChatMessage,
  markChatMessagesRead,
  disconnectChatHub,
  type ChatMessage,
} from "../services/chat";
import { isPaymentRequest, parsePaymentRequest, type PaymentRequestMetadata } from "../types/paymentRequest";

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

function getSenderLabel(msg: ChatMessage): string {
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
    return d.toLocaleTimeString("en-US", {
      hour: "numeric",
      minute: "2-digit",
    });
  } catch {
    return "";
  }
}

interface OrderChatProps {
  orderId: string;
  /** When true, chat uses full height (dedicated chat screen). */
  fullScreen?: boolean;
}

export default function OrderChat({ orderId, fullScreen }: OrderChatProps) {
  const router = useRouter();
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [loading, setLoading] = useState(true);
  const [connected, setConnected] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [input, setInput] = useState("");
  const [sending, setSending] = useState(false);

  const scrollRef = useRef<ScrollView | null>(null);
  const hasRedirectedRef = useRef(false);

  const loadMessages = useCallback(async () => {
    try {
      setLoading(true);
      const list = await getOrderChatMessages(orderId);
      setMessages(list);
    } catch (e: any) {
      if (e?.response?.status === 401 && !hasRedirectedRef.current) {
        hasRedirectedRef.current = true;
        router.replace("/login");
        return;
      }
      console.error("Load chat messages:", e);
    } finally {
      setLoading(false);
    }
  }, [orderId, router]);

  useEffect(() => {
    loadMessages();
  }, [loadMessages]);

  // Keep scrolled to bottom
  const scrollToBottom = useCallback((animated = true) => {
    requestAnimationFrame(() => {
      scrollRef.current?.scrollToEnd({ animated });
    });
  }, []);

  useEffect(() => {
    // scroll on new messages / first load
    const t = setTimeout(() => scrollToBottom(true), 50);
    return () => clearTimeout(t);
  }, [messages.length, scrollToBottom]);

  useEffect(() => {
    let mounted = true;

    const onMessage = (msg: ChatMessage) => {
      if (mounted && msg.orderId === orderId) {
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

    connectChatHub(onMessage, onError, onState)
      .then((ok) => {
        if (ok && mounted) {
          setError(null);
          joinOrderChat(orderId)
            .then(() => markChatMessagesRead(orderId))
            .catch((e) => {
              if (!mounted) return;
              const msg = String(e?.message ?? e);
              if (isAuthError(msg) && !hasRedirectedRef.current) {
                hasRedirectedRef.current = true;
                router.replace("/login");
                return;
              }
              console.warn("Chat join/read:", msg);
            });
        }
      })
      .catch((e) => {
        if (mounted) console.warn("Chat connect:", e?.message ?? e);
      });

    return () => {
      mounted = false;
      leaveOrderChat(orderId).catch(() => {});
      disconnectChatHub().catch(() => {});
    };
  }, [orderId, router]);

  const handleAcceptPayment = (paymentRequest: PaymentRequestMetadata, messageOrderId: string) => {
    // Navigate to cart to create custom order
    router.push({
      pathname: "/cart",
      params: {
        customOrderAmount: paymentRequest.amount.toString(),
        customOrderDescription: paymentRequest.description || "Custom Order",
      },
    });
  };

  const handleSend = async () => {
    const text = input.trim();
    if (!text || !connected || sending) return;

    setSending(true);
    try {
      await sendChatMessage(orderId, text);
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
    <View style={[styles.card, fullScreen && styles.cardFullScreen]}>
      <View style={styles.header}>
        <Ionicons name="chatbubbles" size={22} color="#333" />
        <Text style={styles.title}>Order Chat</Text>
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

      {/* Messages area: flex so it shrinks when keyboard opens */}
      <View
        style={[
          styles.messagesContainer,
          fullScreen && styles.messagesContainerFullScreen,
        ]}
      >
        {loading ? (
          <View style={styles.loadingBox}>
            <ActivityIndicator size="small" color="#6200ee" />
            <Text style={styles.loadingText}>Loading messages...</Text>
          </View>
        ) : messages.length === 0 ? (
          <View style={styles.emptyBox}>
            <Ionicons name="chatbubble-outline" size={40} color="#999" />
            <Text style={styles.emptyText}>
              No messages yet. Start the conversation!
            </Text>
          </View>
        ) : (
          <ScrollView
            ref={scrollRef}
            style={styles.messagesScroll}
            contentContainerStyle={styles.messagesContent}
            keyboardShouldPersistTaps="always"
            keyboardDismissMode="none"
            showsVerticalScrollIndicator
            onContentSizeChange={() => scrollToBottom(false)}
          >
            {messages.map((msg) => {
              const isYou = msg.senderRole === "Customer";
              const paymentRequest = msg.metadataJson && isPaymentRequest(msg.metadataJson) 
                ? parsePaymentRequest(msg.metadataJson) 
                : null;
              
              return (
                <View
                  key={
                    msg.messageId ||
                    `${msg.sentAt}-${msg.message?.slice(0, 10)}`
                  }
                  style={[
                    styles.messageBubble,
                    isYou ? styles.messageBubbleOwn : styles.messageBubbleOther,
                  ]}
                >
                  <View style={styles.messageMeta}>
                    <Text style={styles.messageSender}>
                      {getSenderLabel(msg)}
                    </Text>
                    <Text style={styles.messageTime}>
                      {formatTime(msg.sentAt)}
                    </Text>
                  </View>
                  <Text style={styles.messageBody}>{msg.message}</Text>
                  
                  {/* Payment Request Rendering */}
                  {paymentRequest && !isYou && paymentRequest.status === "pending" && (
                    <View style={styles.paymentRequestContainer}>
                      <View style={styles.paymentRequestHeader}>
                        <Ionicons name="cash" size={20} color="#0097a7" />
                        <Text style={styles.paymentRequestTitle}>Payment Request</Text>
                      </View>
                      {paymentRequest.description && (
                        <Text style={styles.paymentRequestDescription}>
                          {paymentRequest.description}
                        </Text>
                      )}
                      <Text style={styles.paymentRequestAmount}>
                        ${paymentRequest.amount.toFixed(2)}
                      </Text>
                      <TouchableOpacity
                        style={styles.acceptPaymentButton}
                        onPress={() => handleAcceptPayment(paymentRequest, msg.orderId)}
                        activeOpacity={0.7}
                      >
                        <Ionicons name="checkmark-circle" size={18} color="#fff" />
                        <Text style={styles.acceptPaymentButtonText}>Accept Payment</Text>
                      </TouchableOpacity>
                    </View>
                  )}
                </View>
              );
            })}
          </ScrollView>
        )}
      </View>

      {/* Input row: stays above keyboard because screen wraps with KeyboardAvoidingView */}
      <View style={styles.inputRow}>
        <TextInput
          style={styles.input}
          placeholder={
            connected
              ? "Type your message..."
              : "Sign in and connect to chat..."
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
            (!connected || !input.trim() || sending) &&
              styles.sendButtonDisabled,
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
    </View>
  );
}

const styles = StyleSheet.create({
  card: {
    backgroundColor: "#fff",
    borderRadius: 12,
    padding: 16,
    marginBottom: 16,
    marginHorizontal: 12,
    shadowColor: "#000",
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.1,
    shadowRadius: 4,
    elevation: 3,
  },
  cardFullScreen: { flex: 1, marginBottom: 0, marginHorizontal: 0 },

  header: { flexDirection: "row", alignItems: "center", marginBottom: 12, paddingHorizontal: 4 },
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

  messagesContainer: { maxHeight: 240, minHeight: 120 },
  messagesContainerFullScreen: { flex: 1, maxHeight: undefined, minHeight: 0 },

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

  paymentRequestContainer: {
    marginTop: 10,
    padding: 12,
    backgroundColor: "#e3f2fd",
    borderRadius: 8,
    borderLeftWidth: 4,
    borderLeftColor: "#0097a7",
  },
  paymentRequestHeader: {
    flexDirection: "row",
    alignItems: "center",
    marginBottom: 8,
    gap: 8,
  },
  paymentRequestTitle: {
    fontSize: 13,
    fontWeight: "700",
    color: "#0097a7",
  },
  paymentRequestDescription: {
    fontSize: 12,
    color: "#555",
    marginBottom: 6,
  },
  paymentRequestAmount: {
    fontSize: 16,
    fontWeight: "700",
    color: "#0097a7",
    marginBottom: 10,
  },
  acceptPaymentButton: {
    flexDirection: "row",
    alignItems: "center",
    justifyContent: "center",
    backgroundColor: "#0097a7",
    borderRadius: 6,
    paddingVertical: 8,
    gap: 6,
  },
  acceptPaymentButtonText: {
    fontSize: 13,
    fontWeight: "600",
    color: "#fff",
  },

  inputRow: {
    flexDirection: "row",
    alignItems: "flex-end",
    marginTop: 12,
    gap: 8,
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
});
