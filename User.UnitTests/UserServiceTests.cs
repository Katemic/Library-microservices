using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using UserService.Data;
using UserService.Models;
using UserService.Services;

namespace UserService.UnitTests
{
    [TestClass]
    public class UserServiceTests
    {
        private SqliteConnection _connection = null!;
        private UserDbContext _context = null!;
        private Services.UserService _service = null!;
        private HttpClient _httpClient = null!;

        // Prepare an isolated test database before each test.
        [TestInitialize]
        public void Setup()
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<UserDbContext>()
                .UseSqlite(_connection)
                .Options;

            _context = new UserDbContext(options);
            _context.Database.EnsureCreated();

            var configuration = new ConfigurationBuilder().Build();

            _httpClient = new HttpClient();

            var keycloakClient = new KeycloakUserClient(
                _httpClient,
                configuration);

            _service = new Services.UserService(
                _context,
                keycloakClient);
        }

        // Create reusable test data.
        private async Task<User> CreateTestUserAsync()
        {
            return await _service.CreateUserAsync(new User
            {
                FirstName = "Test",
                LastName = "User",
                Email = "test@example.com",
                CPR = "010100-1234",
                TLF = "12345678"
            });
        }

        // Test that a new user is active and starts at version 1.
        [TestMethod]
        public async Task CreateUser_ShouldCreateActiveUser()
        {
            // Arrange
            var user = new User
            {
                FirstName = "Test",
                LastName = "User",
                Email = "test@example.com",
                IsActive = false
            };

            // Act
            var result = await _service.CreateUserAsync(user);

            // Assert
            Assert.IsTrue(result.Id > 0);
            Assert.IsTrue(result.IsActive);
            Assert.AreEqual(1L, result.Version);
        }

        // Test that CPR is normalized correctly.
        [TestMethod]
        public async Task CreateUser_ShouldNormalizeCpr()
        {
            // Arrange
            var user = new User
            {
                Email = "test@example.com",
                CPR = "010100-1234"
            };

            // Act
            var result = await _service.CreateUserAsync(user);

            // Assert
            Assert.AreEqual("0101001234", result.CPR);
        }

        // Test that invalid CPR is rejected.
        [TestMethod]
        public async Task CreateUser_ShouldRejectInvalidCpr()
        {
            // Arrange
            var user = new User
            {
                Email = "test@example.com",
                CPR = "2302443"
            };

            // Act & Assert
            await Assert.ThrowsExactlyAsync<ArgumentException>(
                () => _service.CreateUserAsync(user));

            Assert.AreEqual(0, await _context.Users.CountAsync());
        }

        // Test that an empty email is rejected.
        [TestMethod]
        public async Task CreateUser_ShouldRejectEmptyEmail()
        {
            // Arrange
            var user = new User
            {
                FirstName = "Test",
                LastName = "User",
                Email = ""
            };

            // Act & Assert
            await Assert.ThrowsExactlyAsync<ArgumentException>(
                () => _service.CreateUserAsync(user));

            Assert.AreEqual(0, await _context.Users.CountAsync());
        }

        // Test that an existing Keycloak profile is not duplicated.
        [TestMethod]
        public async Task GetOrCreateUser_ShouldPreventDuplicates()
        {
            // Arrange
            var first = await _service.GetOrCreateKeycloakUserAsync(
                "test-subject",
                "Test",
                "User",
                "test@example.com",
                null,
                "12345678");

            // Act
            var second = await _service.GetOrCreateKeycloakUserAsync(
                "test-subject",
                "Test",
                "User",
                "test@example.com",
                null,
                "12345678");

            // Assert
            Assert.IsTrue(first.Created);
            Assert.IsFalse(second.Created);
            Assert.AreEqual(first.User.Id, second.User.Id);
            Assert.AreEqual(1, await _context.Users.CountAsync());
        }

        // Test that an active user's profile can be updated.
        [TestMethod]
        public async Task UpdateUser_ShouldUpdateProfile()
        {
            // Arrange
            var user = await CreateTestUserAsync();

            // Act
            var result = await _service.UpdateUserAsync(
                user.Id,
                "Updated",
                "TestUser",
                "updated@example.com",
                "87654321");

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("Updated", result.FirstName);
            Assert.AreEqual("TestUser", result.LastName);
            Assert.AreEqual("updated@example.com", result.Email);
            Assert.AreEqual("87654321", result.TLF);
            Assert.AreEqual("0101001234", result.CPR);
        }

        // Test that inactive users cannot be updated.
        [TestMethod]
        public async Task UpdateUser_ShouldRejectInactiveUser()
        {
            // Arrange
            var user = await CreateTestUserAsync();
            await _service.DeactivateUserAsync(user.Id);

            // Act
            var result = await _service.UpdateUserAsync(
                user.Id,
                "Updated",
                "User",
                "updated@example.com",
                "87654321");

            // Assert
            Assert.IsNull(result);
            Assert.AreEqual("test@example.com", user.Email);
        }

        // Test that a nonexistent user cannot be updated.
        [TestMethod]
        public async Task UpdateUser_ShouldRejectNonexistentUser()
        {
            // Arrange
            int nonexistentId = 999;

            // Act
            var result = await _service.UpdateUserAsync(
                nonexistentId,
                "Test",
                "User",
                "test@example.com",
                "12345678");

            // Assert
            Assert.IsNull(result);
        }

        // Test that deactivation performs a soft delete.
        [TestMethod]
        public async Task DeactivateUser_ShouldSetIsActiveToFalse()
        {
            // Arrange
            var user = await CreateTestUserAsync();

            // Act
            var result = await _service.DeactivateUserAsync(user.Id);

            _context.ChangeTracker.Clear();

            var storedUser = await _service.GetUserByIdAsync(user.Id);

            // Assert
            Assert.IsTrue(result);
            Assert.IsNotNull(storedUser);
            Assert.IsFalse(storedUser.IsActive);
        }

        // Test that deactivating a nonexistent user returns false.
        [TestMethod]
        public async Task DeactivateUser_ShouldReturnFalseForMissingUser()
        {
            // Arrange
            int nonexistentId = 999;

            // Act
            var result = await _service.DeactivateUserAsync(nonexistentId);

            // Assert
            Assert.IsFalse(result);
        }

        // Test that changing an email increments the user version.
        [TestMethod]
        public async Task UpdateEmail_ShouldIncrementVersion()
        {
            // Arrange
            var user = await CreateTestUserAsync();
            var previousVersion = user.Version;

            // Act
            var result = await _service.UpdateUserAsync(
                user.Id,
                user.FirstName,
                user.LastName,
                "new@example.com",
                user.TLF);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(previousVersion + 1, result.Version);
        }

        // Test that changing only name and phone preserves the version.
        [TestMethod]
        public async Task UpdateProfile_ShouldPreserveVersion()
        {
            // Arrange
            var user = await CreateTestUserAsync();
            var previousVersion = user.Version;

            // Act
            var result = await _service.UpdateUserAsync(
                user.Id,
                "NewName",
                user.LastName,
                user.Email,
                "87654321");

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(previousVersion, result.Version);
        }

        // Release resources after each test.
        [TestCleanup]
        public void Cleanup()
        {
            _context.Dispose();
            _connection.Dispose();
            _httpClient.Dispose();
        }
    }
}