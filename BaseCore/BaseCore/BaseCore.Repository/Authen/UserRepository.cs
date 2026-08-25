
using Microsoft.EntityFrameworkCore;
using BaseCore.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
namespace BaseCore.Repository.Authen
{
    public interface IUserRepository
    {
        Task<User> GetByUsernameAsync(string username);
        Task<User> GetByIdAsync(string id);
        Task<List<User>> GetAllAsync();
        Task CreateAsync(User user);
        Task UpdateAsync(User user);
        Task DeleteAsync(string id);
        Task<(List<User> Users, int TotalCount)> SearchAsync(string keyword, int page, int pageSize);
    }

    public class UserRepository : IUserRepository
    {
        private readonly MySqlDbContext _context;

        public UserRepository(MySqlDbContext context)
        {
            _context = context;
        }

        public async Task<User> GetByUsernameAsync(string username)
        {
            return await _context.Users
               .FirstOrDefaultAsync(u => u.UserName == username && u.IsActive);
                
        }

        public async Task<User> GetByIdAsync(string id)
        {
            return await _context.Users
               .FirstOrDefaultAsync(u => u.Id == id);
        }

        public async Task<List<User>> GetAllAsync()
        {
            return await _context.Users
                .Where(u => u.IsActive)
                .ToListAsync();
        }

        public async Task CreateAsync(User user)
        {
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(User user)
        {
            _context.Users.Update(user);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(string id)
        {
            var user = await GetByIdAsync(id);
            if (user != null)
            {// Xóa m?m: Chuy?n tr?ng thái IsActive v? false
                user.IsActive = false; 
                _context.Users.Update(user);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<(List<User> Users, int TotalCount)> SearchAsync(string keyword, int page, int pageSize)
        {
            var query = _context.Users.Where(u => u.IsActive);
            

            if (!string.IsNullOrEmpty(keyword))
            {
                var keywordLower = keyword.ToLower();
                query = query.Where(u => u.UserName.ToLower().Contains(keywordLower) ||
                                       u.Name.ToLower().Contains(keywordLower) ||
                                       u.Email.ToLower().Contains(keywordLower) ||
                                       u.Phone.ToLower().Contains(keywordLower));
            }

            var totalCount = await query.CountAsync();
            var users = await query
                .OrderByDescending(u => u.Created)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (users, totalCount);
        }
    }
}
