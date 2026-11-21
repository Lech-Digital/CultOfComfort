package com.lechdigital.cultofcomfort.update;

import android.app.Activity;
import android.content.Intent;
import android.content.pm.PackageInfo;
import android.net.Uri;
import android.os.Build;

import com.google.android.play.core.appupdate.AppUpdateInfo;
import com.google.android.play.core.appupdate.AppUpdateManager;
import com.google.android.play.core.appupdate.AppUpdateManagerFactory;
import com.google.android.play.core.appupdate.AppUpdateOptions;
import com.google.android.play.core.install.InstallStateUpdatedListener;
import com.google.android.play.core.install.model.AppUpdateType;
import com.google.android.play.core.install.model.InstallStatus;
import com.google.android.play.core.install.model.UpdateAvailability;

/**
 * Google Play in-app updates for Unity (2026-11-21, as in Coo): reports what Play offers this copy, starts Play's
 * flexible (background download) or immediate (full-screen) update, installs a downloaded one, and opens the store.
 * The decision is made in C# (UpdatePolicy). A copy not installed from Play gets an error from Play, and the game
 * falls back on the server's word (/api/app-version).
 */
public final class AppUpdateBridge {
    /** Implemented in C#. Called on the Android main thread. */
    public interface Listener {
        /** What Play offers: availability (UpdateAvailability), its version code and priority, the update types allowed. */
        void onInfo(int availability, int availableVersionCode, int priority, boolean immediateAllowed, boolean flexibleAllowed, int installStatus);

        /** A flexible update finished downloading: a restart installs it. */
        void onDownloaded();

        void onError(String message);
    }

    private static AppUpdateManager manager;
    private static AppUpdateInfo lastInfo;
    private static Listener installListener;
    private static InstallStateUpdatedListener registered;

    private AppUpdateBridge() {
    }

    /** This copy's version code. */
    public static long versionCode(Activity activity) {
        try {
            PackageInfo info = activity.getPackageManager().getPackageInfo(activity.getPackageName(), 0);
            return Build.VERSION.SDK_INT >= 28 ? info.getLongVersionCode() : info.versionCode;
        } catch (Exception e) {
            return Long.MAX_VALUE;
        }
    }

    public static void check(final Activity activity, final Listener listener) {
        activity.runOnUiThread(() -> {
            try {
                if (manager == null) {
                    manager = AppUpdateManagerFactory.create(activity);
                }

                installListener = listener;
                if (registered == null) {
                    registered = state -> {
                        if (state.installStatus() == InstallStatus.DOWNLOADED && installListener != null) {
                            installListener.onDownloaded();
                        }
                    };
                    manager.registerListener(registered);
                }

                manager.getAppUpdateInfo()
                    .addOnSuccessListener(info -> {
                        lastInfo = info;
                        listener.onInfo(info.updateAvailability(), info.availableVersionCode(), info.updatePriority(),
                            info.isUpdateTypeAllowed(AppUpdateType.IMMEDIATE), info.isUpdateTypeAllowed(AppUpdateType.FLEXIBLE),
                            info.installStatus());
                    })
                    .addOnFailureListener(e -> listener.onError(e.getClass().getSimpleName() + ": " + e.getMessage()));
            } catch (Exception e) {
                listener.onError(e.getClass().getSimpleName() + ": " + e.getMessage());
            }
        });
    }

    /** Starts Play's update for the last answer: immediate (full-screen) or flexible (in the background). */
    public static boolean start(final Activity activity, final boolean immediate) {
        if (manager == null || lastInfo == null) {
            return false;
        }

        activity.runOnUiThread(() -> {
            try {
                manager.startUpdateFlow(lastInfo, activity,
                    AppUpdateOptions.defaultOptions(immediate ? AppUpdateType.IMMEDIATE : AppUpdateType.FLEXIBLE));
            } catch (Exception e) {
                if (installListener != null) {
                    installListener.onError(e.getClass().getSimpleName() + ": " + e.getMessage());
                }
            }
        });
        return true;
    }

    /** Installs a downloaded flexible update: the app restarts. */
    public static void complete() {
        if (manager != null) {
            manager.completeUpdate();
        }
    }

    /** The game's page in the Play Store (the Play app if there is one, else the web). */
    public static void openStore(final Activity activity) {
        activity.runOnUiThread(() -> {
            String id = activity.getPackageName();
            try {
                activity.startActivity(new Intent(Intent.ACTION_VIEW, Uri.parse("market://details?id=" + id))
                    .addFlags(Intent.FLAG_ACTIVITY_NEW_TASK));
            } catch (Exception e) {
                activity.startActivity(new Intent(Intent.ACTION_VIEW, Uri.parse("https://play.google.com/store/apps/details?id=" + id))
                    .addFlags(Intent.FLAG_ACTIVITY_NEW_TASK));
            }
        });
    }
}
