# AWS Aurora Private Layout

This note records the private AWS footprint used for the Wiley.co database and the schema deployment target.

## Network footprint

- VPC: `wiley-co-aurora-vpc` (`vpc-0b4e1d7362da22c17`)
- CIDR: `10.50.0.0/16`
- Subnet group: `wiley-co-aurora-subnets`
- Private subnets:
  - `subnet-09ec9eece035f626c` in `us-east-2a` (`10.50.1.0/24`)
  - `subnet-018a592be092fed3f` in `us-east-2c` (`10.50.2.0/24`)
- DB security group: `wiley-co-aurora-db-sg` (`sg-0cacdba1850b420f7`)
- Public access: none
- Internet gateway: none attached to the Aurora VPC
- NAT gateway: none attached to the Aurora VPC

## Aurora resources

- Cluster identifier: `wiley-co-aurora-db`
- Instance identifier: `wiley-co-aurora-db-1`
- Engine: `aurora-postgresql`
- Engine version: `14.17`
- Database name: `wileyco`
- Writer port: `5432`
- HTTP endpoint: enabled
- Secret ARN: managed by AWS Secrets Manager for the master user

## Schema target

Apply the canonical schema from [docs/amplify-db-schema.sql](amplify-db-schema.sql) to the `wileyco` database after the writer instance becomes available.

The schema is already normalized for the import pipeline:

- `import_batches`
- `source_files`
- `source_file_variants`
- `chart_of_accounts`
- `customers`
- `vendors`
- `ledger_entries`
- `ledger_entry_lines`
- `trial_balance_lines`
- `profit_loss_monthly_lines`
- `budget_snapshots`

## Deployment rule

Keep this database private. Do not add public subnets, a public IP path, or a public security group rule for Aurora.
