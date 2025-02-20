using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Dto.Responses
{
    public class ResponseDataDTO<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public T Data { get; set; }
        
        public MetaData MetaData { get; set; }
    }

    public class MetaData
    {
        public int PageIndex { get; set; }
        public int PageSize { get; set; }
        public int Total { get; set; }
        public int PageTotal => PageSize == 0 ? 0 : (int)Math.Ceiling((double)Total / PageSize);
    }
}
