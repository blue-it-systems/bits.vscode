import * as assert from 'assert';
import * as vscode from 'vscode';

suite('Extension Test Suite', () => {
    vscode.window.showInformationMessage('Start all tests.');

    test('Extension should be present', () => {
        assert.ok(vscode.extensions.getExtension('blue-it-systems.csharp-test-filter'));
    });

    test('Commands should be registered', async () => {
        const commands = await vscode.commands.getCommands(true);
        assert.ok(commands.includes('csharp-test-filter.getCurrentTestScope'));
        assert.ok(commands.includes('csharp-test-filter.copyTestFilter'));
        assert.ok(commands.includes('csharp-test-filter.getTestFilterForInput'));
    });
});
