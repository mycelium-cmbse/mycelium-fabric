// The talk of a view with its host, shared by the views of the trade-off and of the health of the model: JSON-RPC messages
// through postMessage, as MCP Apps define them. The page loads this script before its own.

// The origin of the host, known from its first message: until then, the messages go to the parent frame whatever its origin.
// A host that shows the view in a frame without an origin of its own ("null") keeps "*".
let hostOrigin = "*";

// The id of the next request of the view, and the requests that wait for the response of the host.
let nextId = 1;
const pending = new Map();

/**
 * Sends a JSON-RPC message to the host, in the parent frame, at its origin once known.
 * @param {object} message The JSON-RPC request, response or notification.
 */
function send(message) {
    window.parent.postMessage(message, hostOrigin);
}

/**
 * Sends a JSON-RPC notification to the host.
 * @param {string} method The method of the notification, for example ui/notifications/initialized.
 * @param {object} params The parameters of the notification.
 */
function notify(method, params) {
    send({ jsonrpc: "2.0", method, params });
}

/**
 * Sends a JSON-RPC request to the host.
 * @param {string} method The method of the request, for example tools/call.
 * @param {object} params The parameters of the request.
 * @returns {Promise<object>} The result of the response, or a rejection with its error.
 */
function request(method, params) {
    const id = nextId++;
    send({ jsonrpc: "2.0", id, method, params });
    return new Promise((resolve, reject) => pending.set(id, { resolve, reject }));
}

/**
 * Listens to the messages of the host: the responses to the requests of the view, the result of the tool that the view
 * shows, the changes of theme, and its own requests, answered with an empty result.
 * @param {function(object): void} onToolResult Called with the result of the tool that the view shows.
 */
function listenToHost(onToolResult) {
    window.addEventListener("message", event => {
        if (event.source !== window.parent || !isHostOrigin(event.origin)) return;
        const message = event.data;
        if (message?.jsonrpc !== "2.0") return;

        if (message.id !== undefined && !message.method) {
            const call = pending.get(message.id);
            pending.delete(message.id);
            if (call) message.error ? call.reject(message.error) : call.resolve(message.result);
            return;
        }

        if (message.id !== undefined) send({ jsonrpc: "2.0", id: message.id, result: {} });

        if (message.method === "ui/notifications/tool-result") onToolResult(message.params);
        if (message.method === "ui/notifications/host-context-changed") applyTheme(message.params);
    });
}

/**
 * Tells whether a message comes from the host: the first message gives its origin, and later messages must come from it.
 * @param {string} origin The origin of the message.
 * @returns {boolean} true when the message comes from the host.
 */
function isHostOrigin(origin) {
    if (hostOrigin === "*" && origin !== "null") hostOrigin = origin;
    return hostOrigin === "*" || origin === hostOrigin;
}

/**
 * Applies the theme of the host: its CSS variables, and light or dark.
 * @param {object} context The context of the host, or nothing.
 */
function applyTheme(context) {
    if (!context) return;
    for (const [name, value] of Object.entries(context.styles?.variables ?? {})) {
        if (value) document.documentElement.style.setProperty(name, value);
    }
    if (context.theme) document.documentElement.style.colorScheme = context.theme;
}

/**
 * Tells the host the size of the view, so that the frame shows it whole.
 */
function sendSize() {
    const html = document.documentElement;
    const previous = html.style.height;
    html.style.height = "max-content";
    const height = Math.ceil(html.getBoundingClientRect().height);
    html.style.height = previous;
    notify("ui/notifications/size-changed", { width: Math.ceil(window.innerWidth), height });
}

/**
 * Starts the talk with the host: initializes, applies its theme, then tells it that the view is ready.
 * @param {string} name The name of the view, for example model-health.
 * @returns {Promise<void>} Resolved once the host knows that the view is ready, or rejected with the error of the host.
 */
function initializeView(name) {
    const params = { protocolVersion: "2026-01-26", appInfo: { name, version: "0.1.0" }, appCapabilities: {} };
    return request("ui/initialize", params).then(result => {
        applyTheme(result?.hostContext);
        notify("ui/notifications/initialized", {});
    });
}

// In the tests, Node loads the scripts as modules: the functions become global, as a classic script makes them in the page.
if (typeof module !== "undefined") Object.assign(globalThis, { send, notify, request, listenToHost, applyTheme, sendSize, initializeView });
