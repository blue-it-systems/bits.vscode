import * as assert from 'assert';
import * as path from 'path';
import * as fs from 'fs';
import { spawnSync } from 'child_process';

// Helper to run dotnet run for a project and capture stdout/stderr.
function runDotnetRun(projectPath: string, input?: string, timeout = 20000) {
    // Use dotnet run to avoid needing to compute the build output path cross-platform.
    const args = ['run', '-c', 'Release', '--project', projectPath];

    const proc = spawnSync('dotnet', args, {
        input: input ?? undefined,
        encoding: 'utf8',
        maxBuffer: 10 * 1024 * 1024,
        timeout
    });

    return { stdout: proc.stdout ?? '', stderr: proc.stderr ?? '', status: proc.status, error: proc.error };
}

suite('Sample Generators', () => {
    test('BasicGenerator should create a timestamped file when run without input', function() {
        this.timeout(30_000);

        const projectPath = path.join(__dirname, '..', '..', 'samples', 'BasicGenerator', 'BasicGenerator.csproj');

        // Clean generated dir
        const genDir = path.join(path.dirname(projectPath), 'Generated');
        try { fs.rmSync(genDir, { recursive: true, force: true }); } catch {}

        const result = runDotnetRun(projectPath);
        assert.strictEqual(result.status, 0, `dotnet run failed: ${result.stderr}`);

        // Look for generator/file JSON lines in stdout
        const emittedPaths: string[] = [];
        for (const line of (result.stdout || '').split(/\r?\n/)) {
            try {
                const j = JSON.parse(line);
                if (j && j.type === 'generator/file' && j.path) emittedPaths.push(j.path as string);
            } catch {
                // ignore
            }
        }

        assert.ok(emittedPaths.length > 0, 'No generator/file message printed');

        // Check file existence
        for (const p of emittedPaths) {
            assert.ok(fs.existsSync(p), `Generated path does not exist: ${p}`);
        }
    });

    test('BasicGenerator should create an `added-*.txt` file when given add command input', function() {
        this.timeout(30_000);
        const projectPath = path.join(__dirname, '..', '..', 'samples', 'BasicGenerator', 'BasicGenerator.csproj');
        const input = JSON.stringify({ command: 'add', message: 'hello world', outputDirectory: 'Generated' });

        const genDir = path.join(path.dirname(projectPath), 'Generated');
        try { fs.rmSync(genDir, { recursive: true, force: true }); } catch {}

        const result = runDotnetRun(projectPath, input);
        assert.strictEqual(result.status, 0, `dotnet run failed: ${result.stderr}`);

        const emitted = (result.stdout || '').split(/\r?\n/).map(l => { try { return JSON.parse(l); } catch { return null; } }).filter(Boolean);
        const files = emitted.filter((m: any) => m.type === 'generator/file').map((m: any) => m.path);

        assert.ok(files.length > 0, 'No files emitted');
        const f = files[0];
        assert.ok(f.includes('added-'), `Expected added- file, got ${f}`);
        assert.ok(fs.existsSync(f), `Generated file not found: ${f}`);
    });

    test('AdvancedGenerator should accept files input and emit generated companion files', function() {
        this.timeout(30_000);
        const projectPath = path.join(__dirname, '..', '..', 'samples', 'AdvancedGenerator', 'AdvancedGenerator.csproj');

        const input = JSON.stringify({ files: [{ path: 'src/Foo.cs', content: 'public class Foo { }' }], outputDirectory: 'Generated' });

        const genDir = path.join(path.dirname(projectPath), 'Generated');
        try { fs.rmSync(genDir, { recursive: true, force: true }); } catch {}

        const result = runDotnetRun(projectPath, input);
        assert.strictEqual(result.status, 0, `dotnet run failed: ${result.stderr}`);

        const emittedPaths: string[] = [];
        for (const line of (result.stdout || '').split(/\r?\n/)) {
            try {
                const j = JSON.parse(line);
                if (j && j.type === 'generator/file' && j.path) emittedPaths.push(j.path as string);
            } catch {
                // ignore
            }
        }

        assert.ok(emittedPaths.length > 0, 'Advanced generator did not emit files');
        for (const p of emittedPaths) {
            assert.ok(fs.existsSync(p), `Advanced generated file not found: ${p}`);
        }
    });
});
