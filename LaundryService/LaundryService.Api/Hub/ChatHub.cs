
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
    // Bạn có thể thêm mã để xử lý khi một client kết nối, ví dụ như thêm vào một nhóm
    // hoặc gửi thông báo đến client mới kết nối.
  }

  // Phương thức này được gọi khi một client ngắt kết nối với hub
  public override async Task OnDisconnectedAsync(Exception exception)
  {
    Console.WriteLine($"Client disconnected: {Context.ConnectionId}");

    // Xử lý logic bổ sung nếu cần, ví dụ: xóa client khỏi nhóm
    // hoặc ghi log lỗi nếu có exception
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
      var message = new Message
      {
        Messageid = Guid.NewGuid(),
        Userid = Guid.Parse(userId),
        Conversationid = Guid.Parse(conversationId),
        Message1 = messageContent,
        Creationdate = DateTime.UtcNow,
        Issent = true,
        Isseen = false, // Chưa được xem
        Status = "active" // Tin nhắn chưa bị xóa
      };

      await _unitOfWork.Repository<Message>().InsertAsync(message);
      await _unitOfWork.SaveChangesAsync();

      // Lấy tin nhắn với thông tin user
      var messageInfo = await _unitOfWork.Repository<Message>()
          .GetAll()
          .Where(m => m.Messageid == message.Messageid && m.Status != "deleted")
          .Include(m => m.User)
          .Select(m => new
          {
            m.Messageid,
            m.Userid,
            m.Message1,
            m.Creationdate,
            m.Isseen,
            m.Status,
            m.User.Fullname,
            m.User.Avatar
          })
          .FirstOrDefaultAsync();

      await Clients.Group(conversationId).SendAsync("ReceiveMessage", messageInfo);
    }
    catch (Exception ex)
    {
      Console.WriteLine($"Error sending message: {ex.Message}");
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
            m.Messageid,
            m.Userid,
            m.Message1,
            m.Creationdate,
            m.Isseen,
            m.Status,
            m.User.Fullname,
            m.User.Avatar
          })
          .ToListAsync();

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
  }

  //Rời khỏi cuộc trò chuyện
  public async Task LeaveConversation(string conversationId)
  {
    await Groups.RemoveFromGroupAsync(Context.ConnectionId, conversationId);
  }


}
