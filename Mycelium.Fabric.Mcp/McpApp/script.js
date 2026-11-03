const btn_apply = document.getElementById('btn_apply');
const btn_cancel = document.getElementById('btn_cancel');
const target_state = document.getElementById('target_state');
const target_changes = document.getElementById('changes');
const target_checks = document.getElementById('checks');
const target_pills = document.getElementById('pills');
const target_note = document.getElementById('note');

let batchId = null;
let token = null;

// The origin of the host, known from its first message: until then, the messages go to the parent frame whatever its origin.
// A host that shows the view in a frame without an origin of its own ("null") keeps "*".
let hostOrigin = "*";

const STATUS_TEXT = {
    Pending: "Waiting for your decision",
    Refused: "Refused by Fabric: the assistant will correct it",
    Applied: "Applied to the model",
    Rejected: "Rejected"
};

const REQUIREMENT_TEXT = {
    Satisfied: "satisfied",
    NotSatisfied: "not satisfied",
    NotVerifiable: "not verifiable",
    NotEvaluated: "not evaluated"
};

// Colors of the Ownership codes given by the server, as in Bloom; other codes take one of the next colors.
const OWNERSHIP_COLORS = { PAY: "#EC4899", AOC: "#06B6D4", POW: "#EAB308", COM: "#8B5CF6", THE: "#EA580C", DAT: "#4F46E5", STR: "#6B7280", PRO: "#DC2626", SOF: "#10B981", MEC: "#92400E" };
const OTHER_OWNERSHIP_COLORS = ["#0F9E8C", "#D946EF", "#84CC16", "#F97316", "#14B8A6", "#A855F7"];

// The kind of check shown for a requirement after the batch, and for a new issue of validate_model; any other value is "info".
const REQUIREMENT_KIND = { NotSatisfied: "bad", Satisfied: "ok" };
const ISSUE_KIND = { Error: "bad", Warning: "warn" };

// The attributes that the server adds up to give the value of a part.
const BUDGET_ATTRIBUTES = new Set(["mass", "power"]);

const ICONS = {
    ok: '<circle cx="12" cy="12" r="9"/><path d="m8.5 12 2.4 2.4 4.6-4.8"/>',
    bad: '<circle cx="12" cy="12" r="9"/><path d="m15 9-6 6M9 9l6 6"/>',
    warn: '<path d="M10.3 3.9 1.9 18a2 2 0 0 0 1.7 3h16.8a2 2 0 0 0 1.7-3L13.7 3.9a2 2 0 0 0-3.4 0"/><path d="M12 9v4.5M12 17.2h.01"/>',
    info: '<circle cx="12" cy="12" r="9"/><path d="M12 11v5.5M12 7.8h.01"/>',
    check: '<path d="M20 6 9 17l-5-5"/>',
    part: '<path d="m12 3 3 3-3 3-3-3zM12 15l3 3-3 3-3-3zM6 9l3 3-3 3-3-3zM18 9l3 3-3 3-3-3z"/>',
    attr: '<path d="M4 17.5a8 8 0 1 1 16 0"/><path d="m12 17.5 3.5-5"/>',
    req: '<path d="M14 3H6.5A1.5 1.5 0 0 0 5 4.5v15A1.5 1.5 0 0 0 6.5 21h11a1.5 1.5 0 0 0 1.5-1.5V8z"/><path d="M14 3v5h5M9 13h6M9 17h4"/>',
    def: '<rect x="4" y="4" width="16" height="16" rx="2"/><path d="M4 9h16"/>',
    port: '<path d="M9 2.5v5M15 2.5v5M6 7.5h12V11a6 6 0 0 1-12 0zM12 17v4.5"/>',
    link: '<path d="M10 14a4 4 0 0 0 5.7 0l3-3a4 4 0 0 0-5.7-5.7l-1 1"/><path d="M14 10a4 4 0 0 0-5.7 0l-3 3a4 4 0 0 0 5.7 5.7l1-1"/>',
    folder: '<path d="M3 7.5A1.5 1.5 0 0 1 4.5 6h4l2 2h9A1.5 1.5 0 0 1 21 9.5v8a1.5 1.5 0 0 1-1.5 1.5h-15A1.5 1.5 0 0 1 3 17.5z"/>',
    cube: '<path d="M21 16.5v-9L12 3 3 7.5v9L12 21z"/><path d="M3.3 7.6 12 12l8.7-4.4M12 21v-9"/>'
};

// Apply: asks the server, through the host, to apply the batch with the comment of the user; both buttons stay disabled after.
btn_apply.addEventListener('click', ()=> {
    const comment = document.getElementById("comment1").value.trim();
    send({
        jsonrpc:"2.0",
        id :2,
        method:"tools/call",
        params: {name:"decide_batch", arguments: {batchId : batchId, token:token, apply:true, comment:comment}}
    });
    btn_apply.disabled = true;
    btn_cancel.disabled = true;
});

// Reject: asks the server, through the host, to reject the batch with the comment of the user; both buttons stay disabled after.
btn_cancel.addEventListener('click', ()=> {
    const comment = document.getElementById("comment1").value.trim();
    send({
        jsonrpc : "2.0",
        id : 3,
        method : "tools/call",
        params: {name: "decide_batch", arguments: {batchId : batchId, token:token, apply:false, comment:comment}}
    });
    btn_apply.disabled = true;
    btn_cancel.disabled = true;
});

// The tabs "Changes" and "Checks": a click shows the panel of the tab.
for (const tab of document.querySelectorAll(".seg button")) {
    tab.addEventListener("click", () => showTab(tab.dataset.t));
}

/**
 * Gets an element of the page.
 * @param {string} id The id of the element.
 * @returns {HTMLElement} The element, or null when the page has none with this id.
 */
const $ = id => document.getElementById(id);

/**
 * Sends a JSON-RPC message to the host, in the parent frame, at its origin once known.
 * @param {object} message The JSON-RPC request, response or notification.
 */
function send(message) { window.parent.postMessage(message, hostOrigin); }

/**
 * Gives a text to the AI assistant, through the host, as context for the next turn of the conversation.
 * @param {string} text The text for the assistant.
 */
function sendMessage(text) {
    send({
        jsonrpc: "2.0",
        id: 4,
        method: "ui/update-model-context",
        params: { content: [{ type: "text", text: text }] }
    });
}

// The messages of the host: the responses to the requests of the view, the result of apply_changes, its own requests and
// notifications.
window.addEventListener("message", e => {
    if (e.source !== window.parent || !isHostOrigin(e.origin)) return;
    const msg = e.data;
    if (msg?.jsonrpc !== "2.0") return;

    if (!msg.method) {
        onResponse(msg);
    } else if (msg.method === "ui/notifications/tool-result") {
        showToolResult(msg.params);
    } else if (msg.id !== undefined) {
        // A request of the host that the view has nothing to do for: it answers with an empty result.
        send({ jsonrpc: "2.0", id: msg.id, result: {} });
    } else if (msg.method === "ui/notifications/host-context-changed") {
        applyTheme(msg.params);
    }
});

/**
 * Tells whether a message comes from the host. Only the host, in the parent frame, talks to the view: the first message gives
 * its origin, and later messages must come from it.
 * @param {string} origin The origin of the message.
 * @returns {boolean} true when the message comes from the origin of the host.
 */
function isHostOrigin(origin) {
    if (hostOrigin === "*" && origin !== "null") hostOrigin = origin;
    return hostOrigin === "*" || origin === hostOrigin;
}

/**
 * Handles the response of the host to a request of the view: the theme once initialized, or the decision on the batch.
 * @param {object} msg The JSON-RPC response, whose id tells the request: 1 to initialize, 2 to apply, 3 to reject.
 */
function onResponse(msg) {
    if (msg.id === 1) {
        applyTheme(msg.result?.hostContext);
        send({ jsonrpc: "2.0", method: "ui/notifications/initialized", params: {} });
    } else if (msg.id === 2 || msg.id === 3) {
        showDecision(msg.result);
    }
}

/**
 * Shows the result of apply_changes: the batch that waits for the decision of the user, or the problems of a refused batch.
 * @param {object} result The result of the tool, whose structuredContent is the review of the batch.
 */
function showToolResult(result) {
    const review = result.structuredContent;
    if (!review) {
        hideDecision();
        target_state.textContent = "The batch could not be read";
        setNote(result.content?.[0]?.text ?? "", "note err");
        return;
    }
    setStatus(review.status);

    if (review.preview) {
        batchId = review.preview.batchId;
        token = review.preview.token ?? null;
        try {
            showPreview(review.preview);
        } catch (error) {
            target_state.textContent = "The view could not show the batch: " + error.message;
        }
        if (!token) target_state.textContent = "The batch has no token: the buttons cannot decide it.";
    } else {
        hideDecision();
        if (review.status === "Refused") {
            showProblems(review.result);
            setNote("Nothing was applied: the assistant has the problems and corrects the batch.");
        }
    }
}

/**
 * Shows the result of decide_batch, then gives its message to the assistant.
 * @param {object} result The result of the tool, whose structuredContent is the review of the decided batch.
 */
function showDecision(result) {
    if (result?.isError || !result?.structuredContent) {
        target_state.textContent = "The decision has failed";
        setNote(result?.content?.[0]?.text ?? "", "note err");
        return;
    }

    const status = result.structuredContent.status;
    setStatus(status);
    hideDecision();

    if (status === "Refused") {
        showProblems(result.structuredContent.result);
        setNote("Nothing was applied.", "done rej", "bad");
    } else if (status === "Applied") {
        setNote("Applied.", "done", "check");
    } else {
        setNote("Rejected.", "done rej");
    }

    sendMessage(result.structuredContent.message);
}

/**
 * Applies the theme of the host: its CSS variables and its color scheme.
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
 * Tells the host the size that the view needs, so that its frame shows the whole view.
 */
function sendSize() {
    const html = document.documentElement;
    const previous = html.style.height;
    html.style.height = "max-content";
    const height = Math.ceil(html.getBoundingClientRect().height);
    html.style.height = previous;
    send({ jsonrpc: "2.0", method: "ui/notifications/size-changed", params: { width: Math.ceil(window.innerWidth), height: height } });
}

new ResizeObserver(sendSize).observe(document.body);

send({
    jsonrpc: "2.0",
    id: 1,
    method: "ui/initialize",
    params: {
        protocolVersion: "2026-01-26",
        appInfo: { name: "review-batch", version: "0.1.0" },
        appCapabilities: {}
    }
});

/**
 * Creates an icon.
 * @param {string} name The name of the icon in ICONS.
 * @param {number} size The width and height of the icon, in pixels.
 * @returns {SVGElement} The icon, hidden from screen readers.
 */
function icon(name, size) {
    const svg = document.createElementNS("http://www.w3.org/2000/svg", "svg");
    svg.setAttribute("class", "ic");
    svg.setAttribute("width", size);
    svg.setAttribute("height", size);
    svg.setAttribute("viewBox", "0 0 24 24");
    svg.setAttribute("fill", "none");
    svg.setAttribute("stroke", "currentColor");
    svg.setAttribute("stroke-width", "1.75");
    svg.setAttribute("stroke-linecap", "round");
    svg.setAttribute("stroke-linejoin", "round");
    svg.setAttribute("aria-hidden", "true");
    svg.innerHTML = ICONS[name];
    return svg;
}

/**
 * Chooses the icon of an element from its metaclass.
 * @param {string} type The metaclass of the element, for example PartUsage.
 * @returns {string} The name of the icon in ICONS.
 */
function typeIcon(type) {
    if (/Satisfy|Connect|Interface|Succession|Allocation|Flow|Binding|Dependency/.test(type)) return "link";
    if (type.includes("Requirement")) return "req";
    if (type === "PartDefinition") return "part";
    if (type.endsWith("Definition")) return "def";
    if (type.includes("Port")) return "port";
    if (type.includes("Attribute")) return "attr";
    if (type.includes("Package")) return "folder";
    return "cube";
}

/**
 * Creates a span.
 * @param {string} className The CSS class of the span.
 * @param {string} [text] The text of the span, if any.
 * @returns {HTMLSpanElement} The span.
 */
function span(className, text) {
    const element = document.createElement("span");
    element.className = className;
    if (text !== undefined) element.textContent = text;
    return element;
}

/**
 * Adds the line that a list shows when it is empty.
 * @param {HTMLElement} target The list.
 * @param {string} text The text of the line.
 */
function addEmpty(target, text) {
    const empty = document.createElement("div");
    empty.className = "empty";
    empty.textContent = text;
    target.appendChild(empty);
}

/**
 * Gets the last segment of a qualified name.
 * @param {string} qualifiedName The qualified name, for example EOSat1::eosat1::camera, or nothing.
 * @returns {string} The name, for example camera, or an empty string.
 */
function shortName(qualifiedName) {
    return qualifiedName?.split("::").pop() ?? "";
}

/**
 * Writes a number with six significant digits at most, without trailing zeros.
 * @param {number} number The number.
 * @returns {string} The number, for example 125.8.
 */
function formatNumber(number) {
    return String(Number(number.toPrecision(6)));
}

/**
 * Writes a number with its sign.
 * @param {number} number The number.
 * @returns {string} The number, for example +3.1 or −2.
 */
function signed(number) {
    return (number < 0 ? "−" : "+") + formatNumber(Math.abs(number));
}

/**
 * Adds the rows of the created, modified or deleted elements as a tree: each element goes under the closest of its owners in
 * the list, and a created definition goes under the usages that it types.
 * @param {string} kind The kind of change: "add", "mod" or "del".
 * @param {object[]} elements The elements, as PreviewElement gives them.
 */
function addRows(kind, elements) {
    const byName = new Map(elements.filter(e => e.qualifiedName).map(e => [e.qualifiedName, e]));
    const children = new Map(elements.map(e => [e, []]));
    const roots = [];

    for (const element of elements) {
        const parent = findParent(element, byName);
        (parent ? children.get(parent) : roots).push(element);
    }

    // As in the mock-up, a created definition is shown under the usages that it types, and named on a single row.
    const definitions = new Map(kind === "add" ? roots.filter(e => e.type.endsWith("Definition")).map(e => [shortName(e.qualifiedName), e]) : []);
    const typing = element => (element.definition ?? "").split(", ").map(name => definitions.get(name)).filter(Boolean);
    const used = new Set(elements.flatMap(typing));
    const folded = [...definitions.values()].filter(definition => used.has(definition) || children.get(definition).length === 0);

    // As in the mock-up, the mass and power of a part are not repeated on rows of their own: the row of the part shows them.
    const shown = (element, child) => !(element.value && element.type === "PartUsage" && child.type === "AttributeUsage" && BUDGET_ATTRIBUTES.has(shortName(child.qualifiedName)));

    const visit = (element, depth, parentOwnership, path) => {
        addRow(kind, element, depth, parentOwnership);
        const ownership = element.ownership ?? parentOwnership;
        children.get(element).filter(child => shown(element, child)).forEach(child => visit(child, depth + 1, ownership, path));

        for (const definition of typing(element).filter(definition => !path.has(definition))) {
            children.get(definition).filter(child => shown(element, child)).forEach(child => visit(child, depth + 1, ownership, new Set(path).add(definition)));
        }
    };

    for (const root of roots) {
        if (root === folded[0]) {
            addDefinitionsRow(folded, elements.filter(e => typing(e).some(definition => folded.includes(definition))));
        } else if (!folded.includes(root)) {
            visit(root, 0, null, new Set());
        }
    }
}

/**
 * Adds the single row of the created definitions that are shown under the usages that they type.
 * @param {object[]} definitions The created definitions.
 * @param {object[]} usages The created usages typed by these definitions: the row shows their Ownership when they share one.
 */
function addDefinitionsRow(definitions, usages) {
    const owners = new Set(definitions.map(definition => definition.qualifiedName?.split("::").slice(-2, -1)[0]));
    const ownerships = new Set(usages.map(usage => usage.ownership).filter(Boolean));
    const iconName = definitions.every(definition => definition.type === "PartDefinition") ? "part" : "def";
    const title = definitions.map(definition => `${definition.qualifiedName ?? ""} (${definition.type})`).join("\n");

    addLine("add", 0, iconName, title, name => {
        name.textContent = definitions.map(definition => shortName(definition.qualifiedName) || definition.type).join(", ");
        const [owner] = owners;
        if (owners.size === 1 && owner) {
            name.append(" ");
            name.appendChild(document.createElement("i")).textContent = "in " + owner;
        }
    }, definitions.length === 1 ? "1 def" : `${definitions.length} defs`, ownerships.size === 1 ? [...ownerships][0] : null);
}

/**
 * Adds a row for each total that the batch changes, for example the mass of eosat1, with its value before and after.
 * @param {object[]} impacts The budget impacts of the preview.
 */
function addImpactRows(impacts) {
    for (const impact of impacts) {
        const unit = impact.unit ? " " + impact.unit : "";
        addLine("mod", 0, "attr", `${impact.part}::${impact.attribute}, total over the part and its sub-parts`,
            name => name.textContent = `${shortName(impact.part)}::${impact.attribute}`,
            `${formatNumber(impact.before)} → ${formatNumber(impact.after)}${unit}`, null);
    }
}

/**
 * Finds the closest owner of an element in a list, from its qualified name.
 * @param {object} element The element.
 * @param {Map<string, object>} byName The elements of the list, indexed by qualified name.
 * @returns {object} The closest owner in the list, or null.
 */
function findParent(element, byName) {
    const segments = element.qualifiedName?.split("::") ?? [];
    for (let length = segments.length - 1; length > 0; length--) {
        const parent = byName.get(segments.slice(0, length).join("::"));
        if (parent) return parent;
    }
    return null;
}

/**
 * Gives the color of an Ownership: the color of Bloom for a known code, else a fixed color computed from the code.
 * @param {string} ownership The code of the Ownership, for example THE.
 * @returns {string} The color.
 */
function ownershipColor(ownership) {
    if (OWNERSHIP_COLORS[ownership]) return OWNERSHIP_COLORS[ownership];
    const hash = [...ownership].reduce((sum, character) => sum + character.codePointAt(0), 0);
    return OTHER_OWNERSHIP_COLORS[hash % OTHER_OWNERSHIP_COLORS.length];
}

/**
 * Adds the row of an element: its name, definition and multiplicity, its value and its Ownership.
 * @param {string} kind The kind of change: "add", "mod" or "del".
 * @param {object} element The element, as PreviewElement gives it.
 * @param {number} depth The depth of the row in the tree.
 * @param {string} parentOwnership The Ownership of the rows above it in the tree, or null.
 */
function addRow(kind, element, depth, parentOwnership) {
    const title = element.qualifiedName ? `${element.qualifiedName} (${element.type})` : element.type;
    // As in the mock-up, a row shows its Ownership only when it differs from the one of the row above it in the tree.
    const ownership = element.ownership !== parentOwnership ? element.ownership : null;

    addLine(kind, depth, typeIcon(element.type), title, name => {
        if (element.qualifiedName) {
            const segments = element.qualifiedName.split("::");
            name.textContent = segments[segments.length - 1];
            if (element.definition) name.append(" : " + element.definition);
            if (element.multiplicity) name.append(` [${element.multiplicity}]`);
            if (depth === 0 && segments.length > 1) {
                name.append(" ");
                name.appendChild(document.createElement("i")).textContent = "in " + segments[segments.length - 2];
            }
        } else {
            name.appendChild(document.createElement("i")).textContent = element.type;
        }
    }, element.value, ownership);
}

/**
 * Adds a row to the list of changes.
 * @param {string} kind The kind of change: "add", "mod" or "del".
 * @param {number} depth The depth of the row in the tree.
 * @param {string} iconName The name of the icon in ICONS.
 * @param {string} title The tooltip of the row.
 * @param {function(HTMLElement): void} fillName Fills the span of the name.
 * @param {string} value The value shown on the right, or nothing.
 * @param {string} ownership The Ownership shown on the right, or nothing.
 */
function addLine(kind, depth, iconName, title, fillName, value, ownership) {
    const row = document.createElement("div");
    row.className = "cr " + kind;
    row.title = title;
    const operation = span("op", { add: "+", mod: "~", del: "−" }[kind]);
    operation.style.marginLeft = depth * 20 + "px";
    row.appendChild(operation);

    const typeSpan = span("ti");
    typeSpan.appendChild(icon(iconName, 13));
    row.appendChild(typeSpan);

    const name = span("nm");
    fillName(name);
    row.appendChild(name);

    if (value) row.appendChild(span("v", value));
    if (ownership) {
        const ownershipSpan = span("own", ownership);
        ownershipSpan.style.setProperty("--oc", ownershipColor(ownership));
        row.appendChild(ownershipSpan);
    }
    target_changes.appendChild(row);
}

/**
 * Adds a tag to the zone of the impacts.
 * @param {HTMLElement} target The zone of the impacts.
 * @param {string} kind The kind of the tag, "ok", "bad" or "warn", or null for a plain tag.
 * @param {string} text The text of the tag.
 */
function addTag(target, kind, text) {
    const tag = span(kind ? "tagv " + kind : "tagv");
    if (kind) tag.appendChild(icon(kind, 12));
    tag.append(text);
    target.appendChild(tag);
}

/**
 * Tells how the batch moves a requirement.
 * @param {object} change The requirement, with its status before and after the batch.
 * @returns {string} "fails" when the batch breaks the requirement, "worse" when it moves a value already beyond the limit
 * further away, "met" when it satisfies it, or null.
 */
function requirementTrend(change) {
    if (change.statusAfter === "NotSatisfied") {
        if (change.statusBefore !== "NotSatisfied") return "fails";
        return change.gapChange < 0 ? "worse" : null;
    }
    return change.statusAfter === "Satisfied" && change.statusBefore === "NotSatisfied" ? "met" : null;
}

/**
 * Shows the tags of the impacts of the batch: the change of each total, the requirements that it breaks or satisfies and
 * the new issues.
 * @param {object} preview The preview of the batch.
 */
function showImpacts(preview) {
    const target = $("impacts");
    target.replaceChildren();

    const impacts = preview.budgetImpacts ?? [];
    for (const impact of impacts) {
        // The name of the part tells the totals apart when the model has several top-level parts.
        const prefix = impacts.filter(other => other.attribute === impact.attribute).length > 1 ? shortName(impact.part) + " " : "";
        const label = impact.attribute[0].toUpperCase() + impact.attribute.slice(1);
        addTag(target, null, `${prefix}${label} ${signed(impact.after - impact.before)}${impact.unit ? " " + impact.unit : ""}`);
    }

    const tagged = new Set();
    for (const change of preview.requirementChanges) {
        const title = change.reqId ?? change.name;
        const trend = requirementTrend(change);
        if (!trend || tagged.has(title)) continue;
        tagged.add(title);
        addTag(target, trend === "met" ? "ok" : "bad", `${title} ${trend}`);
    }

    if (preview.newIssueCount > 0) addTag(target, "warn", plural(preview.newIssueCount, "new issue"));
    target.hidden = target.childElementCount === 0;
}

/**
 * Adds a check to the tab "Checks".
 * @param {string} kind The kind of the check: "ok", "bad", "warn" or "info".
 * @param {string} title The title of the check.
 * @param {string} [detail] The detail shown under the title, if any.
 */
function addCheck(kind, title, detail) {
    const check = document.createElement("div");
    check.className = "chk " + kind;
    check.appendChild(icon(kind, 15));

    const text = document.createElement("div");
    text.textContent = title;
    if (detail) {
        text.appendChild(document.createElement("small")).textContent = detail;
    }
    check.appendChild(text);
    target_checks.appendChild(check);
}

/**
 * Writes a count with its word, in the plural when needed.
 * @param {number} count The count.
 * @param {string} word The word in the singular.
 * @returns {string} The count and its word, for example 2 new issues.
 */
function plural(count, word) {
    return `${count} ${word}${count === 1 ? "" : "s"}`;
}

/**
 * Adds the pill with the number of created, modified or deleted elements, unless there is none.
 * @param {string} kind The kind of change: "add", "mod" or "del".
 * @param {string} sign The sign of the change: "+", "~" or "−".
 * @param {number} count The number of elements.
 */
function addPill(kind, sign, count) {
    if (count === 0) return;
    target_pills.appendChild(span("pill " + kind, sign + count));
}

/**
 * Shows on the tab "Checks" the number of failed checks and warnings, or nothing when there is none.
 */
function updateCheckCount() {
    const count = target_checks.querySelectorAll(".chk.bad, .chk.warn").length;
    $("check_count").textContent = count;
    $("check_count").hidden = count === 0;
}

/**
 * Shows a tab and its panel.
 * @param {string} name The name of the tab: "chg" or "chk".
 */
function showTab(name) {
    for (const element of document.querySelectorAll(".seg button, .segp")) {
        element.classList.toggle("on", (element.dataset.t ?? element.dataset.p) === name);
    }
}

/**
 * Shows the status of the batch under the title.
 * @param {string} status The status of the batch: Pending, Refused, Applied or Rejected.
 */
function setStatus(status) {
    target_state.textContent = STATUS_TEXT[status] ?? status;
}

/**
 * Shows a note at the bottom of the view.
 * @param {string} text The text of the note.
 * @param {string} [className] The CSS class of the note, "note" when not given.
 * @param {string} [iconName] The name of the icon in ICONS, if any.
 */
function setNote(text, className, iconName) {
    target_note.hidden = false;
    target_note.className = className ?? "note";
    target_note.replaceChildren();
    if (iconName) target_note.appendChild(icon(iconName, 15));
    target_note.append(text);
}

/**
 * Shows the preview of a batch: the pills, the tree of the changes, the impacts and the checks.
 * @param {object} preview The preview of the batch, as BatchPreview gives it.
 */
function showPreview(preview) {
    target_changes.replaceChildren();
    target_checks.replaceChildren();
    target_pills.replaceChildren();

    addPill("add", "+", preview.createdElements.length);
    addPill("mod", "~", preview.modifiedElements.length);
    addPill("del", "−", preview.deletedElements.length);

    addRows("add", preview.createdElements);
    addRows("mod", preview.modifiedElements);
    addRows("del", preview.deletedElements);
    if (target_changes.childElementCount === 0) addEmpty(target_changes, "No change");
    addImpactRows(preview.budgetImpacts ?? []);
    showImpacts(preview);

    addRequirementChecks(preview.requirementChanges);
    addIssueChecks(preview);
    updateCheckCount();
    showTab("chg");
}

/**
 * Adds a check for each requirement of the preview, with its status before and after the batch.
 * @param {object[]} requirementChanges The requirements of the preview.
 */
function addRequirementChecks(requirementChanges) {
    for (const r of requirementChanges) {
        const title = r.reqId && r.name ? `${r.reqId} · ${r.name}` : r.reqId ?? r.name;
        const before = REQUIREMENT_TEXT[r.statusBefore] ?? r.statusBefore ?? "new";
        const after = REQUIREMENT_TEXT[r.statusAfter] ?? r.statusAfter ?? "deleted";
        const status = r.statusBefore === r.statusAfter ? after : `${before} → ${after}`;
        addCheck(REQUIREMENT_KIND[r.statusAfter] ?? "info", title, [r.satisfiedBy ? shortName(r.satisfiedBy) : null, r.value, status].filter(Boolean).join(" · "));
    }
}

/**
 * Adds a check for each new issue of validate_model, then a check for the new issues not listed and the resolved issues.
 * @param {object} preview The preview of the batch.
 */
function addIssueChecks(preview) {
    for (const issue of preview.newIssues) {
        addCheck(ISSUE_KIND[issue.severity] ?? "info", issue.message, [issue.element, issue.rule].filter(Boolean).join(" · "));
    }

    if (preview.newIssueCount > preview.newIssues.length) {
        addCheck("info", `… and ${plural(preview.newIssueCount - preview.newIssues.length, "more new issue")}`);
    }

    const resolved = preview.resolvedIssueCount > 0 ? `${plural(preview.resolvedIssueCount, "existing issue")} resolved` : null;
    if (preview.newIssueCount === 0) {
        addCheck("ok", "Validation: no new issue", resolved ?? "validate_model on the model with the batch applied");
    } else if (resolved) {
        addCheck("ok", resolved);
    }
}

/**
 * Hides the comment and the buttons, once the batch can no longer be decided.
 */
function hideDecision() {
    document.getElementById("comment_zone").hidden = true;
    document.getElementById("button_zone").hidden = true;
}

/**
 * Shows the problems of a refused batch on the tab "Checks".
 * @param {object} result The result of apply_changes, with its problems.
 */
function showProblems(result) {
    target_changes.replaceChildren();
    target_checks.replaceChildren();
    $("impacts").hidden = true;
    addEmpty(target_changes, "Nothing was applied");
    for (const problem of result.problems) {
        addCheck("bad", problem);
    }
    updateCheckCount();
    showTab("chk");
}
