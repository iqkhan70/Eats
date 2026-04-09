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
import { makeRedirectUri } from "expo-auth-session";
import { useIdTokenAuthRequest } from "expo-auth-session/providers/google";
import {
  GoogleSignin,
  statusCodes,
} from "@react-native-google-signin/google-signin";
import {
  authService,
  LoginCredentials,
  RegisterCredentials,
} from "../services/auth";

WebBrowser.maybeCompleteAuthSession();

/** Google redirects Android OAuth (installed client) to this scheme, not always com.<package>:/… */
function googleAndroidInstalledAppRedirectUri(
  androidClientId: string | undefined,
): string | undefined {
  const id = androidClientId?.trim();
  if (!id) return undefined;
  const m = id.match(/^([a-zA-Z0-9._-]+)\.apps\.googleusercontent\.com$/);
  if (!m) return undefined;
  return `com.googleusercontent.apps.${m[1]}:/oauthredirect`;
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

  const isExpoGo = Constants.executionEnvironment === ExecutionEnvironment.StoreClient;

  // Android dev/release: native Google Sign-In (Play Services) — no Custom Tabs / deep link.
  useEffect(() => {
    if (Platform.OS !== "android" || isExpoGo) return;
    if (!googleWebClientId) return;
    GoogleSignin.configure({
      webClientId: googleWebClientId,
      offlineAccess: false,
    });
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

  const androidInstalledRedirectUri = React.useMemo(
    () => googleAndroidInstalledAppRedirectUri(googleAndroidClientId),
    [googleAndroidClientId],
  );

  /** Must match Google’s redirect and openAuthSessionAsync returnUrl (Android: event.url.startsWith(returnUrl)). */
  const oauthAndroidReturnUrl = React.useMemo(() => {
    if (Platform.OS !== "android") return packageRedirectUri;
    if (isExpoGo || androidOAuthUseWebClient) return packageRedirectUri;
    return androidInstalledRedirectUri ?? packageRedirectUri;
  }, [
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

  const googleAuthRequestConfig =
    Platform.OS === "android" && isExpoGo
      ? {
          webClientId: effectiveWeb || fallback,
          clientId: googleWebClientId || effectiveWeb || fallback,
        }
      : Platform.OS === "android" && androidOAuthUseWebClient && googleWebClientId
        ? {
            webClientId: effectiveWeb || fallback,
            clientId: googleWebClientId || effectiveWeb || fallback,
            redirectUri: packageRedirectUri,
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

  useEffect(() => {
    if (!__DEV__ || Platform.OS !== "android") return;
    const clip = (s: string) => (s ? `${s.slice(0, 28)}…` : "(empty)");
    console.log(
      "[Google] Expo Go:",
      isExpoGo,
      "| Android OAuth via Web client:",
      androidOAuthUseWebClient,
      "| resolved Android client:",
      clip(googleAndroidClientId),
      "| DEBUG:",
      clip(googleAndroidClientIdDebug),
      "| RELEASE:",
      clip(googleAndroidClientIdRelease),
      "| WEB:",
      clip(googleWebClientId),
      "| redirect (auth):",
      isExpoGo ? "(Expo Go → Web client + auth.expo.io)" : oauthAndroidReturnUrl,
      "| installedRedirect:",
      androidInstalledRedirectUri ?? "(none)",
    );
  }, [
    oauthAndroidReturnUrl,
    androidInstalledRedirectUri,
    androidOAuthUseWebClient,
    googleAndroidClientId,
    googleAndroidClientIdDebug,
    googleAndroidClientIdRelease,
    googleWebClientId,
    isExpoGo,
  ]);

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
      const androidCustomSchemeHint =
        Platform.OS === "android" &&
        (d.includes("custom uri scheme") || d.includes("not enabled for your android client"))
          ? "\n\nFix: Google Cloud → Credentials → this Android OAuth client → Advanced → enable “Custom URI scheme” → Save. Wait a few minutes."
          : "";
      Alert.alert(
        "Google Sign-In failed",
        errDesc
          ? `${errCode}\n\n${errDesc}${androidCustomSchemeHint}`
          : `${errCode}\n\nIf this is Android: add SHA-1 to the Android OAuth client for package com.kram.mobile, or enable Custom URI scheme (Advanced) if you see that error.`,
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
      .then(() => router.replace("/(tabs)"))
      .catch((err: Error) => {
        Alert.alert("Sign In Failed", err.message || "Could not sign in with Google");
      })
      .finally(() => setSocialLoading(false));
  }, [googleResponse]);

  const handleContinueWithGoogle = async () => {
    const hasConfig =
      Platform.OS === "ios"
        ? Boolean(googleWebClientId || googleIosClientId)
        : Platform.OS === "android"
          ? isExpoGo
            ? Boolean(googleWebClientId)
            : Boolean(
                googleWebClientId &&
                  (googleAndroidClientId ||
                    androidOAuthUseWebClient),
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

    // Android (dev / release build): native SDK — avoids Custom Tabs + redirect never returning to the app.
    if (Platform.OS === "android" && !isExpoGo) {
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
          throw new Error(
            "No ID token from Google. Set EXPO_PUBLIC_GOOGLE_WEB_CLIENT_ID (Web client) in .env for server-verifiable tokens.",
          );
        }
        await authService.loginWithExternalToken("google", idToken);
        router.replace("/(tabs)");
      } catch (error: unknown) {
        const err = error as { code?: string; message?: string };
        if (err.code === statusCodes.SIGN_IN_CANCELLED) {
          return;
        }
        Alert.alert(
          "Sign In Failed",
          err.message || "Could not sign in with Google",
        );
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
      identityToken = credential.identityToken;
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
      router.replace("/(tabs)");
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
      router.replace("/(tabs)");
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
      router.replace("/(tabs)");
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
        router.replace("/(tabs)");
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
