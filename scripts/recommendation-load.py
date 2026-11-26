
import base64
import getpass
import json
import time
import uuid
import urllib.request

from datetime import datetime, timezone

RABBITMQ_URL = "http://localhost:30087"
EXCHANGE = "recommendation.scaling-test"

MESSAGE_COUNT = 200
PUBLISH_INTERVAL = 0.05
START_ITEM_ID = 6785789

username = input("RabbitMQ username: ")
password = getpass.getpass("RabbitMQ password: ")

credentials = base64.b64encode(
    f"{username}:{password}".encode()
).decode()

url = (
    f"{RABBITMQ_URL}/api/exchanges/"
    f"%2F/{EXCHANGE}/publish"
)

for index in range(MESSAGE_COUNT):
    item_id = START_ITEM_ID + index

    event = {
        "eventId": str(uuid.uuid4()),
        "eventType": "catalog.item.added",
        "schemaVersion": 1,
        "occurredAtUtc": datetime.now(
            timezone.utc
        ).isoformat(),
        "itemId": item_id,
        "itemVersion": 1,
        "item": {
            "id": item_id,
            "name": f"KEDA Test Book {item_id}",
            "mediaType": "book",
            "releaseYear": 2026,
            "itemVersion": 1,
            "isDeleted": False,
            "creators": [],
            "genres": [],
            "tags": []
        }
    }

    message = {
        "properties": {
            "content_type": "application/json",
            "delivery_mode": 2
        },
        "routing_key": "catalog.item.added",
        "payload": json.dumps(event),
        "payload_encoding": "string"
    }

    request = urllib.request.Request(
        url,
        data=json.dumps(message).encode(),
        headers={
            "Authorization": f"Basic {credentials}",
            "Content-Type": "application/json"
        },
        method="POST"
    )

    with urllib.request.urlopen(
        request, timeout=10
    ) as response:
        result = json.loads(response.read())

        if not result.get("routed"):
            raise RuntimeError(
                "Message was not routed to a queue"
            )

    print(
        f"Published {index + 1}/{MESSAGE_COUNT}"
    )

    time.sleep(PUBLISH_INTERVAL)

print("Load generation complete.")
