using Microsoft.AspNetCore.Http;
using MongoDB.Bson;
using MongoDB.Driver;
using BaseCore.Libs.Repository;
using BaseCore.LogService.Entities;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace BaseCore.LogService
{
    public interface ILogErrorService : IMongoRepository<LogError>
    {
        Task<ICollection<LogError>> GetAllListAsync();
        Task CreateLog(HttpContext httpContext, string message);
    }

    public class LogErrorService : MongoRepository<LogError>, ILogErrorService
    {
        private readonly IDbContext _context;
        public LogErrorService(IDbContext dbContext) : base(dbContext)
        {
            _context = dbContext;
        }

        public async Task CreateLog(HttpContext httpContext, string message)
        {
            var requestBody = string.Empty;
            httpContext.Request.EnableBuffering();
            using (var reader = new StreamReader(httpContext.Request.Body))
            {
                requestBody = reader.ReadToEnd();
                httpContext.Request.Body.Seek(0, SeekOrigin.Begin);
                requestBody = reader.ReadToEnd();
            }

            var pathUrl = string.Format("{0}://{1}{2}", httpContext.Request.Scheme, httpContext.Request.Host, httpContext.Request.Path);
            var logError = new LogError
            {
                Header = $"REQUEST HttpMethod: {httpContext.Request.Method}, Path: {pathUrl}, Content-Type: {httpContext.Request.ContentType}",
                Body = requestBody,
                CreatedUser = httpContext.User.Identity.Name, 
                Message = message
            };

           await CreateAsync(logError);
        }

        public async Task<ICollection<LogError>> GetAllListAsync()
        {
            FilterDefinition<LogError> filter = Builders<LogError>.Filter.Where(m=> m. Id != BsonObjectId.Empty);
            return await GetAllAsync(filter);
        }
    }
}
