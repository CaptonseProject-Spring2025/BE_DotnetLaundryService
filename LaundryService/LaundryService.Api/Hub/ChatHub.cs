
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
      // Tạo tin nhắn mới
      var message = new Message
      {
        Userid = Guid.Parse(userId),
        Conversationid = Guid.Parse(conversationId),
        Message1 = messageContent,
        Creationdate = DateTime.UtcNow,
      };

      // Lưu tin nhắn vào CSDL
      await _unitOfWork.Repository<Message>().InsertAsync(message);
      await _unitOfWork.SaveChangesAsync();

      // Lấy danh sách tin nhắn mới nhất từ cơ sở dữ liệu
      var messages = _unitOfWork.Repository<Message>()
          .GetAll()
          .Where(m => m.Conversationid == Guid.Parse(conversationId))
          .OrderBy(m => m.Creationdate)
          .Include(m => m.User) 
          .Select(m => new
          {
            m.Userid,
            m.Message1,
            m.Creationdate,
            m.User.Fullname,
            m.User.Avatar,
          })
          .ToList();
          Console.WriteLine($"Messages: {messages}");

      // Gửi tin nhắn đến tất cả các client trong nhóm (conversationId)
      await Clients.Group(conversationId).SendAsync("ReceiveMessage", messages);
    }
    catch (FormatException ex)
    {
      // Bắt lỗi nếu userId hoặc conversationId không phải là GUID hợp lệ
      Console.WriteLine($"Invalid GUID format: {ex.Message}");
      throw new ArgumentException("Invalid userId or conversationId format.", ex);
    }
    catch (Exception ex)
    {
      // Bắt lỗi chung và ghi log
      Console.WriteLine($"An error occurred while sending the message: {ex.Message}");
      throw; // Ném lại ngoại lệ để client có thể xử lý
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
