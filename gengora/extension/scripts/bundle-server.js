#!/usr/bin/env node

/**
 * Bundle the LSP server DLL with the extension for distribution
 * This script copies the compiled server from ../server/bin/Release to extension/bin/
 */

const fs = require('fs');
const path = require('path');

const TARGET_FRAMEWORK = 'net8.0';
const BUILD_CONFIG = 'Release';

const extensionRoot = path.join(__dirname, '..');
const serverRoot = path.join(extensionRoot, '..', 'server');
const serverBinPath = path.join(serverRoot, 'bin', BUILD_CONFIG, TARGET_FRAMEWORK);
const extensionBinPath = path.join(extensionRoot, 'bin', TARGET_FRAMEWORK);

console.log('🔧 Bundling Gengora LSP Server...');
console.log(`   Source: ${serverBinPath}`);
console.log(`   Target: ${extensionBinPath}`);

// Check if server was built
if (!fs.existsSync(serverBinPath)) {
    console.error(`❌ Server not found at ${serverBinPath}`);
    console.error('   Please run: cd ../server && dotnet build -c Release');
    process.exit(1);
}

// Create target directory
if (!fs.existsSync(extensionBinPath)) {
    fs.mkdirSync(extensionBinPath, { recursive: true });
    console.log(`   Created: ${extensionBinPath}`);
}

// Copy all files from server bin to extension bin
function copyRecursive(src, dest) {
    const stats = fs.statSync(src);
    
    if (stats.isDirectory()) {
        if (!fs.existsSync(dest)) {
            fs.mkdirSync(dest, { recursive: true });
        }
        const files = fs.readdirSync(src);
        for (const file of files) {
            copyRecursive(path.join(src, file), path.join(dest, file));
        }
    } else {
        fs.copyFileSync(src, dest);
        console.log(`   ✓ ${path.relative(extensionRoot, dest)}`);
    }
}

try {
    copyRecursive(serverBinPath, extensionBinPath);
    console.log('✅ Server bundled successfully!');
} catch (error) {
    console.error(`❌ Failed to bundle server: ${error.message}`);
    process.exit(1);
}
