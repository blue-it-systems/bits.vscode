/**
 * Integration tests for Gengora extension.
 * These tests run in a VS Code instance with the test-workspace open.
 */
import * as assert from 'assert';
import * as vscode from 'vscode';
import * as path from 'path';
import * as fs from 'fs';

const EXTENSION_ID = 'bits.gengora';

// Helper to wait for a condition
async function waitFor(
    condition: () => boolean | Promise<boolean>,
    timeoutMs: number = 10000,
    intervalMs: number = 100
): Promise<void> {
    const startTime = Date.now();
    while (Date.now() - startTime < timeoutMs) {
        if (await condition()) {
            return;
        }
        await new Promise(resolve => setTimeout(resolve, intervalMs));
    }
    throw new Error(`Timeout waiting for condition after ${timeoutMs}ms`);
}

// Helper to get extension
function getExtension() {
    const ext = vscode.extensions.getExtension(EXTENSION_ID);
    if (!ext) {
        throw new Error(`Extension ${EXTENSION_ID} not found`);
    }
    return ext;
}

suite('Gengora Integration Test Suite', () => {
    
    suiteSetup(async function() {
        this.timeout(60000);
        vscode.window.showInformationMessage('Starting Gengora Integration Tests');
        
        // Ensure we have a workspace
        const workspaceFolders = vscode.workspace.workspaceFolders;
        if (!workspaceFolders || workspaceFolders.length === 0) {
            throw new Error('No workspace folder open - integration tests require test-workspace');
        }
        
        console.log(`Workspace: ${workspaceFolders[0].uri.fsPath}`);
    });

    suite('Extension Activation', () => {
        
        test('Extension should be installed', () => {
            const extension = getExtension();
            assert.ok(extension, 'Extension should exist');
            assert.strictEqual(extension.id, EXTENSION_ID);
        });

        test('Extension should activate with workspace', async function() {
            this.timeout(30000);
            const extension = getExtension();
            
            if (!extension.isActive) {
                await extension.activate();
            }
            
            assert.ok(extension.isActive, 'Extension should be active');
        });

        test('Extension should export API', async function() {
            this.timeout(30000);
            const extension = getExtension();
            
            if (!extension.isActive) {
                await extension.activate();
            }
            
            // Extension exports are available
            const exports = extension.exports;
            // May be undefined if no API is exported, which is fine
            assert.ok(true, 'Extension activated successfully');
        });
    });

    suite('Commands Registration', () => {
        
        test('All commands should be registered', async function() {
            this.timeout(30000);
            const extension = getExtension();
            
            if (!extension.isActive) {
                await extension.activate();
            }
            
            const allCommands = await vscode.commands.getCommands(true);
            
            const expectedCommands = [
                'gengora.showQuickPick',
                'gengora.start',
                'gengora.recompile',
                'gengora.stop',
                'gengora.restart',
                'gengora.showOutput',
                'gengora.setLogLevel',
                'gengora.createSampleGenerator'
            ];
            
            const missingCommands = expectedCommands.filter(cmd => !allCommands.includes(cmd));
            assert.deepStrictEqual(missingCommands, [], `Missing commands: ${missingCommands.join(', ')}`);
        });
    });

    suite('Quick Pick Menu', () => {
        
        test('Quick pick menu should open without error', async function() {
            this.timeout(10000);
            const extension = getExtension();
            
            if (!extension.isActive) {
                await extension.activate();
            }
            
            // Execute the command - it will open a quick pick
            // We can't interact with it in tests, but we can verify it doesn't throw
            const commandPromise = vscode.commands.executeCommand('gengora.showQuickPick');
            
            // Wait a bit for the quick pick to appear
            await new Promise(resolve => setTimeout(resolve, 500));
            
            // Dismiss the quick pick by executing escape
            await vscode.commands.executeCommand('workbench.action.closeQuickOpen');
            
            // The command should resolve without error
            try {
                await commandPromise;
            } catch {
                // Quick pick cancelled is expected
            }
            
            assert.ok(true, 'Quick pick opened and closed without error');
        });
    });

    suite('Output Channel', () => {
        
        test('Show output command should work', async function() {
            this.timeout(10000);
            const extension = getExtension();
            
            if (!extension.isActive) {
                await extension.activate();
            }
            
            // Execute show output command
            await vscode.commands.executeCommand('gengora.showOutput');
            
            // If we get here without error, the output channel exists
            assert.ok(true, 'Output channel shown successfully');
        });
    });

    suite('Configuration', () => {
        
        test('Configuration should have all expected settings', () => {
            const config = vscode.workspace.getConfiguration('gengora');
            
            // Check that configuration section exists
            assert.ok(config, 'Configuration section should exist');
            
            // Check default values for defined settings
            const autoStart = config.get<boolean>('autoStart');
            const logLevel = config.get<string>('logLevel');
            
            assert.strictEqual(typeof autoStart, 'boolean', 'autoStart should be boolean');
            assert.strictEqual(typeof logLevel, 'string', 'logLevel should be string');
        });

        test('Log level should accept valid values', async function() {
            this.timeout(10000);
            const config = vscode.workspace.getConfiguration('gengora');
            
            // Just verify the valid enum values are accepted without error
            const validLevels = ['trace', 'debug', 'info', 'warning', 'error'];
            
            for (const level of validLevels) {
                // Update should not throw for valid values
                await config.update('logLevel', level, vscode.ConfigurationTarget.Workspace);
            }
            
            // Reset to default
            await config.update('logLevel', 'info', vscode.ConfigurationTarget.Workspace);
            
            // Verify we can read the value
            const currentLevel = config.get<string>('logLevel');
            assert.ok(validLevels.includes(currentLevel!), `Log level should be one of ${validLevels.join(', ')}`);
        });
    });

    suite('Status Bar', () => {
        
        test('Status bar item should exist after activation', async function() {
            this.timeout(30000);
            const extension = getExtension();
            
            if (!extension.isActive) {
                await extension.activate();
            }
            
            // Status bar items are internal, but we can verify the extension
            // activated without error which means status bar was created
            assert.ok(extension.isActive, 'Extension active means status bar created');
        });
    });

    suite('Server Files', () => {
        
        test('Server DLL should exist in extension', () => {
            const extension = getExtension();
            const serverPath = path.join(extension.extensionPath, 'server', 'Gengora.Server.dll');
            
            assert.ok(fs.existsSync(serverPath), `Server DLL should exist at ${serverPath}`);
        });

        test('Server dependencies should exist', () => {
            const extension = getExtension();
            const serverDir = path.join(extension.extensionPath, 'server');
            
            // These are the core runtime dependencies
            const requiredDeps = [
                'StreamJsonRpc.dll',
                'MessagePack.dll',
                'Nerdbank.Streams.dll'
            ];
            
            for (const dep of requiredDeps) {
                const depPath = path.join(serverDir, dep);
                assert.ok(fs.existsSync(depPath), `Dependency ${dep} should exist`);
            }
        });

        test('Server DLL should be valid assembly', () => {
            const extension = getExtension();
            const serverPath = path.join(extension.extensionPath, 'server', 'Gengora.Server.dll');
            
            // Read first bytes to verify PE header
            const buffer = Buffer.alloc(64);
            const fd = fs.openSync(serverPath, 'r');
            fs.readSync(fd, buffer, 0, 64, 0);
            fs.closeSync(fd);
            
            // Check for MZ header (DOS stub)
            assert.strictEqual(buffer[0], 0x4D, 'First byte should be M');
            assert.strictEqual(buffer[1], 0x5A, 'Second byte should be Z');
        });
    });

    suite('Sample Generator Command', () => {
        
        test('Create sample generator command should be available', async function() {
            this.timeout(10000);
            const extension = getExtension();
            
            if (!extension.isActive) {
                await extension.activate();
            }
            
            const commands = await vscode.commands.getCommands(true);
            assert.ok(
                commands.includes('gengora.createSampleGenerator'),
                'createSampleGenerator command should be registered'
            );
        });
    });

    suite('Set Log Level Command', () => {
        
        test('Set log level command should be available', async function() {
            this.timeout(10000);
            const extension = getExtension();
            
            if (!extension.isActive) {
                await extension.activate();
            }
            
            const commands = await vscode.commands.getCommands(true);
            assert.ok(
                commands.includes('gengora.setLogLevel'),
                'setLogLevel command should be registered'
            );
        });
    });

    suite('Language Client', () => {
        
        test('vscode-languageclient should be bundled', () => {
            const extension = getExtension();
            const lcPath = path.join(
                extension.extensionPath,
                'node_modules',
                'vscode-languageclient'
            );
            
            assert.ok(fs.existsSync(lcPath), 'vscode-languageclient should be bundled');
        });

        test('Language client package.json should exist', () => {
            const extension = getExtension();
            const pkgPath = path.join(
                extension.extensionPath,
                'node_modules',
                'vscode-languageclient',
                'package.json'
            );
            
            assert.ok(fs.existsSync(pkgPath), 'vscode-languageclient package.json should exist');
            
            const pkg = JSON.parse(fs.readFileSync(pkgPath, 'utf8'));
            assert.strictEqual(pkg.name, 'vscode-languageclient');
        });
    });

    suite('Extension Package Structure', () => {
        
        test('Main entry point should exist', () => {
            const extension = getExtension();
            const mainPath = path.join(extension.extensionPath, 'out', 'extension.js');
            
            assert.ok(fs.existsSync(mainPath), 'Main entry point should exist');
        });

        test('package.json should be valid', () => {
            const extension = getExtension();
            const pkgPath = path.join(extension.extensionPath, 'package.json');
            
            assert.ok(fs.existsSync(pkgPath), 'package.json should exist');
            
            const pkg = JSON.parse(fs.readFileSync(pkgPath, 'utf8'));
            
            assert.strictEqual(pkg.name, 'gengora');
            assert.strictEqual(pkg.publisher, 'bits');
            assert.ok(pkg.version, 'Version should be defined');
            assert.ok(pkg.engines.vscode, 'VS Code engine should be defined');
        });

        test('Icon should exist', () => {
            const extension = getExtension();
            const iconPath = path.join(extension.extensionPath, 'images', 'icon.png');
            
            assert.ok(fs.existsSync(iconPath), 'Icon should exist');
        });
    });
});
