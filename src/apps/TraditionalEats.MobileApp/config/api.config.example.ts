/**
 * Example API configuration.
 * Copy to api.config.ts to override defaults (e.g. DEV_IP for local testing).
 * See api.config.ts for the full default configuration used by the app.
 */

export interface AppConfig {
  API_BASE_URL: string;
  CHAT_HUB_URL: string;
}

const DEV_IP = process.env.EXPO_PUBLIC_DEV_IP || '192.168.1.100';
const config: AppConfig = {
  API_BASE_URL: `http://${DEV_IP}:5102/api`,
  CHAT_HUB_URL: `http://${DEV_IP}:5012/chatHub`,
};

export { config as APP_CONFIG };
