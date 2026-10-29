CREATE TABLE tenants (id uuid PRIMARY KEY, name text NOT NULL);
CREATE TABLE memberships (
 issuer text NOT NULL, subject text NOT NULL, tenant_id uuid NOT NULL REFERENCES tenants,
 can_read boolean NOT NULL, can_review boolean NOT NULL, PRIMARY KEY (issuer, subject)
);
CREATE TABLE deliveries (
 id uuid PRIMARY KEY, tenant_id uuid NOT NULL REFERENCES tenants, reference text NOT NULL,
 expected_at timestamptz NOT NULL, observed_at timestamptz, status text NOT NULL CHECK (status IN ('delayed','on_time')),
 carrier_note text NOT NULL CHECK (length(carrier_note)<=1000), reviewed boolean NOT NULL,
 version bigint NOT NULL CHECK (version>0), reviewed_by text, reviewed_at timestamptz,
 CHECK (reviewed = (reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL))
);
CREATE INDEX deliveries_by_tenant ON deliveries(tenant_id,id);
CREATE TABLE audit (
 id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY, correlation_id text NOT NULL,
 at timestamptz NOT NULL, issuer text, subject text, tenant_id uuid,
 operation text NOT NULL, delivery_id uuid, outcome text NOT NULL, reason text NOT NULL,
 before_version bigint, after_version bigint
);
