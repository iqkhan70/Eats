import React, { useState, useEffect } from "react";
import {
  View,
  Text,
  StyleSheet,
  TextInput,
  TouchableOpacity,
  Alert,
  Image,
  KeyboardAvoidingView,
  Platform,
  ScrollView,
  ActivityIndicator,
  Modal,
  Linking,
} from "react-native";
import { useRouter, useLocalSearchParams } from "expo-router";
import { StatusBar } from "expo-status-bar";
import { Ionicons } from "@expo/vector-icons";
import AppHeader from "../components/AppHeader";
import * as AppleAuthentication from "expo-apple-authentication";
import * as WebBrowser from "expo-web-browser";
import * as Application from "expo-application";
import Constants, { ExecutionEnvironment } from "expo-constants";
import { makeRedirectUri, ResponseType } from "expo-auth-session";
import {
  useAuthRequest as useGoogleAuthRequest,
  useIdTokenAuthRequest,
} from "expo-auth-session/providers/google";
import {
  authService,
  LoginCredentials,
  RegisterCredentials,
} from "../services/auth";
import { APP_CONFIG } from "../config/api.config";
import { consumePendingNotificationUrlAsync } from "../services/pushNotifications";

WebBrowser.maybeCompleteAuthSession();

/** Google redirects Android OAuth (installed client) to this scheme, not always com.<package>:/… */
function googleAndroidInstalledAppRedirectUri(
  androidClientId: string | undefined,
): string | undefined {
  const id = androidClientId?.trim();
  if (!id) return undefined;
  const m = id.match(/^([a-zA-Z0-9._-]+)\.apps\.googleusercontent\.com$/);
  if (!m) return undefined;
  // Google’s newer Android / Play Services flows often use /oauth2redirect (see Google Sign-In docs).
  return `com.googleusercontent.apps.${m[1]}:/oauth2redirect`;
}

/**
 * Redirect URIs that must appear on the **Web application** OAuth client (the same ID as
 * EXPO_PUBLIC_GOOGLE_WEB_CLIENT_ID). Native Android Sign-In still calls requestIdToken(webClientId),
 * which can trigger Google’s Web-client + custom-scheme checks on Play even though you are not using
 * expo-auth-session in a store build.
 *
 * Include both /oauthredirect (Expo / older samples) and /oauth2redirect (Google’s Android docs)—if
 * the console is missing the path Google actually uses, you will still see “custom URI / Web client”.
 */
function googleWebClientAuthorizedRedirectUris(
  webClientId: string,
  applicationId: string,
  extraAndroidClientIds: readonly string[],
): string[] {
  const out: string[] = [];
  const push = (s: string) => {
    if (!s || out.includes(s)) return;
    out.push(s);
  };
  const pushFromClientId = (clientId: string) => {
    const m = clientId
      .trim()
      .match(/^([a-zA-Z0-9._-]+)\.apps\.googleusercontent\.com$/);
    if (!m) return;
    const scheme = `com.googleusercontent.apps.${m[1]}`;
    push(`${scheme}:/oauthredirect`);
    push(`${scheme}:/oauth2redirect`);
  };
  pushFromClientId(webClientId);
  if (applicationId.trim()) {
    push(`${applicationId}:/oauthredirect`);
    push(`${applicationId}:/oauth2redirect`);
  }
  for (const id of extraAndroidClientIds) {
    if (id?.trim()) pushFromClientId(id);
  }
  return out;
}

// Figma-inspired design tokens from figma.com
const FIGMA_BLUE = "#0D99FF";
const BG_LIGHT = "#F5F5F5";
const BG_WHITE = "#FFFFFF";
const TEXT_PRIMARY = "#333333";
const TEXT_SECONDARY = "#666666";
const BORDER = "rgba(0, 0, 0, 0.1)";

type Tab = "signin" | "signup";

export default function LoginScreen() {
  const router = useRouter();
  const redirectAfterAuth = React.useCallback(async () => {
    const pendingUrl = await consumePendingNotificationUrlAsync();
    if (pendingUrl?.startsWith("/vendor/")) {
      const roles = await authService.getUserRoles();
      const canOpenVendorRoute =
        roles.includes("Vendor") ||
        roles.includes("Staff") ||
        roles.includes("Admin");

      router.replace((canOpenVendorRoute ? pendingUrl : "/(tabs)") as never);
      return;
    }

    router.replace((pendingUrl || "/(tabs)") as never);
  }, [router]);
  const { tab } = useLocalSearchParams<{ tab?: string }>();
  const [activeTab, setActiveTab] = useState<Tab>(
    tab === "signup" ? "signup" : "signin",
  );

  useEffect(() => {
    if (tab === "signup") setActiveTab("signup");
    else if (tab === "signin") setActiveTab("signin");
  }, [tab]);

  // Sign in state
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [loading, setLoading] = useState(false);

  // Sign up state
  const [firstName, setFirstName] = useState("");
  const [lastName, setLastName] = useState("");
  const [displayName, setDisplayName] = useState("");
  const [signupEmail, setSignupEmail] = useState("");
  const [phoneNumber, setPhoneNumber] = useState("");
  const [signupPassword, setSignupPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [showSignupPassword, setShowSignupPassword] = useState(false);
  const [showConfirmPassword, setShowConfirmPassword] = useState(false);
  const [socialLoading, setSocialLoading] = useState(false);
  const [showAppleEmailModal, setShowAppleEmailModal] = useState(false);
  const [appleEmailRecovery, setAppleEmailRecovery] = useState<{ idToken: string; fullName?: string } | null>(null);
  const [appleRecoveryEmail, setAppleRecoveryEmail] = useState("");

  const googleWebClientId = (process.env.EXPO_PUBLIC_GOOGLE_WEB_CLIENT_ID || process.env.EXPO_PUBLIC_GOOGLE_CLIENT_ID || "").trim();
  const googleIosClientId = (process.env.EXPO_PUBLIC_GOOGLE_IOS_CLIENT_ID || "").trim();
  // Two Android OAuth clients in GCP (different SHA-1s): debug keystore vs release/upload. __DEV__ = local Metro / debug install; false = EAS release / Play.
  const googleAndroidClientIdRelease = (
    process.env.EXPO_PUBLIC_GOOGLE_ANDROID_CLIENT_ID_RELEASE ||
    process.env.EXPO_PUBLIC_GOOGLE_ANDROID_CLIENT_ID ||
    ""
  ).trim();
  const googleAndroidClientIdDebug = (process.env.EXPO_PUBLIC_GOOGLE_ANDROID_CLIENT_ID_DEBUG || "").trim();
  const googleAndroidClientId =
    Platform.OS === "android"
      ? __DEV__
        ? googleAndroidClientIdDebug || googleAndroidClientIdRelease
        : googleAndroidClientIdRelease || googleAndroidClientIdDebug
      : googleAndroidClientIdRelease;

  // When true, Android uses the Web OAuth client (browser flow) like iOS — add com.kram.mobile:/oauthredirect to that Web client’s authorized redirect URIs in Google Cloud.
  const androidOAuthUseWebClient =
    (process.env.EXPO_PUBLIC_GOOGLE_ANDROID_USE_WEB_CLIENT || "")
      .trim()
      .toLowerCase() === "true" ||
    (process.env.EXPO_PUBLIC_GOOGLE_ANDROID_USE_WEB_CLIENT || "").trim() === "1";

  /** If set (https only), Android dev/release builds use in-app browser OAuth with this redirect — avoids native custom-scheme / Web client errors on Play. */
  const androidBrowserOAuthRedirect = (
    process.env.EXPO_PUBLIC_GOOGLE_ANDROID_BROWSER_REDIRECT_URI || ""
  ).trim();

  const isExpoGo = Constants.executionEnvironment === ExecutionEnvironment.StoreClient;

  /** Expo proxy URLs must be https://auth.expo.io/@account/slug — not …/account/slug (missing @). */
  const normalizeAuthExpoRedirect = React.useCallback((raw: string) => {
    const t = raw.trim();
    if (!/^https:\/\/auth\.expo\.io\//i.test(t)) return t;
    try {
      const u = new URL(t);
      if (u.pathname.includes("@")) return t.replace(/\/$/, "");
      const seg = u.pathname.split("/").filter(Boolean);
      if (seg.length >= 2) {
        u.pathname = `/@${seg[0]}/${seg.slice(1).join("/")}`;
        return u.toString().replace(/\/$/, "");
      }
    } catch {
      return t;
    }
    return t;
  }, []);

  /**
   * Expo Go + browser Google OAuth must use an https redirect (e.g. auth.expo.io) with the Web client.
   * Custom schemes (com.kram.mobile:/…) with a Web client id trigger “Custom URI scheme not allowed for Web client”.
   * Dev build / emulator uses native @react-native-google-signin instead — no browser redirect.
   */
  const expoGoogleAuthProxyRedirect = React.useMemo(() => {
    const explicitRaw = (process.env.EXPO_PUBLIC_GOOGLE_AUTH_EXPO_REDIRECT_URI || "").trim();
    if (explicitRaw) return normalizeAuthExpoRedirect(explicitRaw);

    const slug = (Constants.expoConfig?.slug || "kram-mobile").trim();
    const accFromEnv = (process.env.EXPO_PUBLIC_EXPO_ACCOUNT || "").trim();
    const ownerFromAppJson = (Constants.expoConfig?.owner || "").trim();
    const acc = accFromEnv || ownerFromAppJson;
    const full = Constants.expoConfig?.originalFullName;
    const isAnonymousProject =
      typeof full === "string" && full.startsWith("@anonymous/");

    // Prefer stable @owner/slug from app.json or env when Expo reports @anonymous/… (not logged into Expo CLI),
    // so Google Cloud "Authorized redirect URIs" can stay https://auth.expo.io/@yourname/slug.
    if (acc) {
      const fromOwner = normalizeAuthExpoRedirect(
        `https://auth.expo.io/@${acc.replace(/^@/, "")}/${slug}`,
      );
      if (isAnonymousProject) return fromOwner;
    }

    if (typeof full === "string" && full.length > 0) {
      const trimmed = full.replace(/^\//, "");
      return normalizeAuthExpoRedirect(`https://auth.expo.io/${trimmed}`);
    }

    if (acc) {
      return normalizeAuthExpoRedirect(
        `https://auth.expo.io/@${acc.replace(/^@/, "")}/${slug}`,
      );
    }
    return undefined;
  }, [normalizeAuthExpoRedirect]);

  const googleWebClientRedirectUriConsoleHint = React.useMemo(() => {
    if (!googleWebClientId.trim()) return "";
    const pkg = Application.applicationId || "com.kram.mobile";
    const uris = googleWebClientAuthorizedRedirectUris(googleWebClientId, pkg, [
      googleAndroidClientIdDebug,
      googleAndroidClientIdRelease,
    ]);
    const lines = uris.map((u) => `• ${u}`).join("\n");
    const expo =
      typeof expoGoogleAuthProxyRedirect === "string" &&
      expoGoogleAuthProxyRedirect.length > 0
        ? `• ${expoGoogleAuthProxyRedirect} (Expo Go)\n`
        : "";
    const httpsLine =
      androidBrowserOAuthRedirect.length > 0
        ? `\n• ${androidBrowserOAuthRedirect} (Android browser flow — **required** when EXPO_PUBLIC_GOOGLE_ANDROID_BROWSER_REDIRECT_URI is set)\n`
        : "";
    return `\n\nGoogle Cloud → APIs & Services → Credentials → **Web application** client (your EXPO_PUBLIC_GOOGLE_WEB_CLIENT_ID) → **Authorized redirect URIs** → add **every** line below (path matters: both /oauthredirect and /oauth2redirect):\n${httpsLine}${lines}\n${expo}\nSave and wait 5–15 minutes.\n\nThen open **each** OAuth client of type **Android** (debug + release) → **Advanced** → enable **Custom URI scheme** (Google turned this off by default for new apps; without it, Play often fails even if the Web client is correct).\n\nIf you use **EXPO_PUBLIC_GOOGLE_ANDROID_BROWSER_REDIRECT_URI** (https), the app uses **browser** Google Sign-In on Android and does not rely on native custom schemes.`;
  }, [
    googleWebClientId,
    googleAndroidClientIdDebug,
    googleAndroidClientIdRelease,
    expoGoogleAuthProxyRedirect,
    androidBrowserOAuthRedirect,
  ]);

  useEffect(() => {
    if (!__DEV__ || Platform.OS !== "android" || !isExpoGo || !googleWebClientId) return;
    if (expoGoogleAuthProxyRedirect) return;
    console.warn(
      "[Google] Expo Go: set EXPO_PUBLIC_EXPO_ACCOUNT to your Expo username (or EXPO_PUBLIC_GOOGLE_AUTH_EXPO_REDIRECT_URI) so Google OAuth uses https://auth.expo.io/… Add that URL to the Web OAuth client Authorized redirect URIs.",
    );
  }, [isExpoGo, expoGoogleAuthProxyRedirect, googleWebClientId]);

  useEffect(() => {
    if (!__DEV__ || !isExpoGo || !googleWebClientId || !expoGoogleAuthProxyRedirect) return;
    const full = Constants.expoConfig?.originalFullName;
    if (typeof full === "string" && full.startsWith("@anonymous/")) {
      console.warn(
        `[Google] Expo Go: manifest originalFullName is ${full}. If Google opens auth.expo.io then shows "Something went wrong trying to finish signing in", run "npx expo login" so the proxy matches your registered redirect, or set EXPO_PUBLIC_GOOGLE_AUTH_EXPO_REDIRECT_URI to the exact URI in Google Cloud (e.g. ${expoGoogleAuthProxyRedirect}). For a reliable flow use a dev build (native Google Sign-In).`,
      );
    }
  }, [isExpoGo, googleWebClientId, expoGoogleAuthProxyRedirect]);

  // Android dev/release only — @react-native-google-signin is not in Expo Go; dynamic import avoids crashing StoreClient.
  useEffect(() => {
    if (Platform.OS !== "android" || isExpoGo || !googleWebClientId) return;
    let cancelled = false;
    void import("@react-native-google-signin/google-signin")
      .then(({ GoogleSignin }) => {
        if (cancelled) return;
        GoogleSignin.configure({
          webClientId: googleWebClientId,
          offlineAccess: false,
        });
      })
      .catch(() => {
        /* dev build without native module linked */
      });
    return () => {
      cancelled = true;
    };
  }, [googleWebClientId, isExpoGo]);

  const effectiveIos = googleIosClientId || googleWebClientId;
  const effectiveWeb = googleWebClientId || googleIosClientId;
  const fallback = effectiveIos || effectiveWeb || "placeholder";

  const ANDROID_OAUTH_PLACEHOLDER =
    "000000000000000000000000000000000000000000.apps.googleusercontent.com";

  /** Package-based redirect (Web OAuth client on Android, or fallback). */
  const packageRedirectUri = React.useMemo(
    () =>
      makeRedirectUri({
        native: `${Application.applicationId}:/oauthredirect`,
      }),
    [],
  );
  // Browser OAuth can return to a real app route to avoid Expo Router's temporary "page not found" flash.
  const androidBrowserReturnUrl = React.useMemo(
    () =>
      makeRedirectUri({
        native: `${Application.applicationId}:/login`,
      }),
    [],
  );

  const androidInstalledRedirectUri = React.useMemo(
    () => googleAndroidInstalledAppRedirectUri(googleAndroidClientId),
    [googleAndroidClientId],
  );

  /** Must match Google’s redirect and openAuthSessionAsync returnUrl (Android: event.url.startsWith(returnUrl)). */
  const oauthAndroidReturnUrl = React.useMemo(() => {
    if (Platform.OS !== "android") return packageRedirectUri;
    if (androidBrowserOAuthRedirect) return androidBrowserReturnUrl;
    if (isExpoGo || androidOAuthUseWebClient) return packageRedirectUri;
    return androidInstalledRedirectUri ?? packageRedirectUri;
  }, [
    androidBrowserReturnUrl,
    androidInstalledRedirectUri,
    androidOAuthUseWebClient,
    isExpoGo,
    packageRedirectUri,
  ]);

  const googleOAuthSessionActiveRef = React.useRef(false);

  useEffect(() => {
    if (Platform.OS !== "android" || !__DEV__) return;
    const sub = Linking.addEventListener("url", (e) => {
      if (!googleOAuthSessionActiveRef.current) return;
      const u = e.url;
      console.log(
        "[Google] deep link | startsWith(returnUrl)?",
        u.startsWith(oauthAndroidReturnUrl),
        "| returnUrl:",
        oauthAndroidReturnUrl,
        "| url:",
        u,
      );
    });
    return () => sub.remove();
  }, [oauthAndroidReturnUrl]);

  const browserGoogleAuthRequestConfig = React.useMemo(
    () => ({
      webClientId: effectiveWeb || fallback,
      clientId: googleWebClientId || effectiveWeb || fallback,
      redirectUri: androidBrowserOAuthRedirect || packageRedirectUri,
      responseType: ResponseType.IdToken,
    }),
    [
      androidBrowserOAuthRedirect,
      effectiveWeb,
      fallback,
      googleWebClientId,
      packageRedirectUri,
    ],
  );
  const [browserGoogleAuthRequest] =
    useGoogleAuthRequest(browserGoogleAuthRequestConfig);

  const googleAuthRequestConfig =
    Platform.OS === "android" && isExpoGo
      ? {
          webClientId: effectiveWeb || fallback,
          clientId: googleWebClientId || effectiveWeb || fallback,
          ...(expoGoogleAuthProxyRedirect
            ? { redirectUri: expoGoogleAuthProxyRedirect }
            : {}),
        }
      : Platform.OS === "android" && androidOAuthUseWebClient && googleWebClientId
        ? {
            webClientId: effectiveWeb || fallback,
            clientId: googleWebClientId || effectiveWeb || fallback,
            redirectUri: expoGoogleAuthProxyRedirect ?? packageRedirectUri,
          }
        : Platform.OS === "android" && googleAndroidClientId && !androidOAuthUseWebClient
          ? {
              androidClientId: googleAndroidClientId,
              redirectUri: androidInstalledRedirectUri ?? packageRedirectUri,
            }
          : Platform.OS === "android" && !googleAndroidClientId && !androidOAuthUseWebClient
            ? {
                androidClientId: ANDROID_OAUTH_PLACEHOLDER,
                redirectUri: packageRedirectUri,
              }
            : Platform.OS === "android"
              ? {
                  androidClientId: ANDROID_OAUTH_PLACEHOLDER,
                  redirectUri: packageRedirectUri,
                }
              : {
                  iosClientId: effectiveIos || fallback,
                  webClientId: effectiveWeb || fallback,
                  clientId: effectiveWeb || effectiveIos || fallback,
                };
  const [googleAuthRequest, googleResponse, googlePromptAsync] =
    useIdTokenAuthRequest(googleAuthRequestConfig);

  // id_token arrives asynchronously after code exchange; use response state
  const googleSignInPending = React.useRef(false);
  useEffect(() => {
    if (!googleResponse || !googleSignInPending.current) return;
    if (googleResponse.type === "cancel") {
      googleSignInPending.current = false;
      setSocialLoading(false);
      return;
    }
    if (googleResponse.type === "error") {
      googleSignInPending.current = false;
      setSocialLoading(false);
      const p = googleResponse.params as Record<string, string | undefined> | undefined;
      const errCode = p?.error ?? "unknown_error";
      const errDesc = p?.error_description?.trim() ?? "";
      const d = errDesc.toLowerCase();
      const mentionsWebClientOrScheme =
        d.includes("web client") ||
        d.includes("custom scheme") ||
        d.includes("custom uri") ||
        d.includes("redirect_uri") ||
        errCode === "redirect_uri_mismatch";
      const webClientRedirectHint =
        Platform.OS === "android" && mentionsWebClientOrScheme
          ? googleWebClientRedirectUriConsoleHint
          : "";
      const androidAdvancedHint =
        Platform.OS === "android" &&
        (d.includes("custom uri scheme") || d.includes("not enabled for your android client"))
          ? "\n\nAlso: Android OAuth client → Advanced → enable “Custom URI scheme” if Google asks for it."
          : "";
      Alert.alert(
        "Google Sign-In failed",
        errDesc
          ? `${errCode}\n\n${errDesc}${webClientRedirectHint}${androidAdvancedHint}`
          : `${errCode}\n\nIf this is Android: add SHA-1 to the Android OAuth client for package com.kram.mobile, or enable Custom URI scheme (Advanced) if you see that error.${webClientRedirectHint}`,
      );
      return;
    }
    if (googleResponse.type === "dismiss" || googleResponse.type === "locked") {
      googleSignInPending.current = false;
      setSocialLoading(false);
      return;
    }
    if (googleResponse.type !== "success") {
      googleSignInPending.current = false;
      setSocialLoading(false);
      return;
    }
    const idToken = googleResponse.params?.id_token ?? googleResponse.authentication?.idToken;
    if (!idToken) {
      Alert.alert("Sign In Failed", "Could not get sign-in token from Google. Try again.");
      googleSignInPending.current = false;
      setSocialLoading(false);
      return;
    }
    googleSignInPending.current = false;
    authService
      .loginWithExternalToken("google", idToken)
      .then(() => redirectAfterAuth())
      .catch((err: Error) => {
        Alert.alert("Sign In Failed", err.message || "Could not sign in with Google");
      })
      .finally(() => setSocialLoading(false));
  }, [googleResponse, googleWebClientRedirectUriConsoleHint, redirectAfterAuth]);

  const handleContinueWithGoogle = async () => {
    if (__DEV__) {
      const mode =
        Platform.OS === "android"
          ? isExpoGo
            ? "android+ExpoGo"
            : "android+native"
          : Platform.OS === "ios"
            ? "ios"
            : Platform.OS;
      console.log("[Google] Continue with Google pressed", `(${mode})`);
      if (Platform.OS === "android" && isExpoGo) {
        console.warn(
          "[Google] Expo Go uses browser OAuth (Custom Tabs). “Web client / custom scheme” errors = add Authorized redirect URIs on the Web OAuth client, or use a dev build (expo run:android) for native Google Sign-In like the emulator.",
        );
      }
    }
    const hasConfig =
      Platform.OS === "ios"
        ? Boolean(googleWebClientId || googleIosClientId)
        : Platform.OS === "android"
          ? isExpoGo
            ? Boolean(googleWebClientId)
            : Boolean(
                googleWebClientId &&
                  (googleAndroidClientId ||
                    androidOAuthUseWebClient ||
                    androidBrowserOAuthRedirect),
              )
          : Boolean(googleWebClientId);

    if (!hasConfig) {
      Alert.alert(
        "Not Configured",
        Platform.OS === "android" && isExpoGo
          ? "Expo Go: set EXPO_PUBLIC_GOOGLE_WEB_CLIENT_ID and in Google Cloud → Web client → Authorized redirect URIs add https://auth.expo.io/@your-account/your-slug (see Expo Google auth docs). For native Android OAuth, use a development or release build (expo run:android / EAS), not Expo Go."
          : Platform.OS === "android"
            ? androidOAuthUseWebClient
              ? "Set EXPO_PUBLIC_GOOGLE_WEB_CLIENT_ID and add your redirect URI (e.g. com.kram.mobile:/oauthredirect) to that Web client in Google Cloud."
              : "Set EXPO_PUBLIC_GOOGLE_WEB_CLIENT_ID and EXPO_PUBLIC_GOOGLE_ANDROID_CLIENT_ID_DEBUG / _RELEASE (Android OAuth clients in Google Cloud)."
            : Platform.OS === "ios"
              ? "Google Sign-In is not configured. Set EXPO_PUBLIC_GOOGLE_WEB_CLIENT_ID and EXPO_PUBLIC_GOOGLE_IOS_CLIENT_ID in your .env."
              : "Google Sign-In is not configured. Set EXPO_PUBLIC_GOOGLE_WEB_CLIENT_ID in your .env.",
      );
      return;
    }

    if (Platform.OS === "android" && !isExpoGo && androidBrowserOAuthRedirect) {
        if (!browserGoogleAuthRequest?.url) {
        Alert.alert(
          "Google Sign-In unavailable",
          "Google sign-in is still loading. Please try again in a moment.",
        );
        return;
      }

      try {
        setSocialLoading(true);
        googleOAuthSessionActiveRef.current = true;

        const result = await WebBrowser.openAuthSessionAsync(
          browserGoogleAuthRequest.url,
          androidBrowserReturnUrl,
        );

        if (result.type === "cancel" || result.type === "dismiss") {
          return;
        }

        if (result.type !== "success") {
          Alert.alert("Sign In Failed", "Could not complete Google sign-in.");
          return;
        }

        const parsed = browserGoogleAuthRequest.parseReturnUrl(result.url);
        const idToken =
          parsed.params?.id_token ?? parsed.authentication?.idToken;
        if (parsed.type === "success" && idToken) {
          await authService.loginWithExternalToken("google", idToken);
          await redirectAfterAuth();
          return;
        }

        if (parsed.type !== "success") {
          const errMessage =
            parsed.error?.description ||
            parsed.error?.message ||
            "Could not complete Google sign-in.";
          Alert.alert("Sign In Failed", errMessage);
          return;
        }

        Alert.alert(
          "Sign In Failed",
          "Google sign-in completed but no ID token was returned.",
        );
      } catch (error: any) {
        Alert.alert(
          "Sign In Failed",
          error?.message || "Could not sign in with Google",
        );
      } finally {
        googleOAuthSessionActiveRef.current = false;
        setSocialLoading(false);
      }
      return;
    }

    // Android (dev / release): native SDK unless HTTPS browser redirect is configured (Play-friendly, no custom URI scheme).
    if (Platform.OS === "android" && !isExpoGo && !androidBrowserOAuthRedirect) {
      let GoogleSignin: typeof import("@react-native-google-signin/google-signin").GoogleSignin;
      let statusCodes: typeof import("@react-native-google-signin/google-signin").statusCodes;
      try {
        ({ GoogleSignin, statusCodes } = await import(
          "@react-native-google-signin/google-signin"
        ));
      } catch {
        Alert.alert(
          "Google Sign-In unavailable",
          "This build does not include the native Google Sign-In module. Use Expo Go with browser Google, or rebuild: npx expo run:android",
        );
        return;
      }
      try {
        setSocialLoading(true);
        await GoogleSignin.hasPlayServices({ showPlayServicesUpdateDialog: true });
        const res = await GoogleSignin.signIn();
        if (res.type !== "success") {
          return;
        }
        const tokens = await GoogleSignin.getTokens();
        const idToken = tokens.idToken;
        if (!idToken) {
          if (__DEV__) {
            console.warn(
              "[Google Android] getTokens returned no idToken — check EXPO_PUBLIC_GOOGLE_WEB_CLIENT_ID (Web client) in .env",
            );
          }
          Alert.alert(
            "Sign In Failed",
            "No ID token from Google. Set EXPO_PUBLIC_GOOGLE_WEB_CLIENT_ID (Web client) in .env for server-verifiable tokens.",
          );
          return;
        }
        if (__DEV__) {
          console.log(
            "[Google Android] calling BFF:",
            `${APP_CONFIG.API_BASE_URL}/MobileBff/auth/google`,
          );
        }
        try {
          await authService.loginWithExternalToken("google", idToken);
          await redirectAfterAuth();
        } catch (apiError: unknown) {
          const msg =
            apiError instanceof Error ? apiError.message : "Could not complete sign in";
          if (__DEV__) {
            console.warn("[Google Android] backend rejected token (Google OK, API failed):", apiError);
          }
          Alert.alert(
            "Sign In Failed",
            `Your Google account was accepted, but login with our servers failed.\n\n${msg}`,
          );
        }
      } catch (error: unknown) {
        const err = error as { code?: string | number; message?: string };
        if (err.code === statusCodes.SIGN_IN_CANCELLED) {
          return;
        }
        if (__DEV__) {
          console.warn(
            "[Google Android] failed before BFF call (hasPlayServices, signIn, or getTokens):",
            error,
          );
        }
        const rawMsg = err.message || "Could not sign in with Google";
        const codeNum = Number(err.code);
        const isDeveloperError =
          codeNum === 10 ||
          /DEVELOPER_ERROR|developer error/i.test(rawMsg);
        const mentionsCustomWebClient =
          /custom uri scheme|not allowed for.*web|web client/i.test(rawMsg);
        const playSigningHint = !__DEV__
          ? "\n\nPlay Store installs are signed with Google’s app signing key (not your upload key). In Play Console → Test / release → App integrity → App signing: copy the App signing key certificate SHA-1 and add it to the Android OAuth client that matches EXPO_PUBLIC_GOOGLE_ANDROID_CLIENT_ID_RELEASE (package com.kram.mobile). Enable Advanced → Custom URI scheme on that Android client if Google asks."
          : "";
        const gcpWebRedirectHint = mentionsCustomWebClient
          ? googleWebClientRedirectUriConsoleHint
          : "";
        const devHint = isDeveloperError
          ? __DEV__
            ? "\n\nGoogle “Developer Error” = your app signature is not registered for this Android OAuth client. In Google Cloud Console → APIs & Services → Credentials, open the Android client whose ID matches EXPO_PUBLIC_GOOGLE_ANDROID_CLIENT_ID_DEBUG. Set package name com.kram.mobile and add the SHA-1 from your debug keystore:\nkeytool -list -v -keystore ~/.android/debug.keystore -alias androiddebugkey -storepass android -keypass android\n(EAS/local release builds use a different keystore — add that SHA-1 to the release Android client.) Save, wait a few minutes, try again."
            : `\n\nGoogle “Developer Error” on a release/Play build: open the Android OAuth client matching EXPO_PUBLIC_GOOGLE_ANDROID_CLIENT_ID_RELEASE, set package com.kram.mobile, and add the correct SHA-1 (EAS/upload keystore for sideload; Play App signing certificate SHA-1 for Play installs).${playSigningHint}`
          : "";
        Alert.alert("Sign In Failed", `${rawMsg}${gcpWebRedirectHint}${devHint}`);
      } finally {
        setSocialLoading(false);
      }
      return;
    }

    try {
      googleSignInPending.current = true;
      setSocialLoading(true);
      if (Platform.OS === "android") {
        googleOAuthSessionActiveRef.current = true;
      }
      if (__DEV__ && Platform.OS === "android" && googleAuthRequest?.url) {
        const m = googleAuthRequest.url.match(/client_id=([^&]+)/);
        const cid = m ? decodeURIComponent(m[1]) : "";
        console.log(
          "[Google] client_id in browser URL equals WEB env?",
          cid === googleWebClientId,
          "| client_id:",
          cid.slice(0, 48),
          "| execution:",
          Constants.executionEnvironment,
          "| ExpoGo:",
          isExpoGo,
        );
      }
      await googlePromptAsync();
      // Success/cancel handled by useEffect watching googleResponse
    } catch (error: any) {
      googleSignInPending.current = false;
      setSocialLoading(false);
      Alert.alert(
        "Sign In Failed",
        error.message || "Could not sign in with Google",
      );
    } finally {
      googleOAuthSessionActiveRef.current = false;
    }
  };

  const handleContinueWithApple = async () => {
    if (Platform.OS !== "ios") return;
    let identityToken: string | undefined;
    let fullName: string | undefined;
    try {
      setSocialLoading(true);
      setAppleEmailRecovery(null);
      const credential = await AppleAuthentication.signInAsync({
        requestedScopes: [
          AppleAuthentication.AppleAuthenticationScope.FULL_NAME,
          AppleAuthentication.AppleAuthenticationScope.EMAIL,
        ],
      });
      identityToken = credential.identityToken ?? undefined;
      if (!identityToken) {
        throw new Error("No identity token received from Apple");
      }
      fullName = credential.fullName
        ? [credential.fullName.givenName, credential.fullName.familyName]
            .filter(Boolean)
            .join(" ")
        : undefined;
      await authService.loginWithExternalToken(
        "apple",
        identityToken,
        credential.email ?? undefined,
        fullName,
      );
      await redirectAfterAuth();
    } catch (error: any) {
      if (error?.code === "ERR_REQUEST_CANCELED") {
        return;
      }
      if (error?.code === "APPLE_EMAIL_REQUIRED" && identityToken) {
        setAppleEmailRecovery({ idToken: identityToken, fullName });
        setShowAppleEmailModal(true);
        setAppleRecoveryEmail("");
        return;
      }
      Alert.alert(
        "Sign In Failed",
        error?.message || "Could not sign in with Apple",
      );
    } finally {
      setSocialLoading(false);
    }
  };

  const submitAppleEmailRecovery = async () => {
    if (!appleEmailRecovery?.idToken || !appleRecoveryEmail.trim()) {
      Alert.alert("Error", "Please enter your account email");
      return;
    }
    try {
      setSocialLoading(true);
      await authService.loginWithExternalToken(
        "apple",
        appleEmailRecovery.idToken,
        appleRecoveryEmail.trim(),
        appleEmailRecovery.fullName,
      );
      setShowAppleEmailModal(false);
      setAppleEmailRecovery(null);
      setAppleRecoveryEmail("");
      await redirectAfterAuth();
    } catch (error: any) {
      Alert.alert("Sign In Failed", error?.message || "Could not link account");
    } finally {
      setSocialLoading(false);
    }
  };

  const handleLogin = async () => {
    if (!email || !password) {
      Alert.alert("Error", "Please enter both email and password");
      return;
    }

    try {
      setLoading(true);
      const credentials: LoginCredentials = { email, password };
      await authService.login(credentials);
      await redirectAfterAuth();
    } catch (error: any) {
      Alert.alert("Login Failed", error.message || "Invalid credentials");
    } finally {
      setLoading(false);
    }
  };

  const handleRegister = async () => {
    if (!firstName || !lastName) {
      Alert.alert("Error", "Please enter your first and last name");
      return;
    }
    if (!signupEmail || !signupPassword) {
      Alert.alert("Error", "Please enter email and password");
      return;
    }
    if (!phoneNumber) {
      Alert.alert("Error", "Phone number is required for order notifications");
      return;
    }
    if (signupPassword !== confirmPassword) {
      Alert.alert("Error", "Passwords do not match");
      return;
    }
    if (signupPassword.length < 6) {
      Alert.alert("Error", "Password must be at least 6 characters");
      return;
    }

    try {
      setLoading(true);
      const credentials: RegisterCredentials = {
        firstName,
        lastName,
        displayName: displayName || undefined,
        email: signupEmail,
        password: signupPassword,
        phoneNumber,
      };
      await authService.register(credentials);

      const loginCreds: LoginCredentials = {
        email: signupEmail.trim(),
        password: signupPassword,
      };
      try {
        await authService.login(loginCreds);
        await redirectAfterAuth();
      } catch {
        setActiveTab("signin");
      }
    } catch (error: any) {
      Alert.alert(
        "Registration Failed",
        error.message || "Registration failed. Please try again.",
      );
    } finally {
      setLoading(false);
    }
  };

  /** Back: return to previous screen; if nothing to pop (e.g. cold open), go to main tabs. */
  const handleHeaderBack = () => {
    if (router.canGoBack()) {
      router.back();
    } else {
      router.replace("/(tabs)");
    }
  };

  /** Home: always open the main tab bar (home). */
  const goToHome = () => {
    router.replace("/(tabs)");
  };

  return (
    <View style={styles.container}>
      <StatusBar style="light" />
      <AppHeader
        title={activeTab === "signin" ? "Sign in" : "Sign up"}
        onBack={handleHeaderBack}
        right={
          <TouchableOpacity
            onPress={goToHome}
            activeOpacity={0.7}
            hitSlop={{ top: 8, bottom: 8, left: 8, right: 8 }}
            accessibilityRole="button"
            accessibilityLabel="Go to home"
          >
            <Text style={styles.headerHomeText}>Home</Text>
          </TouchableOpacity>
        }
      />
      <KeyboardAvoidingView
        behavior={Platform.OS === "ios" ? "padding" : "height"}
        style={styles.keyboardView}
      >
        <ScrollView
          contentContainerStyle={styles.scrollContent}
          keyboardShouldPersistTaps="handled"
          showsVerticalScrollIndicator={false}
        >
          <View style={styles.brandSection}>
            <View style={styles.brandRow}>
              <Image
                source={require("../assets/logo.png")}
                style={styles.logo}
                resizeMode="contain"
              />
              <Text style={styles.brandTitle}>Kram</Text>
            </View>
            <Text style={styles.brandSubtitle}>
              {activeTab === "signin"
                ? "Connect with nearby vendors"
                : "Create an account"}
            </Text>
          </View>

          <View style={styles.card}>
            <View style={styles.tabBar}>
              <TouchableOpacity
                style={[styles.tab, activeTab === "signin" && styles.tabActive]}
                onPress={() => setActiveTab("signin")}
                activeOpacity={0.8}
              >
                <Text
                  style={[
                    styles.tabText,
                    activeTab === "signin" && styles.tabTextActive,
                  ]}
                >
                  Sign in
                </Text>
              </TouchableOpacity>
              <TouchableOpacity
                style={[styles.tab, activeTab === "signup" && styles.tabActive]}
                onPress={() => setActiveTab("signup")}
                activeOpacity={0.8}
              >
                <Text
                  style={[
                    styles.tabText,
                    activeTab === "signup" && styles.tabTextActive,
                  ]}
                >
                  Sign up
                </Text>
              </TouchableOpacity>
            </View>

            {activeTab === "signin" ? (
              <>
                <View style={styles.inputContainer}>
                  <Ionicons
                    name="mail-outline"
                    size={20}
                    color={TEXT_SECONDARY}
                    style={styles.inputIcon}
                  />
                  <TextInput
                    style={styles.input}
                    placeholder="Email"
                    placeholderTextColor={TEXT_SECONDARY}
                    value={email}
                    onChangeText={setEmail}
                    keyboardType="email-address"
                    autoCapitalize="none"
                    autoComplete="email"
                  />
                </View>

                <View style={styles.inputContainer}>
                  <Ionicons
                    name="lock-closed-outline"
                    size={20}
                    color={TEXT_SECONDARY}
                    style={styles.inputIcon}
                  />
                  <TextInput
                    style={styles.input}
                    placeholder="Password"
                    placeholderTextColor={TEXT_SECONDARY}
                    value={password}
                    onChangeText={setPassword}
                    secureTextEntry={!showPassword}
                    autoCapitalize="none"
                    autoComplete="password"
                  />
                  <TouchableOpacity
                    onPress={() => setShowPassword(!showPassword)}
                    style={styles.eyeIcon}
                    hitSlop={{ top: 10, bottom: 10, left: 10, right: 10 }}
                  >
                    <Ionicons
                      name={showPassword ? "eye-outline" : "eye-off-outline"}
                      size={20}
                      color={TEXT_SECONDARY}
                    />
                  </TouchableOpacity>
                </View>

                <TouchableOpacity
                  style={styles.forgotLink}
                  onPress={() => router.push("/forgot-password")}
                >
                  <Text style={styles.forgotLinkText}>Forgot password?</Text>
                </TouchableOpacity>

                <TouchableOpacity
                  style={[
                    styles.loginButton,
                    loading && styles.loginButtonDisabled,
                  ]}
                  onPress={handleLogin}
                  disabled={loading}
                  activeOpacity={0.8}
                >
                  <Text style={styles.loginButtonText}>
                    {loading ? "Signing In..." : "Sign In"}
                  </Text>
                </TouchableOpacity>

                <View style={styles.divider}>
                  <View style={styles.dividerLine} />
                  <Text style={styles.dividerText}>or</Text>
                  <View style={styles.dividerLine} />
                </View>

                <View style={styles.socialButtons}>
                  <TouchableOpacity
                    style={styles.socialButton}
                    onPress={handleContinueWithGoogle}
                    disabled={socialLoading}
                    activeOpacity={0.8}
                  >
                    <Ionicons
                      name="logo-google"
                      size={20}
                      color={TEXT_PRIMARY}
                    />
                    <Text style={styles.socialButtonText}>
                      Continue with Google
                    </Text>
                  </TouchableOpacity>

                  {Platform.OS === "ios" && (
                    <TouchableOpacity
                      style={styles.socialButton}
                      onPress={handleContinueWithApple}
                      disabled={socialLoading}
                      activeOpacity={0.8}
                    >
                      <Ionicons
                        name="logo-apple"
                        size={22}
                        color={TEXT_PRIMARY}
                      />
                      <Text style={styles.socialButtonText}>
                        Continue with Apple
                      </Text>
                    </TouchableOpacity>
                  )}
                </View>
              </>
            ) : (
              <>
                <View style={styles.inputContainer}>
                  <Ionicons
                    name="person-outline"
                    size={20}
                    color={TEXT_SECONDARY}
                    style={styles.inputIcon}
                  />
                  <TextInput
                    style={styles.input}
                    placeholder="First Name"
                    placeholderTextColor={TEXT_SECONDARY}
                    value={firstName}
                    onChangeText={setFirstName}
                    autoCapitalize="words"
                    autoComplete="given-name"
                  />
                </View>

                <View style={styles.inputContainer}>
                  <Ionicons
                    name="person-outline"
                    size={20}
                    color={TEXT_SECONDARY}
                    style={styles.inputIcon}
                  />
                  <TextInput
                    style={styles.input}
                    placeholder="Last Name"
                    placeholderTextColor={TEXT_SECONDARY}
                    value={lastName}
                    onChangeText={setLastName}
                    autoCapitalize="words"
                    autoComplete="family-name"
                  />
                </View>

                <View style={styles.inputContainer}>
                  <Ionicons
                    name="person-outline"
                    size={20}
                    color={TEXT_SECONDARY}
                    style={styles.inputIcon}
                  />
                  <TextInput
                    style={styles.input}
                    placeholder="Display Name (Optional)"
                    placeholderTextColor={TEXT_SECONDARY}
                    value={displayName}
                    onChangeText={setDisplayName}
                    autoCapitalize="words"
                  />
                </View>

                <View style={styles.inputContainer}>
                  <Ionicons
                    name="mail-outline"
                    size={20}
                    color={TEXT_SECONDARY}
                    style={styles.inputIcon}
                  />
                  <TextInput
                    style={styles.input}
                    placeholder="Email"
                    placeholderTextColor={TEXT_SECONDARY}
                    value={signupEmail}
                    onChangeText={setSignupEmail}
                    keyboardType="email-address"
                    autoCapitalize="none"
                    autoComplete="email"
                  />
                </View>

                <View style={styles.inputContainer}>
                  <Ionicons
                    name="call-outline"
                    size={20}
                    color={TEXT_SECONDARY}
                    style={styles.inputIcon}
                  />
                  <TextInput
                    style={styles.input}
                    placeholder="Phone Number"
                    placeholderTextColor={TEXT_SECONDARY}
                    value={phoneNumber}
                    onChangeText={setPhoneNumber}
                    keyboardType="phone-pad"
                    autoComplete="tel"
                  />
                </View>

                <View style={styles.inputContainer}>
                  <Ionicons
                    name="lock-closed-outline"
                    size={20}
                    color={TEXT_SECONDARY}
                    style={styles.inputIcon}
                  />
                  <TextInput
                    style={styles.input}
                    placeholder="Password"
                    placeholderTextColor={TEXT_SECONDARY}
                    value={signupPassword}
                    onChangeText={setSignupPassword}
                    secureTextEntry={!showSignupPassword}
                    autoCapitalize="none"
                    autoComplete="new-password"
                  />
                  <TouchableOpacity
                    onPress={() => setShowSignupPassword(!showSignupPassword)}
                    style={styles.eyeIcon}
                    hitSlop={{ top: 10, bottom: 10, left: 10, right: 10 }}
                  >
                    <Ionicons
                      name={
                        showSignupPassword ? "eye-outline" : "eye-off-outline"
                      }
                      size={20}
                      color={TEXT_SECONDARY}
                    />
                  </TouchableOpacity>
                </View>

                <View style={styles.inputContainer}>
                  <Ionicons
                    name="lock-closed-outline"
                    size={20}
                    color={TEXT_SECONDARY}
                    style={styles.inputIcon}
                  />
                  <TextInput
                    style={styles.input}
                    placeholder="Confirm Password"
                    placeholderTextColor={TEXT_SECONDARY}
                    value={confirmPassword}
                    onChangeText={setConfirmPassword}
                    secureTextEntry={!showConfirmPassword}
                    autoCapitalize="none"
                  />
                  <TouchableOpacity
                    onPress={() => setShowConfirmPassword(!showConfirmPassword)}
                    style={styles.eyeIcon}
                    hitSlop={{ top: 10, bottom: 10, left: 10, right: 10 }}
                  >
                    <Ionicons
                      name={
                        showConfirmPassword ? "eye-outline" : "eye-off-outline"
                      }
                      size={20}
                      color={TEXT_SECONDARY}
                    />
                  </TouchableOpacity>
                </View>

                <TouchableOpacity
                  style={[
                    styles.loginButton,
                    loading && styles.loginButtonDisabled,
                  ]}
                  onPress={handleRegister}
                  disabled={loading}
                  activeOpacity={0.8}
                >
                  {loading ? (
                    <ActivityIndicator color="#fff" size="small" />
                  ) : (
                    <Text style={styles.loginButtonText}>Sign up</Text>
                  )}
                </TouchableOpacity>

                <View style={styles.divider}>
                  <View style={styles.dividerLine} />
                  <Text style={styles.dividerText}>or</Text>
                  <View style={styles.dividerLine} />
                </View>

                <View style={styles.socialButtons}>
                  <TouchableOpacity
                    style={styles.socialButton}
                    onPress={handleContinueWithGoogle}
                    disabled={socialLoading}
                    activeOpacity={0.8}
                  >
                    <Ionicons
                      name="logo-google"
                      size={20}
                      color={TEXT_PRIMARY}
                    />
                    <Text style={styles.socialButtonText}>
                      Continue with Google
                    </Text>
                  </TouchableOpacity>

                  {Platform.OS === "ios" && (
                    <TouchableOpacity
                      style={styles.socialButton}
                      onPress={handleContinueWithApple}
                      disabled={socialLoading}
                      activeOpacity={0.8}
                    >
                      <Ionicons
                        name="logo-apple"
                        size={22}
                        color={TEXT_PRIMARY}
                      />
                      <Text style={styles.socialButtonText}>
                        Continue with Apple
                      </Text>
                    </TouchableOpacity>
                  )}
                </View>
              </>
            )}
          </View>
        </ScrollView>
      </KeyboardAvoidingView>

      <Modal
        visible={showAppleEmailModal}
        transparent
        animationType="fade"
        onRequestClose={() => setShowAppleEmailModal(false)}
      >
        <View style={styles.modalOverlay}>
          <View style={styles.modalContent}>
            <Text style={styles.modalTitle}>Link Apple Sign In</Text>
            <Text style={styles.modalSubtitle}>
              Enter the email for your account to link Apple Sign In.
            </Text>
            <TextInput
              style={styles.modalInput}
              placeholder="Email"
              placeholderTextColor={TEXT_SECONDARY}
              value={appleRecoveryEmail}
              onChangeText={setAppleRecoveryEmail}
              keyboardType="email-address"
              autoCapitalize="none"
              autoComplete="email"
            />
            <View style={styles.modalButtons}>
              <TouchableOpacity
                style={styles.modalButtonCancel}
                onPress={() => {
                  setShowAppleEmailModal(false);
                  setAppleEmailRecovery(null);
                  setAppleRecoveryEmail("");
                }}
              >
                <Text style={styles.modalButtonCancelText}>Cancel</Text>
              </TouchableOpacity>
              <TouchableOpacity
                style={[styles.modalButtonSubmit, socialLoading && styles.loginButtonDisabled]}
                onPress={submitAppleEmailRecovery}
                disabled={socialLoading}
              >
                {socialLoading ? (
                  <ActivityIndicator color="#fff" size="small" />
                ) : (
                  <Text style={styles.modalButtonSubmitText}>Continue</Text>
                )}
              </TouchableOpacity>
            </View>
          </View>
        </View>
      </Modal>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: BG_LIGHT,
  },
  headerHomeText: {
    fontSize: 15,
    fontWeight: "600",
    color: "#fff",
  },
  keyboardView: {
    flex: 1,
  },
  scrollContent: {
    flexGrow: 1,
    justifyContent: "center",
    paddingHorizontal: 24,
    paddingVertical: 40,
  },
  brandSection: {
    alignItems: "center",
    marginBottom: 32,
  },
  brandRow: {
    flexDirection: "row",
    alignItems: "center",
    gap: 12,
    marginBottom: 12,
  },
  logo: {
    width: 64,
    height: 64,
    borderRadius: 12,
    overflow: "hidden",
  },
  brandTitle: {
    fontSize: 28,
    fontWeight: "700",
    color: TEXT_PRIMARY,
    letterSpacing: -0.5,
  },
  brandSubtitle: {
    fontSize: 15,
    color: TEXT_SECONDARY,
    marginTop: 4,
  },
  card: {
    backgroundColor: BG_WHITE,
    borderRadius: 12,
    padding: 24,
    borderWidth: 1,
    borderColor: BORDER,
  },
  inputContainer: {
    flexDirection: "row",
    alignItems: "center",
    borderWidth: 1,
    borderColor: BORDER,
    borderRadius: 8,
    marginBottom: 16,
    paddingHorizontal: 14,
    backgroundColor: BG_WHITE,
  },
  inputIcon: {
    marginRight: 12,
  },
  input: {
    flex: 1,
    height: 48,
    fontSize: 16,
    color: TEXT_PRIMARY,
  },
  eyeIcon: {
    padding: 4,
    marginLeft: 8,
  },
  tabBar: {
    flexDirection: "row",
    marginBottom: 24,
    borderBottomWidth: 1,
    borderBottomColor: BORDER,
  },
  tab: {
    flex: 1,
    paddingVertical: 12,
    alignItems: "center",
    borderBottomWidth: 2,
    borderBottomColor: "transparent",
  },
  tabActive: {
    borderBottomColor: FIGMA_BLUE,
  },
  tabText: {
    fontSize: 16,
    fontWeight: "500",
    color: TEXT_SECONDARY,
  },
  tabTextActive: {
    color: FIGMA_BLUE,
    fontWeight: "600",
  },
  socialButtons: {
    gap: 12,
    marginTop: 20,
  },
  socialButton: {
    flexDirection: "row",
    alignItems: "center",
    justifyContent: "center",
    paddingVertical: 14,
    paddingHorizontal: 16,
    borderRadius: 8,
    borderWidth: 1,
    borderColor: BORDER,
    backgroundColor: BG_WHITE,
    gap: 10,
  },
  socialButtonText: {
    fontSize: 16,
    fontWeight: "500",
    color: TEXT_PRIMARY,
  },
  divider: {
    flexDirection: "row",
    alignItems: "center",
    marginTop: 20,
    marginBottom: 20,
    gap: 12,
  },
  dividerLine: {
    flex: 1,
    height: 1,
    backgroundColor: BORDER,
  },
  dividerText: {
    fontSize: 14,
    color: TEXT_SECONDARY,
    fontWeight: "500",
  },
  forgotLink: {
    alignSelf: "flex-end",
    marginBottom: 20,
  },
  forgotLinkText: {
    fontSize: 14,
    color: FIGMA_BLUE,
    fontWeight: "500",
  },
  loginButton: {
    backgroundColor: "#f97316",
    paddingVertical: 14,
    borderRadius: 8,
    alignItems: "center",
  },
  loginButtonDisabled: {
    opacity: 0.6,
  },
  loginButtonText: {
    color: "#fff",
    fontSize: 16,
    fontWeight: "600",
  },
  modalOverlay: {
    flex: 1,
    backgroundColor: "rgba(0,0,0,0.5)",
    justifyContent: "center",
    alignItems: "center",
    padding: 24,
  },
  modalContent: {
    backgroundColor: BG_WHITE,
    borderRadius: 12,
    padding: 24,
    width: "100%",
    maxWidth: 340,
  },
  modalTitle: {
    fontSize: 18,
    fontWeight: "600",
    color: TEXT_PRIMARY,
    marginBottom: 8,
  },
  modalSubtitle: {
    fontSize: 14,
    color: TEXT_SECONDARY,
    marginBottom: 16,
    lineHeight: 20,
  },
  modalInput: {
    borderWidth: 1,
    borderColor: BORDER,
    borderRadius: 8,
    paddingHorizontal: 14,
    height: 48,
    fontSize: 16,
    color: TEXT_PRIMARY,
    marginBottom: 20,
  },
  modalButtons: {
    flexDirection: "row",
    gap: 12,
    justifyContent: "flex-end",
  },
  modalButtonCancel: {
    paddingVertical: 12,
    paddingHorizontal: 20,
  },
  modalButtonCancelText: {
    fontSize: 16,
    color: TEXT_SECONDARY,
    fontWeight: "500",
  },
  modalButtonSubmit: {
    backgroundColor: "#f97316",
    paddingVertical: 12,
    paddingHorizontal: 24,
    borderRadius: 8,
    minWidth: 100,
    alignItems: "center",
  },
  modalButtonSubmitText: {
    color: "#fff",
    fontSize: 16,
    fontWeight: "600",
  },
});
