
-- ===========================================
-- Notification Service - Initial Database
-- ===========================================

CREATE TABLE `Users` (
    `Id` INT NOT NULL,
    `Email` VARCHAR(255) NOT NULL,
    `UserVersion` BIGINT NOT NULL DEFAULT 0,

    PRIMARY KEY (`Id`)
)
ENGINE = InnoDB
DEFAULT CHARACTER SET = utf8mb4;


-- ===========================================
-- PROCESSED NOTIFICATIONS
-- ===========================================

CREATE TABLE `ProcessedNotifications` (
    `EventKey` VARCHAR(160) NOT NULL,
    `RecipientEmail` VARCHAR(255) NOT NULL,
    `ProcessedAtUtc` DATETIME(6) NOT NULL,

    PRIMARY KEY (`EventKey`)
)
ENGINE = InnoDB
DEFAULT CHARACTER SET = utf8mb4;
