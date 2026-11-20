-- V3: One catalog item can have many physical copies in inventory,
-- and each loan records which copy was handed out.

-- Keep a normal index on item_id, then drop the one-copy-per-item rule.
ALTER TABLE `inventory`
    ADD INDEX `idx_inventory_item_id` (`item_id`);

ALTER TABLE `inventory`
    DROP INDEX `uk_inventory_item_id`;

ALTER TABLE `loan`
    ADD COLUMN `inventory_id` INT NULL AFTER `item_id`,
    ADD INDEX `idx_loan_inventory_id` (`inventory_id`);

-- Before V3 every item had at most one copy, so existing loans
-- can be linked to that copy directly.
UPDATE `loan` l
JOIN `inventory` i ON i.`item_id` = l.`item_id`
SET l.`inventory_id` = i.`id`
WHERE l.`inventory_id` IS NULL;
