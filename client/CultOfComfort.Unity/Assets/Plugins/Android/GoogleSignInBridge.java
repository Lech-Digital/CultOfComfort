package com.lechdigital.cultofcomfort.auth;

import android.app.Activity;
import android.os.CancellationSignal;

import androidx.credentials.Credential;
import androidx.credentials.CredentialManager;
import androidx.credentials.CredentialManagerCallback;
import androidx.credentials.CustomCredential;
import androidx.credentials.GetCredentialRequest;
import androidx.credentials.GetCredentialResponse;
import androidx.credentials.exceptions.GetCredentialCancellationException;
import androidx.credentials.exceptions.GetCredentialException;

import com.google.android.libraries.identity.googleid.GetSignInWithGoogleOption;
import com.google.android.libraries.identity.googleid.GoogleIdTokenCredential;

/**
 * Sign in with Google for Unity (ADR 0007, 2026-10-20): asks Credential Manager for a Google ID token for the server's
 * Web client id and hands it to the listener (an AndroidJavaProxy on the C# side). The server checks the token.
 */
public final class GoogleSignInBridge {
    /** Implemented in C#. Called on the Android main thread. */
    public interface Listener {
        void onToken(String idToken);

        void onCancelled();

        void onError(String message);
    }

    private GoogleSignInBridge() {
    }

    public static void signIn(final Activity activity, final String webClientId, final Listener listener) {
        activity.runOnUiThread(() -> {
            try {
                GetSignInWithGoogleOption option = new GetSignInWithGoogleOption.Builder(webClientId).build();
                GetCredentialRequest request = new GetCredentialRequest.Builder().addCredentialOption(option).build();
                CredentialManager.create(activity).getCredentialAsync(activity, request, new CancellationSignal(), activity.getMainExecutor(),
                    new CredentialManagerCallback<GetCredentialResponse, GetCredentialException>() {
                        @Override
                        public void onResult(GetCredentialResponse response) {
                            Credential credential = response.getCredential();
                            if (credential instanceof CustomCredential
                                && GoogleIdTokenCredential.TYPE_GOOGLE_ID_TOKEN_CREDENTIAL.equals(credential.getType())) {
                                listener.onToken(GoogleIdTokenCredential.createFrom(credential.getData()).getIdToken());
                            } else {
                                listener.onError("Google sent something other than an ID token.");
                            }
                        }

                        @Override
                        public void onError(GetCredentialException e) {
                            if (e instanceof GetCredentialCancellationException) {
                                listener.onCancelled();
                            } else {
                                listener.onError(e.getClass().getSimpleName() + ": " + e.getMessage());
                            }
                        }
                    });
            } catch (Exception e) {
                listener.onError(e.getClass().getSimpleName() + ": " + e.getMessage());
            }
        });
    }
}
