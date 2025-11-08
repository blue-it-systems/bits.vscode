import * as vscode from 'vscode';

export function activate(context: vscode.ExtensionContext) {
    console.log('C# Test Filter Helper is now active');

    // Register command to show test scope
    const showTestScope = vscode.commands.registerCommand('csharp-test-filter.getCurrentTestScope', async () => {
        const testScope = await getTestScope();
        if (testScope) {
            vscode.window.showInformationMessage(`Test Scope: ${testScope}`);
        }
    });

    // Register command to copy test filter
    const copyTestFilter = vscode.commands.registerCommand('csharp-test-filter.copyTestFilter', async () => {
        const testScope = await getTestScope();
        if (testScope) {
            await vscode.env.clipboard.writeText(testScope);
            vscode.window.showInformationMessage(`Test filter copied: ${testScope}`);
        }
    });

    // Register command for input variable (used in launch.json)
    const getTestFilterForInput = vscode.commands.registerCommand('csharp-test-filter.getTestFilterForInput', async () => {
        const testScope = await getTestScope(false); // Silent mode for input variables
        return testScope || ''; // Return empty string if no scope found
    });

    context.subscriptions.push(showTestScope, copyTestFilter, getTestFilterForInput);
}

async function getTestScope(showMessages: boolean = true): Promise<string | undefined> {
    const editor = vscode.window.activeTextEditor;
    
    if (!editor) {
        if (showMessages) {
            vscode.window.showWarningMessage('No active editor');
        }
        return undefined;
    }

    const document = editor.document;
    
    // Check if it's a C# file
    if (document.languageId !== 'csharp') {
        if (showMessages) {
            vscode.window.showWarningMessage('Current file is not a C# file');
        }
        return undefined;
    }

    const position = editor.selection.active;
    const scope = await analyzeTestScope(document, position, showMessages);
    
    return scope;
}

async function analyzeTestScope(document: vscode.TextDocument, position: vscode.Position, showMessages: boolean = true): Promise<string | undefined> {
    const text = document.getText();
    const lines = text.split('\n');
    const currentLine = position.line;

    // Extract assembly name from file path
    const assemblyName = extractAssemblyName(document.uri.fsPath);
    
    // Find the class and method at cursor position
    const className = findClassName(lines, currentLine);
    const methodName = findMethodName(lines, currentLine);

    if (!className) {
        if (showMessages) {
            vscode.window.showWarningMessage('Could not detect class name');
        }
        return undefined;
    }

    // Build the test filter based on TUnit format: Assembly/Namespace.ClassName/MethodName
    let filter = `*/${className}`;
    
    if (methodName) {
        filter += `/${methodName}`;
    } else {
        filter += '/*';
        if (showMessages) {
            vscode.window.showInformationMessage('No method in scope - using class filter');
        }
    }

    return filter;
}

function extractAssemblyName(filePath: string): string {
    // Extract assembly name from the .csproj or directory structure
    const parts = filePath.split('/');
    
    // Look for .csproj pattern or test project naming
    for (let i = parts.length - 1; i >= 0; i--) {
        if (parts[i].includes('.Tests') || parts[i].includes('.Test')) {
            return parts[i];
        }
    }
    
    // Fallback to parent directory name
    return parts[parts.length - 2] || 'UnknownAssembly';
}

function findClassName(lines: string[], currentLine: number): string | undefined {
    // Search backwards from current line to find class declaration
    for (let i = currentLine; i >= 0; i--) {
        const line = lines[i].trim();
        
        // Match class declarations
        const classMatch = line.match(/(?:public|private|internal|protected)?\s*(?:static|abstract|sealed)?\s*(?:partial)?\s*class\s+(\w+)/);
        if (classMatch) {
            return classMatch[1];
        }
    }
    
    return undefined;
}

function findMethodName(lines: string[], currentLine: number): string | undefined {
    let braceCount = 0;
    let foundMethod: string | undefined;
    
    // Search backwards from current line to find method declaration
    for (let i = currentLine; i >= 0; i--) {
        const line = lines[i].trim();
        
        // Count braces to determine if we're inside a method
        const openBraces = (line.match(/{/g) || []).length;
        const closeBraces = (line.match(/}/g) || []).length;
        
        if (i < currentLine) {
            braceCount += closeBraces - openBraces;
        }
        
        // If we've exited all nested scopes and found a method, return it
        if (foundMethod && braceCount < 0) {
            return foundMethod;
        }
        
        // Match method declarations (including async, static, etc.)
        const methodMatch = line.match(/(?:public|private|internal|protected)?\s*(?:static|async|virtual|override|abstract)?\s*(?:async)?\s*(?:\w+(?:<[^>]+>)?)\s+(\w+)\s*\(/);
        if (methodMatch && !foundMethod) {
            foundMethod = methodMatch[1];
            
            // If we're on the same line as the method declaration, return immediately
            if (i === currentLine) {
                return foundMethod;
            }
        }
        
        // Stop at class declaration
        if (line.match(/(?:public|private|internal|protected)?\s*(?:static|abstract|sealed)?\s*(?:partial)?\s*class\s+\w+/)) {
            break;
        }
    }
    
    return foundMethod;
}

export function deactivate() {}
