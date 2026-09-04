// A Chrome DevTools Protocol client, in plain Node and with no dependency.
//
// **Why this exists at all.** Nineteen browser harnesses in this repository already drive Chrome
// — with `--dump-dom`, one launch per page, over `file://`. That is enough to assert what a piece
// of markup renders to and nothing more: it cannot click, cannot reload, cannot keep local storage
// across a reload, and never sees the real server, the real `_headers`, or WebAssembly starting.
// Everything `PROGRESS.md` item 10 lists as unverified needs a *session* with the browser, which
// means the DevTools Protocol, which means a WebSocket.
//
// **And no `package.json`, which is the constraint that shaped this file.** This repository has
// never had one — `scripts/visual/png.mjs` is a hand-written PNG codec for exactly the same
// reason. Puppeteer would be the obvious answer and it is a large tree that also downloads its
// own Chrome; what is actually needed is a request/response pipe and a handful of domains, which
// is this file. `WebSocket` has been a Node global since v22.4, so even the socket needs nothing.
//
// **The Node floor is checked rather than assumed** — see `requireWebSocket()`. A `WebSocket is
// not defined` on an old runtime would read as a broken harness rather than as a stale Node.

import { spawn } from 'node:child_process';
import { existsSync, mkdtempSync, readFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

export const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

/**
 * The one runtime feature this file needs that an older Node does not have.
 *
 * Named here rather than left to fail at `new WebSocket(...)`, because "WebSocket is not defined"
 * thrown from the middle of a launch reads as a fault in the harness rather than as a Node that
 * is three versions behind — this repository's oldest failure shape in a new spelling.
 */
export function requireWebSocket() {
    if (typeof WebSocket !== 'function') {
        throw new Error(
            `this harness needs Node's global WebSocket (Node >= 22.4); this is ${process.version}.`);
    }
}

/**
 * Where Chrome is.
 *
 * `PP_E2E_CHROME` wins, because scripts/e2e.sh has already looked and knows the answer on this
 * host. The list below is the fallback for running this file by hand.
 */
export function findChrome() {
    if (process.env.PP_E2E_CHROME) return process.env.PP_E2E_CHROME;

    // **macOS is its own branch and was missing one.** `process.platform` is `darwin` there, so
    // it fell through to the Linux list, found none of those paths, and threw "no Chrome found"
    // on a machine with Chrome installed in the ordinary place — the script was unrunnable on a
    // Mac without setting PP_E2E_CHROME by hand, which is not something its help text says you
    // must do. CI is Linux and could never see it.
    const candidates = process.platform === 'win32'
        ? [
            'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe',
            'C:\\Program Files (x86)\\Google\\Chrome\\Application\\chrome.exe',
        ]
        : process.platform === 'darwin'
        ? [
            '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome',
            `${process.env.HOME ?? ''}/Applications/Google Chrome.app/Contents/MacOS/Google Chrome`,
            '/Applications/Chromium.app/Contents/MacOS/Chromium',
        ]
        : [
            '/usr/bin/google-chrome',
            '/usr/bin/google-chrome-stable',
            '/usr/bin/chromium-browser',
            '/usr/bin/chromium',
        ];

    for (const candidate of candidates) if (existsSync(candidate)) return candidate;

    throw new Error('no Chrome found; set PP_E2E_CHROME to its path.');
}

/**
 * One open DevTools session against one page.
 *
 * Deliberately thin: `send` is the whole protocol, and everything below it is a convenience over
 * two or three `send`s that this harness happens to need twice.
 */
class Page {
    #ws;
    #nextId = 1;
    #pending = new Map();
    #listeners = new Map();

    /** Every uncaught exception the page threw, in order. Read by the boot check. */
    exceptions = [];

    /** Every console error the page logged, in order. Read by the boot check. */
    consoleErrors = [];

    /** The status and URL of the most recent main-document response. Read by the routes check. */
    lastDocument = null;

    constructor(ws) {
        this.#ws = ws;
        ws.onmessage = (event) => this.#receive(JSON.parse(event.data));
    }

    #receive(message) {
        if (message.id !== undefined) {
            const waiting = this.#pending.get(message.id);
            if (!waiting) return;
            this.#pending.delete(message.id);
            if (message.error) waiting.reject(new Error(`${message.error.message} (${waiting.method})`));
            else waiting.resolve(message.result);
            return;
        }

        const handlers = this.#listeners.get(message.method);
        if (handlers) for (const handler of handlers.slice()) handler(message.params);
    }

    on(method, handler) {
        if (!this.#listeners.has(method)) this.#listeners.set(method, []);
        this.#listeners.get(method).push(handler);
    }

    off(method, handler) {
        const handlers = this.#listeners.get(method);
        if (!handlers) return;
        const at = handlers.indexOf(handler);
        if (at >= 0) handlers.splice(at, 1);
    }

    send(method, params = {}) {
        const id = this.#nextId++;
        return new Promise((resolve, reject) => {
            this.#pending.set(id, { resolve, reject, method });
            this.#ws.send(JSON.stringify({ id, method, params }));
        });
    }

    /**
     * Evaluate an expression in the page and return its value.
     *
     * `awaitPromise` so a check can await something in the page; `returnByValue` so what comes
     * back is data rather than a remote handle this file would then have to release.
     */
    async evaluate(expression) {
        const result = await this.send('Runtime.evaluate', {
            expression,
            awaitPromise: true,
            returnByValue: true,
        });

        if (result.exceptionDetails) {
            const thrown = result.exceptionDetails.exception?.description
                ?? result.exceptionDetails.text;
            throw new Error(`page threw: ${thrown}`);
        }

        return result.result.value;
    }

    /**
     * Poll an expression until it answers something truthy.
     *
     * **The timeout message carries the last value rather than only the expression**, because
     * "timed out waiting for X" and "X was 0 the whole time" are different bug reports and the
     * second one is the useful one.
     */
    async waitFor(expression, { timeout = 30000, every = 100, what = null } = {}) {
        const deadline = Date.now() + timeout;
        let last = '(never evaluated)';

        while (Date.now() < deadline) {
            try {
                last = await this.evaluate(expression);
                if (last) return last;
            } catch (error) {
                last = `threw: ${error.message}`;
            }
            await sleep(every);
        }

        throw new Error(
            `waited ${timeout}ms for ${what ?? expression}; last answer was ${JSON.stringify(last)}`);
    }

    /** Go to a URL and wait for the document's load event. */
    async goto(url, { timeout = 30000 } = {}) {
        const loaded = this.#once('Page.loadEventFired', timeout, `load of ${url}`);
        const result = await this.send('Page.navigate', { url });
        if (result.errorText) throw new Error(`navigation to ${url} failed: ${result.errorText}`);
        await loaded;
    }

    /** Reload the current document and wait for its load event. */
    async reload({ timeout = 30000 } = {}) {
        const loaded = this.#once('Page.loadEventFired', timeout, 'reload');
        await this.send('Page.reload', { ignoreCache: false });
        await loaded;
    }

    /**
     * Resolve on the next occurrence of one event.
     *
     * **The listener is removed on the way out.** These are registered per navigation, and a
     * harness that navigates a dozen times would otherwise finish holding a dozen dead promises'
     * worth of handlers, each firing on every subsequent load.
     */
    #once(method, timeout, what) {
        return new Promise((resolve, reject) => {
            const fire = () => {
                clearTimeout(timer);
                this.off(method, fire);
                resolve();
            };

            const timer = setTimeout(() => {
                this.off(method, fire);
                reject(new Error(`waited ${timeout}ms for ${method} (${what})`));
            }, timeout);

            this.on(method, fire);
        });
    }

    /**
     * Where an element is on screen, or null if it is not there or has no box.
     *
     * **A zero-sized box is reported rather than clicked**, because dispatching a click at (0,0)
     * lands on whatever happens to be in the corner and the check then fails somewhere else, for
     * a reason that has nothing to do with what it was measuring.
     */
    async boxOf(finder) {
        return await this.evaluate(`(() => {
            const el = (${finder});
            if (!el) return null;
            el.scrollIntoView({ block: 'center', inline: 'center', behavior: 'instant' });
            const r = el.getBoundingClientRect();
            return { x: r.x + r.width / 2, y: r.y + r.height / 2, width: r.width, height: r.height };
        })()`);
    }

    /**
     * Click an element the way a person does: a real mouse event at real coordinates, dispatched
     * by the browser rather than by `el.click()` from inside the page.
     *
     * **That difference is the whole reason this harness exists.** `PROGRESS.md` item 10 records
     * a feature that shipped while nothing in the application ever wrote to the store it read
     * from, because every test reached the store directly. A synthetic `el.click()` is the same
     * mistake one layer out: it reaches the handler without proving anything is reachable.
     */
    async click(finder, description) {
        const box = await this.boxOf(finder);

        if (!box) throw new Error(`nothing to click for ${description}`);
        if (box.width === 0 || box.height === 0) {
            throw new Error(`${description} has a zero-sized box, so it cannot be clicked`);
        }

        const at = { x: box.x, y: box.y, button: 'left', clickCount: 1 };
        await this.send('Input.dispatchMouseEvent', { ...at, type: 'mouseMoved', buttons: 0 });
        await this.send('Input.dispatchMouseEvent', { ...at, type: 'mousePressed', buttons: 1 });
        await this.send('Input.dispatchMouseEvent', { ...at, type: 'mouseReleased', buttons: 0 });
    }

    /** Type into whatever has focus, through the browser's own text-input path. */
    async type(text) {
        await this.send('Input.insertText', { text });
    }
}

/**
 * Start Chrome, attach to its first page, and return it.
 *
 * **`--remote-debugging-port=0` and the port read back out of `DevToolsActivePort`.** A fixed
 * port is a race against anything else on the machine, and against the previous run of this same
 * script — which on a failure is exactly when a stale Chrome is still holding one.
 */
export async function launch({ chrome = null, profileDir = null, extraArgs = [] } = {}) {
    requireWebSocket();

    const binary = chrome ?? findChrome();
    const userDataDir = profileDir ?? mkdtempSync(join(tmpdir(), 'pp-e2e-'));

    const child = spawn(binary, [
        '--headless=new',
        '--disable-gpu',
        '--no-sandbox',
        '--disable-dev-shm-usage',
        '--no-first-run',
        '--no-default-browser-check',
        '--disable-background-timer-throttling',
        '--disable-renderer-backgrounding',
        '--window-size=1280,900',
        '--hide-scrollbars',
        `--user-data-dir=${userDataDir}`,
        '--remote-debugging-port=0',
        ...extraArgs,
        'about:blank',
    ], { stdio: ['ignore', 'pipe', 'pipe'] });

    let stderr = '';
    child.stderr.on('data', (chunk) => { stderr += chunk; });

    const portFile = join(userDataDir, 'DevToolsActivePort');
    const deadline = Date.now() + 30000;
    let port = null;

    while (Date.now() < deadline) {
        if (child.exitCode !== null) {
            throw new Error(`Chrome exited with ${child.exitCode} before listening:\n${stderr}`);
        }
        if (existsSync(portFile)) {
            // **Read inside a try, because the file exists before it is readable.** On Windows,
            // opening it in the instant Chrome is writing it answers `EBUSY: resource busy or
            // locked` — which took a whole run down with a stack trace and no verdict. A
            // not-yet-readable port file is the ordinary case one poll early, not a failure.
            try {
                const first = readFileSync(portFile, 'utf8').split('\n')[0].trim();
                if (first) { port = Number(first); break; }
            } catch { /* being written; ask again on the next tick */ }
        }
        await sleep(50);
    }

    if (!port) throw new Error(`Chrome never wrote ${portFile}:\n${stderr}`);

    // The page target Chrome opened for `about:blank`. Polled rather than assumed present: the
    // debugging port is listening a moment before the first target is registered.
    let target = null;
    while (Date.now() < deadline && !target) {
        const response = await fetch(`http://127.0.0.1:${port}/json/list`);
        target = (await response.json()).find((t) => t.type === 'page' && t.webSocketDebuggerUrl);
        if (!target) await sleep(50);
    }

    if (!target) throw new Error('Chrome opened no page target to attach to.');

    const ws = new WebSocket(target.webSocketDebuggerUrl);
    await new Promise((resolve, reject) => {
        ws.onopen = resolve;
        ws.onerror = () => reject(new Error('could not open the DevTools WebSocket'));
    });

    const page = new Page(ws);

    page.on('Runtime.exceptionThrown', (params) => {
        page.exceptions.push(
            params.exceptionDetails.exception?.description ?? params.exceptionDetails.text);
    });

    page.on('Runtime.consoleAPICalled', (params) => {
        if (params.type !== 'error' && params.type !== 'assert') return;
        page.consoleErrors.push(params.args.map((a) => a.value ?? a.description ?? '').join(' '));
    });

    page.on('Network.responseReceived', (params) => {
        if (params.type !== 'Document') return;
        page.lastDocument = { url: params.response.url, status: params.response.status };
    });

    await page.send('Page.enable');
    await page.send('Runtime.enable');
    await page.send('Network.enable');

    return {
        page,
        async close() {
            try { ws.close(); } catch { /* already gone */ }
            child.kill();
            // Chrome holds the profile open for a moment after being killed; a failed removal is
            // not worth failing a run over, and the directory is under the OS temp root anyway.
            await sleep(200);
            try { rmSync(userDataDir, { recursive: true, force: true }); } catch { /* fine */ }
        },
    };
}
