# Loan + Reservation system tests

These tests check that **Loan** and **Reservation** agree about the inventory when they run as real services.

| Test | What it checks |
|---|---|
| `CreatingLoan_MarksCopyUnavailableInReservation` | When a copy is loaned out in Loan, Reservation marks it `UNAVAILABLE` |
| `ReturningLoan_MarksCopyAvailableInReservation` | When the loan is returned, Reservation marks the copy `AVAILABLE` again |

## How it works

The services run in Docker (from `compose.integration.yaml`): RabbitMQ, Loan with its MySQL database, and Reservation with its MySQL database. Catalog is **not** started. Instead, the tests send the same "item added" message Catalog would send, so Loan and Reservation both know the item.

Each test:

1. sends a Catalog "item added" message and waits until Reservation has the item
2. adds one copy of the item straight to Loan's database (Loan has no API for adding copies)
3. creates (and returns) a loan through Loan's API
4. waits until Reservation's `GET /api/inventory/{id}` shows the expected status

Messages travel through RabbitMQ, so the tests wait up to 30 seconds for each change to arrive. Every test uses random ids, so tests never collide with each other or with old data.

## Running the tests on your own PC

You need Docker Desktop running. Run all commands from the **repository root**.

### 1. Create `system-test.env`

Create a file called `system-test.env` in this folder (`LoanReservationSystemTest/`) with these lines. Choose any passwords you like. The containers are created fresh for the test run and deleted afterwards.

```
RABBIT_USER=systemtest
RABBIT_PASSWORD=<choose a password>
LOAN_ROOT_PASSWORD=<choose a password>
LOAN_DB_PASSWORD=<choose a password>
RESERVATION_ROOT_PASSWORD=<choose a password>
RESERVATION_DB_PASSWORD=<choose a password>
```

The file is in `.gitignore`, so it never gets committed. Docker compose and the tests both read it.

### 2. Start the services

```
docker compose -f compose.integration.yaml --env-file LoanReservationSystemTest/system-test.env -p loan-reservation-systemtest up -d --build loan-api reservation-api
```

`-p loan-reservation-systemtest` gives the test containers their own name, so they don't mix with your normal integration setup. They use the same ports, though (18082, 18083, 3308, 3309, 15675), so stop your normal containers first.

### 3. Run the tests

```
dotnet test LoanReservationSystemTest
```

Or use Test Explorer in Visual Studio.

### 4. Stop the services and delete their data

```
docker compose -f compose.integration.yaml --env-file LoanReservationSystemTest/system-test.env -p loan-reservation-systemtest down -v
```

## In the pipeline

`.github/workflows/loan-reservation-system-tests.yml` runs these tests on GitHub whenever Loan, Reservation, these tests or `compose.integration.yaml` change. It creates `system-test.env` with new random passwords on every run, so no passwords are stored anywhere. If a test fails, the logs from Loan and Reservation are printed in the run.

## Settings

The tests read their settings from environment variables first, then from `system-test.env`.

| Setting | Default |
|---|---|
| `RABBIT_USER`, `RABBIT_PASSWORD` | required |
| `LOAN_DB_PASSWORD` | required |
| `LOAN_API_URL` | `http://localhost:18082/` |
| `RESERVATION_API_URL` | `http://localhost:18083/` |
| `RABBIT_MANAGEMENT_URL` | `http://localhost:15675/` |
| `LOAN_DB_PORT` | `3308` |
