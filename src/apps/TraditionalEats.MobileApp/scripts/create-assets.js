// Simple script to create placeholder PNG assets using Node.js
// This creates minimal valid PNG files

const fs = require('fs');
const path = require('path');

// Minimal 1x1 PNG in base64 (transparent)
// This is a valid PNG that can be resized
const minimalPNG = Buffer.from(
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==',
  'base64'
);

const assetsDir = path.join(__dirname, '..', 'assets');

// Ensure assets directory exists
if (!fs.existsSync(assetsDir)) {
  fs.mkdirSync(assetsDir, { recursive: true });
}

// Try to copy Kram logo from WebApp (single source of truth for icon/splash)
const webAppLogo = path.join(__dirname, '..', '..', 'TraditionalEats.WebApp', 'wwwroot', 'images', 'logo.png');
if (fs.existsSync(webAppLogo)) {
  const dest = path.join(assetsDir, 'logo.png');
  fs.copyFileSync(webAppLogo, dest);
  console.log('✅ Copied logo.png from WebApp (Kram logo → app icon)');
}

// Create placeholder files (only if missing; logo.png used for icon, splash, adaptive-icon, favicon)
const assets = [
  { name: 'logo.png', size: '1024x1024' },
  { name: 'icon.png', size: '1024x1024' },
  { name: 'splash.png', size: '2048x2048' },
  { name: 'adaptive-icon.png', size: '1024x1024' },
  { name: 'favicon.png', size: '48x48' }
];

console.log('Creating placeholder assets...');

assets.forEach(asset => {
  const filePath = path.join(assetsDir, asset.name);
  if (fs.existsSync(filePath)) {
    console.log(`⏭️  ${asset.name} already exists, skipping`);
    return;
  }
  fs.writeFileSync(filePath, minimalPNG);
  console.log(`✅ Created ${asset.name} (${asset.size} - placeholder)`);
});

console.log('\n📝 App icon/splash use assets/logo.png (Kram logo).');
console.log('   Use a 1024x1024px logo for best results. If you have the logo in');
console.log('   WebApp wwwroot/images/logo.png, run this script to copy it.');
