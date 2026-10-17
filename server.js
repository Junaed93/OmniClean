const http = require('http');
const fs = require('fs');
const path = require('path');
const { exec } = require('child_process');

const PORT = 4982;
const PUBLIC_DIR = path.join(__dirname, 'public');
const SCAN_SCRIPT = path.join(__dirname, 'scan.ps1');
const CLEANER_SCRIPT = path.join(__dirname, 'cleaner.ps1');
const TASK_NAME = 'StorageOptimizerAutoClean';

// Helper to run a PowerShell script by file path
function runPowershellFile(scriptPath, args = []) {
    return new Promise((resolve) => {
        const cmd = `powershell.exe -NoProfile -ExecutionPolicy Bypass -File "${scriptPath}" ${args.join(' ')}`;
        exec(cmd, { maxBuffer: 10 * 1024 * 1024 }, (err, stdout, stderr) => {
            if (err) {
                resolve({ success: false, error: stderr || err.message, output: stdout });
            } else {
                resolve({ success: true, output: stdout.trim() });
            }
        });
    });
}

// Helper to run raw command (e.g. for schtasks)
function runCommand(cmd) {
    return new Promise((resolve) => {
        exec(cmd, { maxBuffer: 10 * 1024 * 1024 }, (err, stdout, stderr) => {
            if (err) {
                resolve({ success: false, error: stderr || err.message, output: stdout });
            } else {
                resolve({ success: true, output: stdout.trim() });
            }
        });
    });
}

// MIME types
const MIME_TYPES = {
    '.html': 'text/html; charset=utf-8',
    '.css': 'text/css; charset=utf-8',
    '.js': 'application/javascript; charset=utf-8',
    '.json': 'application/json',
    '.png': 'image/png',
    '.svg': 'image/svg+xml',
    '.ico': 'image/x-icon'
};

const server = http.createServer(async (req, res) => {
    // CORS headers
    res.setHeader('Access-Control-Allow-Origin', '*');
    res.setHeader('Access-Control-Allow-Methods', 'GET, POST, OPTIONS');
    res.setHeader('Access-Control-Allow-Headers', 'Content-Type');

    if (req.method === 'OPTIONS') {
        res.writeHead(204);
        res.end();
        return;
    }

    const url = new URL(req.url, `http://localhost:${PORT}`);

    // API: Status
    if (url.pathname === '/api/status' && req.method === 'GET') {
        try {
            const result = await runPowershellFile(SCAN_SCRIPT);
            if (result.success && result.output) {
                res.writeHead(200, { 'Content-Type': 'application/json' });
                res.end(result.output);
            } else {
                res.writeHead(500, { 'Content-Type': 'application/json' });
                res.end(JSON.stringify({ error: result.error || "Failed to scan" }));
            }
        } catch (err) {
            res.writeHead(500, { 'Content-Type': 'application/json' });
            res.end(JSON.stringify({ error: err.message }));
        }
        return;
    }

    // API: Clean
    if (url.pathname === '/api/clean' && req.method === 'POST') {
        let body = '';
        req.on('data', chunk => body += chunk);
        req.on('end', async () => {
            try {
                const data = JSON.parse(body || '{}');
                const categories = data.categories || ['recycle_bin', 'temp_files', 'nvidia_artifacts', 'capcut_old', 'vscode_cache', 'dev_cache', 'discord_cache'];
                const catArgs = categories.join(',');

                const cleanResult = await runPowershellFile(CLEANER_SCRIPT, ['-Categories', catArgs]);
                const scanResult = await runPowershellFile(SCAN_SCRIPT);
                const scanData = scanResult.success ? JSON.parse(scanResult.output) : {};

                res.writeHead(200, { 'Content-Type': 'application/json' });
                res.end(JSON.stringify({
                    success: cleanResult.success,
                    logs: cleanResult.output.split('\r\n'),
                    disk: scanData.disk || {}
                }));
            } catch (err) {
                res.writeHead(500, { 'Content-Type': 'application/json' });
                res.end(JSON.stringify({ error: err.message }));
            }
        });
        return;
    }

    // API: Schedule
    if (url.pathname === '/api/schedule' && req.method === 'POST') {
        let body = '';
        req.on('data', chunk => body += chunk);
        req.on('end', async () => {
            try {
                const data = JSON.parse(body || '{}');
                const enable = data.enable === true;

                if (enable) {
                    const taskCmd = `powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \\"${CLEANER_SCRIPT}\\"`;
                    const schCmd = `schtasks /create /tn "${TASK_NAME}" /tr "${taskCmd}" /sc weekly /d SUN /st 12:00 /f`;
                    await runCommand(schCmd);
                } else {
                    await runCommand(`schtasks /delete /tn "${TASK_NAME}" /f`);
                }

                res.writeHead(200, { 'Content-Type': 'application/json' });
                res.end(JSON.stringify({ success: true, enabled: enable }));
            } catch (err) {
                res.writeHead(500, { 'Content-Type': 'application/json' });
                res.end(JSON.stringify({ error: err.message }));
            }
        });
        return;
    }

    // Static Files
    let filePath = path.join(PUBLIC_DIR, url.pathname === '/' ? 'index.html' : url.pathname);
    const ext = path.extname(filePath).toLowerCase();
    const contentType = MIME_TYPES[ext] || 'application/octet-stream';

    fs.readFile(filePath, (err, content) => {
        if (err) {
            if (err.code === 'ENOENT') {
                res.writeHead(404, { 'Content-Type': 'text/plain' });
                res.end('Not Found');
            } else {
                res.writeHead(500, { 'Content-Type': 'text/plain' });
                res.end('Server Error: ' + err.code);
            }
        } else {
            res.writeHead(200, { 'Content-Type': contentType });
            res.end(content);
        }
    });
});

server.listen(PORT, '127.0.0.1', () => {
    console.log(`Storage Optimizer backend running at http://127.0.0.1:${PORT}`);
});
