
INSERT INTO `language` (`id`, `language`)
VALUES (11, 'English')
ON DUPLICATE KEY UPDATE
    `language` = VALUES(`language`);

INSERT INTO `publisher` (`id`, `name`)
VALUES (11, 'Northstar Books')
ON DUPLICATE KEY UPDATE
    `name` = VALUES(`name`);
