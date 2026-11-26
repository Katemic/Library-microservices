// JavaScript source code

import http from "k6/http";
import { check, sleep } from "k6";

export const options = {
    stages: [
        { duration: "30s", target: 10 },
        { duration: "1m", target: 50 },
        { duration: "2m", target: 100 },
        { duration: "30s", target: 0 }
    ]
};

const URL = "http://localhost:30081/graphql";

const query = JSON.stringify({
    query: `
        query {
            items {
                id
                name
            }
        }
    `
});

const params = {
    headers: {
        "Content-Type": "application/json"
    },
    timeout: "10s"
};

export default function () {
    const response = http.post(
        URL,
        query,
        params
    );

    check(response, {
        "HTTP status is 200": (r) =>
            r.status === 200,

        "GraphQL query succeeded": (r) => {
            try {
                const body = r.json();

                return !body.errors &&
                    Array.isArray(body.data?.items);
            } catch {
                return false;
            }
        }
    });

    sleep(0.1);
}
