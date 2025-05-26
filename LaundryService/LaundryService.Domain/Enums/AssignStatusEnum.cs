using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Domain.Enums
{
    public enum AssignStatusEnum
    {
        PROCESING, //customerStaff nhận đơn giao
        FAIL, //customerStaff không nhận đơn giao, hoặc nhận rồi nhưng bị lỗi, hệ thống tự hủy đơn
        SUCCESS, // customerStaff nhận đơn giao, hoàn tất (trong trường hợp khách hàng hủy vẫn là SUCCESS)
        
        //-----------------------------//
        //pickup
        ASSIGNED_PICKUP, // Admin giao đơn cho Driver 

        // Driver đã đến lấy đồ thành công, hoàn thành công việc
        // Driver đã đến lấy đồ nhưng không thành công, KH hủy or giao hôm khác
        PICKUP_SUCCESS,
        
        //Driver từ chối đi lấy đồ
        PICKUP_FAILED,

        //-----------------------------//

        ASSIGNED_DELIVERY,
        DELIVERY_SUCCESS,
        DELIVERY_FAILED
    }
}