import * as assert from 'assert';
import * as vscode from 'vscode';

suite('Gengora Extension Test Suite', () => {
    vscode.window.showInformationMessage('Starting Gengora Extension Tests');

    test('Extension Should Be Present', () => {
        assert.ok(vscode.extensions.getExtension('blue-it-systems.gengora'));
    });

    test('Extension Should Activate', async () => {
        const extension = vscode.extensions.getExtension('blue-it-systems.gengora');
        
        if (extension) {
            await extension.activate();
            assert.ok(extension.isActive);
        }
    });

    test('Commands Should Be Registered', async () => {
        const commands = await vscode.commands.getCommands(true);
        
        assert.ok(commands.includes('gengora.recompile'), 'gengora.recompile command should be registered');
        assert.ok(commands.includes('gengora.stop'), 'gengora.stop command should be registered');
        assert.ok(commands.includes('gengora.showOutput'), 'gengora.showOutput command should be registered');
    });

    test('Configuration Should Have Default Values', () => {
        const config = vscode.workspace.getConfiguration('gengora');
        
        assert.strictEqual(config.get('autoStart'), true);
        assert.strictEqual(config.get('logLevel'), 'debug');
        assert.strictEqual(config.get('serverPath'), '');
    });
});
