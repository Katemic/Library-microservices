USE ReservationDb;

CREATE TABLE IF NOT EXISTS Reservations (
    Id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    UserId INT NOT NULL,
    ReservationTime DATETIME NOT NULL,
    Status ENUM('Pending', 'Processing', 'Confirmed', 'Failed')
        NOT NULL DEFAULT 'Pending',
    QueueNumber INT NOT NULL
);

INSERT INTO Reservations
    (UserId, ReservationTime, Status, QueueNumber)
VALUES
    (1, '2026-11-01 10:00:00', 'Pending', 1),
    (2, '2026-11-01 11:00:00', 'Confirmed', 2),
    (3, '2026-11-02 14:30:00', 'Processing', 3);