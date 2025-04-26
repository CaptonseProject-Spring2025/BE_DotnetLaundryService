
using LaundryService.Domain.Entities;
using LaundryService.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;


namespace LaundryService.Api.Controllers
{
  [Route("api/[controller]")]
  [ApiController]
  public class ConversationsController : ControllerBase
  {

    private readonly IUnitOfWork _unitOfWork;
    private readonly IUtil _util;
    private readonly HttpContext httpContext;

    public ConversationsController(IUnitOfWork unitOfWork, IUtil util, IHttpContextAccessor httpContextAccessor)
    {
      _unitOfWork = unitOfWork;
      _util = util;
      httpContext = httpContextAccessor.HttpContext;
    }


    //Kiểm tra cuộc chuyện đã tồn tại hay chưa
    [HttpGet("{userId}")]
    public async Task<ActionResult> GetConversation(string currentUserId, string userId)
    {


      var conversation = await _unitOfWork.Repository<Conversation>()
      .GetAsync(c =>
        (c.Userone == Guid.Parse(currentUserId) && c.Usertwo == Guid.Parse(userId))
        || (c.Userone == Guid.Parse(userId) && c.Usertwo == Guid.Parse(currentUserId))
      );

      if (conversation != null)
      {
        return Ok(new { exits = true, currenUserId = conversation.Conversationid });
      }

      return Ok(new { exits = false });
    }

    [HttpGet("messages/{conversationId}")]
    public async Task<ActionResult> GetMessagesByConversationId(Guid conversationId)
    {
      // Lấy danh sách tin nhắn của cuộc trò chuyện và bao gồm thông tin người dùng
      var messages = await _unitOfWork.Repository<Message>()
          .GetAll()
          .Where(m => m.Conversationid == conversationId)
          .Include(m => m.User) // Eager load User để lấy thông tin người dùng (Fullname, Avatar)
          .OrderBy(m => m.Creationdate) // Sắp xếp theo thời gian gửi
          .Select(m => new
          {
            m.Userid,
            m.Message1,   // Nội dung tin nhắn
            m.Creationdate,  // Thời gian gửi
            m.User.Fullname,  // Fullname của người gửi
            m.User.Avatar    // Avatar của người gửi
          })
          .ToListAsync(); // Sử dụng ToListAsync để lấy dữ liệu từ cơ sở dữ liệu

      if (messages == null || messages.Count == 0)
      {
        return Ok(new { success = false, message = "No messages found." });
      }

      return Ok(new { success = true, messages });
    }


    //Lấy danh sách cuộc trò chuyện của người dùng
    [HttpGet("{userId}/conversations")]
    public async Task<ActionResult> GetConversations(string userId)
    {
      // Lấy danh sách cuộc trò chuyện của người dùng và bao gồm thông tin người dùng
      var conversations = await _unitOfWork.Repository<Conversation>()
          .GetAll()
          .Where(c => c.Userone == Guid.Parse(userId) || c.Usertwo == Guid.Parse(userId))
          .Include(c => c.UseroneNavigation) // Eager load Userone để lấy thông tin người dùng (Fullname, Avatar)
          .Include(c => c.UsertwoNavigation) // Eager load Usertwo để lấy thông tin người dùng (Fullname, Avatar)
          .Include(c => c.Messages) // Eager load Messages để lấy thông tin tin nhắn
          .Select(c => new
          {
            c.Conversationid,
            c.Userone,
            c.Usertwo,
            UserOneFullName = c.UseroneNavigation.Fullname,
            UserOneAvatar = c.UseroneNavigation.Avatar,
            UserTwoFullName = c.UsertwoNavigation.Fullname,
            UserTwoAvatar = c.UsertwoNavigation.Avatar,
            LastMessage = c.Messages.OrderByDescending(m => m.Creationdate).FirstOrDefault().Message1, // Tin nhắn cuối cùng
            LastMessageDate = c.Messages.OrderByDescending(m => m.Creationdate).FirstOrDefault().Creationdate // Thời gian gửi tin nhắn cuối cùng
          })
          .ToListAsync(); // Sử dụng ToListAsync để lấy dữ liệu từ cơ sở dữ liệu

      if (conversations == null || conversations.Count == 0)
      {
        return Ok(new { success = false, message = "No conversations found." });
      }

      return Ok(new { success = true, conversations });
    }



    //Tạo mới 1 cuộc trò chuyện mới
    [HttpPost("")]
    public async Task<ActionResult> CreateConversation([FromBody] CreateConversationRequest request)
    {

      //Kiểm tra UserOneId có tồn tài trong bảng users
      var userOneExists = await _unitOfWork.Repository<User>()
        .GetAsync(u => u.Userid == request.UserOneId);

      if (userOneExists == null)
      {
        return BadRequest(new { success = false, message = "UserOneId không tồn tại." });
      }

      // Kiểm tra UserTwoId có tồn tại trong bảng users
      var userTwoExists = await _unitOfWork.Repository<User>()
                  .GetAsync(u => u.Userid == request.UserOneId);


      if (userTwoExists == null)
      {
        return BadRequest(new { success = false, message = "UserTwoId không tồn tại." });
      }

      // Kiểm tra xem cuộc trò chuyện đã tồn tại hay chưa
      var existingConversation = await _unitOfWork.Repository<Conversation>()
        .GetAsync(c =>
            (c.Userone == request.UserOneId && c.Usertwo == request.UserTwoId) ||
            (c.Userone == request.UserTwoId && c.Usertwo == request.UserOneId)
        );

      if (existingConversation != null)
      {
        return Ok(new { success = false, message = "Cuộc trò chuyện đã tồn tại.", conversationId = existingConversation.Conversationid });
      }


      var newConversation = new Conversation
      {
        Userone = request.UserOneId,
        Usertwo = request.UserTwoId,
        Creationdate = DateTime.UtcNow,
      };

      await _unitOfWork.Repository<Conversation>().InsertAsync(newConversation);
      await _unitOfWork.SaveChangesAsync();

      return Ok(new { success = true, conversationId = newConversation.Conversationid });
    }


  }
}