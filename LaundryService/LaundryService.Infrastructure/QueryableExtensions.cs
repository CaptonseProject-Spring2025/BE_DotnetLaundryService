using LaundryService.Dto.Pagination;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Infrastructure
{
    public static class QueryableExtensions
    {
        public static async Task<PaginationResult<T>> ToPagedListAsync<T>(
            this IQueryable<T> query, int page, int pageSize)
        {
            var totalRecords = await query.CountAsync();
            var data = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            return new PaginationResult<T>(data, totalRecords, page, pageSize);
        }
    }
}
