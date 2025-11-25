import eslint from '@eslint/js';
import tseslint from '@typescript-eslint/eslint-plugin';
import tsparser from '@typescript-eslint/parser';
import globals from 'globals';

export default [
    eslint.configs.recommended,
    {
        files: ['src/**/*.ts'],
        languageOptions: {
            parser: tsparser,
            parserOptions: {
                ecmaVersion: 2022,
                sourceType: 'module'
            },
            globals: {
                ...globals.node,
                // VS Code extension types
                Thenable: 'readonly'
            }
        },
        plugins: {
            '@typescript-eslint': tseslint
        },
        rules: {
            '@typescript-eslint/no-unused-vars': 'warn',
            'no-unused-vars': 'off'
        }
    },
    {
        files: ['src/test/**/*.ts'],
        languageOptions: {
            globals: {
                ...globals.mocha
            }
        }
    },
    {
        ignores: ['out/**', 'node_modules/**', '**/*.js']
    }
];
