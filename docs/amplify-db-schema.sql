create table if not exists import_batches (
    id bigint generated always as identity primary key,
    batch_name text not null,
    source_system text not null,
    started_at timestamptz not null default now(),
    completed_at timestamptz null,
    status text not null default 'pending',
    notes text null
);

create table if not exists source_file_variants (
    id bigint generated always as identity primary key,
    variant_code text not null unique,
    description text null
);

create table if not exists source_files (
    id bigint generated always as identity primary key,
    batch_id bigint not null references import_batches(id) on delete cascade,
    source_file_variant_id bigint null references source_file_variants(id),
    canonical_entity text not null,
    original_file_name text not null,
    normalized_file_name text null,
    sheet_name text null,
    file_hash text not null,
    row_count integer not null default 0,
    column_count integer not null default 0,
    imported_at timestamptz not null default now()
);

create index if not exists ix_source_files_batch_id on source_files(batch_id);
create index if not exists ix_source_files_canonical_entity on source_files(canonical_entity);
create index if not exists ix_source_files_file_hash on source_files(file_hash);

create table if not exists chart_of_accounts (
    id bigint generated always as identity primary key,
    source_file_id bigint not null references source_files(id) on delete cascade,
    source_row_number integer not null,
    account_name text not null,
    account_type text null,
    balance_total numeric(18,2) null,
    description text null,
    account_number text null,
    tax_line text null
);

create index if not exists ix_chart_of_accounts_source_file_id on chart_of_accounts(source_file_id);

create table if not exists customers (
    id bigint generated always as identity primary key,
    source_file_id bigint not null references source_files(id) on delete cascade,
    source_row_number integer not null,
    customer_name text not null,
    bill_to text null,
    primary_contact text null,
    main_phone text null,
    fax text null,
    balance_total numeric(18,2) null
);

create index if not exists ix_customers_source_file_id on customers(source_file_id);

create table if not exists vendors (
    id bigint generated always as identity primary key,
    source_file_id bigint not null references source_files(id) on delete cascade,
    source_row_number integer not null,
    vendor_name text not null,
    account_number text null,
    bill_from text null,
    primary_contact text null,
    main_phone text null,
    fax text null,
    balance_total numeric(18,2) null
);

create index if not exists ix_vendors_source_file_id on vendors(source_file_id);

create table if not exists ledger_entries (
    id bigint generated always as identity primary key,
    source_file_id bigint not null references source_files(id) on delete cascade,
    source_row_number integer not null,
    entry_date date null,
    entry_type text null,
    transaction_number text null,
    name text null,
    memo text null,
    account_name text null,
    split_account text null,
    amount numeric(18,2) null,
    running_balance numeric(18,2) null,
    cleared_flag text null,
    entry_scope text not null
);

create index if not exists ix_ledger_entries_source_file_id on ledger_entries(source_file_id);
create index if not exists ix_ledger_entries_entry_date on ledger_entries(entry_date);
create index if not exists ix_ledger_entries_account_name on ledger_entries(account_name);

create table if not exists ledger_entry_lines (
    id bigint generated always as identity primary key,
    ledger_entry_id bigint not null references ledger_entries(id) on delete cascade,
    line_number integer not null,
    account_name text null,
    memo text null,
    split_account text null,
    amount numeric(18,2) null,
    running_balance numeric(18,2) null,
    is_split_row boolean not null default false
);

create index if not exists ix_ledger_entry_lines_ledger_entry_id on ledger_entry_lines(ledger_entry_id);

create table if not exists trial_balance_lines (
    id bigint generated always as identity primary key,
    source_file_id bigint not null references source_files(id) on delete cascade,
    source_row_number integer not null,
    as_of_date date not null,
    account_name text not null,
    debit numeric(18,2) null,
    credit numeric(18,2) null,
    balance numeric(18,2) null
);

create index if not exists ix_trial_balance_lines_source_file_id on trial_balance_lines(source_file_id);
create index if not exists ix_trial_balance_lines_as_of_date on trial_balance_lines(as_of_date);

create table if not exists profit_loss_monthly_lines (
    id bigint generated always as identity primary key,
    source_file_id bigint not null references source_files(id) on delete cascade,
    source_row_number integer not null,
    line_label text not null,
    line_type text null,
    jan_amount numeric(18,2) null,
    feb_amount numeric(18,2) null,
    mar_amount numeric(18,2) null,
    apr_amount numeric(18,2) null,
    may_amount numeric(18,2) null,
    jun_amount numeric(18,2) null,
    jul_amount numeric(18,2) null,
    aug_amount numeric(18,2) null,
    sep_amount numeric(18,2) null,
    oct_amount numeric(18,2) null,
    nov_amount numeric(18,2) null,
    dec_amount numeric(18,2) null,
    total_amount numeric(18,2) null
);

create index if not exists ix_profit_loss_monthly_lines_source_file_id on profit_loss_monthly_lines(source_file_id);

create table if not exists budget_snapshots (
    id bigint generated always as identity primary key,
    source_file_id bigint null references source_files(id) on delete set null,
    snapshot_name text not null,
    snapshot_date date null,
    created_at timestamptz not null default now(),
    notes text null,
    payload jsonb null
);

create index if not exists ix_budget_snapshots_source_file_id on budget_snapshots(source_file_id);
create index if not exists ix_budget_snapshots_snapshot_date on budget_snapshots(snapshot_date);
