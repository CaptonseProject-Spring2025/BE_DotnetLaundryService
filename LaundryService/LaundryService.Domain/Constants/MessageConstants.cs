using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Domain.Constants
{
    public static class MessageConstants
    {
        public static class CommonMessage
        {
            public const string NOT_ALLOWED = "You are not allowed!";
            public const string NOT_AUTHEN = "Not authen!";
            public const string NOT_FOUND = "Not found!";
            public const string ERROR_HAPPENED = "An error occurred, please contact the admin or try again later!";
            public const string MISSING_PARAM = "Missing input parameters.Please check again!";
        }
    }
}
