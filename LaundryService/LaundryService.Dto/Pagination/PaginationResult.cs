using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Dto.Pagination
{
    public class PaginationResult<T>
    {
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        public int TotalRecords { get; set; }
        public List<T> Data { get; set; }

        public PaginationResult(List<T> data, int totalRecords, int currentPage, int pageSize)
        {
            Data = data;
            TotalRecords = totalRecords;
            PageSize = pageSize;
            CurrentPage = currentPage;
            TotalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);
        }
    }
}
