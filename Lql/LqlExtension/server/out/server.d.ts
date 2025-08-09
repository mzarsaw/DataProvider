interface Position {
    line: number;
    character: number;
}
interface Range {
    start: Position;
    end: Position;
}
interface Diagnostic {
    range: Range;
    message: string;
    severity: number;
}
interface TextDocument {
    uri: string;
    getText(): string;
    lineCount: number;
    lineAt(line: number): {
        text: string;
    };
}
interface CompletionItem {
    label: string;
    kind: number;
    detail?: string;
    documentation?: string;
}
declare class LqlLanguageServer {
    private documents;
    private settings;
    validateDocument(document: TextDocument): Diagnostic[];
    getCompletionItems(): CompletionItem[];
    getHoverInfo(word: string): string | undefined;
    formatDocument(document: TextDocument): {
        range: Range;
        newText: string;
    }[];
}
export declare const lqlLanguageServer: LqlLanguageServer;
export default lqlLanguageServer;
