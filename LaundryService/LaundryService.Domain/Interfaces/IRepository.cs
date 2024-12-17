using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Domain.Interfaces
{
    public interface IRepository<T> where T : class
    {
        DbSet<T> Entities { get; }
        DbContext DbContext { get; }

        /// <summary>
        /// Get all items of an entity by asynchronous method
        /// </summary>
        Task<IList<T>> GetAllAsync();

        /// <summary>
        /// Find one item of an entity by synchronous method
        /// </summary>
        T Find(params object[] keyValues);

        /// <summary>
        /// Find one item of an entity by asynchronous method
        /// </summary>
        Task<T> FindAsync(params object[] keyValues);

        /// <summary>
        /// Insert item into an entity by asynchronous method
        /// </summary>
        Task InsertAsync(T entity, bool saveChanges = true);

        /// <summary>
        /// Insert multiple items into an entity by asynchronous method
        /// </summary>
        Task InsertRangeAsync(IEnumerable<T> entities, bool saveChanges = true);

        /// <summary>
        /// Remove one item from an entity by asynchronous method
        /// </summary>
        Task DeleteAsync(int id, bool saveChanges = true);

        /// <summary>
        /// Remove one item from an entity by asynchronous method
        /// </summary>
        Task DeleteAsync(T entity, bool saveChanges = true);

        /// <summary>
        /// Remove multiple items from an entity by asynchronous method
        /// </summary>
        Task DeleteRangeAsync(IEnumerable<T> entities, bool saveChanges = true);

        /// <summary>
        /// Update an entity by asynchronous method
        /// </summary>
        Task UpdateAsync(T entity, bool saveChanges = true);

        /// <summary>
        /// Update multiple entities by asynchronous method
        /// </summary>
        Task UpdateRangeAsync(IEnumerable<T> entities, bool saveChanges = true);

        Task<T?> GetAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

        IQueryable<T> GetAll();
    }
}
