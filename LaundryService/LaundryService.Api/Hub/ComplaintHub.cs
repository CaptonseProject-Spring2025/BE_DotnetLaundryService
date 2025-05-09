namespace LaundryService.Api.Hub;

using Microsoft.AspNetCore.SignalR;

    public class ComplaintHub : Hub
    {
        public async Task SendComplaintUpdate(string message)
        {
            await Clients.All.SendAsync("ReceiveComplaintUpdate", message);
        }
    }
