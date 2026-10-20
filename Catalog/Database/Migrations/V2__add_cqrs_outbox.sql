
-- ========================================
-- V2: CQRS and Transactional Outbox
-- ========================================

-- Add version tracking for synchronization
-- between MySQL and MongoDB

ALTER TABLE item
ADD COLUMN version BIGINT NOT NULL DEFAULT 1;

-- Add soft deletion / tombstone support

ALTER TABLE item
ADD COLUMN is_deleted BOOLEAN NOT NULL DEFAULT FALSE;

ALTER TABLE item
ADD COLUMN deleted_at_utc DATETIME(6) NULL;

-- Index for active catalog queries

CREATE INDEX idx_item_is_deleted
ON item (is_deleted);


-- ========================================
-- Transactional Outbox
-- ========================================

CREATE TABLE outbox_messages (

    id CHAR(36) NOT NULL,

    item_id INT NOT NULL,
    item_version BIGINT NOT NULL,

    event_type VARCHAR(100) NOT NULL,
    payload LONGTEXT NOT NULL,

    occurred_at_utc DATETIME(6) NOT NULL,

    projected_at_utc DATETIME(6) NULL,
    published_at_utc DATETIME(6) NULL,

    retry_count INT NOT NULL DEFAULT 0,
    last_error TEXT NULL,

    PRIMARY KEY (id),

    INDEX idx_outbox_projection (
        projected_at_utc
    ),

    INDEX idx_outbox_publication (
        published_at_utc
    ),

    INDEX idx_outbox_item_version (
        item_id,
        item_version
    )

)
ENGINE = InnoDB
DEFAULT CHARACTER SET = utf8mb4;
