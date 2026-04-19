from __future__ import annotations

import argparse
import csv
import datetime as dt
import hashlib
import json
import subprocess
from dataclasses import dataclass
from decimal import Decimal, InvalidOperation
from pathlib import Path
from typing import Any

ROOT = Path(__file__).resolve().parents[1]
IMPORT_DIR = ROOT / "Import Data"
REGION = "us-east-2"
RESOURCE_ARN = "arn:aws:rds:us-east-2:570912405222:cluster:wiley-co-aurora-db"
SECRET_ARN = "arn:aws:secretsmanager:us-east-2:570912405222:secret:rds!cluster-4e0416be-f6bd-431a-83a4-6f339e5620f3-gNTse6"
DATABASE = "wileyco"


@dataclass(frozen=True)
class ImportFile:
    path: Path
    canonical_entity: str
    variant_code: str
    normalized_file_name: str


FILES = [
    ImportFile(
        IMPORT_DIR / "Full_TransactionList_ByDate_All.csv",
        "transaction-list-by-date-all",
        "ALL",
        "transaction-list-by-date-all.csv",
    ),
    ImportFile(
        IMPORT_DIR / "Full_GeneralLedger_FY2026.xlsx Util.csv",
        "general-ledger-fy2026",
        "UTIL",
        "general-ledger-fy2026-util.csv",
    ),
    ImportFile(
        IMPORT_DIR / "Full_GeneralLedger_FY2026xlsx WSD.csv",
        "general-ledger-fy2026",
        "WSD",
        "general-ledger-fy2026-wsd.csv",
    ),
]


def aws_data_api(
    command: str, *, sql: str | None = None, transaction_id: str | None = None
) -> dict[str, Any]:
    args = [
        "aws",
        "rds-data",
        command,
        "--region",
        REGION,
        "--resource-arn",
        RESOURCE_ARN,
        "--secret-arn",
        SECRET_ARN,
        "--database",
        DATABASE,
        "--output",
        "json",
    ]
    if sql is not None:
        args.extend(["--sql", sql])
    if transaction_id is not None:
        args.extend(["--transaction-id", transaction_id])

    result = subprocess.run(args, capture_output=True, text=True)
    if result.returncode != 0:
        raise RuntimeError(
            (result.stderr or result.stdout or "AWS Data API call failed").strip()
        )

    payload = result.stdout.strip()
    return json.loads(payload) if payload else {}


def sql_literal(value: Any) -> str:
    if value is None:
        return "NULL"
    if isinstance(value, bool):
        return "TRUE" if value else "FALSE"
    if isinstance(value, dt.date):
        return f"'{value.isoformat()}'"
    if isinstance(value, Decimal):
        return str(value)
    if isinstance(value, int):
        return str(value)

    text = str(value).strip()
    if not text:
        return "NULL"

    return "'" + text.replace("'", "''") + "'"


def file_hash(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def normalized_cell(value: str | None) -> str:
    if value is None:
        return ""
    return value.strip()


def parse_decimal(value: str | None) -> Decimal | None:
    if value is None:
        return None

    text = normalized_cell(value)
    if not text:
        return None

    cleaned = (
        text.replace(",", "")
        .replace("$", "")
        .replace("Ö", "")
        .replace("(", "-")
        .replace(")", "")
    )
    cleaned = cleaned.replace("-SPLIT-", "")
    cleaned = cleaned.strip()
    if not cleaned or cleaned == "-":
        return None

    try:
        return Decimal(cleaned)
    except InvalidOperation:
        return None


def parse_date(value: str | None) -> dt.date | None:
    text = normalized_cell(value)
    if not text:
        return None

    for fmt in ("%m/%d/%Y", "%m/%d/%y", "%Y-%m-%d"):
        try:
            return dt.datetime.strptime(text, fmt).date()
        except ValueError:
            continue
    return None


def trim_row(row: list[str]) -> list[str]:
    return [normalized_cell(cell) for cell in row]


def find_header_row(rows: list[list[str]]) -> tuple[int, dict[str, int]]:
    for index, row in enumerate(rows):
        headers = {value: col_index for col_index, value in enumerate(row) if value}
        if (
            "Type" in headers
            and "Date" in headers
            and ("Amount" in headers or "Balance" in headers)
        ):
            return index, headers
    raise RuntimeError("Could not identify a header row in the CSV file.")


def first_non_empty(row: list[str]) -> str | None:
    for value in row:
        if value:
            return value
    return None


def build_insert_sql(table: str, columns: list[str], rows: list[list[Any]]) -> str:
    if not rows:
        return ""

    column_sql = ", ".join(columns)
    values_sql = []
    for row in rows:
        values_sql.append("(" + ", ".join(sql_literal(value) for value in row) + ")")
    return f"insert into {table} ({column_sql}) values\n" + ",\n".join(values_sql) + ";"


def split_batches(
    items: list[list[Any]], batch_size: int = 100
) -> list[list[list[Any]]]:
    return [items[i : i + batch_size] for i in range(0, len(items), batch_size)]


def decode_record_value(value: dict[str, Any]) -> Any:
    for key in ("stringValue", "longValue", "doubleValue", "booleanValue", "isNull"):
        if key in value:
            if key == "isNull":
                return None
            return value[key]
    return None


def query_single_value(sql: str) -> Any:
    payload = aws_data_api("execute-statement", sql=sql)
    records = payload.get("records", [])
    if not records:
        return None
    return decode_record_value(records[0][0])


def ensure_variant(transaction_id: str, variant_code: str, description: str) -> int:
    sql = (
        "insert into source_file_variants (variant_code, description) values ("
        f"{sql_literal(variant_code)}, {sql_literal(description)}"
        ") on conflict (variant_code) do update set description = excluded.description returning id;"
    )
    payload = aws_data_api("execute-statement", sql=sql, transaction_id=transaction_id)
    return int(decode_record_value(payload["records"][0][0]))


def insert_import_batch(transaction_id: str, batch_name: str, notes: str) -> int:
    sql = (
        "insert into import_batches (batch_name, source_system, status, notes) values ("
        f"{sql_literal(batch_name)}, 'quickbooks-exports', 'completed', {sql_literal(notes)}"
        ") returning id;"
    )
    payload = aws_data_api("execute-statement", sql=sql, transaction_id=transaction_id)
    return int(decode_record_value(payload["records"][0][0]))


def insert_source_file(
    transaction_id: str,
    *,
    batch_id: int,
    variant_id: int,
    canonical_entity: str,
    original_file_name: str,
    normalized_file_name: str,
    file_hash_value: str,
    row_count: int,
    column_count: int,
) -> int:
    sql = (
        "insert into source_files (batch_id, source_file_variant_id, canonical_entity, original_file_name, normalized_file_name, file_hash, row_count, column_count) values ("
        f"{batch_id}, {variant_id}, {sql_literal(canonical_entity)}, {sql_literal(original_file_name)}, {sql_literal(normalized_file_name)}, {sql_literal(file_hash_value)}, {row_count}, {column_count}"
        ") returning id;"
    )
    payload = aws_data_api("execute-statement", sql=sql, transaction_id=transaction_id)
    return int(decode_record_value(payload["records"][0][0]))


def parse_transaction_file(
    path: Path, *, canonical_entity: str
) -> tuple[int, int, list[list[Any]]]:
    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        rows = [trim_row(row) for row in csv.reader(handle)]

    header_index, headers = find_header_row(rows)
    column_count = max(len(row) for row in rows) if rows else 0
    current_account_name: str | None = None
    entries: list[list[Any]] = []

    def get(row: list[str], name: str) -> str | None:
        index = headers.get(name)
        if index is None or index >= len(row):
            return None
        return row[index] or None

    for row_number, row in enumerate(rows[header_index + 1 :], start=header_index + 2):
        if not any(row):
            continue

        entry_type = get(row, "Type")
        entry_date = parse_date(get(row, "Date"))
        transaction_number = get(row, "Num")
        name = get(row, "Name")
        memo = get(row, "Memo")
        amount = parse_decimal(get(row, "Amount"))
        running_balance = parse_decimal(get(row, "Balance"))
        cleared_flag = get(row, "Clr")
        account_name = get(row, "Account")
        split_account = get(row, "Split")

        if canonical_entity == "general-ledger-fy2026":
            if (
                not entry_type
                and not entry_date
                and not transaction_number
                and not name
                and not memo
                and amount is None
            ):
                section_label = first_non_empty(row)
                if section_label:
                    current_account_name = (
                        section_label
                        if not section_label.lower().startswith("total")
                        else current_account_name
                    )
                    entries.append(
                        [
                            row_number,
                            None,
                            (
                                "SECTION"
                                if not section_label.lower().startswith("total")
                                else "TOTAL"
                            ),
                            None,
                            section_label,
                            None,
                            current_account_name,
                            None,
                            running_balance,
                            None,
                            canonical_entity,
                        ]
                    )
                continue

            if not account_name:
                account_name = current_account_name

        if canonical_entity == "transaction-list-by-date-all" and not any(
            (
                entry_type,
                entry_date,
                transaction_number,
                name,
                memo,
                account_name,
                split_account,
                amount,
                running_balance,
            )
        ):
            continue

        if canonical_entity == "general-ledger-fy2026" and not any(
            (
                entry_type,
                entry_date,
                transaction_number,
                name,
                memo,
                account_name,
                split_account,
                amount,
                running_balance,
            )
        ):
            continue

        entries.append(
            [
                row_number,
                entry_date,
                entry_type,
                transaction_number,
                name,
                memo,
                account_name,
                split_account,
                amount,
                running_balance,
                canonical_entity,
            ]
        )

    return len(rows), column_count, entries


def load_rows(
    transaction_id: str, source_file_id: int, entries: list[list[Any]]
) -> None:
    columns = [
        "source_row_number",
        "entry_date",
        "entry_type",
        "transaction_number",
        "name",
        "memo",
        "account_name",
        "split_account",
        "amount",
        "running_balance",
        "cleared_flag",
        "entry_scope",
    ]

    prepared = []
    for entry in entries:
        (
            row_number,
            entry_date,
            entry_type,
            transaction_number,
            name,
            memo,
            account_name,
            split_account,
            amount,
            running_balance,
            entry_scope,
        ) = entry
        prepared.append(
            [
                source_file_id,
                row_number,
                entry_date,
                entry_type,
                transaction_number,
                name,
                memo,
                account_name,
                split_account,
                amount,
                running_balance,
                None,
                entry_scope,
            ]
        )

    insert_columns = [
        "source_file_id",
        *columns,
    ]

    for batch in split_batches(prepared, batch_size=100):
        sql = build_insert_sql("ledger_entries", insert_columns, batch)
        aws_data_api("execute-statement", sql=sql, transaction_id=transaction_id)


def describe_file(file: ImportFile) -> dict[str, Any]:
    csv_rows = []
    with file.path.open("r", encoding="utf-8-sig", newline="") as handle:
        reader = csv.reader(handle)
        for row in reader:
            csv_rows.append(trim_row(row))

    header_index, headers = find_header_row(csv_rows)
    data_rows = [row for row in csv_rows[header_index + 1 :] if any(row)]
    return {
        "row_count": len(csv_rows),
        "column_count": max(len(row) for row in csv_rows) if csv_rows else 0,
        "data_row_count": len(data_rows),
        "headers": list(headers.keys()),
    }


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Load the attached CSV exports into the Aurora database via the RDS Data API."
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Parse files and print counts without writing to Aurora.",
    )
    args = parser.parse_args()

    if not IMPORT_DIR.exists():
        raise FileNotFoundError(f"Missing import directory: {IMPORT_DIR}")

    batch_name = f"attached-import-{dt.datetime.now(dt.UTC).strftime('%Y%m%d-%H%M%S')}"
    notes = "Import of attached ledger CSV files from the workspace."

    print(f"Batch: {batch_name}")
    print()

    file_summaries = []
    for file in FILES:
        if not file.path.exists():
            raise FileNotFoundError(f"Missing input file: {file.path}")

        summary = describe_file(file)
        summary.update(
            {
                "name": file.path.name,
                "canonical_entity": file.canonical_entity,
                "variant_code": file.variant_code,
                "normalized_file_name": file.normalized_file_name,
                "hash": file_hash(file.path),
            }
        )
        file_summaries.append(summary)
        print(
            f"{file.path.name}: rows={summary['row_count']} data_rows={summary['data_row_count']} cols={summary['column_count']} headers={summary['headers']}"
        )

    if args.dry_run:
        print("Dry run complete; no database changes made.")
        return

    transaction_id = aws_data_api("begin-transaction")["transactionId"]
    try:
        batch_id = insert_import_batch(transaction_id, batch_name, notes)

        variant_ids: dict[str, int] = {}
        for file in FILES:
            if file.variant_code not in variant_ids:
                variant_ids[file.variant_code] = ensure_variant(
                    transaction_id,
                    file.variant_code,
                    f"{file.variant_code} source variant",
                )

        for file, summary in zip(FILES, file_summaries):
            existing = query_single_value(
                "select id from source_files where file_hash = "
                + sql_literal(summary["hash"])
                + " limit 1;"
            )
            if existing is not None:
                print(
                    f"Skipping existing file {file.path.name} (source_file_id={existing})"
                )
                continue

            source_file_id = insert_source_file(
                transaction_id,
                batch_id=batch_id,
                variant_id=variant_ids[file.variant_code],
                canonical_entity=file.canonical_entity,
                original_file_name=file.path.name,
                normalized_file_name=file.normalized_file_name,
                file_hash_value=summary["hash"],
                row_count=summary["data_row_count"],
                column_count=summary["column_count"],
            )

            row_count, column_count, entries = parse_transaction_file(
                file.path, canonical_entity=file.canonical_entity
            )
            print(
                f"Loading {file.path.name}: source_file_id={source_file_id}, parsed_rows={len(entries)}, source_rows={row_count}, columns={column_count}"
            )
            load_rows(transaction_id, source_file_id, entries)

        aws_data_api("commit-transaction", transaction_id=transaction_id)
        print("Load complete.")
    except Exception:
        aws_data_api("rollback-transaction", transaction_id=transaction_id)
        raise


if __name__ == "__main__":
    main()
