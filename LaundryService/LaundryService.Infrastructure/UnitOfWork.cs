using LaundryService.Domain.Interfaces;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.Collections.Concurrent;
using System.Threading;

namespace LaundryService.Infrastructure
{
    public class UnitOfWork : IUnitOfWork
    {
        public DbContext DbContext { get; private set; } //DbContext được sử dụng cho UnitOfWork này.
        private readonly ConcurrentDictionary<string, object> _repositories;
        private IDbContextTransaction _transaction; //Giao dịch hiện tại của DbContext.
        private IsolationLevel? _isolationLevel;

        public UnitOfWork(DbFactory dbFactory)
        {
            // Khởi tạo DbContext từ DbFactory
            DbContext = dbFactory.DbContext;
            _repositories = new ConcurrentDictionary<string, object>();
        }

        //public IPersonRepository PersonRepository => _personRepository ??= new PersonRepository(DbContext);

        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return await DbContext.SaveChangesAsync(cancellationToken);
        }

        private async Task StartNewTransactionIfNeeded()
        {
            if (_transaction == null)
            {
                _transaction = _isolationLevel.HasValue ?
                    await DbContext.Database.BeginTransactionAsync(_isolationLevel.GetValueOrDefault()) : await DbContext.Database.BeginTransactionAsync();
            }
        }

        public async Task BeginTransaction()
        {
            await StartNewTransactionIfNeeded();
        }

        public async Task CommitTransaction()
        {
            /*
         	do not open transaction here, because if during the request
         	nothing was changed(only select queries were run), we don't
         	want to open and commit an empty transaction -calling SaveChanges()
         	on _transactionProvider will not send any sql to database in such case
        	*/
            await DbContext.SaveChangesAsync();

            if (_transaction != null)
            {
                await _transaction.CommitAsync();
                await _transaction.DisposeAsync();
                _transaction = null;
            }
        }

        public async Task RollbackTransaction()
        {
            if (_transaction != null)
            {
                await _transaction.RollbackAsync();
                await _transaction.DisposeAsync();
                _transaction = null;
            }
        }

        public void Dispose()
        {
            DbContext?.Dispose();
            DbContext = null;
        }

        /// <summary>
        /// Trả về một repository cho kiểu thực thể được chỉ định. 
        /// Nếu repository đã tồn tại trong scope, repository đó sẽ được trả về.
        /// </summary>
        /// <typeparam name="TEntity">Kiểu thực thể.</typeparam>
        /// <returns>Repository cho kiểu thực thể tương ứng.</returns>
        public IRepository<TEntity> Repository<TEntity>() where TEntity : class
        {
            var typeName = typeof(TEntity).Name;
            // Sử dụng ConcurrentDictionary để tránh lock thủ công
            return (IRepository<TEntity>)_repositories.GetOrAdd(typeName, _ => new Repository<TEntity>(DbContext));
        }
    }
}
