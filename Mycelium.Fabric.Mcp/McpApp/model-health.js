/**
 * Gets an element of the page.
 * @param {string} id The id of the element.
 * @returns {HTMLElement} The element, or null when the page has none with this id.
 */
const $ = id => document.getElementById(id);

// The number of checks or issues asked for in one call: the most that check_requirements and validate_model give.
const PAGE_SIZE = 20;

// The statuses of check_requirements for which the requirement could not be checked.
const NOT_CHECKED = new Set(["NotVerifiable", "NotEvaluated"]);

// The statuses of check_requirements, as the view and its messages write them.
const STATUS_TEXT = { Satisfied: "met", NotSatisfied: "not met", NotVerifiable: "not verifiable", NotEvaluated: "not evaluated" };

// The severities of validate_model, the most serious first: the title of their section and the kind of their items.
const SEVERITIES = [
    { severity: "Error", title: "Errors", kind: "bad" },
    { severity: "Warning", title: "Warnings", kind: "warn" },
    { severity: "Information", title: "Information", kind: "info" }
];

// The drawings of the icons, inside a 24 × 24 box, as in the mock-up.
const ICONS = {
    ok: '<circle cx="12" cy="12" r="9"/><path d="m8.5 12 2.4 2.4 4.6-4.8"/>',
    bad: '<circle cx="12" cy="12" r="9"/><path d="m15 9-6 6M9 9l6 6"/>',
    warn: '<path d="M10.3 3.9 1.9 18a2 2 0 0 0 1.7 3h16.8a2 2 0 0 0 1.7-3L13.7 3.9a2 2 0 0 0-3.4 0"/><path d="M12 9v4.5M12 17.2h.01"/>',
    info: '<circle cx="12" cy="12" r="9"/><path d="M12 11v5.5M12 7.8h.01"/>',
    na: '<circle cx="12" cy="12" r="9"/><path d="M8 12h8"/>',
    target: '<circle cx="12" cy="12" r="7.5"/><circle cx="12" cy="12" r="2.5"/><path d="M12 2v2.5M12 19.5V22M2 12h2.5M19.5 12H22"/>',
    scale: '<path d="M12 3v18M5 21h14M7 7h10M7 7l-3.5 7a3.5 3.5 0 0 0 7 0zM17 7l-3.5 7a3.5 3.5 0 0 0 7 0z"/>',
    wand: '<path d="m15 4 1 2.5L18.5 7.5 16 8.5 15 11l-1-2.5L11.5 7.5 14 6.5zM4 20 13.5 10.5"/>'
};

// What the view knows: the result of check_requirements sent by the host, shown when the view cannot run the checks itself;
// the checks and the validation of the last run, with the time of this run; the names of every rule, the rules that the user
// does not run here, the items whose details are shown, the details read for them, and the number of the last run.
const state = {
    first: null,
    checks: null,
    validation: null,
    checkedAt: null,
    ruleNames: null,
    ignored: new Set(),
    open: new Set(),
    details: new Map(),
    run: 0
};

// The origin of the host, known from its first message: until then, the messages go to the parent frame whatever its origin.
// A host that shows the view in a frame without an origin of its own ("null") keeps "*".
let hostOrigin = "*";

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

// The id of the next request of the view, and the requests that wait for the response of the host.
let nextId = 1;
const pending = new Map();

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

// The messages of the host: the responses to the requests of the view, the result of check_requirements, the changes of
// theme, and its own requests, answered with an empty result.
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
 */
async function start() {
    try {
        const result = await request("ui/initialize", {
            protocolVersion: "2026-01-26",
            appInfo: { name: "model-health", version: "0.1.0" },
            appCapabilities: {}
        });
        applyTheme(result?.hostContext);
        notify("ui/notifications/initialized", {});
    } catch (error) {
        showError("The host did not start the view: " + error.message);
    }
}

/**
 * Reads the content of the result of a tool: its structured content, else the JSON of its text.
 * @param {object} result The result of the tool.
 * @returns {object} The content, or null when the result has no text.
 * @throws {SyntaxError} When the text is not JSON.
 */
function readResult(result) {
    if (result?.structuredContent) return result.structuredContent;
    const text = result?.content?.find(block => block.type === "text")?.text;
    return text ? JSON.parse(text) : null;
}

/**
 * Calls a tool of the server through the host, without the AI assistant.
 * @param {string} name The name of the tool, for example check_requirements.
 * @param {object} args The arguments of the tool.
 * @returns {Promise<object>} The content of the result.
 * @throws {Error} When the host refuses the call or the tool fails, with the error of the tool.
 */
async function callTool(name, args) {
    const result = await request("tools/call", { name, arguments: args });
    if (result?.isError) throw new Error(result.content?.[0]?.text || name + " has failed.");
    return readResult(result);
}

/**
 * Keeps the result of check_requirements called by the assistant, then runs the checks and the validation of the whole model.
 * @param {object} result The result of the tool.
 */
function onToolResult(result) {
    try {
        state.first = result?.isError ? null : readResult(result);
    } catch {
        // A result that is not JSON: the view runs the checks itself anyway.
        state.first = null;
    }
    void refresh(true);
}

/**
 * Runs the checks again, through the host and without the AI assistant: the requirements, unless told otherwise, and the
 * validation with the rules that the user has not set aside; then shows them. Only the last run counts.
 * @param {boolean} withChecks false to keep the checks of the requirements and run the validation only.
 * @returns {Promise<void>} Resolved once the view shows the result or the error.
 */
async function refresh(withChecks) {
    const run = ++state.run;
    setBusy(true);
    try {
        const [checks, validation] = await Promise.all([withChecks ? fetchChecks() : state.checks, fetchValidation()]);
        if (run !== state.run) return;
        Object.assign(state, { checks, validation, checkedAt: Date.now() });
        state.details.clear();
        $("error").hidden = true;
        render();
    } catch (error) {
        if (run !== state.run) return;
        if (!state.checks && state.first) {
            state.checks = state.first;
            render();
            $("subtitle").textContent = "As checked for the assistant";
        }
        showError("The view could not run the checks: " + error.message);
    } finally {
        if (run === state.run) setBusy(false);
    }
}

/**
 * Gets every check of the requirements, page by page.
 * @returns {Promise<object>} The result of check_requirements, with the checks of every page.
 */
async function fetchChecks() {
    const first = await callTool("check_requirements", { offset: 0, limit: PAGE_SIZE });
    const requirements = [...first.requirements];
    let offset = first.nextOffset;
    while (offset != null) {
        const page = await callTool("check_requirements", { offset, limit: PAGE_SIZE });
        requirements.push(...page.requirements);
        offset = page.nextOffset;
    }
    return { ...first, requirements, nextOffset: null };
}

/**
 * Runs the validation with the rules that the user has not set aside, then gets the first page of the issues of each rule
 * that finds some.
 * @returns {Promise<object>} The report of validate_model and the issues by rule, or null when every rule is set aside.
 */
async function fetchValidation() {
    const rules = state.ignored.size > 0 ? state.ruleNames.filter(name => !state.ignored.has(name)) : null;
    if (rules?.length === 0) return null;

    const report = await callTool("validate_model", rules ? { rules, limit: 1 } : { limit: 1 });
    state.ruleNames ??= report.rules.map(rule => rule.name);
    const pages = await Promise.all(report.rules.filter(rule => rule.issueCount > 0).map(rule => fetchIssues(rule.name, 0)));
    return { report, pages: new Map(pages.map(page => [page.rule, page])) };
}

/**
 * Gets a page of the issues of a rule.
 * @param {string} rule The name of the rule.
 * @param {number} offset The number of issues to skip.
 * @returns {Promise<{rule: string, issues: object[], nextOffset: number}>} The issues, and the offset of the next page or null.
 */
async function fetchIssues(rule, offset) {
    const page = await callTool("validate_model", { rules: [rule], offset, limit: PAGE_SIZE });
    return { rule, issues: page.issues, nextOffset: page.nextOffset ?? null };
}

/**
 * Shows that the view is checking, or no longer: the button to check again is disabled meanwhile.
 * @param {boolean} busy true while the view runs the checks.
 */
function setBusy(busy) {
    $("check_again").disabled = busy;
    $("view").setAttribute("aria-busy", String(busy));
    if (busy && state.checkedAt === null) $("subtitle").textContent = "Checking the requirements and the rules…";
}

/**
 * Shows an error under the header.
 * @param {string} text The error.
 */
function showError(text) {
    $("error").textContent = text;
    $("error").hidden = false;
}

/**
 * Creates an element.
 * @param {string} tag The name of the tag.
 * @param {string} [className] The CSS classes, or nothing.
 * @param {*} [text] The text of the element, or nothing.
 * @returns {HTMLElement} The element.
 */
function el(tag, className, text) {
    const element = document.createElement(tag);
    if (className) element.className = className;
    if (text !== undefined) element.textContent = text;
    return element;
}

/**
 * Creates an icon.
 * @param {string} name The name of the icon in ICONS.
 * @param {number} size The width and height of the icon, in pixels.
 * @returns {SVGElement} The icon, hidden from screen readers.
 */
function icon(name, size) {
    const svg = document.createElementNS("http://www.w3.org/2000/svg", "svg");
    for (const [attribute, value] of Object.entries({
        class: "ic", width: size, height: size, viewBox: "0 0 24 24", fill: "none", stroke: "currentColor", "stroke-width": "1.75",
        "stroke-linecap": "round", "stroke-linejoin": "round", "aria-hidden": "true"
    })) {
        svg.setAttribute(attribute, value);
    }
    svg.innerHTML = ICONS[name];
    return svg;
}

/**
 * Creates a small button of an item.
 * @param {string} iconName The name of its icon in ICONS, or null for none.
 * @param {string} label The label of the button.
 * @param {function(HTMLButtonElement): void} onClick What a click does, given the button.
 * @returns {HTMLButtonElement} The button.
 */
function button(iconName, label, onClick) {
    const element = el("button", "btn sm");
    element.type = "button";
    if (iconName) element.append(icon(iconName, 12));
    element.append(el("span", null, label));
    element.addEventListener("click", () => onClick(element));
    return element;
}

/**
 * Writes a count with its word, in the plural when needed.
 * @param {number} count The count.
 * @param {string} word The word in the singular.
 * @returns {string} The count and its word, for example 3 warnings.
 */
function plural(count, word) {
    return `${count} ${word}${count === 1 ? "" : "s"}`;
}

/**
 * Writes a number with six significant digits at most, without trailing zeros.
 * @param {number} number The number.
 * @returns {string} The number, for example 150.96.
 */
function formatNumber(number) {
    return String(Number(number.toPrecision(6)));
}

/**
 * Gets the last segment of a qualified name.
 * @param {string} qualifiedName The qualified name, for example EOSat1::Architecture::eosat1, or nothing.
 * @returns {string} The name, for example eosat1, or an empty string.
 */
function shortName(qualifiedName) {
    return qualifiedName?.split("::").pop() ?? "";
}

/**
 * Gets the short name of the element of an issue: its name, or its metaclass and the name of its owner when it has no name.
 * @param {string} element The element as validate_model writes it, for example EOSat1::Requirements::massBudget or
 * SatisfyRequirementUsage in EOSat1::Requirements.
 * @returns {string} The short name, for example massBudget or SatisfyRequirementUsage in Requirements.
 */
function elementName(element) {
    const [name, owner] = element.split(" in ");
    return owner === undefined ? shortName(name) : name + " in " + shortName(owner);
}

/**
 * Tells when the checks ran.
 * @param {number} time The time of the run, in milliseconds.
 * @returns {string} For example just now, 5 min ago or at 14:32.
 */
function since(time) {
    const minutes = Math.floor((Date.now() - time) / 60000);
    if (minutes < 1) return "just now";
    if (minutes < 60) return minutes + " min ago";
    return "at " + new Date(time).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });
}

/**
 * Gives the kind of a counter from the number of serious and less serious problems.
 * @param {number} bad The number of serious problems.
 * @param {number} warn The number of less serious problems.
 * @returns {string} "bad", "warn" or "ok".
 */
function kindOf(bad, warn) {
    if (bad > 0) return "bad";
    if (warn > 0) return "warn";
    return "ok";
}

/**
 * Gets the name of the model: the first segment of the qualified names that the checks and the issues give.
 * @returns {string} The name, for example EOSat1, or null when nothing gives it.
 */
function modelName() {
    const names = [
        ...state.checks.requirements.map(check => check.satisfiedBy),
        ...[...state.validation?.pages.values() ?? []].flatMap(page => page.issues.map(issue => issue.element))
    ];
    return names.map(name => /^([^\s:]+)::/.exec(name ?? "")?.[1]).find(Boolean) ?? null;
}

/**
 * Gets what names a requirement in the view: its identifier, else its name.
 * @param {object} check The check of the requirement.
 * @returns {string} For example REQ-SYS-001, or Requirement when the requirement has neither.
 */
function requirementLabel(check) {
    return check.reqId ?? check.name ?? "Requirement";
}

/**
 * Gets the title of a requirement: its text, else its name when its identifier names it.
 * @param {object} check The check of the requirement.
 * @returns {string} The title, or an empty string.
 */
function requirementTitle(check) {
    if (check.text) return check.text;
    return check.reqId && check.name ? check.name : "";
}

/**
 * Writes how far the value of a requirement not met is from its limit, the way of the gap read from the last comparison of
 * its constraint (an arrow -> is not a comparison).
 * @param {object} check The check of the requirement.
 * @returns {string} For example over by 0.96 kg or short by 30, or null without gap.
 */
function gapText(check) {
    if (check.status !== "NotSatisfied" || check.gap == null) return null;
    const amount = formatNumber(Math.abs(check.gap)) + (check.unit ? " " + check.unit : "");
    const operator = [...check.constraint.matchAll(/(?<!-)(<=|>=|<|>)/g)].at(-1)?.[1] ?? "";
    if (operator.startsWith("<")) return "over by " + amount;
    if (operator.startsWith(">")) return "short by " + amount;
    return "off by " + amount;
}

/**
 * Writes the value of a requirement with its unit, and without the margins when they count.
 * @param {object} check The check of the requirement.
 * @returns {string} For example 150.96 kg (125.8 kg without margins), or null without value.
 */
function valueText(check) {
    if (check.value == null) return null;
    const unit = check.unit ? " " + check.unit : "";
    const withoutMargins = check.valueWithoutMargins == null ? "" : ` (${formatNumber(check.valueWithoutMargins)}${unit} without margins)`;
    return formatNumber(check.value) + unit + withoutMargins;
}

/**
 * Gets the rules that find issues and that the user has not set aside.
 * @returns {object[]} The rules, as validate_model gives them.
 */
function shownRules() {
    return (state.validation?.report.rules ?? []).filter(rule => rule.issueCount > 0 && !state.ignored.has(rule.name));
}

/**
 * Shows the last checks: the header, the counters, the problems, the requirements met and the footer.
 */
function render() {
    const name = modelName();
    $("title").textContent = name ? "Model health · " + name : "Model health";
    drawSubtitle();
    drawRequirementCounter(state.checks);
    drawValidationCounter(state.validation?.report);
    $("kpis").hidden = false;
    drawItems();
    drawMetList();
    drawFooter();
}

/**
 * Writes under the title the number of elements checked and when, once the view has run the checks.
 */
function drawSubtitle() {
    if (state.checkedAt === null) return;
    const count = state.validation?.report.elementCount;
    $("subtitle").textContent = [count === undefined ? null : plural(count, "element"), "checked " + since(state.checkedAt)].filter(Boolean).join(" · ");
}

/**
 * Writes the text of a counter under its value, in the colour of its kind.
 * @param {string} id The id of the text.
 * @param {string} text The text.
 * @param {string} kind "bad", "warn", "ok", or an empty string for a neutral text.
 */
function setCounterText(id, text, kind) {
    $(id).textContent = text;
    $(id).className = kind ? "s " + kind : "s";
}

/**
 * Draws the counter of the requirements: those met out of all of them, those not met and those not checked.
 * @param {object} checks The result of check_requirements.
 */
function drawRequirementCounter(checks) {
    const notMet = checks.notSatisfiedCount;
    const notChecked = checks.notVerifiableCount + checks.notEvaluatedCount;
    const total = checks.satisfiedCount + notMet + notChecked;

    $("met").replaceChildren(String(checks.satisfiedCount), el("span", null, " / " + total));
    $("met_bar").style.width = total === 0 ? "0%" : Math.round(checks.satisfiedCount / total * 1000) / 10 + "%";
    $("met_track").className = notMet === 0 && notChecked > 0 ? "bar warn" : "bar";

    if (total === 0) {
        setCounterText("met_detail", "No requirement in the model", "");
        return;
    }
    const parts = [notMet > 0 ? notMet + " not met" : null, notChecked > 0 ? notChecked + " not checked" : null].filter(Boolean);
    setCounterText("met_detail", parts.join(" · ") || "All met", kindOf(notMet, notChecked));
}

/**
 * Draws the counter of the validation: the errors, the warnings and the number of rules checked.
 * @param {object} report The report of validate_model, or nothing when no rule ran.
 */
function drawValidationCounter(report) {
    if (!report) {
        $("errors").replaceChildren("–");
        $("validation_bar").style.width = "0%";
        setCounterText("validation_detail", "No rule checked", "");
        return;
    }
    $("errors").replaceChildren(String(report.errorCount), el("span", null, report.errorCount === 1 ? " error" : " errors"));
    $("validation_bar").style.width = "100%";
    $("validation_bar").className = kindOf(report.errorCount, report.warningCount);
    setCounterText("validation_detail", plural(report.warningCount, "warning") + " · " + plural(report.rules.length, "rule"), report.warningCount > 0 ? "warn" : "");
}

/**
 * Draws the problems by section: the requirements not met, those not checked, then the rules that find issues, by severity;
 * or a line that tells that there is nothing to fix.
 */
function drawItems() {
    const target = $("items");
    target.replaceChildren();
    const checks = state.checks.requirements;

    addSection(target, "Not met", checks.filter(check => check.status === "NotSatisfied").map(requirementItem));
    addSection(target, "Not checked", checks.filter(check => NOT_CHECKED.has(check.status)).map(requirementItem));
    const rules = shownRules();
    for (const { severity, title, kind } of SEVERITIES) {
        addSection(target, title, rules.filter(rule => rule.severity === severity).map(rule => ruleItem(rule, kind)));
    }

    if (target.childElementCount === 0) {
        const empty = el("div", "mh-empty");
        empty.append(icon("ok", 14), "Nothing to fix: no requirement is left unmet or unchecked, and no rule finds an issue.");
        target.append(empty);
    }
}

/**
 * Gets the requirements checked against several parts, whose checks name their part.
 * @param {object[]} checks The checks.
 * @returns {Set<string>} The identifiers of these requirements.
 */
function repeatedRequirements(checks) {
    const counts = new Map();
    for (const check of checks) counts.set(check.id, (counts.get(check.id) ?? 0) + 1);
    return new Set([...counts].filter(([, count]) => count > 1).map(([id]) => id));
}

/**
 * Adds a section with its title, unless it has no item.
 * @param {HTMLElement} target The list of the problems.
 * @param {string} title The title of the section.
 * @param {HTMLElement[]} items The items of the section.
 */
function addSection(target, title, items) {
    if (items.length === 0) return;
    target.append(el("div", "sec", title), ...items);
}

/**
 * Creates the item of a requirement not met or not checked: its identifier, text, part, explanation and gap, the warnings of
 * its check, and its actions.
 * @param {object} check The check of the requirement.
 * @returns {HTMLElement} The item.
 */
function requirementItem(check) {
    const notMet = check.status === "NotSatisfied";
    const actions = [];
    if (notMet && check.gap != null) {
        actions.push({ icon: "scale", label: "Run trade-off", run: (element, note) => sendToAssistant(tradeOffMessage(check), element, note) });
    }
    actions.push({ icon: "wand", label: "Fix with Hypha", run: (element, note) => sendToAssistant(fixRequirementMessage(check), element, note) });

    return createItem({
        key: "requirement:" + check.id + ":" + (check.satisfiedBy ?? ""),
        kind: notMet ? "bad" : "na",
        id: requirementLabel(check),
        title: requirementTitle(check),
        tooltip: (check.text ?? check.name) + " (" + STATUS_TEXT[check.status] + ")",
        lines: [[shortName(check.satisfiedBy), check.explanation, gapText(check)].filter(Boolean).join(" · ")],
        warnings: check.warnings ?? [],
        actions,
        drawDetail: panel => drawRequirementDetail(check, panel)
    });
}

/**
 * Creates the item of a rule that finds issues: its name and number of elements, the names of the first ones, and its actions.
 * @param {object} rule The rule, as validate_model gives it.
 * @param {string} kind The kind of the item: "bad", "warn" or "info".
 * @returns {HTMLElement} The item.
 */
function ruleItem(rule, kind) {
    const issues = state.validation.pages.get(rule.name).issues;
    const named = issues.length <= 6 ? issues : issues.slice(0, 5);
    const more = rule.issueCount - named.length;

    return createItem({
        key: "rule:" + rule.name,
        kind,
        id: rule.name,
        title: "× " + rule.issueCount,
        tooltip: rule.description,
        lines: [named.map(issue => elementName(issue.element)).join(", ") + (more > 0 ? ` and ${more} more` : "")],
        warnings: [],
        actions: [
            { icon: "wand", label: "Fix with Hypha", run: (element, note) => sendToAssistant(ruleMessage(rule), element, note) },
            { icon: null, label: "Ignore rule here", run: () => ignoreRule(rule.name) }
        ],
        drawDetail: panel => drawRuleDetail(rule, panel)
    });
}

/**
 * Creates an item, as in the mock-up: a head with an icon, an identifier and a title, lines of detail, the button Reveal then
 * the other actions, a note for what an action has done, and the details hidden until Reveal.
 * @param {object} item The item: key, kind, icon of its kind, id, title, tooltip, lines, warnings, actions (icon, label, run)
 * and drawDetail, which draws its details in a panel.
 * @returns {HTMLElement} The item.
 */
function createItem(item) {
    const element = el("div", "it " + item.kind);
    element.dataset.key = item.key;

    const head = el("div", "h");
    head.title = item.tooltip;
    head.append(icon(item.kind, 14), el("span", "id", item.id), el("span", "t", item.title));
    element.append(head);
    for (const line of item.lines.filter(Boolean)) element.append(el("div", "det", line));
    for (const warning of item.warnings) element.append(el("div", "det mh-warn", warning));

    const panel = el("div", "mh-detail");
    panel.hidden = true;
    const note = el("div", "mh-note");
    note.hidden = true;
    const reveal = button("target", "Reveal", target => toggleDetail(item, panel, target));
    reveal.setAttribute("aria-expanded", "false");
    const actions = el("div", "acts");
    actions.append(reveal, ...item.actions.map(action => button(action.icon, action.label, target => action.run(target, note))));
    element.append(actions, note, panel);

    if (state.open.has(item.key)) showDetail(item, panel, reveal);
    return element;
}

/**
 * Shows or hides the details of an item; the view remembers the items whose details are shown until the next run.
 * @param {object} item The item.
 * @param {HTMLElement} panel The panel of the details.
 * @param {HTMLButtonElement} reveal The button Reveal.
 */
function toggleDetail(item, panel, reveal) {
    if (panel.hidden) {
        state.open.add(item.key);
        showDetail(item, panel, reveal);
    } else {
        state.open.delete(item.key);
        panel.hidden = true;
        reveal.setAttribute("aria-expanded", "false");
    }
}

/**
 * Shows the details of an item.
 * @param {object} item The item.
 * @param {HTMLElement} panel The panel of the details.
 * @param {HTMLButtonElement} reveal The button Reveal.
 */
function showDetail(item, panel, reveal) {
    panel.hidden = false;
    reveal.setAttribute("aria-expanded", "true");
    item.drawDetail(panel);
}

/**
 * Draws a list of labels and values in a panel, without the missing values.
 * @param {HTMLElement} panel The panel.
 * @param {Array<[string, string]>} rows The labels and their values.
 */
function drawRows(panel, rows) {
    const list = el("dl");
    for (const [label, value] of rows.filter(([, value]) => value != null && value !== "")) {
        list.append(el("dt", null, label), el("dd", null, value));
    }
    panel.replaceChildren(list);
}

/**
 * Draws the details of a requirement: its qualified name and type, read with get_element_details, its constraint, assumption,
 * satisfying part and value.
 * @param {object} check The check of the requirement.
 * @param {HTMLElement} panel The panel of the details.
 * @returns {Promise<void>} Resolved once the details are drawn.
 */
async function drawRequirementDetail(check, panel) {
    const rows = [
        ["Constraint", check.constraint ?? "none"],
        ["Assumption", check.assumption],
        ["Satisfied by", check.satisfiedBy ?? "no part"],
        ["Value", valueText(check)]
    ];
    const cached = state.details.get(check.id);
    if (cached) {
        drawRows(panel, [["Requirement", cached.qualifiedName], ["Type", cached.type], ...rows]);
        return;
    }

    drawRows(panel, [["Requirement", "Reading…"], ...rows]);
    try {
        const details = await callTool("get_element_details", { elementId: check.id });
        state.details.set(check.id, details);
        drawRows(panel, [["Requirement", details.qualifiedName], ["Type", details.type], ...rows]);
    } catch (error) {
        drawRows(panel, [["Requirement", "The details could not be read: " + error.message], ...rows]);
    }
}

/**
 * Draws the issues of a rule read so far, each with its element and message, and a button for the next page if any.
 * @param {object} rule The rule.
 * @param {HTMLElement} panel The panel of the details.
 */
function drawRuleDetail(rule, panel) {
    const page = state.validation.pages.get(rule.name);
    const list = el("div", "mh-issues");
    for (const issue of page.issues) {
        const row = el("div", "mh-issue");
        row.append(el("b", null, issue.element), el("span", null, issue.message));
        list.append(row);
    }
    panel.replaceChildren(list);

    if (page.nextOffset != null) {
        const more = button(null, "Show " + Math.min(PAGE_SIZE, rule.issueCount - page.issues.length) + " more", element => showMoreIssues(rule, panel, element));
        more.className = "btn sm link";
        panel.append(more);
    }
}

/**
 * Reads the next page of the issues of a rule, then draws them all.
 * @param {object} rule The rule.
 * @param {HTMLElement} panel The panel of the details.
 * @param {HTMLButtonElement} more The button that asked for them, disabled meanwhile.
 * @returns {Promise<void>} Resolved once the issues or the error are shown.
 */
async function showMoreIssues(rule, panel, more) {
    const page = state.validation.pages.get(rule.name);
    more.disabled = true;
    try {
        const next = await fetchIssues(rule.name, page.nextOffset);
        page.issues.push(...next.issues);
        page.nextOffset = next.nextOffset;
        drawRuleDetail(rule, panel);
    } catch (error) {
        more.disabled = false;
        showError("The issues could not be read: " + error.message);
    }
}

/**
 * Sets a rule aside: the view no longer runs it, until the user runs it again.
 * @param {string} name The name of the rule.
 */
function ignoreRule(name) {
    state.ignored.add(name);
    state.open.delete("rule:" + name);
    drawItems();
    drawFooter();
    void refresh(false);
}

/**
 * Draws the requirements met, folded under their number.
 */
function drawMetList() {
    const checks = state.checks.requirements;
    const repeated = repeatedRequirements(checks);
    const met = checks.filter(check => check.status === "Satisfied");

    $("met_list").hidden = met.length === 0;
    $("met_summary").textContent = plural(met.length, "requirement") + " met";
    $("met_rows").replaceChildren(...met.map(check => {
        const row = el("div");
        row.title = [check.constraint, check.explanation].filter(Boolean).join(" · ");
        const part = repeated.has(check.id) ? " · " + shortName(check.satisfiedBy) : "";
        row.append(el("b", null, requirementLabel(check)), el("span", null, requirementTitle(check) + part));
        return row;
    }));
}

/**
 * Draws the footer: the button that shows the issues of every rule, and the rules set aside with the button to run them again.
 */
function drawFooter() {
    $("show_all").hidden = shownRules().length === 0;

    const ignored = $("ignored");
    ignored.hidden = state.ignored.size === 0;
    ignored.replaceChildren();
    if (state.ignored.size === 0) return;

    const again = el("button", "btn sm link", "Run them again");
    again.type = "button";
    again.addEventListener("click", () => {
        state.ignored.clear();
        drawFooter();
        void refresh(false);
    });
    ignored.append("Not run here: " + [...state.ignored].join(", ") + ". ", again);
}

/**
 * Describes a requirement and its check for the assistant.
 * @param {object} check The check of the requirement.
 * @returns {string[]} The lines: the requirement with its identifier and text, the check, the constraint, the gap and the warnings.
 */
function describeRequirement(check) {
    const [label, ...others] = [check.reqId, check.name, "id " + check.id].filter(Boolean);
    const name = others.length > 0 ? `${label} (${others.join(", ")})` : label;
    const lines = [`Requirement ${name}` + (check.text ? `: "${check.text}"` : "")];
    const part = check.satisfiedBy ? " for " + check.satisfiedBy : "";
    lines.push(`Check of Fabric: ${STATUS_TEXT[check.status]}${part}. ${check.explanation}`);
    if (check.constraint) lines.push("Constraint: " + check.constraint + (check.assumption ? "; assumption: " + check.assumption : ""));
    const gap = gapText(check);
    if (gap) lines.push("Gap: " + gap + ".");
    for (const warning of check.warnings ?? []) lines.push("Warning: " + warning);
    return lines;
}

/**
 * Writes the message that asks the assistant to fix a requirement not met, or to make a requirement checkable.
 * @param {object} check The check of the requirement.
 * @returns {string} The message.
 */
function fixRequirementMessage(check) {
    const request = check.status === "NotSatisfied" ? "Fix this requirement, which is not met:" : "Make this requirement checkable:";
    return [request, ...describeRequirement(check), "Prepare the batch with apply_changes."].join("\n");
}

/**
 * Writes the message that asks the assistant for a trade-off that meets a requirement.
 * @param {object} check The check of the requirement.
 * @returns {string} The message.
 */
function tradeOffMessage(check) {
    return ["Run a trade-off to meet this requirement:", ...describeRequirement(check), "Use solve_trade_off with this requirement as a constraint."].join("\n");
}

/**
 * Writes the message that asks the assistant to fix the issues of a rule: the rule, then its first issues.
 * @param {object} rule The rule.
 * @returns {string} The message.
 */
function ruleMessage(rule) {
    const issues = state.validation.pages.get(rule.name).issues.slice(0, PAGE_SIZE);
    const lines = [`Fix the issues of the validation rule ${rule.name} (${rule.severity}, ${plural(rule.issueCount, "element")}): ${rule.description}`];
    lines.push(...issues.map(issue => `- ${issue.element} (id ${issue.elementId}): ${issue.message}`));
    const rest = rule.issueCount - issues.length;
    if (rest > 0) lines.push(`- and ${rest} more: call validate_model with rules ["${rule.name}"] to list them.`);
    lines.push("Prepare the batch with apply_changes.");
    return lines.join("\n");
}

/**
 * Sends a message to the conversation: put in the chat box when the host allows it, otherwise given to the assistant as
 * context; the button is disabled once sent, and the note tells the user what to do next.
 * @param {string} text The message.
 * @param {HTMLButtonElement} element The button of the action.
 * @param {HTMLElement} note Where to tell the user what to do next.
 * @returns {Promise<void>} Resolved once the host has answered.
 */
async function sendToAssistant(text, element, note) {
    const content = [{ type: "text", text }];
    element.disabled = true;
    note.hidden = false;
    try {
        const result = await request("ui/message", { role: "user", content });
        if (result?.isError) throw new Error("ui/message refused");
        note.textContent = "The message is in the chat box: check it and press Enter.";
    } catch {
        try {
            await request("ui/update-model-context", { content });
            note.textContent = "Sent to the assistant: type \"go\" in the chat.";
        } catch {
            note.textContent = "The host refused the message.";
            element.disabled = false;
            return;
        }
    }
    element.querySelector("span").textContent = "Sent";
}

// The events of the view: check again, show the issues of every rule, the size of the page and the time since the checks.
$("check_again").addEventListener("click", () => refresh(true));
$("show_all").addEventListener("click", () => {
    for (const rule of shownRules()) state.open.add("rule:" + rule.name);
    drawItems();
});
new ResizeObserver(sendSize).observe(document.body);
window.setInterval(drawSubtitle, 30000);
void start();
