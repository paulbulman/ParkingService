﻿namespace Parking.Data.UnitTests
{
    using System.Threading.Tasks;
    using Aws;
    using Moq;
    using Xunit;

    public static class ConfigurationRepositoryTests
    {
        [Fact]
        public static async Task Converts_raw_data_to_configuration()
        {
            var mockStorageProvider = new Mock<IStorageProvider>(MockBehavior.Strict);

            var rawData = "{\r\n  \"NearbyDistance\": 3.5,\r\n  \"ShortLeadTimeSpaces\": 2,\r\n  \"TotalSpaces\": 9\r\n}";
            mockStorageProvider.Setup(p => p.GetConfiguration()).ReturnsAsync(rawData);
            
            var configurationRepository = new ConfigurationRepository(mockStorageProvider.Object);

            var result = await configurationRepository.GetConfiguration();

            Assert.NotNull(result);

            Assert.Equal(3.5m, result.NearbyDistance);
            Assert.Equal(2, result.ShortLeadTimeSpaces);
            Assert.Equal(9, result.TotalSpaces);
        }
    }
}