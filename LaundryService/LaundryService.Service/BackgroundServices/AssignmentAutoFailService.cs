using LaundryService.Domain.Entities;
using LaundryService.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Service.BackgroundServices
{
    public class AssignmentAutoFailService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AssignmentAutoFailService> _logger;

        public AssignmentAutoFailService(IServiceScopeFactory scopeFactory, ILogger<AssignmentAutoFailService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("AssignmentAutoFailService started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Mỗi 3 phút chạy 1 lần
                    await Task.Delay(TimeSpan.FromMinutes(3), stoppingToken);

                    using var scope = _scopeFactory.CreateScope();
                    var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

                    // 1) Lấy danh sách assignment đang processing > 30'
                    var thirtyMinsAgo = DateTime.UtcNow.AddMinutes(-30);

                    var processingAssignments = unitOfWork.Repository<Orderassignmenthistory>()
                        .GetAll()
                        .Where(a => a.Status == "PROCESSING"
                                    && a.Assignedat < thirtyMinsAgo)
                        .ToList();

                    if (processingAssignments.Any())
                    {
                        _logger.LogInformation($"Auto-failing {processingAssignments.Count} processing assignments older than 30 mins.");

                        foreach (var a in processingAssignments)
                        {
                            a.Status = "FAIL";
                            a.Declinereason = "Hủy bởi hệ thống. Hết thời gian xử lý đơn hàng.";
                            a.Completedat = DateTime.UtcNow;
                        }

                        // Cập nhật DB
                        await unitOfWork.Repository<Orderassignmenthistory>().UpdateRangeAsync(processingAssignments, saveChanges: false);
                        await unitOfWork.SaveChangesAsync();

                        _logger.LogInformation("Auto-fail updates saved.");
                    }
                }
                catch (TaskCanceledException)
                {
                    // Ignore, do nothing khi bị hủy
                }
                catch (Exception ex)
                {
                    // Log lỗi, nhưng vòng while vẫn tiếp tục
                    _logger.LogError(ex, "Error in AssignmentAutoFailService background job");
                }
            }

            _logger.LogInformation("AssignmentAutoFailService stopping.");
        }
    }
}
