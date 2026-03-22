/**
 * Vendor chat (generic customer<->vendor questions): REST API for history + SignalR for real-time.
 * Messages are fetched via Mobile BFF; SignalR connects directly to ChatService VendorChatHub.
 */
import AsyncStorage from "@react-native-async-storage/async-storage";
import * as SignalR from "@microsoft/signalr";
import { api } from "./api";
import { APP_CONFIG } from "../config/api.config";

export interface VendorChatMessage {
  messageId: string;
  conversationId: string;
  senderId: string;
  senderRole: string;
  senderDisplayName?: string;
  message: string;
  sentAt: string;
  /** Optional JSON metadata for extensible message types (e.g., payment requests). */
  metadataJson?: string;
}

export interface VendorConversation {
  conversationId: string;
  restaurantId: string;
  customerId: string;
  customerDisplayName?: string;
  lastMessageAt?: string;
  updatedAt?: string;
}

let vendorHubConnection: SignalR.HubConnection | null = null;
const messageListeners = new Set<OnVendorMessageReceived>();

/**
 * Add a listener for vendor messages (e.g. Restaurant Mode).
 * Returns an unsubscribe function.
 */
export function addVendorMessageListener(
  callback: OnVendorMessageReceived,
): () => void {
  messageListeners.add(callback);
  return () => messageListeners.delete(callback);
}

function broadcastMessage(msg: VendorChatMessage): void {
  for (const cb of messageListeners) {
    try {
      cb(msg);
    } catch (e) {
      console.log("Vendor message listener error:", e);
    }
  }
}

export async function createOrGetVendorConversation(
  restaurantId: string,
): Promise<VendorConversation> {
  const { data } = await api.post<VendorConversation>(
    "/MobileBff/vendor-chat/conversations",
    { restaurantId },
  );
  return data;
}

export async function getVendorConversationMessages(
  conversationId: string,
): Promise<VendorChatMessage[]> {
  const { data } = await api.get<VendorChatMessage[]>(
    `/MobileBff/vendor-chat/conversations/${conversationId}/messages`,
  );
  return Array.isArray(data) ? data : [];
}

export async function getMyVendorConversations(): Promise<VendorConversation[]> {
  const { data } = await api.get<VendorConversation[]>(
    "/MobileBff/vendor-chat/conversations/mine",
  );
  return Array.isArray(data) ? data : [];
}

export async function getVendorInbox(): Promise<VendorConversation[]> {
  const { data } = await api.get<VendorConversation[]>(
    "/MobileBff/vendor-chat/inbox",
  );
  return Array.isArray(data) ? data : [];
}

export type OnVendorMessageReceived = (message: VendorChatMessage) => void;
export type OnVendorError = (error: string) => void;
export type OnVendorConnectionStateChanged = (connected: boolean) => void;

export async function connectVendorChatHub(
  onMessage: OnVendorMessageReceived,
  onError: OnVendorError,
  onConnectionStateChanged?: OnVendorConnectionStateChanged,
): Promise<boolean> {
  messageListeners.add(onMessage);

  if (vendorHubConnection?.state === SignalR.HubConnectionState.Connected) {
    onConnectionStateChanged?.(true);
    return true;
  }

  if (vendorHubConnection) {
    try {
      await vendorHubConnection.stop();
    } catch (_) {}
    vendorHubConnection = null;
  }

  const token = await AsyncStorage.getItem("access_token");
  if (!token) {
    onError("Sign in to chat.");
    return false;
  }

  const hubUrl = APP_CONFIG.VENDOR_CHAT_HUB_URL;
  vendorHubConnection = new SignalR.HubConnectionBuilder()
    .withUrl(hubUrl, {
      accessTokenFactory: async () =>
        (await AsyncStorage.getItem("access_token")) ?? "",
      transport: SignalR.HttpTransportType.LongPolling,
    })
    .withAutomaticReconnect()
    .configureLogging(SignalR.LogLevel.None)
    .build();

  vendorHubConnection.on("ReceiveVendorMessage", (payload: unknown) => {
    try {
      const raw = payload as Record<string, unknown>;
      const msg: VendorChatMessage = {
        messageId: String(raw?.messageId ?? raw?.MessageId ?? ""),
        conversationId: String(raw?.conversationId ?? raw?.ConversationId ?? ""),
        senderId: String(raw?.senderId ?? raw?.SenderId ?? ""),
        senderRole: String(raw?.senderRole ?? raw?.SenderRole ?? ""),
        senderDisplayName: (raw?.senderDisplayName ?? raw?.SenderDisplayName) as string | undefined,
        message: String(raw?.message ?? raw?.Message ?? ""),
        sentAt: String(raw?.sentAt ?? raw?.SentAt ?? new Date().toISOString()),
        metadataJson: (raw?.metadataJson ?? raw?.MetadataJson) as string | undefined,
      };
      if (msg.message != null || msg.metadataJson != null) {
        onMessage(msg);
        broadcastMessage(msg);
      }
    } catch (e) {
      onError(String(e));
    }
  });

  vendorHubConnection.on("Error", (err: string) => {
    onError(err || "Chat error");
  });

  vendorHubConnection.onreconnected(() => {
    onConnectionStateChanged?.(true);
  });

  vendorHubConnection.onclose((err) => {
    onConnectionStateChanged?.(false);
    if (err) onError(err.message || "Connection closed");
  });

  try {
    await vendorHubConnection.start();
    onConnectionStateChanged?.(true);
    return true;
  } catch (e: any) {
    onError(e?.message || "Failed to connect to chat");
    return false;
  }
}

export async function joinVendorConversation(
  conversationId: string,
): Promise<void> {
  if (vendorHubConnection?.state === SignalR.HubConnectionState.Connected) {
    await vendorHubConnection.invoke("JoinVendorConversation", conversationId);
  }
}

export async function leaveVendorConversation(
  conversationId: string,
): Promise<void> {
  if (vendorHubConnection?.state === SignalR.HubConnectionState.Connected) {
    await vendorHubConnection.invoke("LeaveVendorConversation", conversationId);
  }
}

/**
 * Join a restaurant group to receive real-time order notifications (Restaurant Mode).
 * Call when vendor enables "Accepting orders".
 */
export async function joinVendorRestaurant(restaurantId: string): Promise<void> {
  if (vendorHubConnection?.state === SignalR.HubConnectionState.Connected) {
    await vendorHubConnection.invoke("JoinVendorRestaurant", restaurantId);
  }
}

/**
 * Leave a restaurant group (when turning off Restaurant Mode).
 */
export async function leaveVendorRestaurant(restaurantId: string): Promise<void> {
  if (vendorHubConnection?.state === SignalR.HubConnectionState.Connected) {
    await vendorHubConnection.invoke("LeaveVendorRestaurant", restaurantId);
  }
}

export async function sendVendorMessage(
  conversationId: string,
  message: string,
  metadataJson?: string,
): Promise<void> {
  if (vendorHubConnection?.state !== SignalR.HubConnectionState.Connected) {
    throw new Error("Not connected to chat");
  }
  await vendorHubConnection.invoke("SendVendorMessage", conversationId, message, metadataJson);
}

export async function disconnectVendorChatHub(): Promise<void> {
  if (vendorHubConnection) {
    try {
      await vendorHubConnection.stop();
    } catch (_) {}
    vendorHubConnection = null;
  }
}

