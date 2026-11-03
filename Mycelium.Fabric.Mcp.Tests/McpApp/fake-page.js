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
     * @returns {{contains: function(string): boolean, toggle: function(string, boolean): boolean}} The classes.
     */
    get classList() {
        const names = () => this.className.split(/\s+/).filter(Boolean);

        return {
            contains: name => names().includes(name),
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
        const list = selectors.split(",").map(selector => selector.trim().split(/\s+/).map(parseCompound));
        return this.descendants().filter(element => list.some(compounds => matches(element, compounds)));
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
 * Reads a selector without combinator, as "button" or ".chk.bad".
 * @param {string} text The selector.
 * @returns {{tag: string, classes: string[]}} The tag name, empty for any tag, and the classes.
 */
function parseCompound(text) {
    const [tag, ...classes] = text.split(".");
    return { tag: tag.toUpperCase(), classes };
}

/**
 * Tells whether an element matches a selector without combinator.
 * @param {FakeElement} element The element.
 * @param {{tag: string, classes: string[]}} compound The selector.
 * @returns {boolean} true when the element matches.
 */
function matchesCompound(element, compound) {
    return (!compound.tag || element.tagName === compound.tag) && compound.classes.every(name => element.classList.contains(name));
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
 * Loads a page and runs its script, as a host shows an MCP App in a frame.
 * @param {string} pagePath The path of the HTML file of the page.
 * @param {string} scriptPath The path of the script of the page.
 * @returns {object} The page: its document, the messages sent to the host, and functions to send a message of the host and to
 * resize the view.
 */
function loadPage(pagePath, scriptPath) {
    const sent = [];
    const host = { postMessage: (message, targetOrigin) => sent.push({ message, targetOrigin }) };
    const messageListeners = [];
    let resize = null;

    globalThis.document = new FakeDocument(readFileSync(pagePath, "utf8"));
    globalThis.window = { parent: host, innerWidth: 599.5, addEventListener: (type, listener) => messageListeners.push(listener) };
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

    delete require.cache[require.resolve(scriptPath)];
    require(scriptPath);

    return {
        document: globalThis.document,
        sent,
        receive: (data, origin = "https://host.example", source = host) => messageListeners.forEach(listener => listener({ data, origin, source })),
        resize: () => resize()
    };
}

module.exports = { loadPage };
