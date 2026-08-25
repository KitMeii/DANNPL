using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

using MongoDB.Bson;
using MongoDB.Driver;
using BaseCore.Libs.Repository;
using BaseCore.LogService.Entities;

namespace BaseCore.LogService
{
    public interface ILogActionService : IMongoRepository<LogAction>
    {
        Task<ICollection<LogAction>> GetAllListAsync();

        Task CreateLog(LogAction logAction);
    }

    public class LogActionService : MongoRepository<LogAction>, ILogActionService
    {
        private readonly IDbContext _context;
        public LogActionService(IDbContext dbContext) : base(dbContext)
        {
            _context = dbContext;
        }

        public async Task<ICollection<LogAction>> GetAllListAsync()
        {
            FilterDefinition<LogAction> filter = Builders<LogAction>.Filter.Where(m => m.Id != BsonObjectId.Empty);
            return await GetAllAsync(filter);
        }

        public async Task CreateLog(LogAction logAction)
        {
            await CreateAsync(logAction);
        }
    }
}
