# Clerk accounts and access

Town staff sign in through Cognito user pool `us-east-2_DmY7BCBIp` (region `us-east-2`, account profile `steve`). App client `2m6vp91m9938jpbg2efivr2p8k`. Groups are `Clerk`, `Driver`, and `admin` (`admin` is lowercase).

Use the AWS console or the CLI as profile `steve`. Do not put passwords in this repo, in chat, or in email.

## Create a clerk

1. Cognito → User pools → `us-east-2_DmY7BCBIp` → Users → Create user.
2. Username is the name the clerk will type. Set a temporary password and require a change at first sign-in.
3. Add the user to group **Clerk** (rate study, apartments, TIKR, and Trash Truck cards). Use **Driver** only for Trash Truck. Use **admin** for a staff member who should see every card and call the API.

```bash
aws cognito-idp admin-create-user \
  --profile steve --region us-east-2 \
  --user-pool-id us-east-2_DmY7BCBIp \
  --username clerk.name \
  --user-attributes Name=email,Value=clerk.name@townofwiley.gov Name=email_verified,Value=true \
  --desired-delivery-mediums EMAIL

aws cognito-idp admin-add-user-to-group \
  --profile steve --region us-east-2 \
  --user-pool-id us-east-2_DmY7BCBIp \
  --username clerk.name \
  --group-name Clerk
```

A token with no `cognito:groups` claim is still treated as a clerk until groups are assigned. Assign the group before the clerk's first production day.

## Reset a password

Cognito → Users → the person → Reset password. Or:

```bash
aws cognito-idp admin-set-user-password \
  --profile steve --region us-east-2 \
  --user-pool-id us-east-2_DmY7BCBIp \
  --username clerk.name \
  --password '<temporary>' \
  --no-permanent
```

Tell the clerk the temporary password in person or by phone. Do not text or email it.

## Turn on MFA

1. User pool → Sign-in → Multi-factor authentication → Optional (or Required when every clerk has an authenticator app).
2. Allow authenticator apps (TOTP).
3. The clerk enrolls at the Cognito Hosted UI the next time they sign in.

MFA was not changed in AWS from this repo. Confirm the pool setting in the console before telling clerks it is on.

## When sign-in fails

The app shows `/authentication/login-failed` (and the same help on a legacy password error). It tells the clerk to call Town Hall at (719) 829-4974 or email clerk@townofwiley.gov. That contact is public on https://townofwiley.gov. Do not send passwords to that mailbox.
