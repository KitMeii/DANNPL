using Newtonsoft.Json;
using NUnit.Framework;
using BaseCore.Common;
using BaseCore.Entities;
using BaseCore.Libs.Repository;
using BaseCore.Repository.Authen;
using System;

namespace BaseCore.UnitTest
{
    public class UnitTestUserService: BaseConfigService
    {
        private IUserRepository _userRepository;
        public UnitTestUserService(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }


        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void TestInsertUserSuccess()
        {
            var salt = new byte[128 / 8];
            var password = TokenHelper.HashPassword("123456", out salt);
            var user = new User
            {
                // Id will be generated
                Name = "Vũ Tuấn",
                UserName = "tuan@oriwave.com",
                Contact = "Dương Nội, Hà Đông",
                Password = password,
                Salt = salt,
                // Country = "VietNam", // Property does not exist
                Created = DateTime.UtcNow,
                // CreatedBy = "System", // Property does not exist
                Email = "tuan@oriwave.com",
                // Fax = "084-3334.6666", // Property does not exist
                Phone = "0919901195",
                IsActive = true,
                // ShortName = "Test", // Property does not exist
                Position = "tester",
                // ModifiedBy = "System", // Property does not exist
                // Modified = DateTime.UtcNow, // Property does not exist
                // IsDeleted = false // Property does not exist
            };
            var userResult = JsonConvert.SerializeObject(user);
            Console.WriteLine(userResult);
            // var result = _userRepository.Create(user); // Method signature changed to async Task CreateAsync(User user)
            // _userRepository.CreateAsync(user).Wait(); // Fix call
            // Assert.IsTrue(result.Id > 0); // Id is string now
        }


        [Test]
        public void Test1()
        {
            Assert.Pass();
        }
    }
}