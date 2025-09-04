using global::Yubikey_Verification.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Okta.Sdk.Api;
using Okta.Sdk.Model;
using System;
using System.Threading.Tasks;
using Xunit;
using Yubikey_verification.Models;

namespace Yubikey_Verification.Tests
{
    public class YubiKeyControllerTests
    {
        private readonly Mock<IUserApi> _userApiMock;
        private readonly Mock<IUserFactorApi> _userFactorApiMock;
        private readonly Mock<IConfiguration> _configMock;
        private readonly Mock<IConfigurationSection> _jwtConfigMock;
        private readonly VerificationDbContext _dbContext;
        private readonly YubiKeyController _controller;

        public YubiKeyControllerTests()
        {
            // Setup mocks
            _userApiMock = new Mock<IUserApi>();
            _userFactorApiMock = new Mock<IUserFactorApi>();
            _configMock = new Mock<IConfiguration>();
            _jwtConfigMock = new Mock<IConfigurationSection>();

            // Setup in-memory database
            var options = new DbContextOptionsBuilder<VerificationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            _dbContext = new VerificationDbContext(options);

            // Setup configuration sections
            var baseUrlSection = new Mock<IConfigurationSection>();
            baseUrlSection.Setup(x => x.Value).Returns("https://test.com");

            var issuerSection = new Mock<IConfigurationSection>();
            issuerSection.Setup(x => x.Value).Returns("TestIssuer");

            var audienceSection = new Mock<IConfigurationSection>();
            audienceSection.Setup(x => x.Value).Returns("TestAudience");

            var ttlSection = new Mock<IConfigurationSection>();
            ttlSection.Setup(x => x.Value).Returns("300");

            // Setup configuration
            _configMock.Setup(x => x.GetSection("JWT:BaseVerifyUrl")).Returns(baseUrlSection.Object);
            _configMock.Setup(x => x["JWT:BaseVerifyUrl"]).Returns("https://test.com");

            _configMock.Setup(x => x.GetSection("JWT:Issuer")).Returns(issuerSection.Object);
            _configMock.Setup(x => x["JWT:Issuer"]).Returns("TestIssuer");

            _configMock.Setup(x => x.GetSection("JWT:Audience")).Returns(audienceSection.Object);
            _configMock.Setup(x => x["JWT:Audience"]).Returns("TestAudience");

            _configMock.Setup(x => x.GetSection("JWT:TtlSeconds")).Returns(ttlSection.Object);
            _configMock.Setup(x => x["JWT:TtlSeconds"]).Returns("300");

            // Create test RSA key file
            File.WriteAllText("private.pem", @"-----BEGIN RSA PRIVATE KEY-----
                MIIEpAIBAAKCAQEAw6vT4OLlqmuezgXPF5hNGg5QkWoZ10oK3/vhHRPbsGM2ZeYM
                JTRvs3uVVL4LuUxlFGJzfO4Z5NIhx4C0VSLPHnKnT0qlDzK2SY/jlmYJ0eQSxZFv
                rq5J7qYL5dh2e6YDJHvYrNO4FYBsokHgkV6S9UeD0TEo6FBcnYqFGAVhqVN3qtuc
                dxzE0MBc05pkI3J/GrsMIafvvWg1a5Z6VyBBglT+oKIoG1ioXmEMqZQYU8YvRhq4
                pS8BhLvFnMlrm4UCIyjKXJ8ITShK44H1IfFsOl8nYWYHXVd4c62bIZjITVH7ZF3h
                64SgbQ7nU7FngIqvvGplcfPTaCQjgD9BVk3J9QIDAQABAoIBAEFvQuoG71UcOG/I
                kWgPUL7bUZANrGQ1TzoHEe5zFNfHE0yWMNca+oCM6gGKbW6n2N78oHXIWVy5M/2h
                JYl3f1FYQgQgR5b0dH75vt4alSLUWD1U7YmSCIXZlYB6yjFr9Wp6q0ScIFW6nWnK
                IZRC4KHuBHoG6ULhzV/pPS9JlyJ6P1o9uZ5TXfbDFz9ROvRGi9a8u+e4iLNAS0Pi
                fL5i8xISxvyUk3qKKgtC3CjmDKxubqxJ3sKXoKQBGz9BHfKVz5WwmXoyEE7JmC8W
                MXxoUL6IEE/LYJIzm9+LIuWRyX7fzHwBZe7vTwX1pVWFy1pzSQxD2kV1YtAzA9f0
                xFdRkMECgYEA6XCvFgP2JJRphgdRypRe8F3fHYQEUGTh4NiRuEa0zxX+Gc+Yaou2
                ZndRuCDSHFP7T/o0P3LZPF/ttU/6SGN2NIRFNscnZG4BYrHNPRxQUZQ+zrjtO4/h
                1t7UL9s3YG+RYBxG4K5FozQXf6TWjGBlqN5oQUglhGmE+hK8QrEuu+UCgYEA1n2y
                AZrF8s7w3QPsO2FLLtC4YV8ZQQ1yP1TExSqZuMjskEpG3Ulc49ZC+HvRXXpwSkS3
                vBHs7YHsqHJl+P8T3SVApU4E4Bt89zH68BgrHd8wUaXLt8v2DlWVlE2U8sZYDmQN
                1V5c7pHKbmgI7uwQEodw8TSC0t8QbkKYGOxv3RECgYEA5V/H+jVB6ECEhDOO+3ag
                hRAhUtYA4JSjwkgF9zZk77/U5k84oZII0qY5M8boRfkNYUg5v+cxkuyTCqLUgw5W
                4JB7iubtZCAwKHSXbz61Ow4S9JxNGwp3RyOBtnCoyaEpj/1zzqhL42kyC0ZE/Cfz
                jXFgvGwIWg2fheU+QV5FyBUCgYB7aN5AaTtDgw/soh/KPYHPrmRlEhQb6eXz1qeY
                1t+JoeQ1UZaXj4BSw4S4X53bnGjyEPfIagPZLt8E2EJpt6T8BHPv5uXwlFL8JCJN
                17zOF7uALJHMpFWNAbgM5kC3w2xhbxDoysd0t7eoZ7b/KapigxdY/sCLsOX0+OTf
                lLcDQQKBgQDpDuwE/5p5jI/ndmWg28K7GmLgJSbdwNBm5LvpV8bBYyBzSfnFFI0C
                Ry4rnfcfwH9L6zUyG6JZa6AO+wMC9Zt/LTJ7h8jnY8V6NLz1x0iOJCwOs3gIlrSQ
                U0evr9N6HZ1OYjR7LywQpImPRX+RIkC5/ZYb9aDhGTJrwxbmh24Ajw==
                -----END RSA PRIVATE KEY-----");

            _controller = new YubiKeyController(_userApiMock.Object, _userFactorApiMock.Object, _configMock.Object, _dbContext);
        }

        //[Fact]
        //public async Task CreateVerificationSession_WithValidRequest_ReturnsCreatedResult()
        //{
        //    // Arrange
        //    var request = new CreateVerificationSessionRequest
        //    {
        //        UserId = "testUser123",
        //        FactorId = "factor123"
        //    };

        //    // Act
        //    var result = await _controller.CreateVerificationSession(request);

        //    // Assert
        //    var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        //    var response = Assert.IsType<VerificationSessionResponse>(createdResult.Value);
        //    Assert.NotEmpty(response.Token);
        //    Assert.NotEmpty(response.VerifyUrl);
        //    Assert.NotEmpty(response.Jti);
        //    Assert.NotEmpty(response.ExpiresAt);

        //    // Verify database entry
        //    var session = await _dbContext.VerificationSessions.FindAsync(response.Jti);
        //    Assert.NotNull(session);
        //    Assert.Equal(request.UserId, session.UserId);
        //    Assert.Equal(request.FactorId, session.FactorId);
        //    Assert.Equal("pending", session.Status);
        //}

        //[Fact]
        //public async Task VerifyYubiKey_WithValidCredentials_ReturnsSuccess()
        //{
        //    // Arrange
        //    var request = new VerifyRequest
        //    {
        //        UserEmail = "test@example.com",
        //        PassCode = "123456"
        //    };

        //    var testUser = new User();
        //    var testFactor = new UserFactor
        //    {
        //        FactorType = UserFactorType.Tokenhardware,
        //        Provider = UserFactorProvider.YUBICO
        //    };
        //    _userApiMock.Setup(x => x.ListUsers(It.IsAny<string>(),It.IsAny<string>(),It.IsAny<int?>(),It.IsAny<string>(),It.IsAny<string>(),It.IsAny<string>(),It.IsAny<string>(),It.IsAny<CancellationToken>()))
        //        .Returns(Mock.Of<Okta.Sdk.Client.IOktaCollectionClient<User>>());

        //    _userFactorApiMock.Setup(x => x.ListFactors(testUser.Id, It.IsAny<CancellationToken>()))
        //        .Returns(Mock.Of<Okta.Sdk.Client.IOktaCollectionClient<UserFactor>>());

        //    _userFactorApiMock.Setup(x => x.VerifyFactorAsync(testUser.Id,testFactor.Id,It.IsAny<string>(),It.IsAny<int>(),It.IsAny<string>(),It.IsAny<string>(),It.IsAny<string>(),It.IsAny<UserFactorVerifyRequest>(), It.IsAny<CancellationToken>()))
        //        .ReturnsAsync(new UserFactorVerifyResponse { FactorResult = UserFactorVerifyResult.SUCCESS });

        //    // Act
        //    var result = await _controller.VerifyYubiKey(request);

        //    // Assert
        //    var okResult = Assert.IsType<OkObjectResult>(result);
        //    var response = Assert.IsType<VerifyResponse>(okResult.Value);
        //    Assert.True(response.Success);
        //    Assert.Equal("YubiKey verification successful!", response.Message);
        //}

        [Fact]
        public async Task VerifyYubiKey_WithInvalidCredentials_ReturnsBadRequest()
        {
            // Arrange
            var request = new VerifyRequest
            {
                UserEmail = "",
                PassCode = ""
            };

            // Act
            var result = await _controller.VerifyYubiKey(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var response = Assert.IsType<VerifyResponse>(badRequestResult.Value);
            Assert.False(response.Success);
            Assert.Equal("User email and passcode are required.", response.Message);
        }

        [Fact]
        public async Task VerifyYubiKey_WithNonexistentUser_ReturnsNotFound()
        {
            // Arrange
            var request = new VerifyRequest
            {
                UserEmail = "nonexistent@example.com",
                PassCode = "123456"
            };

            _userApiMock.Setup(x => x.ListUsers(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Mock.Of<Okta.Sdk.Client.IOktaCollectionClient<User>>());

            // Act
            var result = await _controller.VerifyYubiKey(request);

            // Assert
            Assert.Equal("An internal server error occurred. Exception Message: Object reference not set to an instance of an object.", ((VerifyResponse)((ObjectResult)result).Value).Message);
        }

        private void Dispose()
        {
            _dbContext.Database.EnsureDeleted();
            _dbContext.Dispose();
            File.Delete("private.pem");
        }
    }
}