# 0007 — Accounts: Recovery Codes Now, Google Sign-In When It Is Configured

- **Date:** 2026-10-14
- **Status:** Accepted

## Context

A village is owned by a guest token in the device's PlayerPrefs (ADR 0006). Reinstalling the app, or moving to a new phone, loses the village. The owner asked for real accounts, meaning Sign in with Google or Apple, before anyone else plays.

Google sign-in on Android needs things only the owner can create:
- a Google Cloud project with an OAuth "Web" client id (the audience of the ID token the server checks);
- an "Android" client registered with the app's package name and the SHA-1 of its signing key;
- a native sign-in plugin (Credential Manager) in the Unity build.

Apple sign-in needs an Apple developer account, and matters only once there is an iOS build.

## Decision

Two ways to keep a village, behind one inbound port (`IAccountService`):

1. **Recovery codes (live now).** Settings → "My village code" asks `POST /api/auth/recovery-code` for a code such as `ABCD-EFGH-JKLM`:
   - 12 characters from an alphabet without 0/O and 1/I, which gives 60 bits.
   - Only its SHA-256 hash is stored.
   - Each request replaces the previous code.

   On a new device, "Use a code" calls `POST /api/auth/recover`. That issues a new bearer token for the village, so the old device's token stops working (one device at a time). Anything the new device had queued for its own village is dropped. Recover attempts share the guest rate limit: 10 per hour per address.

2. **Google sign-in (server ready, off).** `POST /api/auth/google` takes a Google ID token and verifies it with Google.Apis.Auth (signature, issuer, expiry, and audience equal to `Auth:GoogleClientIds`).
   - If the Google account is already linked, the device gets that village.
   - Otherwise, the current guest village is linked to the account and keeps going.
   - With no guest village, a new village is started.

   While `Auth:GoogleClientIds` is empty, the endpoint answers 501. The client button comes with the native plugin, once the owner has created the OAuth clients.

**Schema:** `players` gains `RecoveryCodeHash` and `GoogleSubject`, both nullable and unique. The live database was made by `EnsureCreated`, which never alters tables, so startup runs an idempotent upgrade: it checks `PRAGMA table_info`, then runs `ALTER TABLE` and `CREATE UNIQUE INDEX IF NOT EXISTS`. A test covers an old database. EF migrations replace this when the next module needs schema changes.

## Consequences

### Positive

- A village survives a reinstall or a new phone today, with no third-party setup.
- Google sign-in needs no further server work: set `Auth__GoogleClientIds__0` in the container's environment and redeploy.
- Tokens and codes are stored only as hashes. A leaked database doesn't hand out villages.

### Negative

- The player has to write the code down. A lost code means a lost village until Google sign-in is switched on.
- The hand-rolled schema upgrade is a stop-gap and must not grow.
- Apple sign-in waits for an iOS build.

## Addendum (2026-10-14): the owner's testing gate

The live server refuses development commands, and the dev buttons are hidden for everyone. To test on a phone:

```sh
# the key lives in deploy/dev.key (git-ignored) and on the server in /opt/cultofcomfort/secrets.env (Village__DevKey)
adb push deploy/dev.key /sdcard/Android/data/com.lechdigital.cultofcomfort/files/dev.key   # then restart the app
adb shell rm /sdcard/Android/data/com.lechdigital.cultofcomfort/files/dev.key             # to switch it off
```

With the file present, Settings shows Tension +50, Skip warning and Fill stores, building cards show Finish now, and every sync carries `X-Dev-Key`. The server lets development commands through only when the key matches (constant-time comparison, at least 24 characters). To rotate the key, write a new one to both places and redeploy.
