
-- Fixed Catalog reference data for development
-- Creates no items, books, board games or reviews.

-- Languages
INSERT INTO `language` (`id`, `language`) VALUES
  (11, 'English'),
  (12, 'Danish')
ON DUPLICATE KEY UPDATE
  `language` = VALUES(`language`);

-- Publishers
INSERT INTO `publisher` (`id`, `name`) VALUES
  (11, 'Northstar Books'),
  (12, 'Lantern Games')
ON DUPLICATE KEY UPDATE
  `name` = VALUES(`name`);

-- Creators
INSERT INTO `creator`
  (`id`, `first_name`, `last_name`,
   `birthday`, `description`)
VALUES
  (21, 'Mira', 'Ellis', '1982-04-08',
   'Fiction writer'),
  (22, 'Jonah', 'Reed', NULL,
   'Author and game designer'),
  (23, 'Freya', 'Sol', '1990-11-02',
   NULL),
  (24, 'Kai', 'Andersen', NULL,
   'Board-game designer')
ON DUPLICATE KEY UPDATE
  `first_name` = VALUES(`first_name`),
  `last_name` = VALUES(`last_name`),
  `birthday` = VALUES(`birthday`),
  `description` = VALUES(`description`);

-- Genres
INSERT INTO `genre` (`id`, `name`) VALUES
  (31, 'Fantasy'),
  (32, 'Strategy'),
  (33, 'Mystery')
ON DUPLICATE KEY UPDATE
  `name` = VALUES(`name`);

-- Tags
INSERT INTO `tag` (`id`, `name`) VALUES
  (41, 'Adventure'),
  (42, 'Family'),
  (43, 'Puzzle'),
  (44, 'Nordic')
ON DUPLICATE KEY UPDATE
  `name` = VALUES(`name`);
