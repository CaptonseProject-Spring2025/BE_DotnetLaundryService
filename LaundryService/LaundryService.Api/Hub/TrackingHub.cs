namespace LaundryService.Api.Hub;

using LaundryService.Domain.Interfaces.Services;
using LaundryService.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

[Authorize]
public class TrackingHub : Hub
{
    private readonly IUtil _util;
    private readonly ITrackingPermissionService _perm;

    public TrackingHub(IUtil util, ITrackingPermissionService perm)
    {
        _util = util;
        _perm = perm;
    }

    public override async Task OnConnectedAsync()
    {
        var http = Context.GetHttpContext()!;
        var orderId = http.Request.Query["orderId"].ToString();
        if (string.IsNullOrEmpty(orderId))
            throw new HubException("Missing orderId.");

        var userId = _util.GetCurrentUserIdOrThrow(http);

        if (await _perm.CanDriverTrackAsync(orderId, userId)
         || await _perm.CanCustomerViewAsync(orderId, userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, orderId);
        }
        else
        {
            throw new HubException("Unauthorized to join this order.");
        }

        await base.OnConnectedAsync();
    }

    public async Task SendLocation(string orderId, double lat, double lng)
    {
        var http = Context.GetHttpContext()!;
        var userId = _util.GetCurrentUserIdOrThrow(http);

        if (!await _perm.CanDriverTrackAsync(orderId, userId))
            throw new HubException("Unauthorized to send location.");

        await Clients.OthersInGroup(orderId)
                     .SendAsync("ReceiveLocation", lat, lng);
    }
}
