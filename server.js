const http = require('http');
const fs = require('fs');
const path = require('path');
const { execFile } = require('child_process');

const PORT = Number(process.env.PORT) || 4982;
const PUBLIC_DIR = path.join(__dirname, 'public');
const SCAN_SCRIPT = path.join(__dirname, 'scan.ps1');
const CLEANER_SCRIPT = path.join(__dirname, 'cleaner.ps1');
const TASK_NAME = 'StorageOptimizerAutoClean';

// Known cleanup categories. MUST stay in sync with the ids in targets.ps1
// (test/smoke.test.js fails if the two lists drift apart).
const CATEGORIES = Object.freeze([
    'recycle_bin',
    'temp_files',
    'nvidia_artifacts',
    'capcut_old',
    'vscode_cache',
    'dev_cache',
    'discord_cache'
]);

const CATEGORY_ID_RE = /^[a-z][a-z0-9_]*$/;

// This server binds to loopback only and is reachable from the user's own
// browser. Requests carrying a foreign Host (DNS-rebinding) or a foreign
// Origin (cross-site fetch) are rejected, and no CORS headers are emitted,
// so web pages other than this app cannot read or trigger anything here.
const ALLOWED_HOSTS = new Set([`127.0.0.1:${PORT}`, `localhost:${PORT}`, `[::1]:${PORT}`]);
const ALLOWED_ORIGINS = new Set([`http://127.0.0.1:${PORT}`, `http://localhost:${PORT}`]);

const STATUS_TTL_MS = 20_000;
const SCAN_TIMEOUT_MS = 120_000;
const CLEAN_TIMEOUT_MS = 600_000;
const SCHEDULE_TIMEOUT_MS = 30_000;
const MAX_BODY_BYTES = 8 * 1024;

// ---- helpers -------------------------------------------------------------

const MIME_TYPES = {
    '.html': 'text/html; charset=utf-8',
    '.css': 'text/css; charset=utf-8',
    '.js': 'application/javascript; charset=utf-8',
    '.json': 'application/json',
    '.png': 'image/png',
    '.svg': 'image/svg+xml',
    '.ico': 'image/x-icon'
};

// Run a PowerShell script with an args ARRAY (execFile never invokes a shell,
// so no shell metacharacter in an argument can ever become a command).
function runPowerShell(scriptPath, args, timeoutMs) {
    return new Promise((resolve) => {
        const cliArgs = ['-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', scriptPath, ...args];
        execFile('powershell.exe', cliArgs, {
            timeout: timeoutMs,
            maxBuffer: 10 * 1024 * 1024,
            windowsHide: true
        }, (err, stdout, stderr) => {
            resolve({
                success: !err,
                code: err ? err.code : 0,
                error: err ? (stderr || err.message) : '',
                output: (stdout || '').trim()
            });
        });
    });
}

function runSchtasks(args, timeoutMs) {
    return new Promise((resolve) => {
        execFile('schtasks.exe', args, {
            timeout: timeoutMs,
            maxBuffer: 1024 * 1024,
            windowsHide: true
        }, (err, stdout, stderr) => {
            resolve({
                success: !err,
                error: err ? (stderr || err.message) : '',
                output: (stdout || '').trim()
            });
        });
    });
}

function sendJson(res, status, obj) {
    const body = JSON.stringify(obj);
    res.writeHead(status, {
        'Content-Type': 'application/json; charset=utf-8',
        'Cache-Control': 'no-store',
        'X-Content-Type-Options': 'nosniff'
    });
    res.end(body);
}

function readBody(req) {
    return new Promise((resolve, reject) => {
        let size = 0;
        const chunks = [];
        req.on('data', (chunk) => {
            size += chunk.length;
            if (size > MAX_BODY_BYTES) {
                const err = new Error('Request body too large');
                err.statusCode = 413;
                reject(err);
                req.destroy();
                return;
            }
            chunks.push(chunk);
        });
        req.on('end', () => resolve(Buffer.concat(chunks).toString('utf8')));
        req.on('error', reject);
    });
}

// Run scan.ps1 and cache the parsed result briefly so the UI's repeated
// status calls do not trigger a full recursive disk scan every time.
let statusCache = { ts: 0, data: null };
let statusInflight = null;

async function scanStatus(forceFresh = false) {
    const now = Date.now();
    if (!forceFresh && statusCache.data && now - statusCache.ts < STATUS_TTL_MS) {
        return statusCache.data;
    }
    if (statusInflight) return statusInflight;

    statusInflight = (async () => {
        const result = await runPowerShell(SCAN_SCRIPT, [], SCAN_TIMEOUT_MS);
        if (!result.success || !result.output) {
            throw new Error(result.error || 'Scan produced no output');
        }
        let data;
        try {
            data = JSON.parse(result.output);
        } catch {
            throw new Error('Scan produced invalid JSON');
        }
        statusCache = { ts: Date.now(), data };
        return data;
    })();

    try {
        return await statusInflight;
    } finally {
        statusInflight = null;
    }
}

function invalidateStatusCache() {
    statusCache = { ts: 0, data: null };
}

// ---- request handling ----------------------------------------------------

let cleanInProgress = false;

async function handleRequest(req, res) {
    // 1. Only talk to ourselves (blocks DNS-rebinding: a page served from
    //    evil.com resolving to 127.0.0.1 would send Host: evil.com:4982).
    const host = req.headers.host || '';
    if (!ALLOWED_HOSTS.has(host)) {
        sendJson(res, 403, { error: 'Forbidden host' });
        return;
    }

    // 2. If an Origin is present it must be this app (blocks cross-site
    //    form/fetch POSTs; non-browser clients can spoof this, which is why
    //    the allowlist + execFile hardening below matters too).
    const origin = req.headers.origin;
    if (origin && !ALLOWED_ORIGINS.has(origin)) {
        sendJson(res, 403, { error: 'Forbidden origin' });
        return;
    }

    if (req.method === 'OPTIONS') {
        // No CORS headers on purpose: cross-origin callers must fail.
        res.writeHead(204);
        res.end();
        return;
    }

    let url;
    try {
        url = new URL(req.url, `http://127.0.0.1:${PORT}`);
    } catch {
        sendJson(res, 400, { error: 'Malformed request target' });
        return;
    }

    // 3. API: Status
    if (url.pathname === '/api/status' && req.method === 'GET') {
        try {
            const data = await scanStatus();
            sendJson(res, 200, data);
        } catch (err) {
            sendJson(res, 500, { error: err.message });
        }
        return;
    }

    // 4. API: Clean
    if (url.pathname === '/api/clean' && req.method === 'POST') {
        let data;
        try {
            const body = await readBody(req);
            data = JSON.parse(body || '{}');
        } catch (err) {
            sendJson(res, err.statusCode || 400, { error: err.message });
            return;
        }

        const categories = data.categories;
        const valid = Array.isArray(categories)
            && categories.length > 0
            && categories.every((c) => typeof c === 'string' && CATEGORY_ID_RE.test(c) && CATEGORIES.includes(c));
        if (!valid) {
            sendJson(res, 400, {
                error: 'Invalid categories: must be a non-empty array of known category ids',
                allowed: CATEGORIES
            });
            return;
        }

        if (cleanInProgress) {
            sendJson(res, 409, { error: 'A cleanup is already running' });
            return;
        }

        cleanInProgress = true;
        try {
            const cleanResult = await runPowerShell(CLEANER_SCRIPT, ['-Categories', categories.join(',')], CLEAN_TIMEOUT_MS);
            invalidateStatusCache();
            let disk = {};
            try {
                const scanData = await scanStatus(true);
                disk = scanData.disk || {};
            } catch { /* keep previous disk values */ }

            sendJson(res, 200, {
                success: cleanResult.success,
                logs: cleanResult.output.split(/\r?\n/).filter((l) => l.trim() !== ''),
                error: cleanResult.success ? undefined : (cleanResult.error || 'Cleanup script reported an error'),
                disk
            });
        } catch (err) {
            sendJson(res, 500, { error: err.message });
        } finally {
            cleanInProgress = false;
        }
        return;
    }

    // 5. API: Schedule
    if (url.pathname === '/api/schedule' && req.method === 'POST') {
        let data;
        try {
            const body = await readBody(req);
            data = JSON.parse(body || '{}');
        } catch (err) {
            sendJson(res, err.statusCode || 400, { error: err.message });
            return;
        }

        if (typeof data.enable !== 'boolean') {
            sendJson(res, 400, { error: '"enable" must be a boolean' });
            return;
        }

        const enable = data.enable;
        let result;
        if (enable) {
            // schtasks receives this as a single argv element (execFile), so
            // no cmd.exe quoting layers are involved.
            const taskCmd = `powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "${CLEANER_SCRIPT}"`;
            result = await runSchtasks(
                ['/create', '/tn', TASK_NAME, '/tr', taskCmd, '/sc', 'weekly', '/d', 'SUN', '/st', '12:00', '/f'],
                SCHEDULE_TIMEOUT_MS
            );
        } else {
            result = await runSchtasks(['/delete', '/tn', TASK_NAME, '/f'], SCHEDULE_TIMEOUT_MS);
        }

        invalidateStatusCache();
        if (result.success) {
            sendJson(res, 200, { success: true, enabled: enable });
        } else {
            sendJson(res, 500, { success: false, enabled: enable, error: result.error || 'schtasks failed' });
        }
        return;
    }

    // 6. Static files (never leave public/)
    if (req.method !== 'GET' && req.method !== 'HEAD') {
        sendJson(res, 405, { error: 'Method not allowed' });
        return;
    }

    // NOTE: url.pathname is intentionally NOT percent-decoded. Decoding would
    // turn "%2e%2e%5c" into "..\" which path.resolve would then follow out of
    // public/ on Windows. Our files are plain ASCII names.
    const relative = url.pathname.replace(/^\/+/, '');
    const filePath = path.resolve(PUBLIC_DIR, relative === '' ? 'index.html' : relative);
    if (filePath !== PUBLIC_DIR && !filePath.startsWith(PUBLIC_DIR + path.sep)) {
        sendJson(res, 403, { error: 'Forbidden path' });
        return;
    }

    const ext = path.extname(filePath).toLowerCase();
    const contentType = MIME_TYPES[ext] || 'application/octet-stream';

    fs.readFile(filePath, (err, content) => {
        if (err) {
            if (err.code === 'ENOENT') {
                res.writeHead(404, { 'Content-Type': 'text/plain; charset=utf-8' });
                res.end('Not Found');
            } else {
                res.writeHead(500, { 'Content-Type': 'text/plain; charset=utf-8' });
                res.end('Server Error: ' + err.code);
            }
            return;
        }
        res.writeHead(200, {
            'Content-Type': contentType,
            'X-Content-Type-Options': 'nosniff',
            'Referrer-Policy': 'no-referrer',
            'Content-Security-Policy': "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; font-src https://fonts.gstatic.com; connect-src 'self'; img-src 'self' data:; object-src 'none'; base-uri 'none'",
            'Cache-Control': 'no-store'
        });
        res.end(req.method === 'HEAD' ? undefined : content);
    });
}

const server = http.createServer((req, res) => {
    // A throw inside the handler must never take the whole server down.
    handleRequest(req, res).catch((err) => {
        console.error('[server] unhandled request error:', err);
        if (!res.headersSent) {
            sendJson(res, 500, { error: 'Internal server error' });
        } else {
            res.end();
        }
    });
});

process.on('unhandledRejection', (reason) => {
    console.error('[server] unhandled rejection:', reason);
});
process.on('uncaughtException', (err) => {
    console.error('[server] uncaught exception:', err);
});

server.listen(PORT, '127.0.0.1', () => {
    console.log(`Storage Optimizer backend running at http://127.0.0.1:${PORT}`);
});
