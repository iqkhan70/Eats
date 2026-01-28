/**
 * Chat service: REST API for message history + SignalR for real-time.
 * Messages are fetched via Mobile BFF; SignalR connects directly to ChatService.
 */
import AsyncStorage from '@react-native-async-storage/async-storage';
import * as SignalR from '@microsoft/signalr';
import { api } from './api';
import { APP_CONFIG } from '../config/app.config';

export interface ChatMessage {
  messageId: string;
  orderId: string;
  senderId: string;
  senderRole: string;
  message: string;
  sentAt: string;
}

let hubConnection: SignalR.HubConnection | null = null;

export async function getOrderChatMessages(orderId: string): Promise<ChatMessage[]> {
  const { data } = await api.get<ChatMessage[]>(`/MobileBff/orders/${orderId}/chat/messages`);
  return Array.isArray(data) ? data : [];
}

export async function getOrderChatUnreadCount(orderId: string): Promise<number> {
  try {
    const { data } = await api.get<{ unreadCount: number }>(`/MobileBff/orders/${orderId}/chat/unread-count`);
    return data?.unreadCount ?? 0;
  } catch {
    return 0;
  }
}

export type OnMessageReceived = (message: ChatMessage) => void;
export type OnError = (error: string) => void;
export type OnConnectionStateChanged = (connected: boolean) => void;

export async function connectChatHub(
  onMessage: OnMessageReceived,
  onError: OnError,
  onConnectionStateChanged?: OnConnectionStateChanged
): Promise<boolean> {
  if (hubConnection?.state === SignalR.HubConnectionState.Connected) {
    onConnectionStateChanged?.(true);
    return true;
  }

  const token = await AsyncStorage.getItem('access_token');
  if (!token) {
    onError('Sign in to chat.');
    return false;
  }

  const hubUrl = APP_CONFIG.CHAT_HUB_URL;
  hubConnection = new SignalR.HubConnectionBuilder()
    .withUrl(hubUrl, {
      accessTokenFactory: async () => (await AsyncStorage.getItem('access_token')) ?? '',
      // Use LongPolling on React Native; WebSockets often fail negotiation on device/simulator
      transport: SignalR.HttpTransportType.LongPolling,
    })
    .withAutomaticReconnect()
    .build();

  hubConnection.on('ReceiveMessage', (payload: unknown) => {
    try {
      const msg = payload as ChatMessage;
      if (msg?.message != null) {
        onMessage({
          messageId: msg.messageId ?? '',
          orderId: msg.orderId ?? '',
          senderId: msg.senderId ?? '',
          senderRole: msg.senderRole ?? '',
          message: msg.message ?? '',
          sentAt: msg.sentAt ?? new Date().toISOString(),
        });
      }
    } catch (e) {
      onError(String(e));
    }
  });

  hubConnection.on('Error', (err: string) => {
    onError(err || 'Chat error');
  });

  hubConnection.onreconnected(() => {
    onConnectionStateChanged?.(true);
  });

  hubConnection.onclose((err) => {
    onConnectionStateChanged?.(false);
    if (err) {
      onError(err.message || 'Connection closed');
    }
  });

  try {
    await hubConnection.start();
    onConnectionStateChanged?.(true);
    return true;
  } catch (e: any) {
    onError(e?.message || 'Failed to connect to chat');
    return false;
  }
}

export function isChatConnected(): boolean {
  return hubConnection?.state === SignalR.HubConnectionState.Connected;
}

export async function joinOrderChat(orderId: string): Promise<void> {
  if (hubConnection?.state === SignalR.HubConnectionState.Connected) {
    await hubConnection.invoke('JoinOrderChat', orderId);
  }
}

export async function leaveOrderChat(orderId: string): Promise<void> {
  if (hubConnection?.state === SignalR.HubConnectionState.Connected) {
    await hubConnection.invoke('LeaveOrderChat', orderId);
  }
}

export async function sendChatMessage(orderId: string, message: string): Promise<void> {
  if (hubConnection?.state !== SignalR.HubConnectionState.Connected) {
    throw new Error('Not connected to chat');
  }
  await hubConnection.invoke('SendMessage', orderId, message);
}

export async function markChatMessagesRead(orderId: string): Promise<void> {
  if (hubConnection?.state === SignalR.HubConnectionState.Connected) {
    await hubConnection.invoke('MarkMessagesAsRead', orderId);
  }
}

export async function disconnectChatHub(): Promise<void> {
  if (hubConnection) {
    try {
      await hubConnection.stop();
    } catch (_) {}
    hubConnection = null;
  }
}
