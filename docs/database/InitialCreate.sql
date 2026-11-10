IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
CREATE TABLE [CatalogueItems] (
    [Id] uniqueidentifier NOT NULL,
    [Code] nvarchar(32) NOT NULL,
    [Description] nvarchar(200) NOT NULL,
    [Unit] nvarchar(16) NOT NULL,
    CONSTRAINT [PK_CatalogueItems] PRIMARY KEY ([Id])
);

CREATE TABLE [Projects] (
    [Id] uniqueidentifier NOT NULL,
    [Code] nvarchar(32) NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [Client] nvarchar(200) NULL,
    CONSTRAINT [PK_Projects] PRIMARY KEY ([Id])
);

CREATE TABLE [Users] (
    [Id] uniqueidentifier NOT NULL,
    [Email] nvarchar(256) NOT NULL,
    [DisplayName] nvarchar(128) NOT NULL,
    [Role] nvarchar(32) NOT NULL,
    CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
);

CREATE TABLE [CatalogueRates] (
    [Id] uniqueidentifier NOT NULL,
    [CatalogueItemId] uniqueidentifier NOT NULL,
    [EffectiveFrom] date NOT NULL,
    [Rate] decimal(18,2) NOT NULL,
    [LabourComponent] decimal(18,2) NOT NULL,
    CONSTRAINT [PK_CatalogueRates] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_CatalogueRates_Labour] CHECK ([LabourComponent] >= 0 AND [LabourComponent] <= [Rate]),
    CONSTRAINT [FK_CatalogueRates_CatalogueItems_CatalogueItemId] FOREIGN KEY ([CatalogueItemId]) REFERENCES [CatalogueItems] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [Estimates] (
    [Id] uniqueidentifier NOT NULL,
    [ProjectId] uniqueidentifier NOT NULL,
    [Number] nvarchar(32) NOT NULL,
    [Description] nvarchar(500) NOT NULL,
    [PricingDate] date NOT NULL,
    [Status] nvarchar(16) NOT NULL,
    [CreatedById] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [UpdatedAt] datetimeoffset NOT NULL,
    [RowVersion] rowversion NULL,
    CONSTRAINT [PK_Estimates] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Estimates_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Estimates_Users_CreatedById] FOREIGN KEY ([CreatedById]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [ProjectAssignments] (
    [ProjectId] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_ProjectAssignments] PRIMARY KEY ([ProjectId], [UserId]),
    CONSTRAINT [FK_ProjectAssignments_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ProjectAssignments_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [EstimateHistory] (
    [Id] bigint NOT NULL IDENTITY,
    [EstimateId] uniqueidentifier NOT NULL,
    [Action] nvarchar(32) NOT NULL,
    [FromStatus] nvarchar(16) NULL,
    [ToStatus] nvarchar(16) NULL,
    [UserId] uniqueidentifier NOT NULL,
    [OccurredAt] datetimeoffset NOT NULL,
    [Comment] nvarchar(2000) NULL,
    CONSTRAINT [PK_EstimateHistory] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_EstimateHistory_Estimates_EstimateId] FOREIGN KEY ([EstimateId]) REFERENCES [Estimates] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_EstimateHistory_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [EstimateLines] (
    [Id] bigint NOT NULL IDENTITY,
    [EstimateId] uniqueidentifier NOT NULL,
    [CatalogueItemId] uniqueidentifier NOT NULL,
    [ItemCode] nvarchar(32) NOT NULL,
    [ItemDescription] nvarchar(200) NOT NULL,
    [Unit] nvarchar(16) NOT NULL,
    [RateEffectiveFrom] date NOT NULL,
    [UnitRate] decimal(18,2) NOT NULL,
    [UnitLabourCost] decimal(18,2) NOT NULL,
    [Quantity] decimal(18,3) NOT NULL,
    [MarkupPercent] decimal(5,2) NOT NULL,
    [NetAmount] decimal(18,2) NOT NULL,
    [MarkupAmount] decimal(18,2) NOT NULL,
    [LineTotal] decimal(18,2) NOT NULL,
    [LabourAmount] decimal(18,2) NOT NULL,
    CONSTRAINT [PK_EstimateLines] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_EstimateLines_CatalogueItems_CatalogueItemId] FOREIGN KEY ([CatalogueItemId]) REFERENCES [CatalogueItems] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_EstimateLines_Estimates_EstimateId] FOREIGN KEY ([EstimateId]) REFERENCES [Estimates] ([Id]) ON DELETE CASCADE
);

CREATE UNIQUE INDEX [IX_CatalogueItems_Code] ON [CatalogueItems] ([Code]);

CREATE UNIQUE INDEX [IX_CatalogueRates_CatalogueItemId_EffectiveFrom] ON [CatalogueRates] ([CatalogueItemId], [EffectiveFrom]);

CREATE INDEX [IX_EstimateHistory_EstimateId_OccurredAt] ON [EstimateHistory] ([EstimateId], [OccurredAt]);

CREATE INDEX [IX_EstimateHistory_UserId] ON [EstimateHistory] ([UserId]);

CREATE UNIQUE INDEX [UX_EstimateHistory_SingleApproval] ON [EstimateHistory] ([EstimateId]) WHERE [Action] = 'Approved';

CREATE INDEX [IX_EstimateLines_CatalogueItemId] ON [EstimateLines] ([CatalogueItemId]);

CREATE UNIQUE INDEX [IX_EstimateLines_EstimateId_CatalogueItemId] ON [EstimateLines] ([EstimateId], [CatalogueItemId]);

CREATE INDEX [IX_EstimateLines_EstimateId_Totals] ON [EstimateLines] ([EstimateId]) INCLUDE ([NetAmount], [MarkupAmount], [LineTotal], [LabourAmount]);

CREATE INDEX [IX_Estimates_CreatedById] ON [Estimates] ([CreatedById]);

CREATE UNIQUE INDEX [IX_Estimates_ProjectId_Number] ON [Estimates] ([ProjectId], [Number]);

CREATE INDEX [IX_ProjectAssignments_UserId] ON [ProjectAssignments] ([UserId]);

CREATE UNIQUE INDEX [IX_Projects_Code] ON [Projects] ([Code]);

CREATE UNIQUE INDEX [IX_Users_Email] ON [Users] ([Email]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261110090000_InitialCreate', N'10.0.1');

COMMIT;
GO


