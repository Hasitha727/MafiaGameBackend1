-- MafiaGame Backend - Microsoft SQL Server Database Schema Script

IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'MafiaGameDb')
BEGIN
    CREATE DATABASE [MafiaGameDb];
END
GO

USE [MafiaGameDb];
GO

-- 1. Users Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Users')
BEGIN
    CREATE TABLE [Users] (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        [Username] NVARCHAR(50) NOT NULL UNIQUE,
        [Email] NVARCHAR(100) NOT NULL,
        [PasswordHash] NVARCHAR(MAX) NOT NULL,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE()
    );
END
GO

-- 2. PlayerGameStats Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PlayerGameStats')
BEGIN
    CREATE TABLE [PlayerGameStats] (
        [UserId] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        [GamesPlayed] INT NOT NULL DEFAULT 0,
        [GamesWon] INT NOT NULL DEFAULT 0,
        [SuccessfulMafiaVotes] INT NOT NULL DEFAULT 0,
        [BadPoints] INT NOT NULL DEFAULT 0,
        FOREIGN KEY ([UserId]) REFERENCES [Users]([Id]) ON DELETE CASCADE
    );
END
GO

-- 3. GameRooms Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'GameRooms')
BEGIN
    CREATE TABLE [GameRooms] (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        [RoomCode] NVARCHAR(6) NOT NULL UNIQUE,
        [HostUserId] UNIQUEIDENTIFIER NOT NULL,
        [Status] INT NOT NULL DEFAULT 0, -- 0: Lobby, 1: Night, 2: Day, 3: Ended
        [CurrentDay] INT NOT NULL DEFAULT 1,
        [PhaseEndTime] DATETIME2 NULL,
        [WinnerTeam] NVARCHAR(50) NOT NULL DEFAULT '',
        [Modifiers] INT NOT NULL DEFAULT 0,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE()
    );
END
GO

-- 4. RoomPlayers Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'RoomPlayers')
BEGIN
    CREATE TABLE [RoomPlayers] (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        [RoomId] UNIQUEIDENTIFIER NOT NULL,
        [UserId] UNIQUEIDENTIFIER NOT NULL,
        [Role] INT NOT NULL DEFAULT 0,
        [IsAlive] BIT NOT NULL DEFAULT 1,
        [IsHost] BIT NOT NULL DEFAULT 0,
        [IsConnected] BIT NOT NULL DEFAULT 1,
        [SignalRConnectionId] NVARCHAR(100) NULL,
        [DisconnectedAt] DATETIME2 NULL,
        FOREIGN KEY ([RoomId]) REFERENCES [GameRooms]([Id]) ON DELETE CASCADE,
        FOREIGN KEY ([UserId]) REFERENCES [Users]([Id]) ON DELETE CASCADE
    );
END
GO

-- 5. GameEventLogs Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'GameEventLogs')
BEGIN
    CREATE TABLE [GameEventLogs] (
        [Id] BIGINT IDENTITY(1,1) PRIMARY KEY,
        [RoomId] UNIQUEIDENTIFIER NOT NULL,
        [DayNumber] INT NOT NULL,
        [Phase] INT NOT NULL,
        [EventType] NVARCHAR(50) NOT NULL,
        [Description] NVARCHAR(MAX) NOT NULL,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        FOREIGN KEY ([RoomId]) REFERENCES [GameRooms]([Id]) ON DELETE CASCADE
    );
END
GO
