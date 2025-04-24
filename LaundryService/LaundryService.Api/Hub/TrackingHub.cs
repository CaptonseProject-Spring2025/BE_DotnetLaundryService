namespace LaundryService.Api.Hub;

using LaundryService.Domain.Interfaces.Services;
using LaundryService.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

[Authorize]
public class TrackingHub : Hub
{
    private readonly IUtil _util;
    private readonly ITrackingPermissionService _perm;


    private static readonly ConcurrentDictionary<string, string> _connectionOrders
         = new ConcurrentDictionary<string, string>();

    private static readonly ConcurrentDictionary<string, (double lat, double lng)> _lastLocations
        = new ConcurrentDictionary<string, (double lat, double lng)>();

    public TrackingHub(IUtil util, ITrackingPermissionService perm)
    {
        _util = util;
        _perm = perm;
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        // Khi client disconnect, dọn luôn mapping
        _connectionOrders.TryRemove(Context.ConnectionId, out _);
        return base.OnDisconnectedAsync(exception);
    }

    public async Task JoinOrder(string orderId)
    {
        if (string.IsNullOrWhiteSpace(orderId))
        {
            await Clients.Caller.SendAsync("ReceiveError", "Missing or empty orderId.");
            Context.Abort();
            return;
        }

        var http = Context.GetHttpContext()!;
        Guid userId;

        try
        {
            userId = _util.GetCurrentUserIdOrThrow(http);
        }
        catch
        {
            await Clients.Caller.SendAsync("ReceiveError", "Invalid token.");
            Context.Abort();
            return;
        }

        var canDriver = await _perm.CanDriverTrackAsync(orderId, userId);
        var canCustomer = await _perm.CanCustomerViewAsync(orderId, userId);

        if (!canDriver && !canCustomer)
        {
            await Clients.Caller.SendAsync("ReceiveError", "Unauthorized to join this order.");
            Context.Abort();
            return;
        }

        // Thêm vào group
        await Groups.AddToGroupAsync(Context.ConnectionId, orderId);

        // Lưu orderId cho connection này
        _connectionOrders[Context.ConnectionId] = orderId;

        // Replay vị trí gần nhất nếu có
        if (_lastLocations.TryGetValue(orderId, out var last))
        {
            await Clients.Caller.SendAsync("ReceiveLocation", last.lat, last.lng);
        }
    }

    public async Task SendLocation(double lat, double lng)
    {
        // Lấy orderId đã join
        if (!_connectionOrders.TryGetValue(Context.ConnectionId, out var orderId))
            throw new HubException("You must JoinOrder before sending location.");

        // Lấy userId và kiểm tra driver permission
        var http = Context.GetHttpContext()!;
        Guid userId = _util.GetCurrentUserIdOrThrow(http);
        if (!await _perm.CanDriverTrackAsync(orderId, userId))
            throw new HubException("Unauthorized to send location.");

        _lastLocations.AddOrUpdate(orderId,
            (lat, lng),
            (key, old) => (lat, lng));

        // Gửi tọa độ cho cả group
        await Clients.OthersInGroup(orderId)
                     .SendAsync("ReceiveLocation", lat, lng);
    }
}
