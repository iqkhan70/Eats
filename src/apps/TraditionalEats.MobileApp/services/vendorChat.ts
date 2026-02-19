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
  if (vendorHubConnection?.state === SignalR.HubConnectionState.Connected) {
    onConnectionStateChanged?.(true);
    return true;
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
    .build();

  vendorHubConnection.on("ReceiveVendorMessage", (payload: unknown) => {
    try {
      const msg = payload as VendorChatMessage;
      if (msg?.message != null) {
        onMessage({
          messageId: msg.messageId ?? "",
          conversationId: msg.conversationId ?? "",
          senderId: msg.senderId ?? "",
          senderRole: msg.senderRole ?? "",
          senderDisplayName: (msg as { senderDisplayName?: string })
            .senderDisplayName,
          message: msg.message ?? "",
          sentAt: msg.sentAt ?? new Date().toISOString(),
        });
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

