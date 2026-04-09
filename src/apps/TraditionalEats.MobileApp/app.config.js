// Merges app.json with extra URL schemes so deep links (including OAuth) can open the app.
// Primary OAuth redirect from expo-auth-session: com.<android.package>:/oauthredirect (see login.tsx).
// Optional: schemes derived from Android OAuth client IDs (harmless if unused).
const appJson = require("./app.json");

/** @param {string | undefined} clientId e.g. 123-abc.apps.googleusercontent.com */
function googleAndroidSchemeFromClientId(clientId) {
  if (!clientId || typeof clientId !== "string") return null;
  const m = clientId.trim().match(/^([a-zA-Z0-9._-]+)\.apps\.googleusercontent\.com$/);
  if (!m) return null;
  return `com.googleusercontent.apps.${m[1]}`;
}

/** Expo plugin for @react-native-google-signin/google-signin (no Firebase): requires Web client id prefix. */
function googleSignInIosUrlSchemeFromWebClientId(webClientId) {
  if (!webClientId || typeof webClientId !== "string") return null;
  const m = webClientId.trim().match(/^([a-zA-Z0-9._-]+)\.apps\.googleusercontent\.com$/);
  if (!m) return null;
  return `com.googleusercontent.apps.${m[1]}`;
}

module.exports = () => {
  const expo = { ...appJson.expo };
  const base = Array.isArray(expo.scheme)
    ? [...expo.scheme]
    : expo.scheme
      ? [expo.scheme]
      : [];
  const set = new Set(base);
  for (const id of [
    process.env.EXPO_PUBLIC_GOOGLE_ANDROID_CLIENT_ID_DEBUG,
    process.env.EXPO_PUBLIC_GOOGLE_ANDROID_CLIENT_ID_RELEASE,
    process.env.EXPO_PUBLIC_GOOGLE_ANDROID_CLIENT_ID,
  ]) {
    const s = googleAndroidSchemeFromClientId(id || "");
    if (s) set.add(s);
  }
  const list = [...set];
  const pkg = "com.kram.mobile";
  const rest = list.filter((s) => s !== pkg);
  // Put package scheme first so OAuth / Linking prefer com.<package>:/… (see expo-auth-session Android redirect).
  expo.scheme = list.includes(pkg) ? [pkg, ...rest] : list;

  const gsScheme = googleSignInIosUrlSchemeFromWebClientId(
    process.env.EXPO_PUBLIC_GOOGLE_WEB_CLIENT_ID || "",
  );
  const plugins = [...(expo.plugins || [])];
  if (gsScheme) {
    plugins.push([
      "@react-native-google-signin/google-signin",
      { iosUrlScheme: gsScheme },
    ]);
  }
  expo.plugins = plugins;

  return { expo };
};
