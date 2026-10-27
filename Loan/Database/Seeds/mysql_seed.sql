INSERT INTO `loan`
    (`id`, `status`, `item_id`, `item_name`, `loaner_id`, `loaned_at_utc`, `due_at_utc`, `returned_at_utc`)
VALUES
    (1001, 'Active', 101, 'The Lantern Archive', 9001, '2026-10-01 09:00:00', '2026-10-15 09:00:00', NULL),
    (1002, 'Returned', 102, 'Harbor Traders', 9002, '2026-10-02 10:30:00', '2026-10-16 10:30:00', '2026-10-10 14:15:00')
ON DUPLICATE KEY UPDATE
    `status` = VALUES(`status`),
    `item_id` = VALUES(`item_id`),
    `item_name` = VALUES(`item_name`),
    `loaner_id` = VALUES(`loaner_id`),
    `loaned_at_utc` = VALUES(`loaned_at_utc`),
    `due_at_utc` = VALUES(`due_at_utc`),
    `returned_at_utc` = VALUES(`returned_at_utc`);