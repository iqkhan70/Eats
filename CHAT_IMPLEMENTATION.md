# Real-Time Chat Implementation

## Overview

Real-time chat functionality has been implemented for order-based communication between customers and vendors. The implementation uses SignalR for real-time messaging and follows the microservices architecture pattern.

## Architecture

### ChatService Microservice (Port 5012)

- **SignalR Hub**: `/chatHub` - Handles real-time WebSocket connections
- **REST API**: `/api/Chat` - Provides message history and unread counts
- **Database**: MySQL database `traditional_eats_chat` with two tables:
  - `chat_messages`: Stores all chat messages
  - `chat_participants`: Tracks participants in each order chat

### Key Features

1. **Order-Based Chat Channels**: Each order has its own dedicated chat room
2. **Role-Based Access Control**: Customers can only access their own orders, vendors can access orders for their restaurants, admins can access all orders
3. **Real-Time Messaging**: Instant message delivery via SignalR
4. **Message History**: Persistent storage of all messages
5. **Read Receipts**: Tracks read/unread status of messages
6. **Unread Count**: API endpoint to get unread message count per order

## Components Created

### Backend

1. **ChatService** (`src/services/TraditionalEats.ChatService/`)
   - `OrderChatHub.cs`: SignalR hub for real-time communication
   - `ChatService.cs`: Business logic for chat operations
   - `ChatController.cs`: REST API endpoints
   - `ChatDbContext.cs`: EF Core database context
   - Entities: `ChatMessage`, `ChatParticipant`

2. **BFF Integration**
   - Web BFF: Added chat endpoints at `/api/WebBff/orders/{orderId}/chat/*`
   - Mobile BFF: Added chat endpoints at `/api/MobileBff/orders/{orderId}/chat/*`

### Frontend - Web App

1. **ChatService** (`src/apps/TraditionalEats.WebApp/Services/ChatService.cs`)
   - SignalR client wrapper
   - Methods: `ConnectAsync`, `JoinOrderChatAsync`, `SendMessageAsync`, `GetMessagesAsync`, etc.

2. **OrderChat Component** (`src/apps/TraditionalEats.WebApp/Components/OrderChat.razor`)
   - Real-time chat UI component
   - Integrated into `OrderDetails.razor` page
   - Features: Message history, real-time updates, send messages, connection status

### Frontend - Mobile App

1. **Chat service** (`src/apps/TraditionalEats.MobileApp/services/chat.ts`)
   - REST: get messages and unread count via Mobile BFF
   - SignalR: connect to ChatService hub (`CHAT_HUB_URL` in `config/app.config.ts`), join/leave, send, mark read
   - Uses **LongPolling** transport (not WebSockets) so negotiation works on React Native; WebSockets often cause “Failed to complete negotiation” on device/simulator
2. **OrderChat component** (`src/apps/TraditionalEats.MobileApp/components/OrderChat.tsx`)
   - Message list, input, send button, connection status
3. **Order details** (`app/orders/[orderId].tsx`)
   - Renders `<OrderChat orderId={...} />` so chat appears on the order details screen

## Database Schema

### chat_messages

- `message_id` (Guid, PK)
- `order_id` (Guid, indexed)
- `sender_id` (Guid, indexed)
- `sender_role` (string: Customer/Vendor/Admin)
- `message` (TEXT)
- `sent_at` (DateTime)
- `is_read` (bool)
- `read_at` (DateTime?)

### chat_participants

- `participant_id` (Guid, PK)
- `order_id` (Guid, indexed)
- `user_id` (Guid, indexed)
- `role` (string)
- `joined_at` (DateTime)
- `last_read_at` (DateTime?)

## Setup Instructions

### 1. Create Database

```bash
# Create the chat database
mysql -u root -p
CREATE DATABASE traditional_eats_chat;
```

### 2. Run Migrations

```bash
cd src/services/TraditionalEats.ChatService
dotnet ef migrations add InitialCreate
dotnet ef database update
```

### 3. Configure Services

- Update `appsettings.Development.json` with your database connection string
- **Ensure ChatService is running** (e.g. `dotnet run` in `TraditionalEats.ChatService`) on port 5012. For mobile/network access it must listen on **all interfaces**: set Kestrel `Url` to `http://0.0.0.0:5012` in `appsettings.Development.json` (not `localhost`, or the phone cannot reach it).
- Update BFF configurations to include ChatService URL (`Services:ChatService`)
- **WebApp**: Set `ChatHubUrl` in `appsettings.json` if ChatService runs on a different host/port (e.g. when testing from another device, use your machine’s IP: `http://YOUR_IP:5012/chatHub`)
- **JWT**: ChatService and IdentityService both use **shared config** (`AddSharedConfiguration` → `appsettings.Shared.json` in BuildingBlocks). The shared file defines `Jwt:Key`, `Jwt:Secret`, `Jwt:Issuer`, `Jwt:Audience`, so tokens are consistent. No extra JWT config is needed unless you override in a service’s own appsettings.

### 4. Start Services

```bash
# Start ChatService
cd src/services/TraditionalEats.ChatService
dotnet run

# Start Web BFF (already includes chat endpoints)
cd src/bff/TraditionalEats.Web.Bff
dotnet run

# Start Mobile BFF (already includes chat endpoints)
cd src/bff/TraditionalEats.Mobile.Bff
dotnet run
```

## API Endpoints

### ChatService (Direct)

- `GET /api/Chat/orders/{orderId}/messages` - Get message history
- `GET /api/Chat/orders/{orderId}/unread-count` - Get unread count

### Web BFF

- `GET /api/WebBff/orders/{orderId}/chat/messages` - Get message history
- `GET /api/WebBff/orders/{orderId}/chat/unread-count` - Get unread count

### Mobile BFF

- `GET /api/MobileBff/orders/{orderId}/chat/messages` - Get message history
- `GET /api/MobileBff/orders/{orderId}/chat/unread-count` - Get unread count

### SignalR Hub

- **Hub URL**: `http://localhost:5012/chatHub` (or your ChatService base URL + `/chatHub`)
- **Note**: The hub URL is a SignalR endpoint, **not a webpage**. Opening it in a browser will not show a page; it is used by the web and mobile apps for WebSocket/long-polling connections.
- Methods:
  - `JoinOrderChat(Guid orderId)` - Join a chat room
  - `LeaveOrderChat(Guid orderId)` - Leave a chat room
  - `SendMessage(Guid orderId, string message)` - Send a message
  - `MarkMessagesAsRead(Guid orderId)` - Mark messages as read
- Events:
  - `ReceiveMessage` - New message received
  - `UserJoined` - User joined the chat
  - `UserLeft` - User left the chat
  - `Error` - Error occurred

## Security

- All endpoints require JWT authentication (`[Authorize]`)
- Access control verified before allowing chat access:
  - Customers: Can only access their own orders
  - Vendors: Can access orders for their restaurants
  - Admins: Can access all orders
- SignalR connection requires valid JWT token (passed as query parameter `access_token`)

## Next Steps

1. **Mobile App Implementation**
   - Create `ChatService.ts` for React Native
   - Create `OrderChat.tsx` component
   - Integrate into mobile order details page

2. **Enhancements**
   - Typing indicators
   - File/image attachments
   - Message reactions
   - Push notifications for new messages
   - Chat history pagination

3. **Testing**
   - Test real-time messaging between customer and vendor
   - Test access control (customer can't access other customers' orders)
   - Test message persistence
   - Test unread count functionality
