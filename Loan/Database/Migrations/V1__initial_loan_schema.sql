CREATE TABLE IF NOT EXISTS `loan` (
    `id` INT NOT NULL AUTO_INCREMENT,
    `item_id` INT NOT NULL,
    `loaner_id` INT NOT NULL,
    `loaned_at_utc` DATETIME(6) NOT NULL,
    `due_at_utc` DATETIME(6) NOT NULL,
    `returned_at_utc` DATETIME(6) NULL,
    PRIMARY KEY (`id`),
    INDEX `idx_loan_item_id` (`item_id`),
    INDEX `idx_loan_loaner_id` (`loaner_id`)
) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;