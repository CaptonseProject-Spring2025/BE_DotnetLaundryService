namespace LaundryService.Api.Hub;

using LaundryService.Domain.Entities;
using LaundryService.Domain.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

public class ChatHub : Hub
{
    private readonly IUnitOfWork _unitOfWork;

    public ChatHub(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    // Phương thức này được gọi khi một client kết nối với hub
    public override async Task OnConnectedAsync()
    {
        Console.WriteLine($"Client connected: {Context.ConnectionId}");
        await base.OnConnectedAsync();
    }

    // Phương thức này được gọi khi một client ngắt kết nối với hub
    public override async Task OnDisconnectedAsync(Exception exception)
    {
        Console.WriteLine($"Client disconnected: {Context.ConnectionId}");

        if (exception != null)
        {
            Console.WriteLine($"Disconnection due to error: {exception.Message}");
        }

        await base.OnDisconnectedAsync(exception);
    }

    //Gửi tin nhắn
    public async Task SendMessage(string userId, string conversationId, string messageContent)
    {
        try
        {
            // Tạo tin nhắn mới
            var message = new Message
            {
                Messageid = Guid.NewGuid(),
                Userid = Guid.Parse(userId),
                Conversationid = Guid.Parse(conversationId),
                Message1 = messageContent,
                Creationdate = DateTime.UtcNow,
                Issent = true,
                Isseen = false,
                Status = "active"
            };

            // Lưu tin nhắn vào CSDL
            await _unitOfWork.Repository<Message>().InsertAsync(message);
            await _unitOfWork.SaveChangesAsync();

            // Lấy thông tin tin nhắn vừa tạo kèm thông tin user
            var messageInfo = await _unitOfWork.Repository<Message>()
                .GetAll()
                .Where(m => m.Messageid == message.Messageid && m.Status != "deleted")
                .Include(m => m.User)
                .Select(m => new
                {
                    messageid = m.Messageid.ToString().ToLower(),
                    userid = m.Userid.ToString().ToLower(),
                    message1 = m.Message1,
                    creationdate = m.Creationdate,
                    isseen = m.Isseen,
                    status = m.Status,
                    fullname = m.User.Fullname,
                    avatar = m.User.Avatar
                })
                .FirstOrDefaultAsync();

            Console.WriteLine($"Sending message to group {conversationId}: {System.Text.Json.JsonSerializer.Serialize(messageInfo)}");

            // Gửi tin nhắn đến tất cả các client trong nhóm (conversationId)    
            await Clients.Group(conversationId).SendAsync("ReceiveMessage", messageInfo);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending message: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            throw;
        }
    }

    // Đánh dấu tin nhắn đã đọc
    public async Task MarkMessageAsRead(string messageId, string userId)
    {
        try
        {
            var message = await _unitOfWork.Repository<Message>()
                .GetAsync(m => m.Messageid == Guid.Parse(messageId));

            if (message != null && message.Userid != Guid.Parse(userId))
            {
                message.Isseen = true;

                await _unitOfWork.Repository<Message>().UpdateAsync(message);
                await _unitOfWork.SaveChangesAsync();

                // Thông báo cho người gửi
                await Clients.User(message.Userid.ToString()).SendAsync("MessageRead", messageId);
                
                Console.WriteLine($"Message {messageId} marked as read by user {userId}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error marking message as read: {ex.Message}");
        }
    }

    // Xóa tin nhắn
    public async Task DeleteMessage(string messageId, string userId)
    {
        try
        {
            var message = await _unitOfWork.Repository<Message>()
                .GetAsync(m => m.Messageid == Guid.Parse(messageId));

            if (message != null && message.Userid == Guid.Parse(userId))
            {
                message.Status = "deleted";

                await _unitOfWork.Repository<Message>().UpdateAsync(message);
                await _unitOfWork.SaveChangesAsync();

                await Clients.Group(message.Conversationid.ToString()).SendAsync("MessageDeleted", messageId);
                
                Console.WriteLine($"Message {messageId} deleted by user {userId}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting message: {ex.Message}");
        }
    }

    // Lấy tin nhắn (cập nhật để loại bỏ tin nhắn đã xóa)
    public async Task GetMessages(string conversationId)
    {
        try
        {
            var messages = await _unitOfWork.Repository<Message>()
                .GetAll()
                .Where(m => m.Conversationid == Guid.Parse(conversationId) && m.Status != "deleted")
                .OrderBy(m => m.Creationdate)
                .Include(m => m.User)
                .Select(m => new
                {
                    messageid = m.Messageid.ToString().ToLower(),
                    userid = m.Userid.ToString().ToLower(),
                    message1 = m.Message1,
                    creationdate = m.Creationdate,
                    isseen = m.Isseen,
                    status = m.Status,
                    fullname = m.User.Fullname,
                    avatar = m.User.Avatar
                })
                .ToListAsync();

            Console.WriteLine($"Sending {messages.Count} messages to group {conversationId}");
            await Clients.Group(conversationId).SendAsync("ReceiveMessages", messages);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting messages: {ex.Message}");
        }
    }

    //Tham gia cuộc trò chuyện
    public async Task JoinConversation(string conversationId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, conversationId);
        Console.WriteLine($"Client {Context.ConnectionId} joined conversation {conversationId}");
    }

    //Rời khỏi cuộc trò chuyện
    public async Task LeaveConversation(string conversationId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, conversationId);
        Console.WriteLine($"Client {Context.ConnectionId} left conversation {conversationId}");
    }

    // Bắt đầu typing indicator
    public async Task StartTyping(string userId, string conversationId)
    {
        try
        {
            // Lấy thông tin user để gửi tên
            var user = await _unitOfWork.Repository<User>()
                .GetAsync(u => u.Userid == Guid.Parse(userId));

            string userName = user?.Fullname ?? "User";

            // Gửi thông báo typing đến tất cả client khác trong nhóm (trừ người gửi)
            await Clients.GroupExcept(conversationId, Context.ConnectionId)
                .SendAsync("UserTyping", userId, userName);

            Console.WriteLine($"User {userName} ({userId}) started typing in conversation {conversationId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in StartTyping: {ex.Message}");
        }
    }

    // Dừng typing indicator
    public async Task StopTyping(string userId, string conversationId)
    {
        try
        {
            // Gửi thông báo dừng typing đến tất cả client khác trong nhóm (trừ người gửi)
            await Clients.GroupExcept(conversationId, Context.ConnectionId)
                .SendAsync("UserStoppedTyping", userId);

            Console.WriteLine($"User {userId} stopped typing in conversation {conversationId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in StopTyping: {ex.Message}");
        }
    }
}