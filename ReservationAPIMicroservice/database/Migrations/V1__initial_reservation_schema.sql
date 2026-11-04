
-- Reservation initial relational schema
-- No hardcoded Catalog item seed records.

CREATE TABLE `Item` (
    `id` INT NOT NULL,
    `name` VARCHAR(255) NOT NULL,
    `isDeleted` BOOLEAN NOT NULL DEFAULT FALSE,

    -- Latest Catalog version processed
    `item_version` BIGINT NOT NULL DEFAULT 0,

    -- Catalog's original deletion timestamp
    `deleted_at_utc` DATETIME(6) NULL,

    PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;


CREATE TABLE `Inventory` (
    `id` INT NOT NULL,
    `status` ENUM(
        'AVAILABLE',
        'RESERVED',
        'UNAVAILABLE'
    ) NOT NULL DEFAULT 'AVAILABLE',

    `barcode` VARCHAR(255) NOT NULL,
    `placement` VARCHAR(255) NOT NULL,
    `item` INT NOT NULL,

    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_inventory_barcode` (`barcode`),

    INDEX `idx_inventory_item` (`item`),

    CONSTRAINT `fk_inventory_item`
        FOREIGN KEY (`item`)
        REFERENCES `Item` (`id`)
        ON UPDATE CASCADE
        ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;


CREATE TABLE `Reservation` (
    `id` INT NOT NULL AUTO_INCREMENT,
    `UserID` INT NOT NULL,
    `reservationTime` DATETIME NOT NULL,

    `status` ENUM(
        'PENDING',
        'CONFIRMED',
        'CANCELLED',
        'COMPLETED'
    ) NOT NULL DEFAULT 'PENDING',

    `queueNumber` INT NOT NULL,
    `item` INT NOT NULL,

    PRIMARY KEY (`id`),

    INDEX `idx_reservation_user` (`UserID`),
    INDEX `idx_reservation_time` (`reservationTime`),
    INDEX `idx_reservation_item` (`item`),

    CONSTRAINT `fk_reservation_item`
        FOREIGN KEY (`item`)
        REFERENCES `Item` (`id`)
        ON UPDATE CASCADE
        ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
