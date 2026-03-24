import { Redirect } from "expo-router";

/**
 * Legacy route: sign-up lives on the login screen (Sign up tab).
 * Keeps old links working without a second registration UI.
 */
export default function RegisterRedirectScreen() {
  return <Redirect href="/login?tab=signup" />;
}
