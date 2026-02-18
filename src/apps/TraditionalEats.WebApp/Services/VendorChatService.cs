using Microsoft.AspNetCore.SignalR.Client;
using System.Net.Http.Json;
using System.Text.Json;

namespace TraditionalEats.WebApp.Services;

public class VendorChatService : IAsyncDisposable
{
    private readonly HttpClient _httpClient;
    private readonly AuthService _authService;
    private HubConnection? _hubConnection;
    private readonly ILogger<VendorChatService>? _logger;

    public VendorChatService(HttpClient httpClient, AuthService authService, ILogger<VendorChatService>? logger = null)
    {
        _httpClient = httpClient;
        _authService = authService;
        _logger = logger;
    }

    public event Action<VendorChatMessage>? MessageReceived;
    public event Action<string>? Error;
    public event Action<bool>? ConnectionStateChanged;

    public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;

    public async Task ConnectAsync(string hubUrl)
    {
        try
        {
            if (_hubConnection != null && _hubConnection.State == HubConnectionState.Connected)
                return;

            var token = await _authService.GetAccessTokenAsync();
            if (string.IsNullOrEmpty(token))
            {
                _logger?.LogWarning("Cannot connect to vendor chat hub: No access token");
                return;
            }

            _hubConnection = new HubConnectionBuilder()
                .WithUrl(hubUrl, options =>
                {
                    options.AccessTokenProvider = async () => await _authService.GetAccessTokenAsync() ?? string.Empty;
                    options.SkipNegotiation = false;
                    options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
                })
                .WithAutomaticReconnect()
                .Build();

            _hubConnection.On<object>("ReceiveVendorMessage", payload =>
            {
                try
                {
                    var json = JsonSerializer.Serialize(payload);
                    var msg = JsonSerializer.Deserialize<VendorChatMessage>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    if (msg != null)
                        MessageReceived?.Invoke(msg);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error deserializing vendor chat message");
                }
            });

            _hubConnection.On<string>("Error", err => Error?.Invoke(err));

            _hubConnection.Reconnected += _ =>
            {
                ConnectionStateChanged?.Invoke(true);
                return Task.CompletedTask;
            };
            _hubConnection.Closed += _ =>
            {
                ConnectionStateChanged?.Invoke(false);
                return Task.CompletedTask;
            };

            await _hubConnection.StartAsync();
            ConnectionStateChanged?.Invoke(true);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error connecting to vendor chat hub");
            Error?.Invoke($"Failed to connect: {ex.Message}");
        }
    }

    public async Task JoinConversationAsync(Guid conversationId)
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
            await _hubConnection.InvokeAsync("JoinVendorConversation", conversationId);
        else
            Error?.Invoke("Not connected to chat server");
    }

    public async Task LeaveConversationAsync(Guid conversationId)
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
            await _hubConnection.InvokeAsync("LeaveVendorConversation", conversationId);
    }

    public async Task SendMessageAsync(Guid conversationId, string message)
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
            await _hubConnection.InvokeAsync("SendVendorMessage", conversationId, message);
        else
            Error?.Invoke("Not connected to chat");
    }

    public async Task<List<VendorChatMessage>> GetMessagesAsync(Guid conversationId)
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<List<VendorChatMessage>>(
                $"WebBff/vendor-chat/conversations/{conversationId}/messages");
            return response ?? new List<VendorChatMessage>();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting vendor chat messages");
            return new List<VendorChatMessage>();
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

public class VendorChatMessage
{
    public Guid MessageId { get; set; }
    public Guid ConversationId { get; set; }
    public Guid SenderId { get; set; }
    public string SenderRole { get; set; } = string.Empty;
    public string? SenderDisplayName { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
}

