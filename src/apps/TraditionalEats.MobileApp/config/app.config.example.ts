/**
 * Example Application Configuration
 * 
 * Copy this file to app.config.ts and update with your values
 */

export interface AppConfig {
  api: {
    baseUrl: string;
    timeout: number;
  };
  environment: 'development' | 'production';
}

// For phone testing: Replace 'localhost' with your computer's IP address
// Find your IP:
//   macOS: ipconfig getifaddr en0
//   Windows: ipconfig (look for IPv4 Address)
//   Linux: hostname -I
const DEV_IP = '192.168.1.100'; // Replace with your computer's IP

const config: AppConfig = {
  api: {
    baseUrl: __DEV__
      ? `http://${DEV_IP}:5102/api`
      : 'https://api.traditionaleats.com/api',
    timeout: 10000,
  },
  environment: __DEV__ ? 'development' : 'production',
};

export default config;
