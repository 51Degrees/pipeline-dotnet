// Runs the creator context demo page's script in Node with a small stand
// in for a browser, and prints what the page did as one line of JSON.
//
// Usage: node creator_context_page_harness.js <page.html> [51did]
//
// The optional second argument is the identifier the page is opened with,
// which is the transplant path. Without it the page creates one.
//
// The stand in gives the page a document, a window, a location and a
// fetch. Inserting a script element stands for loading the 51Degrees
// client script: the harness answers it by putting an object on the
// window whose complete function hands back a fixed cloud answer with
// the six 51Did values in it, which is what the real script does once it
// has run the page's snippets. Nothing here reaches a network.
//
// The printed object says which script the page asked for, which
// addresses it fetched, and what each row of the page ended up saying, so
// a test can check that the page creates through the client script and
// verifies the identifier the script reported.
'use strict';
const fs = require('fs');
const vm = require('vm');

const pageFile = process.argv[2];
const given = process.argv[3];

const page = fs.readFileSync(pageFile, 'utf8');
// A checkout on Windows has carriage returns in it, so the ends of
// lines are matched either way.
const match = page.match(/<script>\r?\n([\s\S]*?)\r?\n<\/script>/);
if (!match) {
    console.log(JSON.stringify({ error: 'no script block in the page' }));
    process.exit(1);
}
const source = match[1]
    .replace(/__RESOURCE__/g, 'TEST-RESOURCE-KEY')
    .replace(/__CHALLENGE__/g, 'test-challenge')
    .replace(/__API__/g, 'https://cloud.51degrees.com/api/v4/');

// The identifiers the stand in client script reports. Base64 with a plus
// and a slash in it, so a test can see the page make the value safe for a
// URL before it sends it.
const IDS = {
    idprobglobal: 'prob+global/value=',
    idproblic: 'prob+lic/value=',
    idhemglobal: 'hem+global/value=',
    idhemlic: 'hem+lic/value=',
    idrandglobal: 'rand+global/value=',
    idrandlic: 'rand+lic/value='
};

const record = { scripts: [], fetches: [], rows: {}, revealed: [], errors: [] };

function element(id) {
    return {
        id: id,
        style: {},
        children: [],
        set textContent(value) { record.rows[this.id] = String(value); },
        get textContent() { return record.rows[this.id] || ''; },
        set className(value) { record.rows[this.id + '.class'] = value; },
        get className() { return record.rows[this.id + '.class'] || ''; },
        appendChild: function (child) { this.children.push(child); },
        onclick: null
    };
}

const elements = {};
function byId(id) {
    if (!elements[id]) { elements[id] = element(id); }
    return elements[id];
}

const document = {
    // The page reads this the way a browser gives it, as one string of
    // name and value pairs. The client script stand in writes the two
    // below when it "loads", which is what the real script does.
    cookie: 'unrelated=ignored',
    getElementById: byId,
    createElement: function (tag) {
        const made = element('<' + tag + '>');
        if (tag === 'script') {
            // Loading the client script. The page sets src, then this
            // harness answers as the real script does, by leaving its
            // object on the window.
            let source = '';
            Object.defineProperty(made, 'src', {
                get: function () { return source; },
                set: function (value) {
                    source = value;
                    record.scripts.push(value);
                }
            });
        }
        return made;
    },
    head: {
        appendChild: function (node) {
            // The client script has "loaded". Give the page the object it
            // looks for, then tell it the load finished.
            document.cookie = 'unrelated=ignored; '
                + '51D_ScreenPixelsHeight=1080; 51D_ProfileIds=1-2-3';
            sandbox.window.fod = {
                complete: function (callback) {
                    callback({ fodid: Object.assign({}, IDS) });
                }
            };
            if (typeof node.onload === 'function') { node.onload(); }
        }
    },
    body: { appendChild: function () {} }
};

function response(body) {
    return Promise.resolve({
        ok: true,
        status: 200,
        url: 'stub',
        text: function () { return Promise.resolve(JSON.stringify(body)); }
    });
}

function fetchStub(url) {
    record.fetches.push(url);
    if (url.indexOf('id/verify-full') !== -1
        || url.indexOf('id/verify-context') !== -1) {
        return response({ result: 'sealed-result' });
    }
    if (url.indexOf('/redeem') !== -1) {
        return response({
            signature: 'verified',
            context: 'verified',
            serverSignature: 'verified'
        });
    }
    return response({});
}

const sandbox = {
    console: {
        log: function () {},
        warn: function () {},
        error: function (...a) { record.errors.push(a.join(' ')); }
    },
    document: document,
    fetch: fetchStub,
    URLSearchParams: URLSearchParams,
    Promise: Promise,
    setTimeout: setTimeout,
    clearTimeout: clearTimeout,
    navigator: { clipboard: { writeText: function () {} } },
    location: {
        search: given ? '?51did=' + encodeURIComponent(given) : '',
        origin: 'http://localhost:5100',
        pathname: '/'
    }
};
sandbox.window = sandbox;
sandbox.globalThis = sandbox;

vm.createContext(sandbox);
try {
    vm.runInContext(source, sandbox, { filename: 'page.html' });
} catch (error) {
    record.errors.push(String(error));
}

// The page's work is a chain of promises, so let them settle before
// reporting. Several turns, because each step waits for the one before.
let turns = 0;
function settle() {
    turns += 1;
    if (turns < 20) {
        setTimeout(settle, 0);
        return;
    }
    console.log(JSON.stringify(record));
}
settle();
