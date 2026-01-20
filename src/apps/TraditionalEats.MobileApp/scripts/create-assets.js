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

// Create placeholder files
const assets = [
  { name: 'icon.png', size: '1024x1024' },
  { name: 'splash.png', size: '2048x2048' },
  { name: 'adaptive-icon.png', size: '1024x1024' },
  { name: 'favicon.png', size: '48x48' }
];

console.log('Creating placeholder assets...');

assets.forEach(asset => {
  const filePath = path.join(assetsDir, asset.name);
  
  // For now, create a minimal PNG
  // Note: These will be very small (1x1) but valid PNGs
  // Expo will handle resizing, or you can replace them with proper images later
  fs.writeFileSync(filePath, minimalPNG);
  console.log(`✅ Created ${asset.name} (${asset.size} - placeholder)`);
});

console.log('\n📝 Note: These are minimal placeholder PNGs.');
console.log('   Replace them with proper images before production:');
console.log('   - icon.png: 1024x1024px');
console.log('   - splash.png: 2048x2048px (or larger)');
console.log('   - adaptive-icon.png: 1024x1024px');
console.log('   - favicon.png: 48x48px (or 16x16, 32x32)');
