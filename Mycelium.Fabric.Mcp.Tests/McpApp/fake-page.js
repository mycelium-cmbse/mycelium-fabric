// ------------------------------------------------------------------------------------------------
//  <copyright file="fake-page.js" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

// Runs the page of an MCP App in Node.js, without a browser: a minimal DOM built from the HTML file of the page, and a host,
// in the parent frame, that records the messages of the view and sends its own.

const { readFileSync } = require("node:fs");

/**
 * The elements that have no closing tag.
 */
const VOID_ELEMENTS = new Set(["meta", "link", "br", "hr", "img", "input"]);

/**
 * The style of an element: its custom properties and the properties set directly.
 */
class FakeStyle {
    /**
     * Sets a custom property.
     * @param {string} name The name of the property, for example --oc.
     * @param {string} value The value of the property.
     */
    setProperty(name, value) {
        this[name] = value;
    }
}

/**
 * A text node.
 */
class FakeText {
    /**
     * Creates a text node.
     * @param {*} text The text, turned into a string.
     */
    constructor(text) {
        this.textContent = String(text);
    }
}

/**
 * An element, with what the scripts of the MCP Apps use of the DOM.
 */
class FakeElement {
    /**
     * Creates an element.
     * @param {string} tagName The name of the tag.
     */
    constructor(tagName) {
        this.tagName = tagName.toUpperCase();
        this.childNodes = [];
        this.parentNode = null;
        this.attributes = new Map();
        this.listeners = new Map();
        this.dataset = {};
        this.style = new FakeStyle();
        this.className = "";
        this.hidden = false;
        this.disabled = false;
        this.innerHTML = "";
        this.title = "";
        this.value = "";
    }

    /**
     * The id of the element.
     * @returns {string} The id, or an empty string.
     */
    get id() {
        return this.attributes.get("id") ?? "";
    }

    /**
     * The child elements, without the text nodes.
     * @returns {FakeElement[]} The child elements.
     */
    get children() {
        return this.childNodes.filter(node => node instanceof FakeElement);
    }

    /**
     * The parent element.
     * @returns {FakeElement} The parent, or null.
     */
    get parentElement() {
        return this.parentNode instanceof FakeElement ? this.parentNode : null;
    }

    /**
     * The number of child elements.
     * @returns {number} The number of child elements.
     */
    get childElementCount() {
        return this.children.length;
    }

    /**
     * The text of the element and of its descendants.
     * @returns {string} The text.
     */
    get textContent() {
        return this.childNodes.map(node => node.textContent).join("");
    }

    /**
     * Replaces the children of the element by a text.
     * @param {*} text The text, turned into a string.
     */
    set textContent(text) {
        this.childNodes = [new FakeText(text)];
    }

    /**
     * The CSS classes of the element.
     * @returns {{contains: function(string): boolean, add: function(string): void, remove: function(string): void,
     * toggle: function(string, boolean): boolean}} The classes.
     */
    get classList() {
        const names = () => this.className.split(/\s+/).filter(Boolean);

        return {
            contains: name => names().includes(name),
            add: name => {
                this.className = [...names().filter(other => other !== name), name].join(" ");
            },
            remove: name => {
                this.className = names().filter(other => other !== name).join(" ");
            },
            toggle: (name, force) => {
                const on = force ?? !names().includes(name);
                this.className = [...names().filter(other => other !== name), ...(on ? [name] : [])].join(" ");
                return on;
            }
        };
    }

    /**
     * Sets an attribute; the class, the hidden attribute and the data attributes go to their properties too.
     * @param {string} name The name of the attribute.
     * @param {*} value The value of the attribute.
     */
    setAttribute(name, value) {
        this.attributes.set(name, String(value));

        if (name === "class") {
            this.className = String(value);
        } else if (name === "hidden") {
            this.hidden = true;
        } else if (name.startsWith("data-")) {
            this.dataset[name.slice("data-".length)] = String(value);
        }
    }

    /**
     * Gets an attribute; a data attribute comes from the dataset.
     * @param {string} name The name of the attribute.
     * @returns {string} The value, or null when the element has no such attribute.
     */
    getAttribute(name) {
        const value = name.startsWith("data-") ? this.dataset[name.slice("data-".length)] : this.attributes.get(name);
        return value === undefined ? null : String(value);
    }

    /**
     * Adds a child at the end.
     * @param {FakeElement|FakeText} child The child.
     * @returns {FakeElement|FakeText} The child.
     */
    appendChild(child) {
        child.parentNode = this;
        this.childNodes.push(child);
        return child;
    }

    /**
     * Adds children at the end; a string becomes a text node.
     * @param {...(FakeElement|string)} nodes The children.
     */
    append(...nodes) {
        for (const node of nodes) {
            this.appendChild(typeof node === "string" ? new FakeText(node) : node);
        }
    }

    /**
     * Replaces the children.
     * @param {...(FakeElement|string)} nodes The new children.
     */
    replaceChildren(...nodes) {
        this.childNodes = [];
        this.append(...nodes);
    }

    /**
     * Moves elements just after this one, among its siblings.
     * @param {...FakeElement} nodes The elements.
     */
    after(...nodes) {
        this.insertSiblings(nodes, 1);
    }

    /**
     * Moves elements just before this one, among its siblings.
     * @param {...FakeElement} nodes The elements.
     */
    before(...nodes) {
        this.insertSiblings(nodes, 0);
    }

    /**
     * Moves elements next to this one, in its parent.
     * @param {FakeElement[]} nodes The elements.
     * @param {number} shift 0 to put them before this element, 1 after.
     */
    insertSiblings(nodes, shift) {
        const parent = this.parentNode;
        for (const node of nodes) {
            node.parentNode.childNodes = node.parentNode.childNodes.filter(child => child !== node);
            node.parentNode = parent;
        }
        parent.childNodes.splice(parent.childNodes.indexOf(this) + shift, 0, ...nodes);
    }

    /**
     * Adds a listener of an event.
     * @param {string} type The type of the event, for example click.
     * @param {function(object): void} listener The listener.
     */
    addEventListener(type, listener) {
        this.listeners.set(type, [...(this.listeners.get(type) ?? []), listener]);
    }

    /**
     * Clicks the element: its click listeners run.
     */
    click() {
        for (const listener of this.listeners.get("click") ?? []) {
            listener({});
        }
    }

    /**
     * Fires an event on the element: the listeners of the element, then of its ancestors, run, as the event bubbles up.
     * @param {string} type The type of the event, for example change.
     * @param {object} [properties] The other properties of the event, for example key or clientY.
     * @returns {object} The event, with defaultPrevented.
     */
    dispatch(type, properties = {}) {
        const event = { type, target: this, defaultPrevented: false, ...properties };
        event.preventDefault = () => {
            event.defaultPrevented = true;
        };

        for (let node = this; node instanceof FakeElement; node = node.parentNode) {
            for (const listener of node.listeners.get(type) ?? []) {
                listener(event);
            }
        }

        return event;
    }

    /**
     * Gives the focus to the element.
     */
    focus() {
        globalThis.document.activeElement = this;
    }

    /**
     * Scrolls the element into view: the element remembers it.
     */
    scrollIntoView() {
        this.scrolledIntoView = true;
    }

    /**
     * Captures the pointer: the element keeps the pointer id.
     * @param {number} pointerId The id of the pointer.
     */
    setPointerCapture(pointerId) {
        this.pointerCapture = pointerId;
    }

    /**
     * Gets the descendants of the element, in the order of the page.
     * @returns {FakeElement[]} The descendants.
     */
    descendants() {
        return this.children.flatMap(child => [child, ...child.descendants()]);
    }

    /**
     * Finds the descendants that match a list of selectors made of tag names and classes, as in ".seg button, .segp".
     * @param {string} selectors The selectors.
     * @returns {FakeElement[]} The matching descendants.
     */
    querySelectorAll(selectors) {
        const list = parseSelectors(selectors);
        return this.descendants().filter(element => list.some(compounds => matches(element, compounds)));
    }

    /**
     * Finds the first descendant that matches a list of selectors.
     * @param {string} selectors The selectors.
     * @returns {FakeElement} The descendant, or null.
     */
    querySelector(selectors) {
        return this.querySelectorAll(selectors)[0] ?? null;
    }

    /**
     * Finds the element itself or its closest ancestor that matches a list of selectors.
     * @param {string} selectors The selectors.
     * @returns {FakeElement} The element, or null.
     */
    closest(selectors) {
        const list = parseSelectors(selectors);
        for (let node = this; node instanceof FakeElement; node = node.parentNode) {
            if (list.some(compounds => matches(node, compounds))) {
                return node;
            }
        }

        return null;
    }

    /**
     * Gets the size of the element, fixed for the tests.
     * @returns {{width: number, height: number}} The size.
     */
    getBoundingClientRect() {
        return { width: 640, height: 412.3 };
    }
}

/**
 * Reads a list of selectors, each made of selectors without combinator joined by the descendant combinator.
 * @param {string} selectors The selectors, as ".seg button, .segp".
 * @returns {{tag: string, classes: string[], attributes: object[]}[][]} The selectors.
 */
function parseSelectors(selectors) {
    return selectors.split(",").map(selector => selector.trim().split(/\s+/).map(parseCompound));
}

/**
 * Reads a selector without combinator, as "button", ".chk.bad" or ".line[data-index]".
 * @param {string} text The selector.
 * @returns {{tag: string, classes: string[], attributes: {name: string, value: string}[]}} The tag name, empty for any tag,
 * the classes, and the attributes with their value, undefined when only the attribute is required.
 */
function parseCompound(text) {
    const attributes = [...text.matchAll(/\[([\w-]+)(?:="([^"]*)")?\]/g)].map(([, name, value]) => ({ name, value }));
    const [tag, ...classes] = text.replaceAll(/\[[^\]]*\]/g, "").split(".");
    return { tag: tag.toUpperCase(), classes, attributes };
}

/**
 * Tells whether an element matches a selector without combinator.
 * @param {FakeElement} element The element.
 * @param {{tag: string, classes: string[], attributes: {name: string, value: string}[]}} compound The selector.
 * @returns {boolean} true when the element matches.
 */
function matchesCompound(element, compound) {
    return (!compound.tag || element.tagName === compound.tag)
        && compound.classes.every(name => element.classList.contains(name))
        && compound.attributes.every(({ name, value }) => element.getAttribute(name) !== null && (value === undefined || element.getAttribute(name) === value));
}

/**
 * Tells whether an element matches selectors joined by the descendant combinator.
 * @param {FakeElement} element The element.
 * @param {{tag: string, classes: string[]}[]} compounds The selectors, the last one for the element itself.
 * @returns {boolean} true when the element matches.
 */
function matches(element, compounds) {
    if (!matchesCompound(element, compounds.at(-1))) {
        return false;
    }

    let index = compounds.length - 2;
    for (let ancestor = element.parentNode; ancestor && index >= 0; ancestor = ancestor.parentNode) {
        if (matchesCompound(ancestor, compounds[index])) {
            index--;
        }
    }

    return index < 0;
}

/**
 * The document of a page, built from its HTML file.
 */
class FakeDocument {
    /**
     * Builds the document from well-formed HTML.
     * @param {string} html The HTML of the page.
     */
    constructor(html) {
        const root = new FakeElement("#document");
        const stack = [root];

        // Each token is a tag, a comment or the doctype, or a text.
        for (const [token] of html.matchAll(/<[^>]+>|[^<]+/g)) {
            if (token.startsWith("<!")) {
                continue;
            }

            if (token.startsWith("</")) {
                stack.pop();
            } else if (token.startsWith("<")) {
                const [, tagName, attributes] = /^<([\w-]+)(.*?)\/?>$/s.exec(token);
                const element = stack.at(-1).appendChild(new FakeElement(tagName));
                for (const [, name, value] of attributes.matchAll(/([\w:-]+)(?:="([^"]*)")?/g)) {
                    element.setAttribute(name, value ?? "");
                }
                if (!token.endsWith("/>") && !VOID_ELEMENTS.has(tagName.toLowerCase())) {
                    stack.push(element);
                }
            } else if (token.trim()) {
                stack.at(-1).appendChild(new FakeText(token.trim()));
            }
        }

        this.documentElement = root.children[0];
        this.body = this.documentElement.children.find(element => element.tagName === "BODY");
    }

    /**
     * Gets an element by its id.
     * @param {string} id The id.
     * @returns {FakeElement} The element, or null.
     */
    getElementById(id) {
        return this.documentElement.descendants().find(element => element.id === id) ?? null;
    }

    /**
     * Finds the elements that match a list of selectors.
     * @param {string} selectors The selectors.
     * @returns {FakeElement[]} The matching elements.
     */
    querySelectorAll(selectors) {
        return this.documentElement.querySelectorAll(selectors);
    }

    /**
     * Creates an element.
     * @param {string} tagName The name of the tag.
     * @returns {FakeElement} The element.
     */
    createElement(tagName) {
        return new FakeElement(tagName);
    }

    /**
     * Creates an element in a namespace, as an SVG element.
     * @param {string} namespace The namespace, ignored.
     * @param {string} tagName The name of the tag.
     * @returns {FakeElement} The element.
     */
    createElementNS(namespace, tagName) {
        return new FakeElement(tagName);
    }
}

/**
 * Loads a page and runs its scripts in order, as a host shows an MCP App in a frame.
 * @param {string} pagePath The path of the HTML file of the page.
 * @param {...string} scriptPaths The paths of the scripts of the page, in the order of the page.
 * @returns {object} The page: its document, the messages sent to the host, and functions to send a message of the host, to
 * resize the view and to run the callbacks of its timers.
 */
function loadPage(pagePath, ...scriptPaths) {
    const sent = [];
    const host = { postMessage: (message, targetOrigin) => sent.push({ message, targetOrigin }) };
    const messageListeners = [];
    const timers = [];
    let resize = null;

    globalThis.document = new FakeDocument(readFileSync(pagePath, "utf8"));
    globalThis.window = {
        parent: host,
        innerWidth: 599.5,
        addEventListener: (type, listener) => messageListeners.push(listener),
        // The timers of the page do not run by themselves: the tests run their callbacks.
        setInterval: callback => timers.push(callback)
    };
    globalThis.ResizeObserver = class {
        /**
         * Keeps the callback, that the tests call to resize the view.
         * @param {function(): void} callback The callback.
         */
        constructor(callback) {
            resize = callback;
        }

        /**
         * Observes an element: nothing to do.
         */
        observe() {
            // The tests resize the view themselves.
        }
    };

    for (const scriptPath of scriptPaths) {
        delete require.cache[require.resolve(scriptPath)];
        require(scriptPath);
    }

    return {
        document: globalThis.document,
        sent,
        receive: (data, origin = "https://host.example", source = host) => messageListeners.forEach(listener => listener({ data, origin, source })),
        resize: () => resize(),
        tick: () => timers.forEach(callback => callback())
    };
}

module.exports = { loadPage };
