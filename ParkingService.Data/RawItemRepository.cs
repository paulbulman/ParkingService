﻿namespace ParkingService.Data
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Amazon.DynamoDBv2;
    using Amazon.DynamoDBv2.DataModel;
    using Amazon.DynamoDBv2.DocumentModel;
    using Amazon.S3;
    using Amazon.S3.Model;
    using NodaTime;
    using NodaTime.Text;

    public interface IRawItemRepository
    {
        Task<string> GetConfiguration();

        Task<IReadOnlyCollection<RawItem>> GetRequests(YearMonth yearMonth);

        Task<IReadOnlyCollection<RawItem>> GetReservations(YearMonth yearMonth);

        Task<string> GetScheduledTasks();

        Task<IReadOnlyCollection<RawItem>> GetUsers();

        Task SaveItems(IEnumerable<RawItem> rawItems);

        Task SaveScheduledTasks(string rawData);
    }

    public class RawItemRepository : IRawItemRepository
    {
        private const string ScheduledTasksObjectKey = "scheduledTasks.json";

        private readonly IAmazonDynamoDB dynamoDbClient;

        private readonly IAmazonS3 s3Client;

        public RawItemRepository(IAmazonDynamoDB dynamoDbClient, IAmazonS3 s3Client)
        {
            this.dynamoDbClient = dynamoDbClient;
            this.s3Client = s3Client;
        }

        private static string BucketName => Environment.GetEnvironmentVariable("BUCKET_NAME");

        private static string TableName => Environment.GetEnvironmentVariable("TABLE_NAME");

        public async Task<string> GetConfiguration() => await GetBucketData("configuration.json");

        public async Task<IReadOnlyCollection<RawItem>> GetRequests(YearMonth yearMonth)
        {
            var hashKeyValue = $"REQUESTS#{YearMonthPattern.Iso.Format(yearMonth)}";

            return await QuerySecondaryIndex(hashKeyValue);
        }

        public async Task<IReadOnlyCollection<RawItem>> GetReservations(YearMonth yearMonth)
        {
            using var context = new DynamoDBContext(dynamoDbClient);

            var config = new DynamoDBOperationConfig
            {
                OverrideTableName = TableName
            };

            const string HashKeyValue = "GLOBAL";
            var conditionValue = $"RESERVATIONS#{YearMonthPattern.Iso.Format(yearMonth)}";
            var query = context.QueryAsync<RawItem>(HashKeyValue, QueryOperator.Equal, new[] { conditionValue }, config);

            return await query.GetRemainingAsync();
        }

        public async Task<string> GetScheduledTasks() => await GetBucketData(ScheduledTasksObjectKey);

        public async Task<IReadOnlyCollection<RawItem>> GetUsers()
        {
            const string HashKeyValue = "PROFILE";

            return await QuerySecondaryIndex(HashKeyValue);
        }

        public async Task SaveItems(IEnumerable<RawItem> rawItems)
        {
            using var context = new DynamoDBContext(dynamoDbClient);

            var config = new DynamoDBOperationConfig
            {
                OverrideTableName = TableName
            };

            foreach (var rawItem in rawItems)
            {
                await context.SaveAsync(rawItem, config);
            }
        }

        public async Task SaveScheduledTasks(string rawData) => await SaveBucketData(ScheduledTasksObjectKey, rawData);

        private async Task<string> GetBucketData(string objectKey)
        {
            var request = new GetObjectRequest
            {
                BucketName = BucketName,
                Key = objectKey
            };

            using var response = await s3Client.GetObjectAsync(request);

            await using var responseStream = response.ResponseStream;

            using var reader = new StreamReader(responseStream);

            return await reader.ReadToEndAsync();
        }

        private async Task SaveBucketData(string objectKey, string rawData) =>
            await s3Client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = BucketName,
                Key = objectKey,
                ContentBody = rawData
            });

        private async Task<IReadOnlyCollection<RawItem>> QuerySecondaryIndex(string hashKeyValue)
        {
            const string SecondaryIndexName = "SK-PK-index";

            using var context = new DynamoDBContext(dynamoDbClient);

            var config = new DynamoDBOperationConfig
            {
                IndexName = SecondaryIndexName,
                OverrideTableName = TableName
            };

            var query = context.QueryAsync<RawItem>(hashKeyValue, config);

            return await query.GetRemainingAsync();
        }
    }
}