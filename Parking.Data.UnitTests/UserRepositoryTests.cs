﻿// ReSharper disable StringLiteralTypo
namespace Parking.Data.UnitTests
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Aws;
    using Model;
    using Moq;
    using TestHelpers;
    using Xunit;

    public static class UserRepositoryTests
    {
        [Fact]
        public static async Task Create_user_creates_new_identity_provider_user()
        {
            var mockIdentityProvider = new Mock<IIdentityProvider>();

            var userRepository = new UserRepository(Mock.Of<IDatabaseProvider>(), mockIdentityProvider.Object);

            var user = CreateUser.With(
                userId: string.Empty,
                emailAddress: "john.doe@example.com",
                firstName: "John",
                lastName: "Doe");

            await userRepository.CreateUser(user);

            mockIdentityProvider.Verify(p => p.CreateUser("john.doe@example.com", "John", "Doe"), Times.Once);
        }

        [Fact]
        public static async Task Create_user_creates_new_database_user()
        {
            var mockIdentityProvider = new Mock<IIdentityProvider>();

            mockIdentityProvider
                .Setup(p => p.CreateUser(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync("User1");

            var mockDatabaseProvider = new Mock<IDatabaseProvider>();

            var userRepository = new UserRepository(mockDatabaseProvider.Object, mockIdentityProvider.Object);

            var user = CreateUser.With(
                userId: string.Empty,
                alternativeRegistrationNumber: "A999XYZ",
                commuteDistance: 1.23m,
                emailAddress: "john.doe@example.com",
                firstName: "John",
                lastName: "Doe",
                registrationNumber: "AB12CDE"
            );

            await userRepository.CreateUser(user);

            mockDatabaseProvider.Verify(
                p => p.SaveItem(It.Is<RawItem>(actual =>
                    actual.PrimaryKey == "USER#User1" &&
                    actual.SortKey == "PROFILE" &&
                    actual.AlternativeRegistrationNumber == "A999XYZ" &&
                    actual.CommuteDistance == 1.23m &&
                    actual.EmailAddress == "john.doe@example.com" &&
                    actual.FirstName == "John" &&
                    actual.LastName == "Doe" &&
                    actual.RegistrationNumber == "AB12CDE")),
                Times.Once);
        }

        [Fact]
        public static async Task Create_user_returns_newly_created_user()
        {
            var mockIdentityProvider = new Mock<IIdentityProvider>();
            mockIdentityProvider
                .Setup(p => p.CreateUser(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync("User1");

            var userRepository = new UserRepository(Mock.Of<IDatabaseProvider>(), mockIdentityProvider.Object);

            var user = CreateUser.With(
                userId: string.Empty,
                alternativeRegistrationNumber: "A999XYZ",
                commuteDistance: 1.23m,
                emailAddress: "john.doe@example.com",
                firstName: "John",
                lastName: "Doe",
                registrationNumber: "AB12CDE"
            );

            var result = await userRepository.CreateUser(user);

            Assert.NotNull(result);

            CheckUser(new[] { result }, "User1", "A999XYZ", 1.23m, "john.doe@example.com", "John", "Doe", "AB12CDE");
        }

        [Fact]
        public static async Task UserExists_returns_true_when_user_with_given_ID_exists()
        {
            const string UserId = "User1";

            var mockDatabaseProvider = new Mock<IDatabaseProvider>(MockBehavior.Strict);

            var rawItem = RawItem.CreateUser(
                primaryKey: "USER#User1",
                sortKey: "PROFILE",
                alternativeRegistrationNumber: "W789XYZ",
                commuteDistance: 1.23m,
                emailAddress: "1@abc.com",
                firstName: "Sean",
                lastName: "Cantera",
                registrationNumber: "AB12CDE");

            mockDatabaseProvider
                .Setup(p => p.GetUser(UserId))
                .ReturnsAsync(rawItem);

            var userRepository = new UserRepository(mockDatabaseProvider.Object, Mock.Of<IIdentityProvider>());

            var result = await userRepository.UserExists(UserId);

            Assert.True(result);
        }

        [Fact]
        public static async Task UserExists_returns_false_when_no_user_with_given_ID_exists()
        {
            const string UserId = "User1";

            var mockDatabaseProvider = new Mock<IDatabaseProvider>(MockBehavior.Strict);

            mockDatabaseProvider.Setup(p => p.GetUser(UserId)).ReturnsAsync((RawItem?)null);

            var userRepository = new UserRepository(mockDatabaseProvider.Object, Mock.Of<IIdentityProvider>());

            var result = await userRepository.UserExists(UserId);

            Assert.False(result);
        }

        [Fact]
        public static async Task GetUser_returns_null_when_requested_user_does_not_exist()
        {
            var mockDatabaseProvider = new Mock<IDatabaseProvider>(MockBehavior.Strict);

            mockDatabaseProvider.Setup(p => p.GetUser(It.IsAny<string>())).ReturnsAsync((RawItem?)null);

            var userRepository = new UserRepository(mockDatabaseProvider.Object, Mock.Of<IIdentityProvider>());

            var result = await userRepository.GetUser("UserId");

            Assert.Null(result);
        }

        [Fact]
        public static async Task GetUser_converts_raw_item_to_user()
        {
            const string UserId = "User1";

            var mockDatabaseProvider = new Mock<IDatabaseProvider>(MockBehavior.Strict);

            var rawItem = RawItem.CreateUser(
                primaryKey: "USER#User1",
                sortKey: "PROFILE",
                alternativeRegistrationNumber: "W789XYZ",
                commuteDistance: 1.23m,
                emailAddress: "1@abc.com",
                firstName: "Sean",
                lastName: "Cantera",
                registrationNumber: "AB12CDE");

            mockDatabaseProvider.Setup(p => p.GetUser(UserId)).ReturnsAsync(rawItem);

            var userRepository = new UserRepository(mockDatabaseProvider.Object, Mock.Of<IIdentityProvider>());

            var result = await userRepository.GetUser(UserId);

            Assert.NotNull(result);

            CheckUser(new[] { result! }, UserId, "W789XYZ", 1.23m, "1@abc.com", "Sean", "Cantera", "AB12CDE");
        }

        [Fact]
        public static async Task GetUsers_converts_raw_items_to_users()
        {
            var mockDatabaseProvider = new Mock<IDatabaseProvider>(MockBehavior.Strict);

            var rawItems = new[]
            {
                RawItem.CreateUser(primaryKey: "USER#Id1", sortKey: "PROFILE", alternativeRegistrationNumber: "W789XYZ", commuteDistance: 1.23m, emailAddress: "1@abc.com", firstName: "Sean", lastName: "Cantera", registrationNumber: "AB12CDE"),
                RawItem.CreateUser(primaryKey: "USER#Id2", sortKey: "PROFILE", alternativeRegistrationNumber: null, commuteDistance: 2.34m, emailAddress: "2@abc.com", firstName: "Clyde", lastName: "Memory", registrationNumber: "FG34HIJ"),
                RawItem.CreateUser(primaryKey: "USER#Id3", sortKey: "PROFILE", alternativeRegistrationNumber: null, commuteDistance: null, emailAddress: "3@xyz.co.uk", firstName: "Kalle", lastName: "Rochewell", registrationNumber: null)
            };
            mockDatabaseProvider.Setup(p => p.GetUsers()).ReturnsAsync(rawItems);

            var userRepository = new UserRepository(mockDatabaseProvider.Object, Mock.Of<IIdentityProvider>());

            var result = await userRepository.GetUsers();

            Assert.NotNull(result);

            Assert.Equal(rawItems.Length, result.Count);

            CheckUser(result, "Id1", "W789XYZ", 1.23m, "1@abc.com", "Sean", "Cantera", "AB12CDE");
            CheckUser(result, "Id2", null, 2.34m, "2@abc.com", "Clyde", "Memory", "FG34HIJ");
            CheckUser(result, "Id3", null, null, "3@xyz.co.uk", "Kalle", "Rochewell", null);
        }

        [Fact]
        public static async Task GetTeamLeaderUsers_filters_team_leader_users()
        {
            var mockDatabaseProvider = new Mock<IDatabaseProvider>(MockBehavior.Strict);

            var rawUsers = new[]
            {
                RawItem.CreateUser(primaryKey: "USER#Id1", sortKey: "PROFILE", alternativeRegistrationNumber: null, commuteDistance: null, emailAddress: "1@abc.com", firstName: "Shalom", lastName: "Georgiades", registrationNumber: null),
                RawItem.CreateUser(primaryKey: "USER#Id2", sortKey: "PROFILE", alternativeRegistrationNumber: null, commuteDistance: null, emailAddress: "2@abc.com", firstName: "Randolf", lastName: "Blogg", registrationNumber: null),
                RawItem.CreateUser(primaryKey: "USER#Id3", sortKey: "PROFILE", alternativeRegistrationNumber: null, commuteDistance: null, emailAddress: "3@xyz.co.uk", firstName: "Kris", lastName: "Whibley", registrationNumber: null)
            };
            mockDatabaseProvider.Setup(p => p.GetUsers()).ReturnsAsync(rawUsers);

            var mockIdentityProvider = new Mock<IIdentityProvider>(MockBehavior.Strict);

            var rawTeamLeaderUserIds = new[] { "Id1", "Id3" };
            mockIdentityProvider.Setup(p => p.GetUserIdsInGroup("TeamLeader")).ReturnsAsync(rawTeamLeaderUserIds);

            var userRepository = new UserRepository(mockDatabaseProvider.Object, mockIdentityProvider.Object);

            var result = await userRepository.GetTeamLeaderUsers();

            Assert.NotNull(result);

            Assert.Equal(rawTeamLeaderUserIds.Length, result.Count);

            CheckUser(result, "Id1", null, null, "1@abc.com", "Shalom", "Georgiades", null);
            CheckUser(result, "Id3", null, null, "3@xyz.co.uk", "Kris", "Whibley", null);
        }

        [Fact]
        public static async Task Save_user_updates_user_in_identity_provider()
        {
            var user = CreateUser.With(userId: "User1", firstName: "John", lastName: "Doe");

            var mockIdentityProvider = new Mock<IIdentityProvider>();

            var userRepository = new UserRepository(Mock.Of<IDatabaseProvider>(), mockIdentityProvider.Object);

            await userRepository.SaveUser(user);

            mockIdentityProvider.Verify(p => p.UpdateUser("User1", "John", "Doe"), Times.Once);
        }

        [Fact]
        public static async Task Save_user_updates_user_in_database()
        {
            var user = CreateUser.With(
                userId: "User1",
                alternativeRegistrationNumber: "A999XYZ",
                commuteDistance: 12.3m,
                emailAddress: "john.doe@example.com",
                firstName: "John",
                lastName: "Doe",
                registrationNumber: "AB12CDE");

            var mockDatabaseProvider = new Mock<IDatabaseProvider>();

            var userRepository = new UserRepository(mockDatabaseProvider.Object, Mock.Of<IIdentityProvider>());

            await userRepository.SaveUser(user);

            mockDatabaseProvider.Verify(
                p => p.SaveItem(It.Is<RawItem>(actual =>
                    actual.PrimaryKey == "USER#User1" &&
                    actual.SortKey == "PROFILE" &&
                    actual.AlternativeRegistrationNumber == "A999XYZ" &&
                    actual.CommuteDistance == 12.3m &&
                    actual.EmailAddress == "john.doe@example.com" &&
                    actual.FirstName == "John" &&
                    actual.LastName == "Doe" &&
                    actual.RegistrationNumber == "AB12CDE")),
                Times.Once);
        }

        private static void CheckUser(
            IEnumerable<User> result,
            string expectedUserId,
            string? expectedAlternativeRegistrationNumber,
            decimal? expectedCommuteDistance,
            string expectedEmailAddress,
            string expectedFirstName,
            string expectedLastName,
            string? expectedRegistrationNumber)
        {
            var actual = result.Where(u =>
                u.UserId == expectedUserId &&
                u.AlternativeRegistrationNumber == expectedAlternativeRegistrationNumber &&
                u.CommuteDistance == expectedCommuteDistance &&
                u.EmailAddress == expectedEmailAddress &&
                u.FirstName == expectedFirstName &&
                u.LastName == expectedLastName &&
                u.RegistrationNumber == expectedRegistrationNumber);

            Assert.Single(actual);
        }
    }
}