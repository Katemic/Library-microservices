
-- V2: Track the latest Catalog version received by Loan

ALTER TABLE `item`
ADD COLUMN `item_version` BIGINT NOT NULL DEFAULT 0;
