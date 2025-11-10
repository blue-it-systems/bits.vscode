import * as vscode from 'vscode';

interface TestScopeInfo {
    assembly: string;
    className: string;
    methodName?: string;
    filter: string;
}

interface SymbolInfo {
    name: string;
    kind: vscode.SymbolKind;
    range: vscode.Range;
}

// Cache for document symbols to avoid repeated requests
const symbolCache = new Map<string, { symbols: vscode.DocumentSymbol[], timestamp: number }>();
const CACHE_TTL = 5000; // 5 seconds

// Store last selected test method
let lastTestFilter: string | undefined;

// Reusable output channel
let outputChannel: vscode.OutputChannel | undefined;

export function activate(context: vscode.ExtensionContext) {
    console.log('C# Test Filter Helper is now active');
    
    // Create output channel once
    outputChannel = vscode.window.createOutputChannel('C# Test Filter');

    // Register command to show test scope
    const showTestScope = vscode.commands.registerCommand('csharp-test-filter.getCurrentTestScope', async () => {
        console.log('[C# Test Filter] Command getCurrentTestScope triggered!');
        const testScopeInfo = await getTestScope();
        console.log('[C# Test Filter] getTestScope returned:', testScopeInfo);
        if (testScopeInfo) {
            const details = [
                `Assembly: ${testScopeInfo.assembly}`,
                `Class: ${testScopeInfo.className}`,
                testScopeInfo.methodName ? `Method: ${testScopeInfo.methodName}` : 'Method: (none - class scope)',
                `Filter: ${testScopeInfo.filter}`
            ].join('\n');
            vscode.window.showInformationMessage(`Test Scope:\n${details}`, { modal: false });
        } else {
            console.log('[C# Test Filter] No test scope info returned!');
        }
    });

    // Register command to copy test filter
    const copyTestFilter = vscode.commands.registerCommand('csharp-test-filter.copyTestFilter', async () => {
        const testScopeInfo = await getTestScope();
        if (testScopeInfo) {
            await vscode.env.clipboard.writeText(testScopeInfo.filter);
            vscode.window.showInformationMessage(`Test filter copied: ${testScopeInfo.filter}`);
        }
    });

    // Register command for input variable (used in launch.json)
    const getTestFilterForInput = vscode.commands.registerCommand('csharp-test-filter.getTestFilterForInput', async () => {
        const testScopeInfo = await getTestScope(false); // Silent mode for input variables
        return testScopeInfo || { assembly: '', className: '', filter: '' }; // Return full object
    });

    // Register individual property commands for launch.json flexibility
    const getFilter = vscode.commands.registerCommand('csharp-test-filter.getFilter', async () => {
        const testScopeInfo = await getTestScope(false);
        const filter = testScopeInfo?.filter || '';
        console.log(`[C# Test Filter] Generated filter: ${filter}`);
        console.log(`[C# Test Filter] Full scope:`, testScopeInfo);
        
        // Auto-set breakpoint if method is detected and no breakpoints exist
        if (testScopeInfo?.methodName) {
            await ensureBreakpointAtMethodEntry(testScopeInfo);
        }
        
        // Check if debug output is enabled
        const config = vscode.workspace.getConfiguration('csharpTestFilter');
        const showDebugOutput = config.get<boolean>('showDebugOutput', false);
        
        if (showDebugOutput && outputChannel) {
            outputChannel.clear();
            outputChannel.appendLine(`=== Test Filter Debug Info ===`);
            outputChannel.appendLine(`Filter: ${filter}`);
            outputChannel.appendLine(`Assembly: ${testScopeInfo?.assembly}`);
            outputChannel.appendLine(`Namespace: ${testScopeInfo?.className ? 'extracted from file' : 'not found'}`);
            outputChannel.appendLine(`Class: ${testScopeInfo?.className}`);
            outputChannel.appendLine(`Method: ${testScopeInfo?.methodName || '(none - class level)'}`);
            outputChannel.appendLine(`Detection: Language Server`);
            outputChannel.show(true);
        }
        return filter;
    });

    const getClassName = vscode.commands.registerCommand('csharp-test-filter.getClassName', async () => {
        const testScopeInfo = await getTestScope(false);
        return testScopeInfo?.className || '';
    });

    const getMethodName = vscode.commands.registerCommand('csharp-test-filter.getMethodName', async () => {
        const testScopeInfo = await getTestScope(false);
        return testScopeInfo?.methodName || '';
    });

    context.subscriptions.push(showTestScope, copyTestFilter, getTestFilterForInput, getFilter, getClassName, getMethodName);
}

async function getTestScope(showMessages: boolean = true): Promise<TestScopeInfo | undefined> {
    const editor = vscode.window.activeTextEditor;
    
    if (!editor) {
        if (showMessages) {
            vscode.window.showWarningMessage('No active editor');
        }
        // Return last test filter if available
        if (lastTestFilter) {
            return { assembly: '', className: '', filter: lastTestFilter };
        }
        return undefined;
    }

    const document = editor.document;
    
    // Check if it's a C# file
    if (document.languageId !== 'csharp') {
        if (showMessages) {
            vscode.window.showWarningMessage('Current file is not a C# file');
        }
        // Return last test filter if available
        if (lastTestFilter) {
            return { assembly: '', className: '', filter: lastTestFilter };
        }
        return undefined;
    }

    const position = editor.selection.active;
    
    try {
        const scopeInfo = await analyzeTestScopeWithLanguageServer(document, position, showMessages);
        
        // If we found a test method, save it
        if (scopeInfo?.methodName) {
            lastTestFilter = scopeInfo.filter;
            console.log('[C# Test Filter] Saved last test filter:', lastTestFilter);
        } else if (scopeInfo && !scopeInfo.methodName && lastTestFilter) {
            // We're at class level but have a previous test method - use it
            console.log('[C# Test Filter] Using last test filter:', lastTestFilter);
            return { ...scopeInfo, filter: lastTestFilter };
        }
        
        return scopeInfo;
    } catch (error) {
        console.error('[C# Test Filter] Error using language server, falling back to regex:', error);
        if (showMessages) {
            vscode.window.showWarningMessage('C# language server not available, using fallback method');
        }
        // Fallback to regex-based detection
        const scopeInfo = await analyzeTestScope(document, position, showMessages);
        
        // If we found a test method, save it
        if (scopeInfo?.methodName) {
            lastTestFilter = scopeInfo.filter;
            console.log('[C# Test Filter] Saved last test filter:', lastTestFilter);
        } else if (scopeInfo && !scopeInfo.methodName && lastTestFilter) {
            // We're at class level but have a previous test method - use it
            console.log('[C# Test Filter] Using last test filter:', lastTestFilter);
            return { ...scopeInfo, filter: lastTestFilter };
        }
        
        return scopeInfo;
    }
}

async function ensureBreakpointAtMethodEntry(testScopeInfo: TestScopeInfo): Promise<void> {
    const editor = vscode.window.activeTextEditor;
    if (!editor || !testScopeInfo.methodName) {
        return;
    }

    const document = editor.document;
    
    // Get all symbols to find the method range
    const symbols = await getDocumentSymbols(document);
    if (!symbols || symbols.length === 0) {
        return;
    }

    // Find the method symbol
    const classSymbol = findClassByName(symbols, testScopeInfo.className);
    if (!classSymbol) {
        return;
    }

    const methodSymbol = findMethodByName(classSymbol, testScopeInfo.methodName);
    if (!methodSymbol) {
        return;
    }

    // Check if there are any breakpoints in the method range
    const methodRange = methodSymbol.range;
    const breakpoints = vscode.debug.breakpoints.filter(bp => {
        if (bp instanceof vscode.SourceBreakpoint) {
            return bp.location.uri.toString() === document.uri.toString() &&
                   bp.location.range.start.line >= methodRange.start.line &&
                   bp.location.range.end.line <= methodRange.end.line;
        }
        return false;
    });

    if (breakpoints.length === 0) {
        // No breakpoints found - add one at the opening brace of the method
        let braceLineNumber = methodRange.start.line;
        
        // Find the line with the opening brace
        for (let line = methodRange.start.line; line <= methodRange.end.line; line++) {
            const lineText = document.lineAt(line).text;
            if (lineText.includes('{')) {
                braceLineNumber = line;
                break;
            }
        }

        const breakpoint = new vscode.SourceBreakpoint(
            new vscode.Location(document.uri, new vscode.Position(braceLineNumber, 0))
        );
        
        vscode.debug.addBreakpoints([breakpoint]);
        console.log(`[C# Test Filter] Auto-added breakpoint at line ${braceLineNumber + 1} (opening brace) in method ${testScopeInfo.methodName}`);
    } else {
        console.log(`[C# Test Filter] Breakpoint(s) already exist in method ${testScopeInfo.methodName}`);
    }
}

async function analyzeTestScopeWithLanguageServer(document: vscode.TextDocument, position: vscode.Position, showMessages: boolean = true): Promise<TestScopeInfo | undefined> {
    // Get document symbols from the language server
    const symbols = await getDocumentSymbols(document);
    
    if (!symbols || symbols.length === 0) {
        throw new Error('No symbols available from language server');
    }

    // Extract assembly name from file path
    const assemblyName = extractAssemblyName(document.uri.fsPath);
    
    // Extract namespace from symbols
    const namespace = findNamespaceFromSymbols(symbols);
    
    // Find the class and method at cursor position
    const classSymbol = findClassAtPosition(symbols, position);
    
    if (!classSymbol) {
        if (showMessages) {
            vscode.window.showWarningMessage('Could not detect class at cursor position');
        }
        return undefined;
    }

    const methodSymbol = findMethodAtPosition(classSymbol, position);

    // Build the test filter based on TUnit format: /AssemblyName/Namespace/ClassName/MethodName
    let filter = `/*/${namespace || '*'}/${classSymbol.name}`;
    
    if (methodSymbol) {
        filter += `/${methodSymbol.name}`;
    } else {
        filter += '/*';
        if (showMessages) {
            vscode.window.showInformationMessage('No method in scope - using class filter');
        }
    }

    return {
        assembly: assemblyName,
        className: classSymbol.name,
        methodName: methodSymbol?.name,
        filter: filter
    };
}

async function getDocumentSymbols(document: vscode.TextDocument): Promise<vscode.DocumentSymbol[] | undefined> {
    const cacheKey = document.uri.toString();
    const cached = symbolCache.get(cacheKey);
    
    // Return cached symbols if still valid
    if (cached && Date.now() - cached.timestamp < CACHE_TTL) {
        return cached.symbols;
    }

    // Request symbols from language server
    const symbols = await vscode.commands.executeCommand<vscode.DocumentSymbol[]>(
        'vscode.executeDocumentSymbolProvider',
        document.uri
    );

    if (symbols) {
        symbolCache.set(cacheKey, { symbols, timestamp: Date.now() });
    }

    return symbols;
}

function findNamespaceFromSymbols(symbols: vscode.DocumentSymbol[]): string | undefined {
    for (const symbol of symbols) {
        if (symbol.kind === vscode.SymbolKind.Namespace) {
            return symbol.name;
        }
        // Recursively search in children
        if (symbol.children && symbol.children.length > 0) {
            const nested = findNamespaceFromSymbols(symbol.children);
            if (nested) {
                return nested;
            }
        }
    }
    return undefined;
}

function findClassAtPosition(symbols: vscode.DocumentSymbol[], position: vscode.Position): vscode.DocumentSymbol | undefined {
    for (const symbol of symbols) {
        // Check if this is a class and contains the position
        if (symbol.kind === vscode.SymbolKind.Class && symbol.range.contains(position)) {
            return symbol;
        }
        
        // Recursively search in children (e.g., classes inside namespaces)
        if (symbol.children && symbol.children.length > 0) {
            const nested = findClassAtPosition(symbol.children, position);
            if (nested) {
                return nested;
            }
        }
    }
    return undefined;
}

function findMethodAtPosition(classSymbol: vscode.DocumentSymbol, position: vscode.Position): vscode.DocumentSymbol | undefined {
    if (!classSymbol.children) {
        return undefined;
    }

    for (const child of classSymbol.children) {
        // Check if this is a method and contains the position
        if (child.kind === vscode.SymbolKind.Method && child.range.contains(position)) {
            return child;
        }
    }
    
    return undefined;
}

function findClassByName(symbols: vscode.DocumentSymbol[], className: string): vscode.DocumentSymbol | undefined {
    for (const symbol of symbols) {
        if (symbol.kind === vscode.SymbolKind.Class && symbol.name === className) {
            return symbol;
        }
        // Check children recursively (e.g., classes inside namespaces)
        if (symbol.children && symbol.children.length > 0) {
            const found = findClassByName(symbol.children, className);
            if (found) {
                return found;
            }
        }
    }
    return undefined;
}

function findMethodByName(classSymbol: vscode.DocumentSymbol, methodName: string): vscode.DocumentSymbol | undefined {
    if (!classSymbol.children) {
        return undefined;
    }

    for (const child of classSymbol.children) {
        if (child.kind === vscode.SymbolKind.Method && child.name === methodName) {
            return child;
        }
    }
    
    return undefined;
}

async function analyzeTestScope(document: vscode.TextDocument, position: vscode.Position, showMessages: boolean = true): Promise<TestScopeInfo | undefined> {
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

    // Extract namespace from the file
    const namespace = extractNamespace(lines);

    // Build the test filter based on TUnit format: /AssemblyName/Namespace/ClassName/MethodName
    let filter = `/*/${namespace || '*'}/${className}`;
    
    if (methodName) {
        filter += `/${methodName}`;
    } else {
        filter += '/*';
        if (showMessages) {
            vscode.window.showInformationMessage('No method in scope - using class filter');
        }
    }

    return {
        assembly: assemblyName,
        className: className,
        methodName: methodName,
        filter: filter
    };
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

function extractNamespace(lines: string[]): string | undefined {
    // Search for namespace declaration in the file
    for (const line of lines) {
        const namespaceMatch = line.trim().match(/namespace\s+([\w.]+)/);
        if (namespaceMatch) {
            return namespaceMatch[1];
        }
    }
    return undefined;
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
    let inMethod = false;
    
    // Search backwards from current line to find method declaration
    for (let i = currentLine; i >= 0; i--) {
        const line = lines[i].trim();
        
        // Stop at class declaration - check this FIRST to avoid matching class name as method
        if (line.match(/(?:public|private|internal|protected)?\s*(?:static|abstract|sealed)?\s*(?:partial)?\s*class\s+\w+/)) {
            // If we're ON the class line or haven't entered a method yet, return undefined
            if (i === currentLine || !inMethod) {
                return undefined;
            }
            break;
        }
        
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
        
        // Match method declarations - must have parentheses for parameters
        const methodMatch = line.match(/(?:public|private|internal|protected)?\s*(?:static|async|virtual|override|abstract)?\s*(?:async)?\s*(?:\w+(?:<[^>]+>)?)\s+(\w+)\s*\(/);
        if (methodMatch) {
            foundMethod = methodMatch[1];
            inMethod = true;
            
            // If we're on the same line as the method declaration, return immediately
            if (i === currentLine) {
                return foundMethod;
            }
        }
    }
    
    return inMethod ? foundMethod : undefined;
}

export function deactivate() {}
