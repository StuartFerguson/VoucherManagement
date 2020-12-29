﻿namespace VoucherManagement.BusinessLogic.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Abstractions.TestingHelpers;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using EstateReporting.Database;
    using EstateReporting.Database.Entities;
    using EventHandling;
    using MessagingService.Client;
    using Microsoft.Data.Sqlite;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Diagnostics;
    using Microsoft.Extensions.Configuration;
    using Moq;
    using SecurityService.Client;
    using Shared.EntityFramework;
    using Shared.EventStore.EventStore;
    using Shared.General;
    using Shared.Logger;
    using Testing;
    using VoucherAggregate;
    using Xunit;

    public class VoucherDomainEventHandlerTests
    {
        private async Task<EstateReportingContext> GetContext(String databaseName, TestDatabaseType databaseType = TestDatabaseType.InMemory)
        {
            EstateReportingContext context = null;
            if (databaseType == TestDatabaseType.InMemory)
            {
                DbContextOptionsBuilder<EstateReportingContext> builder = new DbContextOptionsBuilder<EstateReportingContext>()
                                                                          .UseInMemoryDatabase(databaseName)
                                                                          .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));
                context = new EstateReportingContext(builder.Options);
            }
            else if (databaseType == TestDatabaseType.SqliteInMemory)
            {
                SqliteConnection inMemorySqlite = new SqliteConnection("Data Source=:memory:");
                inMemorySqlite.Open();

                DbContextOptionsBuilder<EstateReportingContext> builder = new DbContextOptionsBuilder<EstateReportingContext>().UseSqlite(inMemorySqlite);
                context = new EstateReportingContext(builder.Options);
                await context.Database.EnsureCreatedAsync();

            }
            else
            {
                throw new NotSupportedException($"Database type [{databaseType}] not supported");
            }

            return context;
        }

        public enum TestDatabaseType
        {
            InMemory = 0,
            SqliteInMemory = 1
        }

        [Fact]
        public async Task VoucherDomainEventHandler_VoucherIssuedEvent_WithEmailAddress_IsHandled()
        {
            IConfigurationRoot configurationRoot = new ConfigurationBuilder().AddInMemoryCollection(TestData.DefaultAppSettings).Build();
            ConfigurationReader.Initialise(configurationRoot);
            Logger.Initialise(NullLogger.Instance);

            Mock<ISecurityServiceClient> securityServiceClient = new Mock<ISecurityServiceClient>();
            securityServiceClient.Setup(s => s.GetToken(It.IsAny<String>(), It.IsAny<String>(), It.IsAny<CancellationToken>())).ReturnsAsync(TestData.TokenResponse);

            Mock<IAggregateRepository<VoucherAggregate>> voucherAggregateRepository = new Mock<IAggregateRepository<VoucherAggregate>>();
            voucherAggregateRepository.Setup(t => t.GetLatestVersion(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                                      .ReturnsAsync(TestData.GetVoucherAggregateWithRecipientEmail);
            
            EstateReportingContext context = await this.GetContext(Guid.NewGuid().ToString("N"), TestDatabaseType.InMemory);
            context.Transactions.Add(new Transaction
                                     {
                                         TransactionId = TestData.TransactionId,
                                         EstateId = TestData.EstateId,
                                         ContractId = TestData.ContractId
                                     });
            context.Contracts.Add(new Contract
                                  {
                                      ContractId = TestData.ContractId,
                                      EstateId = TestData.EstateId,
                                      Description = TestData.OperatorIdentifier
                                  });
            await context.SaveChangesAsync(CancellationToken.None);

            Mock<IDbContextFactory<EstateReportingContext>> dbContextFactory = new Mock<IDbContextFactory<EstateReportingContext>>();
            dbContextFactory.Setup(d => d.GetContext(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(context);

            Mock<IMessagingServiceClient> messagingServiceClient = new Mock<IMessagingServiceClient>();

            DirectoryInfo path = Directory.GetParent(Assembly.GetExecutingAssembly().Location);
            MockFileSystem fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
                                                           {
                                                               { $"{path}/VoucherMessages/VoucherEmail.html", new MockFileData("Transaction Number: [TransactionNumber]") }
                                                           });

            VoucherDomainEventHandler voucherDomainEventHandler = new VoucherDomainEventHandler(securityServiceClient.Object,
                                                                                                voucherAggregateRepository.Object,
                                                                                                dbContextFactory.Object,
                                                                                                messagingServiceClient.Object,
                                                                                                fileSystem);

            await voucherDomainEventHandler.Handle(TestData.VoucherIssuedEvent, CancellationToken.None);
        }

        [Fact]
        public async Task VoucherDomainEventHandler_VoucherIssuedEvent_WithRecipientMobile_IsHandled()
        {
            IConfigurationRoot configurationRoot = new ConfigurationBuilder().AddInMemoryCollection(TestData.DefaultAppSettings).Build();
            ConfigurationReader.Initialise(configurationRoot);
            Logger.Initialise(NullLogger.Instance);

            Mock<ISecurityServiceClient> securityServiceClient = new Mock<ISecurityServiceClient>();
            securityServiceClient.Setup(s => s.GetToken(It.IsAny<String>(), It.IsAny<String>(), It.IsAny<CancellationToken>())).ReturnsAsync(TestData.TokenResponse);

            Mock<IAggregateRepository<VoucherAggregate>> voucherAggregateRepository = new Mock<IAggregateRepository<VoucherAggregate>>();
            voucherAggregateRepository.Setup(t => t.GetLatestVersion(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                                      .ReturnsAsync(TestData.GetVoucherAggregateWithRecipientEmail);

            EstateReportingContext context = await this.GetContext(Guid.NewGuid().ToString("N"), TestDatabaseType.InMemory);
            context.Transactions.Add(new Transaction
            {
                TransactionId = TestData.TransactionId,
                EstateId = TestData.EstateId,
                ContractId = TestData.ContractId
            });
            context.Contracts.Add(new Contract
            {
                ContractId = TestData.ContractId,
                EstateId = TestData.EstateId,
                Description = TestData.OperatorIdentifier
            });
            await context.SaveChangesAsync(CancellationToken.None);

            Mock<IDbContextFactory<EstateReportingContext>> dbContextFactory = new Mock<IDbContextFactory<EstateReportingContext>>();
            dbContextFactory.Setup(d => d.GetContext(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(context);

            Mock<IMessagingServiceClient> messagingServiceClient = new Mock<IMessagingServiceClient>();

            DirectoryInfo path = Directory.GetParent(Assembly.GetExecutingAssembly().Location);
            MockFileSystem fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
                                                           {
                                                               { $"{path}/VoucherMessages/VoucherEmail.html", new MockFileData("Transaction Number: [TransactionNumber]") }
                                                           });

            VoucherDomainEventHandler voucherDomainEventHandler = new VoucherDomainEventHandler(securityServiceClient.Object,
                                                                                                voucherAggregateRepository.Object,
                                                                                                dbContextFactory.Object,
                                                                                                messagingServiceClient.Object,
                                                                                                fileSystem);

            await voucherDomainEventHandler.Handle(TestData.VoucherIssuedEvent, CancellationToken.None);
        }
    }
}