﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.Azure.Cosmos.Fluent;
    using System.Net;
    using System.Linq;
    using Newtonsoft.Json.Linq;

    // Similar tests to CosmosContainerTests but with Fluent syntax
    [TestClass]
    public class CosmosContainerSettingsFluentTests : BaseCosmosClientHelper
    {
        private static long ToEpoch(DateTime dateTime) => (long)(dateTime - (new DateTime(1970, 1, 1))).TotalSeconds;

        [TestInitialize]
        public async Task TestInitialize()
        {
            await base.TestInit();
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await base.TestCleanup();
        }

        [TestMethod]
        public async Task ContainerContractTest()
        {
            CosmosContainerResponse response = 
                await this.database.Containers.Create(new Guid().ToString(), "/id")
                    .ApplyAsync();
            Assert.IsNotNull(response);
            Assert.IsTrue(response.RequestCharge > 0);
            Assert.IsNotNull(response.Headers);
            Assert.IsNotNull(response.Headers.ActivityId);

            CosmosContainerSettings containerSettings = response.Resource;
            Assert.IsNotNull(containerSettings.Id);
            Assert.IsNotNull(containerSettings.ResourceId);
            Assert.IsNotNull(containerSettings.ETag);
            Assert.IsTrue(containerSettings.LastModified.HasValue);

            Assert.IsTrue(containerSettings.LastModified.Value > new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), containerSettings.LastModified.Value.ToString());
        }

        [TestMethod]
        public async Task PartitionedCRUDTest()
        {
            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath = "/users";

            CosmosContainerResponse containerResponse =
                await this.database.Containers.Create(containerName, partitionKeyPath)
                    .WithIndexingPolicy()
                        .WithIndexingMode(Cosmos.IndexingMode.None)
                        .WithoutAutomaticIndexing()
                        .Attach()
                    .ApplyAsync();

            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
            Assert.AreEqual(containerName, containerResponse.Resource.Id);
            Assert.AreEqual(partitionKeyPath, containerResponse.Resource.PartitionKey.Paths.First());
            CosmosContainer cosmosContainer = containerResponse;
            Assert.AreEqual(Cosmos.IndexingMode.None, containerResponse.Resource.IndexingPolicy.IndexingMode);
            Assert.IsFalse(containerResponse.Resource.IndexingPolicy.Automatic);

            containerResponse = await cosmosContainer.ReadAsync();
            Assert.AreEqual(HttpStatusCode.OK, containerResponse.StatusCode);
            Assert.AreEqual(containerName, containerResponse.Resource.Id);
            Assert.AreEqual(partitionKeyPath, containerResponse.Resource.PartitionKey.Paths.First());
            Assert.AreEqual(Cosmos.IndexingMode.None, containerResponse.Resource.IndexingPolicy.IndexingMode);
            Assert.IsFalse(containerResponse.Resource.IndexingPolicy.Automatic);

            containerResponse = await containerResponse.Container.DeleteAsync();
            Assert.AreEqual(HttpStatusCode.NoContent, containerResponse.StatusCode);
        }

        [TestMethod]
        public async Task ThroughputTest()
        {
            int expectedThroughput = 2400;
            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath = "/users";

            CosmosContainerResponse containerResponse 
                = await this.database.Containers.Create(containerName, partitionKeyPath)
                .WithThroughput(expectedThroughput)
                .ApplyAsync();
            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
            CosmosContainer cosmosContainer = this.database.Containers[containerName];

            int? readThroughput = await cosmosContainer.ReadProvisionedThroughputAsync();
            Assert.IsNotNull(readThroughput);
            Assert.AreEqual(expectedThroughput, readThroughput);

            containerResponse = await cosmosContainer.DeleteAsync();
            Assert.AreEqual(HttpStatusCode.NoContent, containerResponse.StatusCode);
        }

        [TestMethod]
        public async Task TimeToLiveTest()
        {
            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath = "/users";
            int timeToLiveInSeconds = 10;
            CosmosContainerResponse containerResponse = await this.database.Containers.Create(containerName, partitionKeyPath)
                .WithDefaultTimeToLive(timeToLiveInSeconds)
                .ApplyAsync();

            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
            CosmosContainer cosmosContainer = containerResponse;
            CosmosContainerSettings responseSettings = containerResponse;

            Assert.AreEqual(timeToLiveInSeconds, responseSettings.DefaultTimeToLive);

            CosmosContainerResponse readResponse = await cosmosContainer.ReadAsync();
            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
            Assert.AreEqual(timeToLiveInSeconds, readResponse.Resource.DefaultTimeToLive);

            JObject itemTest = JObject.FromObject(new { id = Guid.NewGuid().ToString(), users = "testUser42" });
            CosmosItemResponse<JObject> createResponse = await cosmosContainer.Items.CreateItemAsync<JObject>(partitionKey: itemTest["users"].ToString(), item: itemTest);
            JObject responseItem = createResponse;
            Assert.IsNull(responseItem["ttl"]);

            containerResponse = await cosmosContainer.DeleteAsync();
            Assert.AreEqual(HttpStatusCode.NoContent, containerResponse.StatusCode);
        }

        [TestMethod]
        public async Task NoPartitionedCreateFail()
        {
            string containerName = Guid.NewGuid().ToString();
            try
            {
                await this.database.Containers.Create(containerName, null)
                    .ApplyAsync();
                Assert.Fail("Create should throw null ref exception");
            }
            catch (ArgumentNullException ae)
            {
                Assert.IsNotNull(ae);
            }
        }

        [TestMethod]
        public async Task TimeToLivePropertyPath()
        {
            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath = "/user";
            int timeToLivetimeToLiveInSeconds = 10;
            
            CosmosContainerResponse containerResponse = null;
            try
            {
                containerResponse = await this.database.Containers.Create(containerName, partitionKeyPath)
                    .WithTimeToLivePropertyPath("/creationDate")
                    .ApplyAsync();
                Assert.Fail("CreateColleciton with TtlPropertyPath and with no DefaultTimeToLive should have failed.");
            }
            catch (CosmosException exeption)
            {
                // expected because DefaultTimeToLive was not specified
                Assert.AreEqual(HttpStatusCode.BadRequest, exeption.StatusCode);
            }

            // Verify the container content.
            containerResponse = await this.database.Containers.Create(containerName, partitionKeyPath)
                   .WithTimeToLivePropertyPath("/creationDate")
                   .WithDefaultTimeToLive(timeToLivetimeToLiveInSeconds)
                   .ApplyAsync();
            CosmosContainer cosmosContainer = containerResponse;
            Assert.AreEqual(timeToLivetimeToLiveInSeconds, containerResponse.Resource.DefaultTimeToLive);
            Assert.AreEqual("/creationDate", containerResponse.Resource.TimeToLivePropertyPath);

            CosmosContainerSettings responseSettings = containerResponse;

            //verify removing the ttl property path
            containerResponse = await this.database.Containers.Replace(containerName)
                  .WithDefaultTimeToLive(timeToLivetimeToLiveInSeconds)
                  .ApplyAsync();
            cosmosContainer = containerResponse;
            Assert.AreEqual(timeToLivetimeToLiveInSeconds, containerResponse.Resource.DefaultTimeToLive);
            Assert.IsNull(containerResponse.Resource.TimeToLivePropertyPath);

            //adding back the ttl property path
            containerResponse = await this.database.Containers.Replace(containerName)
                  .WithDefaultTimeToLive(timeToLivetimeToLiveInSeconds)
                  .WithTimeToLivePropertyPath("/creationDate")
                  .ApplyAsync();
            cosmosContainer = containerResponse;
            Assert.AreEqual(containerResponse.Resource.TimeToLivePropertyPath, "/creationDate");

            //Creating an item and reading before expiration
            var payload = new { id = "testId", user = "testUser", creationDate = ToEpoch(DateTime.UtcNow) };
            CosmosItemResponse<dynamic> createItemResponse = await cosmosContainer.Items.CreateItemAsync<dynamic>(payload.user, payload);
            Assert.IsNotNull(createItemResponse.Resource);
            Assert.AreEqual(createItemResponse.StatusCode, HttpStatusCode.Created);
            CosmosItemResponse<dynamic> readItemResponse = await cosmosContainer.Items.ReadItemAsync<dynamic>(payload.user, payload.id);
            Assert.IsNotNull(readItemResponse.Resource);
            Assert.AreEqual(readItemResponse.StatusCode, HttpStatusCode.OK);

            containerResponse = await cosmosContainer.DeleteAsync();
            Assert.AreEqual(HttpStatusCode.NoContent, containerResponse.StatusCode);
        }
    }
}
