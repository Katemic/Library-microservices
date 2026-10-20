
-- Catalog development fixtures for MySQL V1
-- Run AFTER the Flyway initial migration.
-- External loaner IDs do not require foreign keys.

START TRANSACTION;

-- LANGUAGES
INSERT INTO `language` (`id`, `language`) VALUES
  (11, 'English'),
  (12, 'Danish')
ON DUPLICATE KEY UPDATE
  `language` = VALUES(`language`);

-- PUBLISHERS
INSERT INTO `publisher` (`id`, `name`) VALUES
  (11, 'Northstar Books'),
  (12, 'Lantern Games')
ON DUPLICATE KEY UPDATE
  `name` = VALUES(`name`);

-- CREATORS
INSERT INTO `creator`
  (`id`, `first_name`, `last_name`, `birthday`, `description`)
VALUES
  (21, 'Mira', 'Ellis', '1982-04-08', 'Fiction writer'),
  (22, 'Jonah', 'Reed', NULL, 'Author and game designer'),
  (23, 'Freya', 'Sol', '1990-11-02', NULL),
  (24, 'Kai', 'Andersen', NULL, 'Board-game designer')
ON DUPLICATE KEY UPDATE
  `first_name` = VALUES(`first_name`),
  `last_name` = VALUES(`last_name`),
  `birthday` = VALUES(`birthday`),
  `description` = VALUES(`description`);

-- GENRES
INSERT INTO `genre` (`id`, `name`) VALUES
  (31, 'Fantasy'),
  (32, 'Strategy'),
  (33, 'Mystery')
ON DUPLICATE KEY UPDATE
  `name` = VALUES(`name`);

-- TAGS
INSERT INTO `tag` (`id`, `name`) VALUES
  (41, 'Adventure'),
  (42, 'Family'),
  (43, 'Puzzle'),
  (44, 'Nordic')
ON DUPLICATE KEY UPDATE
  `name` = VALUES(`name`);

-- ITEMS
INSERT INTO `item` (
    `id`, `name`, `release_year`, `description`,
    `review_summary`, `media_type`, `image`,
    `language_id`, `publisher_id`, `average_stars`
) VALUES
  (101, 'The Lantern Archive', 2021,
   'A librarian discovers a hidden map.',
   'Readers enjoy the mystery.',
   'book', 'lantern-archive.jpg', 11, 11, 4.5),

  (102, 'Harbor Traders', 2022,
   'A strategy board game about competing merchants.',
   'Players praise the strategic choices.',
   'boardgame', 'harbor-traders.jpg', 11, 12, 4.0),

  (103, 'Northern Lights', 2020,
   'A mystery set in a remote northern town.',
   NULL, 'book', NULL, 12, 11, NULL)
ON DUPLICATE KEY UPDATE
  `name` = VALUES(`name`),
  `release_year` = VALUES(`release_year`),
  `description` = VALUES(`description`),
  `review_summary` = VALUES(`review_summary`),
  `media_type` = VALUES(`media_type`),
  `image` = VALUES(`image`),
  `language_id` = VALUES(`language_id`),
  `publisher_id` = VALUES(`publisher_id`),
  `average_stars` = VALUES(`average_stars`);

-- BOOK DETAILS
INSERT INTO `book`
  (`id`, `ISBN`, `no_of_pages`, `version`, `item_id`)
VALUES
  (201, '9780000000101', 320, 'First edition', 101),
  (203, '9780000000118', 248, NULL, 103)
ON DUPLICATE KEY UPDATE
  `ISBN` = VALUES(`ISBN`),
  `no_of_pages` = VALUES(`no_of_pages`),
  `version` = VALUES(`version`),
  `item_id` = VALUES(`item_id`);

-- BOARDGAME DETAILS
INSERT INTO `boardgame`
  (`id`, `no_of_players`, `play_time`, `age_group`, `item_id`)
VALUES
  (202, '2-4', '45-60 minutes', '10+', 102)
ON DUPLICATE KEY UPDATE
  `no_of_players` = VALUES(`no_of_players`),
  `play_time` = VALUES(`play_time`),
  `age_group` = VALUES(`age_group`),
  `item_id` = VALUES(`item_id`);

-- ITEM-CREATOR RELATIONSHIPS
INSERT INTO `item_creator` (`item_id`, `creator_id`) VALUES
  (101, 21),
  (101, 22),
  (102, 22),
  (102, 24),
  (103, 23)
ON DUPLICATE KEY UPDATE
  `creator_id` = VALUES(`creator_id`);

-- ITEM-GENRE RELATIONSHIPS
INSERT INTO `item_genre` (`item_id`, `genre_id`) VALUES
  (101, 31),
  (101, 33),
  (102, 32),
  (103, 33)
ON DUPLICATE KEY UPDATE
  `genre_id` = VALUES(`genre_id`);

-- ITEM-TAG RELATIONSHIPS
INSERT INTO `item_tag` (`item_id`, `tag_id`) VALUES
  (101, 41),
  (101, 43),
  (102, 42),
  (102, 43),
  (103, 44)
ON DUPLICATE KEY UPDATE
  `tag_id` = VALUES(`tag_id`);

-- REVIEWS
-- loaner_id references external users, without a foreign key
INSERT INTO `review`
  (`loaner_id`, `item_id`, `no_of_stars`, `text`)
VALUES
  (9001, 101, 5.0, 'Excellent worldbuilding.'),
  (9002, 101, 4.0, 'Fun, but slow at first.'),
  (9001, 102, 4.0, 'Great replay value.')
ON DUPLICATE KEY UPDATE
  `no_of_stars` = VALUES(`no_of_stars`),
  `text` = VALUES(`text`);

COMMIT;

