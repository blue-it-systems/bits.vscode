import { defineConfig } from '@vscode/test-cli';
import * as path from 'path';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));

export default defineConfig([
	{
		// Unit tests - no workspace needed
		label: 'unit',
		files: 'out/test/extension.test.js',
		mocha: {
			timeout: 60000
		}
	},
	{
		// Integration tests - with test-workspace
		label: 'integration',
		files: 'out/test/integration.test.js',
		workspaceFolder: path.resolve(__dirname, '../test-workspace'),
		mocha: {
			timeout: 120000
		}
	}
]);
