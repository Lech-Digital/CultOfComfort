# Handover — Google Play open testing for Cult of Comfort (2026-10-21)

Where the Play Store launch stands, and what is left. Everything to paste is in `PLAY-LISTING.md`.

## Done

- **Google sign-in, end to end.**
  - Google Cloud project `cult-of-comfort`: the consent screen is External and **In production**. Branding has the logo, the home page `https://lechdigital.nl/` and the privacy link, with authorised domain `lechdigital.nl`.
  - OAuth clients:
    - **Web**: `306329299380-ldti7e8ln5afc69r6mm3g64ldjgfipfu.apps.googleusercontent.com`. It is set in `GoogleSignIn.cs` and in `deploy/docker-compose.prod.yml`, and the server is deployed with it.
    - **Android, debug key**: SHA-1 `BB:C3:BA:10:…:45:6D`.
  - Settings shows "Signed in with Google ✓" once signed in. The village code buttons are gone.
- **lechdigital.nl** (all pushed and live):
  - project page `projects/cult-of-comfort/`, plus a card in the Garage section;
  - `privacy/` and `delete-account/` pages;
  - Selected work shows only live apps; Cleared and CoreChoice are marked Live.
- **Release build.**
  - `BuildScripts.BuildAndroidRelease` makes a signed AAB at `client/CultOfComfort.Unity/Builds/Android/CultOfComfort.aab` (versionCode 1, 0.1.0, target SDK 36, internet permission only).
  - The upload key is `~/keystores/cultofcomfort-upload.jks`, alias `upload`. Its password is in `~/keystores/cultofcomfort-upload.pass.txt`. Upload SHA-1: `18:3F:3C:71:AE:D4:BE:84:32:97:53:8B:5B:C8:5B:C7:BA:A9:68:40`.
  - **Back both files up.**
- **Store assets** (in this folder):
  - `icon-512.png` and `feature-1024x500.png`;
  - eight framed screenshots in `phone/`, captured from the `coc-shots` emulator. `_src/build.mjs` regenerates them and `_src/count.mjs` checks the text lengths.
- **Play Console** (Lech Digital account): the app **"Cult of Comfort: Tower Defense"** exists, app id `4975581921492478316`, package `com.lechdigital.cultofcomfort`, game, free, en-US.
- **Default store listing — check that it was saved.** The short and full descriptions, the icon, the feature graphic and all eight screenshots were entered, but the Chrome extension disconnected before **Save as draft**. If it was lost, every image is in the Play Console asset library ("Add assets" lists them), and the text is in `PLAY-LISTING.md` §2.

## Left to do

1. **Store listing:** put the screenshots in order 1 to 8 (they were added as 2, 3, 5, 6, 1, 8, 7, 4), then save.
2. **Store settings** (`PLAY-LISTING.md` §4): category Game → Strategy, the tags, email `andrzej@lechdigital.nl`, website `https://lechdigital.nl/projects/cult-of-comfort/`.
3. **App content** (§5):
   - privacy policy;
   - app access: all functionality available without login;
   - ads: no;
   - content rating questionnaire;
   - target audience: 13 and over;
   - Data safety, with deletion URL `https://lechdigital.nl/projects/cult-of-comfort/delete-account/`;
   - government, financial and health declarations: none.
4. **Open testing** (§6): countries, a new release with `CultOfComfort.aab`, Play App Signing, release name `0.1.0 (1) — first open test`, release notes from §6. **Send for review and roll out yourself.** The account is an organisation account, so going to production later needs no closed-test gate.
5. **After the first upload** (§7): take the **App signing key SHA-1** from Play Console → App integrity, then in Google Cloud `cult-of-comfort` → Clients, create an **Android** client for `com.lechdigital.cultofcomfort` with that SHA-1. Without it, Google sign-in fails in the Play-installed build. Optionally add a client for the upload-key SHA-1 too.
6. **Leftovers:**
   - The empty Google Cloud project `advance-age-512215-f6` was created by mistake. Delete it when convenient.
   - The `coc-shots` emulator may still be running. Stop it with `adb -s emulator-5554 emu kill`.
   - The Pixel 10 hasn't had the build with the "Signed in ✓" change installed yet. Run `BuildAndroid`, then `adb install -r client/CultOfComfort.Unity/Builds/Android/CultOfComfort.apk`.

## Notes for the next session

- **GitHub:** `git push` under `~/Code/private` works as adziusmaster without switching accounts. `~/.gitconfig-private` handles git, and the `.zshrc` folder hook sets `GH_TOKEN` for `gh`.
- **Play Console uploads:** the file inputs are hidden. Open "Add assets", make the `input[type=file]` visible with JS, give it an aria-label, then use `file_upload` with its ref.
- **New uploads:** every new AAB needs a higher `-versionCode`. Build with
  `CULTOFCOMFORT_KEYSTORE_PASS=$(cat ~/keystores/cultofcomfort-upload.pass.txt) Unity … -executeMethod CultOfComfort.Editor.BuildScripts.BuildAndroidRelease -versionCode N -versionName x.y.z`.
