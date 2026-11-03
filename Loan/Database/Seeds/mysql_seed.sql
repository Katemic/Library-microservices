INSERT INTO `loan`
    (`id`, `status`, `item_id`, `item_name`, `loaner_id`, `loaned_at_utc`, `due_at_utc`, `returned_at_utc`)
VALUES
    (1001, 'Overdue', 101, 'The Lantern Archive', 9001, '2026-10-01 09:00:00', '2026-10-15 09:00:00', NULL),
    (1002, 'Returned', 102, 'Harbor Traders', 9002, '2026-10-02 10:30:00', '2026-10-16 10:30:00', '2026-10-10 14:15:00')
ON DUPLICATE KEY UPDATE
    `status` = VALUES(`status`),
    `item_id` = VALUES(`item_id`),
    `item_name` = VALUES(`item_name`),
    `loaner_id` = VALUES(`loaner_id`),
    `loaned_at_utc` = VALUES(`loaned_at_utc`),
    `due_at_utc` = VALUES(`due_at_utc`),
    `returned_at_utc` = VALUES(`returned_at_utc`);

INSERT INTO `inventory`
    (`id`, `item_id`, `status`, `barcode`, `placement`)
VALUES
    (301, 101, 'Loaned', 'CAT-101', 'Main library - shelf A1'),
    (302, 102, 'Available', 'CAT-102', 'Main library - shelf A2')
ON DUPLICATE KEY UPDATE
    `item_id` = VALUES(`item_id`),
    `status` = VALUES(`status`),
    `barcode` = VALUES(`barcode`),
    `placement` = VALUES(`placement`);

INSERT INTO `fine`
    (`id`, `loan_id`, `loaner_id`, `item_id`, `item_name`, `amount`, `status`,
     `created_at_utc`, `paid_at_utc`, `due_at_utc`)
VALUES
    (501, 1001, 9001, 101, 'The Lantern Archive', 10.00, 'Overdue',
     '2026-10-15 09:00:00', NULL, '2026-10-15 09:00:00')
ON DUPLICATE KEY UPDATE
    `loan_id` = VALUES(`loan_id`),
    `loaner_id` = VALUES(`loaner_id`),
    `item_id` = VALUES(`item_id`),
    `item_name` = VALUES(`item_name`),
    `amount` = VALUES(`amount`),
    `status` = VALUES(`status`),
    `created_at_utc` = VALUES(`created_at_utc`),
    `paid_at_utc` = VALUES(`paid_at_utc`),
    `due_at_utc` = VALUES(`due_at_utc`);