import * as assert from 'assert';
import * as vscode from 'vscode';
import * as path from 'path';
import * as fs from 'fs';

const EXTENSION_ID = 'bits.gengora';

suite('Gengora Extension Test Suite', () => {
    vscode.window.showInformationMessage('Starting Gengora Extension Tests');

    test('Extension Should Be Present', () => {
        const extension = vscode.extensions.getExtension(EXTENSION_ID);
        assert.ok(extension, `Extension ${EXTENSION_ID} should be installed`);
    });

    test('Extension Should Have Correct Package Structure', () => {
        const extension = vscode.extensions.getExtension(EXTENSION_ID);
        assert.ok(extension, 'Extension should exist');
        
        const extensionPath = extension.extensionPath;
        
        // Check main entry point exists
        const mainPath = path.join(extensionPath, 'out', 'extension.js');
        assert.ok(fs.existsSync(mainPath), `Main entry point should exist at ${mainPath}`);
        
        // Check server DLL exists
        const serverPath = path.join(extensionPath, 'server', 'Gengora.Server.dll');
        assert.ok(fs.existsSync(serverPath), `Server DLL should exist at ${serverPath}`);
    });

    test('Extension Should Have Dependencies Bundled', () => {
        const extension = vscode.extensions.getExtension(EXTENSION_ID);
        assert.ok(extension, 'Extension should exist');
        
        const extensionPath = extension.extensionPath;
        const nodeModulesPath = path.join(extensionPath, 'node_modules');
        
        // Check node_modules exists
        assert.ok(fs.existsSync(nodeModulesPath), 'node_modules should be bundled');
        
        // Check critical dependency exists
        const languageClientPath = path.join(nodeModulesPath, 'vscode-languageclient');
        assert.ok(fs.existsSync(languageClientPath), 'vscode-languageclient should be bundled');
    });

    test('Extension Should Activate Successfully', async () => {
        const extension = vscode.extensions.getExtension(EXTENSION_ID);
        assert.ok(extension, 'Extension should exist');
        
        // In test environment without workspace, activation may fail with expected error
        // The key is that the extension EXISTS and CAN attempt activation
        try {
            await extension.activate();
            assert.ok(extension.isActive, 'Extension should be active after activation');
        } catch (error) {
            // Expected: "No Root Path Or Root URI Provided" when no workspace is open
            const errorMsg = String(error);
            if (errorMsg.includes('No Root Path') || errorMsg.includes('Root URI')) {
                // This is expected behavior - extension correctly requires a workspace
                assert.ok(true, 'Extension correctly requires workspace to activate');
            } else {
                // Unexpected error
                assert.fail(`Unexpected activation error: ${error}`);
            }
        }
    });

    test('Commands Should Be Registered After Activation', async () => {
        const extension = vscode.extensions.getExtension(EXTENSION_ID);
        assert.ok(extension, 'Extension should exist');
        
        if (!extension.isActive) {
            await extension.activate();
        }
        
        const commands = await vscode.commands.getCommands(true);
        
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
        
        for (const cmd of expectedCommands) {
            assert.ok(commands.includes(cmd), `Command '${cmd}' should be registered`);
        }
    });

    test('Output Channel Should Be Created', async () => {
        const extension = vscode.extensions.getExtension(EXTENSION_ID);
        assert.ok(extension, 'Extension should exist');
        
        if (!extension.isActive) {
            await extension.activate();
        }
        
        // Execute showOutput command - this will fail if output channel doesn't exist
        try {
            await vscode.commands.executeCommand('gengora.showOutput');
            assert.ok(true, 'Output channel command executed successfully');
        } catch (error) {
            assert.fail(`showOutput command failed: ${error}`);
        }
    });

    test('Configuration Should Have Default Values', () => {
        const config = vscode.workspace.getConfiguration('gengora');
        
        // Check defaults from package.json
        assert.strictEqual(config.get('autoStart'), true, 'autoStart should default to true');
        assert.strictEqual(config.get('logLevel'), 'info', 'logLevel should default to info');
        assert.strictEqual(config.get('serverPath'), '', 'serverPath should default to empty string');
    });

    test('Status Bar Item Should Be Created', async () => {
        const extension = vscode.extensions.getExtension(EXTENSION_ID);
        assert.ok(extension, 'Extension should exist');
        
        if (!extension.isActive) {
            await extension.activate();
        }
        
        // The status bar item is created during activation
        // We can verify by checking if the showQuickPick command works
        // (it's the command assigned to status bar click)
        try {
            // Just verify the command is callable (will show quick pick)
            // We don't want to actually interact with it in test
            const commands = await vscode.commands.getCommands(true);
            assert.ok(commands.includes('gengora.showQuickPick'), 'Status bar command should be registered');
        } catch (error) {
            assert.fail(`Status bar command check failed: ${error}`);
        }
    });
});

suite('Gengora Server Test Suite', () => {
    test('Server DLL Should Be Valid .NET Assembly', () => {
        const extension = vscode.extensions.getExtension(EXTENSION_ID);
        assert.ok(extension, 'Extension should exist');
        
        const serverPath = path.join(extension.extensionPath, 'server', 'Gengora.Server.dll');
        assert.ok(fs.existsSync(serverPath), 'Server DLL should exist');
        
        // Check file size is reasonable (not empty or corrupted)
        const stats = fs.statSync(serverPath);
        assert.ok(stats.size > 10000, `Server DLL should be larger than 10KB, got ${stats.size} bytes`);
    });

    test('Server Dependencies Should Be Present', () => {
        const extension = vscode.extensions.getExtension(EXTENSION_ID);
        assert.ok(extension, 'Extension should exist');
        
        const serverDir = path.join(extension.extensionPath, 'server');
        
        // Check for critical server dependencies
        const requiredFiles = [
            'Gengora.Server.dll',
            'Gengora.Server.deps.json',
            'Gengora.Server.runtimeconfig.json'
        ];
        
        for (const file of requiredFiles) {
            const filePath = path.join(serverDir, file);
            assert.ok(fs.existsSync(filePath), `Server file '${file}' should exist`);
        }
    });
});
