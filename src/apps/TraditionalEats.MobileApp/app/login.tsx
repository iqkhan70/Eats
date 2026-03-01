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
} from "react-native";
import { useRouter, useLocalSearchParams } from "expo-router";
import { Ionicons } from "@expo/vector-icons";
import { useSafeAreaInsets } from "react-native-safe-area-context";
import * as AppleAuthentication from "expo-apple-authentication";
import * as WebBrowser from "expo-web-browser";
import { useIdTokenAuthRequest } from "expo-auth-session/providers/google";
import {
  authService,
  LoginCredentials,
  RegisterCredentials,
} from "../services/auth";

WebBrowser.maybeCompleteAuthSession();

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
  const insets = useSafeAreaInsets();
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

  const googleClientId = process.env.EXPO_PUBLIC_GOOGLE_CLIENT_ID;
  const [, , googlePromptAsync] = useIdTokenAuthRequest({
    clientId: googleClientId || "placeholder",
    iosClientId: googleClientId || "placeholder",
    androidClientId: googleClientId || "placeholder",
    webClientId: googleClientId || "placeholder",
  });

  const handleContinueWithGoogle = async () => {
    if (!googleClientId) {
      Alert.alert(
        "Not Configured",
        "Google Sign-In is not configured. Set EXPO_PUBLIC_GOOGLE_CLIENT_ID in your environment.",
      );
      return;
    }
    try {
      setSocialLoading(true);
      const result = await googlePromptAsync();
      if (result?.type === "success" && result.params?.id_token) {
        await authService.loginWithExternalToken(
          "google",
          result.params.id_token,
        );
        router.replace("/(tabs)");
      } else if (result?.type === "cancel") {
        // User cancelled, no need to show error
      } else {
        Alert.alert("Sign In Failed", "Could not sign in with Google");
      }
    } catch (error: any) {
      Alert.alert(
        "Sign In Failed",
        error.message || "Could not sign in with Google",
      );
    } finally {
      setSocialLoading(false);
    }
  };

  const handleContinueWithApple = async () => {
    if (Platform.OS !== "ios") return;
    try {
      setSocialLoading(true);
      const credential = await AppleAuthentication.signInAsync({
        requestedScopes: [
          AppleAuthentication.AppleAuthenticationScope.FULL_NAME,
          AppleAuthentication.AppleAuthenticationScope.EMAIL,
        ],
      });
      const identityToken = credential.identityToken;
      if (!identityToken) {
        throw new Error("No identity token received from Apple");
      }
      const fullName = credential.fullName
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
        // User cancelled, no need to show error
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
      Alert.alert("Success", "Registration successful! Please sign in.", [
        { text: "OK", onPress: () => setActiveTab("signin") },
      ]);
    } catch (error: any) {
      Alert.alert(
        "Registration Failed",
        error.message || "Registration failed. Please try again.",
      );
    } finally {
      setLoading(false);
    }
  };

  return (
    <View style={[styles.container, { paddingTop: insets.top }]}>
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
            <Image
              source={require("../assets/logo.png")}
              style={styles.logo}
              resizeMode="contain"
            />
            <Text style={styles.brandTitle}>Kram</Text>
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
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: BG_LIGHT,
  },
  keyboardView: {
    flex: 1,
  },
  scrollContent: {
    flexGrow: 1,
    paddingHorizontal: 24,
    paddingBottom: 40,
  },
  brandSection: {
    alignItems: "center",
    marginTop: 24,
    marginBottom: 32,
  },
  logo: {
    width: 64,
    height: 64,
    marginBottom: 12,
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
});
