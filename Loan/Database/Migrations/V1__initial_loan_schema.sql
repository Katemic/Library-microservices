CREATE TABLE IF NOT EXISTS `loan` (
    `id` INT NOT NULL AUTO_INCREMENT,
    `status` ENUM('Active', 'Returned', 'Overdue') NOT NULL DEFAULT 'Active',
    `item_id` INT NOT NULL,
    `item_name` VARCHAR(255) NOT NULL,
    `loaner_id` INT NOT NULL,
    `loaned_at_utc` DATETIME(6) NOT NULL,
    `due_at_utc` DATETIME(6) NOT NULL,
    `returned_at_utc` DATETIME(6) NULL,
    PRIMARY KEY (`id`),
    INDEX `idx_loan_item_id` (`item_id`),
    INDEX `idx_loan_loaner_id` (`loaner_id`)
) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;

CREATE TABLE IF NOT EXISTS `item` (
    `id` INT NOT NULL,
    `name` VARCHAR(255) NOT NULL,
    `is_deleted` BOOLEAN NOT NULL DEFAULT FALSE,
    `deleted_at_utc` DATETIME(6) NULL,
    PRIMARY KEY (`id`)
) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;

CREATE TABLE IF NOT EXISTS `inventory` (
    `id` INT NOT NULL AUTO_INCREMENT,
    `item_id` INT NOT NULL,
    `status` ENUM('Available', 'Loaned', 'Unavailable') NOT NULL DEFAULT 'Available',
    `barcode` VARCHAR(128) NOT NULL,
    `placement` VARCHAR(128) NOT NULL,
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_inventory_item_id` (`item_id`)
) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;

CREATE TABLE IF NOT EXISTS `fine` (
    `id` INT NOT NULL AUTO_INCREMENT,
    `loan_id` INT NOT NULL,
    `loaner_id` INT NOT NULL,
    `item_id` INT NOT NULL,
    `item_name` VARCHAR(255) NOT NULL,
    `amount` DECIMAL(10,2) NOT NULL,
    `status` ENUM('Created', 'Overdue', 'Paid') NOT NULL DEFAULT 'Created',
    `created_at_utc` DATETIME(6) NOT NULL,
    `paid_at_utc` DATETIME(6) NULL,
    `due_at_utc` DATETIME(6) NOT NULL,
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_fine_loan_id` (`loan_id`)
) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;