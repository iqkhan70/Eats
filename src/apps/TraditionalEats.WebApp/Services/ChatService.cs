using Microsoft.AspNetCore.SignalR.Client;
using System.Net.Http.Json;
using System.Text.Json;

namespace TraditionalEats.WebApp.Services;

public class ChatService : IAsyncDisposable
{
    private readonly HttpClient _httpClient;
    private readonly AuthService _authService;
    private HubConnection? _hubConnection;
    private readonly ILogger<ChatService>? _logger;

    public ChatService(HttpClient httpClient, AuthService authService, ILogger<ChatService>? logger = null)
    {
        _httpClient = httpClient;
        _authService = authService;
        _logger = logger;
    }

    public event Action<ChatMessage>? MessageReceived;
    public event Action<string, string>? UserJoined;
    public event Action<string>? UserLeft;
    public event Action<string>? Error;
    public event Action<bool>? ConnectionStateChanged;

    public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;

    public async Task ConnectAsync(string hubUrl)
    {
        try
        {
            if (_hubConnection != null && _hubConnection.State == HubConnectionState.Connected)
            {
                return;
            }

            var token = await _authService.GetAccessTokenAsync();
            if (string.IsNullOrEmpty(token))
            {
                _logger?.LogWarning("Cannot connect to chat hub: No access token");
                return;
            }

            _hubConnection = new HubConnectionBuilder()
                .WithUrl($"{hubUrl}?access_token={token}", options =>
                {
                    options.AccessTokenProvider = async () => await _authService.GetAccessTokenAsync() ?? string.Empty;
                })
                .WithAutomaticReconnect()
                .Build();

            // Register event handlers
            _hubConnection.On<object>("ReceiveMessage", (message) =>
            {
                try
                {
                    var json = JsonSerializer.Serialize(message);
                    var chatMessage = JsonSerializer.Deserialize<ChatMessage>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    if (chatMessage != null)
                    {
                        MessageReceived?.Invoke(chatMessage);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error deserializing chat message");
                }
            });

            _hubConnection.On<object>("UserJoined", (data) =>
            {
                try
                {
                    var json = JsonSerializer.Serialize(data);
                    var obj = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                    if (obj != null && obj.TryGetValue("userId", out var userId) && obj.TryGetValue("role", out var role))
                    {
                        UserJoined?.Invoke(userId.ToString()!, role.ToString()!);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error handling UserJoined event");
                }
            });

            _hubConnection.On<object>("UserLeft", (data) =>
            {
                try
                {
                    var json = JsonSerializer.Serialize(data);
                    var obj = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                    if (obj != null && obj.TryGetValue("userId", out var userId))
                    {
                        UserLeft?.Invoke(userId.ToString()!);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error handling UserLeft event");
                }
            });

            _hubConnection.On<string>("Error", (error) =>
            {
                Error?.Invoke(error);
            });

            _hubConnection.Reconnected += _ =>
            {
                _logger?.LogInformation("Chat hub reconnected");
                ConnectionStateChanged?.Invoke(true);
                return Task.CompletedTask;
            };
            _hubConnection.Closed += error =>
            {
                _logger?.LogWarning(error, "Chat hub connection closed");
                ConnectionStateChanged?.Invoke(false);
                return Task.CompletedTask;
            };

            await _hubConnection.StartAsync();
            _logger?.LogInformation("Connected to chat hub");
            ConnectionStateChanged?.Invoke(true);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error connecting to chat hub");
            Error?.Invoke($"Failed to connect: {ex.Message}");
        }
    }

    public async Task JoinOrderChatAsync(Guid orderId)
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
        {
            await _hubConnection.InvokeAsync("JoinOrderChat", orderId);
        }
    }

    public async Task LeaveOrderChatAsync(Guid orderId)
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
        {
            await _hubConnection.InvokeAsync("LeaveOrderChat", orderId);
        }
    }

    public async Task SendMessageAsync(Guid orderId, string message)
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
        {
            await _hubConnection.InvokeAsync("SendMessage", orderId, message);
        }
        else
        {
            Error?.Invoke("Not connected to chat");
        }
    }

    public async Task MarkMessagesAsReadAsync(Guid orderId)
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
        {
            await _hubConnection.InvokeAsync("MarkMessagesAsRead", orderId);
        }
    }

    public async Task<List<ChatMessage>> GetMessagesAsync(Guid orderId)
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<List<ChatMessage>>($"WebBff/orders/{orderId}/chat/messages");
            return response ?? new List<ChatMessage>();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting chat messages");
            return new List<ChatMessage>();
        }
    }

    public async Task<int> GetUnreadCountAsync(Guid orderId)
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<UnreadCountResponse>($"WebBff/orders/{orderId}/chat/unread-count");
            return response?.UnreadCount ?? 0;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting unread count");
            return 0;
        }
    }

    public async Task DisconnectAsync()
    {
        if (_hubConnection != null)
        {
            await _hubConnection.StopAsync();
            await _hubConnection.DisposeAsync();
            _hubConnection = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
    }
}

public class ChatMessage
{
    public Guid MessageId { get; set; }
    public Guid OrderId { get; set; }
    public Guid SenderId { get; set; }
    public string SenderRole { get; set; } = string.Empty;
    public string? SenderDisplayName { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
    public bool IsRead { get; set; }
}

public class UnreadCountResponse
{
    public Guid OrderId { get; set; }
    public int UnreadCount { get; set; }
}
