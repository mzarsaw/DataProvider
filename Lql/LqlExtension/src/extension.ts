// LQL VS Code Extension
// Simplified implementation without external dependencies

// Mock VS Code API types
interface ExtensionContext {
    subscriptions: any[];
    extensionPath: string;
}

interface TextDocument {
    uri: { fsPath: string };
    getText(): string;
    lineAt(line: number): { text: string };
    lineCount: number;
    languageId: string;
}

interface Position {
    line: number;
    character: number;
}

interface Range {
    start: Position;
    end: Position;
}

interface TextEdit {
    range: Range;
    newText: string;
}

interface Hover {
    contents: string[];
    range?: Range;
}

interface Disposable {
    dispose(): void;
}

// Mock VS Code commands and workspace
const mockVscode = {
    commands: {
        registerCommand: (command: string, callback: (...args: any[]) => any): Disposable => {
            console.log(`Registered command: ${command}`);
            return { dispose: () => {} };
        }
    },
    languages: {
        registerDocumentFormattingProvider: (selector: any, provider: any): Disposable => {
            console.log('Registered formatting provider');
            return { dispose: () => {} };
        },
        registerHoverProvider: (selector: any, provider: any): Disposable => {
            console.log('Registered hover provider');
            return { dispose: () => {} };
        }
    },
    workspace: {
        getConfiguration: (section?: string) => ({
            get: (key: string, defaultValue?: any) => defaultValue,
            has: (key: string) => false,
            inspect: (key: string) => undefined,
            update: (key: string, value: any) => Promise.resolve()
        }),
        onDidChangeConfiguration: (listener: any) => ({ dispose: () => {} })
    },
    window: {
        showInformationMessage: (message: string) => {
            console.log(`Info: ${message}`);
            return Promise.resolve();
        },
        showErrorMessage: (message: string) => {
            console.log(`Error: ${message}`);
            return Promise.resolve();
        },
        showWarningMessage: (message: string) => {
            console.log(`Warning: ${message}`);
            return Promise.resolve();
        }
    },
    Range: class {
        constructor(public start: Position, public end: Position) {}
    },
    Position: class {
        constructor(public line: number, public character: number) {}
    },
    TextEdit: {
        replace: (range: Range, newText: string): TextEdit => ({ range, newText })
    }
};

// LQL Language Features
class LqlLanguageFeatures {
    private settings = {
        formatting: { enabled: true, indentSize: 4 },
        validation: { enabled: true },
        hover: { enabled: true }
    };

    // Format LQL document
    formatDocument(document: TextDocument): TextEdit[] {
        if (!this.settings.formatting.enabled) {
            return [];
        }

        const edits: TextEdit[] = [];
        const text = document.getText();
        const lines = text.split('\n');
        let indentLevel = 0;
        const indentSize = this.settings.formatting.indentSize;

        for (let i = 0; i < lines.length; i++) {
            const line = lines[i];
            const trimmedLine = line.trim();
            
            if (trimmedLine === '') continue;

            // Decrease indent for closing brackets
            if (trimmedLine.match(/^[\}\]\)]/)) {
                indentLevel = Math.max(0, indentLevel - 1);
            }

            const expectedIndent = ' '.repeat(indentLevel * indentSize);
            const currentIndent = line.match(/^\s*/)?.[0] || '';

            if (currentIndent !== expectedIndent) {
                const range = new mockVscode.Range(
                    new mockVscode.Position(i, 0),
                    new mockVscode.Position(i, currentIndent.length)
                );
                edits.push(mockVscode.TextEdit.replace(range, expectedIndent));
            }

            // Increase indent for opening brackets and certain keywords
            if (trimmedLine.match(/[\{\[\(]$/) || 
                trimmedLine.match(/^(let|fn|if|case)\b/)) {
                indentLevel++;
            }
        }

        return edits;
    }

    // Provide hover information
    provideHover(document: TextDocument, position: Position): Hover | undefined {
        if (!this.settings.hover.enabled) {
            return undefined;
        }

        const line = document.lineAt(position.line).text;
        const wordRange = this.getWordRangeAtPosition(line, position.character);
        
        if (!wordRange) {
            return undefined;
        }

        const word = line.substring(wordRange.start, wordRange.end);
        const hoverText = this.getHoverText(word);
        
        if (hoverText) {
            return {
                contents: [hoverText],
                range: new mockVscode.Range(
                    new mockVscode.Position(position.line, wordRange.start),
                    new mockVscode.Position(position.line, wordRange.end)
                )
            };
        }

        return undefined;
    }

    private getWordRangeAtPosition(line: string, character: number): { start: number; end: number } | undefined {
        const wordPattern = /[a-zA-Z_][a-zA-Z0-9_]*/;
        let start = character;
        let end = character;

        // Find word start
        while (start > 0 && wordPattern.test(line[start - 1])) {
            start--;
        }

        // Find word end
        while (end < line.length && wordPattern.test(line[end])) {
            end++;
        }

        if (start === end) {
            return undefined;
        }

        return { start, end };
    }

    private getHoverText(word: string): string | undefined {
        const functionDocs: { [key: string]: string } = {
            'select': 'Projects specified columns from the input data.\n\n**Syntax:** `|> select column1, column2, ...`',
            'filter': 'Filters rows based on a condition.\n\n**Syntax:** `|> filter (condition)`',
            'join': 'Joins two tables based on a condition.\n\n**Syntax:** `|> join table2 on condition`',
            'group_by': 'Groups rows by specified columns.\n\n**Syntax:** `|> group_by column1, column2, ...`',
            'order_by': 'Orders rows by specified columns.\n\n**Syntax:** `|> order_by column [asc|desc]`',
            'having': 'Filters groups based on aggregate conditions.\n\n**Syntax:** `|> having (condition)`',
            'limit': 'Limits the number of returned rows.\n\n**Syntax:** `|> limit n`',
            'offset': 'Skips the first n rows.\n\n**Syntax:** `|> offset n`',
            'count': 'Counts the number of rows.\n\n**Syntax:** `count(*)` or `count(column)`',
            'sum': 'Calculates the sum of numeric values.\n\n**Syntax:** `sum(column)`',
            'avg': 'Calculates the average of numeric values.\n\n**Syntax:** `avg(column)`',
            'max': 'Finds the maximum value.\n\n**Syntax:** `max(column)`',
            'min': 'Finds the minimum value.\n\n**Syntax:** `min(column)`',
            'concat': 'Concatenates strings.\n\n**Syntax:** `concat(str1, str2, ...)`',
            'substring': 'Extracts a substring.\n\n**Syntax:** `substring(string, start, length)`',
            'length': 'Returns the length of a string.\n\n**Syntax:** `length(string)`',
            'trim': 'Removes leading and trailing whitespace.\n\n**Syntax:** `trim(string)`',
            'upper': 'Converts string to uppercase.\n\n**Syntax:** `upper(string)`',
            'lower': 'Converts string to lowercase.\n\n**Syntax:** `lower(string)`'
        };

        return functionDocs[word];
    }

    // Validate LQL document
    validateDocument(document: TextDocument): string[] {
        if (!this.settings.validation.enabled) {
            return [];
        }

        const diagnostics: string[] = [];
        const text = document.getText();
        const lines = text.split(/\r?\n/g);

        for (let i = 0; i < lines.length; i++) {
            const line = lines[i];
            const trimmedLine = line.trim();

            // Skip empty lines and comments
            if (trimmedLine === '' || trimmedLine.startsWith('--')) {
                continue;
            }

            // Check for pipeline operator spacing
            const pipeIndex = line.indexOf('|>');
            if (pipeIndex > 0 && line[pipeIndex - 1] !== ' ') {
                diagnostics.push(`Line ${i + 1}: Pipeline operator should be preceded by a space`);
            }

            // Check for unmatched brackets
            const openBrackets = (line.match(/[\{\[\(]/g) || []).length;
            const closeBrackets = (line.match(/[\}\]\)]/g) || []).length;
            if (openBrackets !== closeBrackets) {
                diagnostics.push(`Line ${i + 1}: Unmatched brackets detected`);
            }
        }

        return diagnostics;
    }

    // Show compiled SQL (placeholder)
    showCompiledSql(document: TextDocument): string {
        const lqlCode = document.getText();
        // This is a placeholder - in a real implementation, this would compile LQL to SQL
        return `-- Compiled SQL for LQL code\n-- Original LQL:\n${lqlCode.split('\n').map(line => `-- ${line}`).join('\n')}\n\n-- Generated SQL (placeholder)\nSELECT * FROM table_name;`;
    }
}

// Extension activation
export function activate(context: ExtensionContext): void {
    console.log('LQL Extension is now active!');
    
    const lqlFeatures = new LqlLanguageFeatures();

    // Register commands
    const formatCommand = mockVscode.commands.registerCommand('lql.format', () => {
        mockVscode.window.showInformationMessage('LQL Format command executed');
    });

    const validateCommand = mockVscode.commands.registerCommand('lql.validate', () => {
        mockVscode.window.showInformationMessage('LQL Validate command executed');
    });

    const showSqlCommand = mockVscode.commands.registerCommand('lql.showCompiledSql', () => {
        mockVscode.window.showInformationMessage('LQL Show Compiled SQL command executed');
    });

    // Register language providers
    const formattingProvider = mockVscode.languages.registerDocumentFormattingProvider(
        { scheme: 'file', language: 'lql' },
        {
            provideDocumentFormattingEdits: (document: TextDocument) => {
                return lqlFeatures.formatDocument(document);
            }
        }
    );

    const hoverProvider = mockVscode.languages.registerHoverProvider(
        { scheme: 'file', language: 'lql' },
        {
            provideHover: (document: TextDocument, position: Position) => {
                return lqlFeatures.provideHover(document, position);
            }
        }
    );

    // Add to subscriptions
    context.subscriptions.push(
        formatCommand,
        validateCommand,
        showSqlCommand,
        formattingProvider,
        hoverProvider
    );

    // Configuration change handler
    const configChangeHandler = mockVscode.workspace.onDidChangeConfiguration(() => {
        console.log('LQL configuration changed');
    });
    
    context.subscriptions.push(configChangeHandler);

    console.log('LQL Extension activation complete');
}

// Extension deactivation
export function deactivate(): void {
    console.log('LQL Extension deactivated');
}

// Export for testing
export { LqlLanguageFeatures };