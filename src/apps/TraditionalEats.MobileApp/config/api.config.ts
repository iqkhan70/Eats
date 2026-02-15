/**
 * API / environment configuration (renamed from app.config to avoid Expo app.config resolution).
 *
 * Update these values based on your environment:
 * - Development: Use your computer's IP address for phone testing
 * - ngrok / TestFlight: Use ngrok URLs so TestFlight users can hit your local backend
 * - Staging: Use staging domain (www.caseflowstage.store)
 * - Production: Use production domain (www.kram.tech) for TestFlight/App Store
 */

export interface AppConfig {
  API_BASE_URL: string;
  /** SignalR chat hub URL (ChatService). Same host as BFF in dev, port 5012. */
  CHAT_HUB_URL: string;
}

// Environment selection: 'localhost' | 'ip' | 'ngrok' | 'staging' | 'production'
// Can be set via EXPO_PUBLIC_ENV environment variable
const ENV_MODE = process.env.EXPO_PUBLIC_ENV || 'ip';

// Find your computer's IP address:
// On macOS: ipconfig getifaddr en0
// On Windows: Check ipconfig output
// On Linux: hostname -I
const DEV_IP = process.env.EXPO_PUBLIC_DEV_IP || '192.168.86.248';
const STAGING_DOMAIN = 'www.caseflowstage.store';
// Production host for TestFlight/App Store (no protocol). Override with EXPO_PUBLIC_PRODUCTION_URL=e.g. www.kram.tech
const PRODUCTION_DOMAIN = (process.env.EXPO_PUBLIC_PRODUCTION_URL || 'www.kram.tech').replace(/^https?:\/\//, '').replace(/\/.*$/, '');

// ngrok: for TestFlight / external testers. Run ngrok and set URLs before building.
// You need two tunnels: Mobile BFF (5102) and ChatService (5012). Run in two terminals:
//   ngrok http 5102   → set EXPO_PUBLIC_NGROK_API_URL to the https URL (no trailing slash)
//   ngrok http 5012   → set EXPO_PUBLIC_NGROK_CHAT_URL to the https URL (no trailing slash)
const NGROK_API_URL = (process.env.EXPO_PUBLIC_NGROK_API_URL || '').replace(/\/$/, '');
const NGROK_CHAT_URL = (process.env.EXPO_PUBLIC_NGROK_CHAT_URL || '').replace(/\/$/, '');

// Determine base URL based on environment mode
let apiBaseUrl: string;
let chatHubUrl: string;

if (ENV_MODE === 'staging') {
  // Staging environment (HTTPS)
  apiBaseUrl = `https://${STAGING_DOMAIN}/api`;
  chatHubUrl = `https://${STAGING_DOMAIN}/chatHub`;
} else if (ENV_MODE === 'ngrok') {
  // ngrok – for TestFlight users hitting your local backend
  if (!NGROK_API_URL) {
    throw new Error('EXPO_PUBLIC_NGROK_API_URL is required when EXPO_PUBLIC_ENV=ngrok. Run: ngrok http 5102');
  }
  apiBaseUrl = `${NGROK_API_URL}/api`;
  // Chat requires a second tunnel (ngrok http 5012) and EXPO_PUBLIC_NGROK_CHAT_URL; else order chat won't connect
  chatHubUrl = NGROK_CHAT_URL ? `${NGROK_CHAT_URL}/chatHub` : `${NGROK_API_URL}/chatHub`;
} else if (ENV_MODE === 'localhost') {
  // Localhost development
  apiBaseUrl = 'http://localhost:5102/api';
  chatHubUrl = 'http://localhost:5012/chatHub';
} else if (ENV_MODE === 'ip') {
  // IP address for phone testing on same network
  apiBaseUrl = `http://${DEV_IP}:5102/api`;
  chatHubUrl = `http://${DEV_IP}:5012/chatHub`;
} else {
  // Production – www.kram.tech (TestFlight / App Store). Override with EXPO_PUBLIC_PRODUCTION_URL if needed.
  apiBaseUrl = `https://${PRODUCTION_DOMAIN}/api`;
  chatHubUrl = `https://${PRODUCTION_DOMAIN}/chatHub`;
}

const config: AppConfig = {
  API_BASE_URL: apiBaseUrl,
  CHAT_HUB_URL: chatHubUrl,
};

export { config as APP_CONFIG };
