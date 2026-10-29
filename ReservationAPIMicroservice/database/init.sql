USE ReservationDb;

CREATE TABLE IF NOT EXISTS Item (
    id INT NOT NULL,
    name VARCHAR(255) NOT NULL,
    isDeleted BOOLEAN NOT NULL DEFAULT FALSE,

    PRIMARY KEY (id)
);

-- 2. Inventory table
CREATE TABLE IF NOT EXISTS Inventory (
    id INT NOT NULL,
    status ENUM('AVAILABLE', 'RESERVED', 'UNAVAILABLE') NOT NULL DEFAULT 'AVAILABLE',
    barcode VARCHAR(255) NOT NULL UNIQUE,
    placement VARCHAR(255) NOT NULL,
    item INT NOT NULL,

    PRIMARY KEY (id),
    FOREIGN KEY (item) REFERENCES Item(id)
        ON UPDATE CASCADE
        ON DELETE RESTRICT
);

-- 3. Reservation table
CREATE TABLE IF NOT EXISTS Reservation (
    id INT NOT NULL AUTO_INCREMENT,
    UserID INT NOT NULL,
    reservationTime DATETIME NOT NULL,
    status ENUM('PENDING', 'CONFIRMED', 'CANCELLED', 'COMPLETED') NOT NULL DEFAULT 'PENDING',
    queueNumber INT NOT NULL,
    item INT NOT NULL,

    PRIMARY KEY (id),
    FOREIGN KEY (item) REFERENCES Item(id)
        ON UPDATE CASCADE
        ON DELETE RESTRICT,

    INDEX idx_reservation_user (UserID),
    INDEX idx_reservation_time (reservationTime)
);

-- Test data insertion
-- 1. Insert items (IDs are manually assigned)
INSERT INTO Item (id, name, isDeleted) VALUES
(1, 'Laptop', FALSE),
(2, 'Projector', FALSE),
(3, 'Wireless Mouse', FALSE),
(4, 'Old Keyboard', TRUE);

-- 2. Insert inventory
INSERT INTO Inventory (id, status, barcode, placement, item) VALUES
(1, 'AVAILABLE', 'BC1001', 'Shelf A1', 1),
(2, 'RESERVED', 'BC1002', 'Shelf A2', 1),
(3, 'AVAILABLE', 'BC2001', 'Shelf B1', 2),
(4, 'UNAVAILABLE', 'BC3001', 'Shelf C1', 3),
(5, 'AVAILABLE', 'BC3002', 'Shelf C2', 3),
(6, 'UNAVAILABLE', 'BC4001', 'Storage D1', 4);

-- 3. Insert reservations
INSERT INTO Reservation
    (UserID, reservationTime, status, queueNumber, item)
VALUES
(101, '2026-10-29 09:00:00', 'CONFIRMED', 0, 1),
(102, '2026-10-29 09:30:00', 'CONFIRMED',   0, 2),
(103, '2026-10-29 10:00:00', 'COMPLETED', 0, 3),
(101, '2026-10-29 11:00:00', 'PENDING', 1, 1),
(104, '2026-10-30 13:00:00', 'PENDING',   1, 3);