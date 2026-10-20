-- V1__initial_catalog_schema.sql
-- Source: DLS commands.pdf (Catalog relational schema).
-- Flyway runs this migration against the database configured in FLYWAY_URL
-- (catalog_write). Tables use the current database.
--
-- IMPORTANT: review.loaner_id is an external service identifier.
-- It remains NOT NULL and part of the review primary key, but intentionally
-- has NO foreign key to a `loaner` table owned by another microservice.
-- All other foreign keys from the supplied schema are preserved.
--
-- This migration is the source of truth for the initial MySQL schema.
-- Apply on an empty catalog_write database. Do not edit after applying.
-- create V2__...sql for later changes.

-- -----------------------------------------------------
-- Table `language`
-- -----------------------------------------------------
CREATE TABLE `language` (
  `id` INT NOT NULL AUTO_INCREMENT,
  `language` VARCHAR(45) NOT NULL,
  PRIMARY KEY (`id`),
  UNIQUE INDEX `language_UNIQUE` (`language` ASC) VISIBLE)
ENGINE = InnoDB
DEFAULT CHARACTER SET = utf8mb4;


-- -----------------------------------------------------
-- Table `publisher`
-- -----------------------------------------------------
CREATE TABLE `publisher` (
  `id` INT NOT NULL AUTO_INCREMENT,
  `name` VARCHAR(70) NOT NULL,
  PRIMARY KEY (`id`))
ENGINE = InnoDB
DEFAULT CHARACTER SET = utf8mb4;


-- -----------------------------------------------------
-- Table `item`
-- -----------------------------------------------------
CREATE TABLE `item` (
  `id` INT NOT NULL AUTO_INCREMENT,
  `name` VARCHAR(255) NOT NULL,
  `release_year` SMALLINT UNSIGNED NULL DEFAULT NULL,
  `description` TEXT NULL DEFAULT NULL,
  `review_summary` TEXT NULL DEFAULT NULL,
  `media_type` ENUM('book', 'boardgame') NOT NULL,
  `image` VARCHAR(2048) NULL DEFAULT NULL,
  `language_id` INT NOT NULL,
  `publisher_id` INT NOT NULL,
  `average_stars` DECIMAL(2,1) NULL DEFAULT NULL,
  PRIMARY KEY (`id`),
  INDEX `language_id_idx` (`language_id` ASC) VISIBLE,
  INDEX `publisher_id_idx` (`publisher_id` ASC) VISIBLE,
  INDEX `idx_item_name` (`name` ASC) VISIBLE,
  INDEX `idx_item_media_type` (`media_type` ASC) VISIBLE,
  CONSTRAINT `fk_item_language`
    FOREIGN KEY (`language_id`)
    REFERENCES `language` (`id`),
  CONSTRAINT `fk_item_publisher`
    FOREIGN KEY (`publisher_id`)
    REFERENCES `publisher` (`id`),
  CONSTRAINT `chk_item_release_year`
  CHECK (`release_year` IS NULL OR `release_year` BETWEEN 1000 AND 2100),
  CONSTRAINT `chk_item_average_stars`
  CHECK (`average_stars` IS NULL OR `average_stars` BETWEEN 0 AND 5))
ENGINE = InnoDB
DEFAULT CHARACTER SET = utf8mb4;


-- -----------------------------------------------------
-- Table `boardgame`
-- -----------------------------------------------------
CREATE TABLE `boardgame` (
  `id` INT NOT NULL AUTO_INCREMENT,
  `no_of_players` VARCHAR(20) NULL DEFAULT NULL,
  `play_time` VARCHAR(20) NULL DEFAULT NULL,
  `age_group` VARCHAR(20) NULL DEFAULT NULL,
  `item_id` INT NOT NULL,
  PRIMARY KEY (`id`),
  UNIQUE INDEX `item_id_UNIQUE` (`item_id` ASC) VISIBLE,
  INDEX `item_id_idx` (`item_id` ASC) VISIBLE,
  CONSTRAINT `fk_boardgame_item`
    FOREIGN KEY (`item_id`)
    REFERENCES `item` (`id`)
    ON DELETE CASCADE)
ENGINE = InnoDB
DEFAULT CHARACTER SET = utf8mb4;


-- -----------------------------------------------------
-- Table `book`
-- -----------------------------------------------------
CREATE TABLE `book` (
  `id` INT NOT NULL AUTO_INCREMENT,
  `ISBN` VARCHAR(17) NULL DEFAULT NULL,
  `no_of_pages` SMALLINT UNSIGNED NULL DEFAULT NULL,
  `version` VARCHAR(45) NULL DEFAULT NULL,
  `item_id` INT NOT NULL,
  PRIMARY KEY (`id`),
  UNIQUE INDEX `item_id_UNIQUE` (`item_id` ASC) VISIBLE,
  UNIQUE INDEX `ISBN_UNIQUE` (`ISBN` ASC) VISIBLE,
  INDEX `item_id_idx` (`item_id` ASC) VISIBLE,
  CONSTRAINT `fk_book_item`
    FOREIGN KEY (`item_id`)
    REFERENCES `item` (`id`)
    ON DELETE CASCADE)
ENGINE = InnoDB
DEFAULT CHARACTER SET = utf8mb4;

-- -----------------------------------------------------
-- Table `creator`
-- -----------------------------------------------------
CREATE TABLE `creator` (
  `id` INT NOT NULL AUTO_INCREMENT,
  `first_name` VARCHAR(45) NULL DEFAULT NULL,
  `last_name` VARCHAR(45) NULL DEFAULT NULL,
  `birthday` DATE NULL DEFAULT NULL,
  `description` MEDIUMTEXT NULL DEFAULT NULL,
  PRIMARY KEY (`id`))
ENGINE = InnoDB
DEFAULT CHARACTER SET = utf8mb4;

-- -----------------------------------------------------
-- Table `genre`
-- -----------------------------------------------------
CREATE TABLE `genre` (
  `id` INT NOT NULL AUTO_INCREMENT,
  `name` VARCHAR(45) NULL DEFAULT NULL,
  PRIMARY KEY (`id`),
  UNIQUE INDEX `name_UNIQUE` (`name` ASC) VISIBLE)
ENGINE = InnoDB
DEFAULT CHARACTER SET = utf8mb4;


-- -----------------------------------------------------
-- Table `item_creator`
-- -----------------------------------------------------
CREATE TABLE `item_creator` (
  `item_id` INT NOT NULL,
  `creator_id` INT NOT NULL,
  PRIMARY KEY (`item_id`, `creator_id`),
  INDEX `creator_id_idx` (`creator_id` ASC) VISIBLE,
  CONSTRAINT `fk_itemcreator_creator`
    FOREIGN KEY (`creator_id`)
    REFERENCES `creator` (`id`),
  CONSTRAINT `fk_itemcreator_item`
    FOREIGN KEY (`item_id`)
    REFERENCES `item` (`id`)
    ON DELETE CASCADE)
ENGINE = InnoDB
DEFAULT CHARACTER SET = utf8mb4;


-- -----------------------------------------------------
-- Table `item_genre`
-- -----------------------------------------------------
CREATE TABLE `item_genre` (
 `item_id` INT NOT NULL,
 `genre_id` INT NOT NULL,
 PRIMARY KEY (`item_id`, `genre_id`),
 INDEX `genre_id_idx` (`genre_id` ASC) VISIBLE,
 CONSTRAINT `fk_itemgenre_genre`
   FOREIGN KEY (`genre_id`)
   REFERENCES `genre` (`id`),
 CONSTRAINT `fk_itemgenre_item`
   FOREIGN KEY (`item_id`)
   REFERENCES `item` (`id`)
   ON DELETE CASCADE)
ENGINE = InnoDB
DEFAULT CHARACTER SET = utf8mb4;


-- -----------------------------------------------------
-- Table `tag`
-- -----------------------------------------------------
CREATE TABLE `tag` (
  `id` INT NOT NULL AUTO_INCREMENT,
  `name` VARCHAR(45) NULL DEFAULT NULL,
  PRIMARY KEY (`id`),
  UNIQUE INDEX `name_UNIQUE` (`name` ASC) VISIBLE)
ENGINE = InnoDB
DEFAULT CHARACTER SET = utf8mb4;


-- -----------------------------------------------------
-- Table `item_tag`
-- -----------------------------------------------------
CREATE TABLE `item_tag` (
  `item_id` INT NOT NULL,
  `tag_id` INT NOT NULL,
  PRIMARY KEY (`item_id`, `tag_id`),
  INDEX `tag_id_idx` (`tag_id` ASC) VISIBLE,
  CONSTRAINT `fk_itemtag_item`
    FOREIGN KEY (`item_id`)
    REFERENCES `item` (`id`)
    ON DELETE CASCADE,
  CONSTRAINT `fk_itemtag_tag`
    FOREIGN KEY (`tag_id`)
    REFERENCES `tag` (`id`))
ENGINE = InnoDB
DEFAULT CHARACTER SET = utf8mb4;

-- -----------------------------------------------------
-- Table `review`
-- -----------------------------------------------------
CREATE TABLE `review` (
 `loaner_id` INT NOT NULL,
 `item_id` INT NOT NULL,
 `no_of_stars` DECIMAL(2,1) NOT NULL,
 `text` TEXT NULL DEFAULT NULL,
 PRIMARY KEY (`loaner_id`, `item_id`),
 INDEX `item_id_idx` (`item_id` ASC) VISIBLE,
 CONSTRAINT `fk_review_item`
   FOREIGN KEY (`item_id`)
   REFERENCES `item` (`id`)
   ON DELETE CASCADE,
 CONSTRAINT `chk_review_stars`
   CHECK (`no_of_stars` BETWEEN 1 AND 5))
ENGINE = InnoDB
DEFAULT CHARACTER SET = utf8mb4;
