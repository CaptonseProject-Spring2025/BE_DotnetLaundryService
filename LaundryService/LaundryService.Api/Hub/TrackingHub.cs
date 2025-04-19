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
        try
        {
            var http = Context.GetHttpContext()!;
            var orderId = http.Request.Query["orderId"].ToString();
            if (string.IsNullOrEmpty(orderId))
                throw new HubException("Missing orderId.");

            Guid userId;
            try
            {
                userId = _util.GetCurrentUserIdOrThrow(http);
            }
            catch
            {
                throw new HubException("Invalid token: Cannot retrieve userId.");
            }

            var canDriver = await _perm.CanDriverTrackAsync(orderId, userId);
            var canCustomer = await _perm.CanCustomerViewAsync(orderId, userId);

            if (!canDriver && !canCustomer)
                throw new HubException("Unauthorized to join this order.");

            await Groups.AddToGroupAsync(Context.ConnectionId, orderId);
        }
        catch (HubException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new HubException("Connection failed: " + ex.Message);
        }

        await base.OnConnectedAsync();
    }

    public async Task SendLocation(string orderId, double lat, double lng)
    {
        try
        {
            var http = Context.GetHttpContext()!;
            Guid userId;
            try
            {
                userId = _util.GetCurrentUserIdOrThrow(http);
            }
            catch
            {
                throw new HubException("Invalid token: Cannot retrieve userId.");
            }

            if (!await _perm.CanDriverTrackAsync(orderId, userId))
                throw new HubException("Unauthorized to send location.");

            await Clients.OthersInGroup(orderId)
                         .SendAsync("ReceiveLocation", lat, lng);
        }
        catch (HubException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new HubException("Internal error: " + ex.Message);
        }
    }
}
