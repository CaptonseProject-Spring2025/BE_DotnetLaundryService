using LaundryService.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Infrastructure
{
    public class Repository<T> : IRepository<T> where T : class
    {
        public DbSet<T> Entities => DbContext.Set<T>();
        public DbContext DbContext { get; }

        public Repository(DbContext dbContext)
        {
            DbContext = dbContext;
        }

        /// <summary>
        /// Trả về tất cả các thực thể dưới dạng danh sách bất đồng bộ
        /// </summary>
        public async Task<IList<T>> GetAllAsync()
        {
            return await Entities.ToListAsync();
        }

        public async Task<IList<T>> GetAllAsync(Expression<Func<T, bool>> predicate)
        {
            return await Entities.Where(predicate).ToListAsync();
        }

        /// <summary>
        /// Trả về một thực thể theo khóa chính
        /// </summary>
        public T Find(params object[] keyValues)
        {
            return Entities.Find(keyValues);
        }

        /// <summary>
        /// Tìm kiếm một thực thể bất đồng bộ theo khóa chính
        /// </summary>
        public async Task<T> FindAsync(params object[] keyValues)
        {
            return await Entities.FindAsync(keyValues);
        }

        /// <summary>
        /// Thêm một thực thể vào DbSet
        /// </summary>
        public async Task InsertAsync(T entity, bool saveChanges = true)
        {
            await Entities.AddAsync(entity);
            if (saveChanges)
            {
                await DbContext.SaveChangesAsync();
            }
        }

        /// <summary>
        /// Thêm một danh sách thực thể vào DbSet
        /// </summary>
        public async Task InsertRangeAsync(IEnumerable<T> entities, bool saveChanges = true)
        {
            await Entities.AddRangeAsync(entities);
            if (saveChanges)
            {
                await DbContext.SaveChangesAsync();
            }
        }

        /// <summary>
        /// Xóa một thực thể bằng khóa chính (kiểu Guid) với tùy chọn lưu thay đổi ngay
        /// </summary>
        public async Task DeleteAsync(Guid id, bool saveChanges = true)
        {
            var entity = await Entities.FindAsync(id);
            if (entity != null)
            {
                await DeleteAsync(entity, saveChanges);
            }
        }

        /// <summary>
        /// Xóa một thực thể
        /// </summary>
        public async Task DeleteAsync(T entity, bool saveChanges = true)
        {
            Entities.Remove(entity);
            if (saveChanges)
            {
                await DbContext.SaveChangesAsync();
            }
        }

        /// <summary>
        /// Xóa một danh sách thực thể với kiểm tra nếu danh sách trống
        /// </summary>
        public async Task DeleteRangeAsync(IEnumerable<T> entities, bool saveChanges = true)
        {
            var entityList = entities as T[] ?? entities.ToArray();
            if (entityList.Any())
            {
                Entities.RemoveRange(entityList);
            }

            if (saveChanges)
            {
                await DbContext.SaveChangesAsync();
            }
        }

        /// <summary>
        /// Cập nhật một thực thể
        /// </summary>
        public async Task UpdateAsync(T entity, bool saveChanges = true)
        {
            Entities.Update(entity);
            if (saveChanges)
            {
                await DbContext.SaveChangesAsync();
            }
        }

        /// <summary>
        /// Cập nhật một danh sách thực thể
        /// </summary>
        public async Task UpdateRangeAsync(IEnumerable<T> entities, bool saveChanges = true)
        {
            Entities.UpdateRange(entities);
            if (saveChanges)
            {
                await DbContext.SaveChangesAsync();
            }
        }

        /// <summary>
        /// Trả về một thực thể dựa trên điều kiện
        /// </summary>
        public async Task<T?> GetAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
        {
            return await Entities.FirstOrDefaultAsync(predicate, cancellationToken);
        }

        public async Task<T?> GetAsync(Expression<Func<T, bool>> predicate)
        {
            return await Entities.FirstOrDefaultAsync(predicate);
        }

        /// <summary>
        /// Trả về IQueryable để thực hiện các truy vấn tùy chỉnh
        /// </summary>
        public IQueryable<T> GetAll()
        {
            return Entities.AsQueryable();
        }
    }
}
