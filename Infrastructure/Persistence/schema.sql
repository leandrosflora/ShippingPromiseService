CREATE TABLE shipping_promise_audits (
    id UUID PRIMARY KEY,
    request_json JSONB NOT NULL,
    response_json JSONB NOT NULL,
    candidates_json JSONB NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL
);
