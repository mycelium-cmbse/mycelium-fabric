// ------------------------------------------------------------------------------------------------
//  <copyright file="model-health.test.js" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

// Tests of the view "Model health" (McpApp/model-health.js), run by Node.js:
// node --test Mycelium.Fabric.Mcp.Tests/McpApp/model-health.test.js

const { describe, it, beforeEach, afterEach } = require("node:test");
const assert = require("node:assert/strict");
const path = require("node:path");

const { loadPage } = require("./fake-page.js");

const APP_FOLDER = path.join(__dirname, "..", "..", "Mycelium.Fabric.Mcp", "McpApp");
const HOST_ORIGIN = "https://host.example";

// The tools that the view may call: it reads the model, and never changes it.
const READ_TOOLS = new Set(["check_requirements", "validate_model", "get_element_details"]);

const NO_CONSTRAINT = "The requirement has no required constraint: add one with apply_changes (constraint of the RequirementUsage).";
const NO_PART = "No part satisfies the requirement: add one with apply_changes (SatisfyRequirementUsage).";

/**
 * Builds the rules of validate_model, with the number of issues that each one finds among the given issues.
 * @param {object[]} issues The issues.
 * @returns {object[]} The nine rules, as validate_model gives them.
 */
function rules(issues) {
    return [
        ["broken-reference", "Error", "A relationship misses an element that the metamodel requires, for example the type of a part whose definition has been removed."],
        ["duplicate-qualified-name", "Error", "Several elements have the same qualified name, so resolving this name is ambiguous."],
        ["orphan-element", "Error", "An element has no owner and is not a root namespace, so it is outside the containment tree of the model."],
        ["part-outside-system", "Warning", "A part with parts or values is next to a part that satisfies requirements, in the same package, instead of inside the part that holds the system, so the budgets and checks of one do not count the other."],
        ["requirement-without-constraint", "Warning", "A requirement has no required constraint, so it is only text and cannot be verified."],
        ["requirement-without-satisfy", "Warning", "No satisfy link refers to a requirement, so no part is known to satisfy it and it cannot be checked on the design."],
        ["untyped-usage", "Warning", "A part, item or port has no type and no feature, so nothing tells what it is."],
        ["requirement-without-id", "Information", "A requirement has no identifier (reqId), so it cannot be traced to the document it comes from."],
        ["unused-definition", "Information", "No feature is typed by a definition and nothing specializes it, so it takes no part in the model."]
    ].map(([name, severity, description]) => ({ name, severity, description, issueCount: issues.filter(issue => issue.rule === name).length }));
}

/**
 * Builds an issue of validate_model.
 * @param {string} rule The name of the rule.
 * @param {string} severity The severity of the rule.
 * @param {string} elementId The identifier of the element.
 * @param {string} element The element, as validate_model writes it.
 * @param {string} message The message.
 * @returns {object} The issue.
 */
function issue(rule, severity, elementId, element, message) {
    return { rule, severity, elementId, element, message };
}

/**
 * Builds EOSat-1 as Fabric loads it from Satellite.json: six requirements with a text, none checkable yet, and twelve
 * warnings in two rules (real results of check_requirements and validate_model).
 * @returns {object} The model: its requirements, issues, rules, number of elements and details of its elements.
 */
function eosat() {
    const requirements = [
        ["6b93c533-0395-7e18-1bf6-4475deb47ba5", "REQ-SYS-001", "massBudget", "The satellite launch mass, computed as the total dry mass of all units plus a 20% system margin, shall not exceed 150 kg."],
        ["d17c3498-3404-4a62-9728-c58e59a4954f", "REQ-SYS-002", "payloadPower", "The payload (optical camera and mass memory) shall consume less than 70 W in imaging mode."],
        ["24253a34-a8ba-fc0a-0dba-e5e83d42bd5f", "REQ-SYS-003", "attitudeKnowledge", "The AOCS shall provide an attitude knowledge accuracy better than 10 arcsec, using at least two star trackers for redundancy."],
        ["a2c360e5-0019-9ec5-031e-14eadfab77b8", "REQ-SYS-004", "wheelRedundancy", "The AOCS shall include at least four reaction wheels so that the mission survives the failure of one wheel."],
        ["1e9fa5b1-208e-12b8-440b-e106061d3eb3", "REQ-SYS-005", "downlinkRate", "The X-band downlink shall provide a data rate of at least 150 Mbps."],
        ["4c3c1285-20ba-ed54-45b3-f54699899da3", "REQ-SYS-006", "eclipseEnergy", "The battery shall provide at least 300 Wh to power the satellite during the longest eclipse."]
    ].map(([id, reqId, name, text]) => ({ id, reqId, name, text, status: "NotVerifiable", explanation: NO_CONSTRAINT + " " + NO_PART }));

    const byName = [...requirements].sort((left, right) => left.name.localeCompare(right.name));
    const issues = [
        ...byName.map(requirement => issue("requirement-without-constraint", "Warning", requirement.id, "EOSat1::Requirements::" + requirement.name,
            "The requirement has no required constraint, for example subj.mass <= 150 [kg], so it cannot be verified.")),
        ...byName.map(requirement => issue("requirement-without-satisfy", "Warning", requirement.id, "EOSat1::Requirements::" + requirement.name,
            "No satisfy link refers to the requirement, for example satisfy massBudget by eosat1, so no part is known to satisfy it."))
    ];

    return {
        requirements,
        issues,
        rules: rules(issues),
        elementCount: 548,
        details: {
            "6b93c533-0395-7e18-1bf6-4475deb47ba5": { id: "6b93c533-0395-7e18-1bf6-4475deb47ba5", name: "massBudget", shortName: "REQ-SYS-001", type: "RequirementUsage", qualifiedName: "EOSat1::Requirements::massBudget", childCount: 1 }
        }
    };
}

/**
 * Builds EOSat-1 once the batch of REQ-SYS-001 is applied: a constraint with a margin of 20 % and eosat1 as satisfying part
 * (real result of check_requirements).
 * @returns {object} The model.
 */
function eosatAfterBatch() {
    const model = eosat();
    Object.assign(model.requirements[0], {
        status: "NotSatisfied", constraint: "subj.mass * 1.2 <= 150 [kg]", satisfiedBy: "EOSat1::Architecture::eosat1", value: 125.8, gap: -0.96,
        explanation: "150.96 [kg] > 150 [kg]"
    });
    model.issues = model.issues.filter(item => item.elementId !== model.requirements[0].id);
    model.rules = rules(model.issues);
    model.elementCount = 605;
    return model;
}

/**
 * Builds a model like KESTREL-1 (values of its real checks): requirements met, met on two parts, not met with and without a
 * gap, not checked, and issues of every severity, one rule with more issues than a page.
 * @returns {object} The model.
 */
function kestrel() {
    const architecture = "Kestrel::Architecture::kestrelMission::kestrel1";
    const requirements = [
        { id: "392f6b8e", reqId: "REQ-AOCS-001", name: "starTrackerAccuracy", text: "Each star tracker shall provide an attitude knowledge accuracy better than 5 arcsec.", status: "Satisfied", constraint: "subj.accuracy < 5 [arcsec]", satisfiedBy: architecture + "::aocsSubsystem::starTracker1", value: 3, unit: "arcsec", gap: 2, explanation: "3 [arcsec] < 5 [arcsec]" },
        { id: "392f6b8e", reqId: "REQ-AOCS-001", name: "starTrackerAccuracy", text: "Each star tracker shall provide an attitude knowledge accuracy better than 5 arcsec.", status: "Satisfied", constraint: "subj.accuracy < 5 [arcsec]", satisfiedBy: architecture + "::aocsSubsystem::starTracker2", value: 3, unit: "arcsec", gap: 2, explanation: "3 [arcsec] < 5 [arcsec]" },
        { id: "f5f195ac", reqId: "REQ-AOCS-002", name: "wheelRedundancy", text: "The AOCS shall include four reaction wheels so that the mission survives the failure of one wheel.", status: "NotVerifiable", satisfiedBy: architecture + "::aocsSubsystem", explanation: NO_CONSTRAINT },
        { id: "f587d208", reqId: "REQ-COM-001", name: "downlinkRate", text: "The X-band downlink shall provide a data rate of at least 400 Mbps.", status: "NotSatisfied", constraint: "subj.dataRate >= 400", satisfiedBy: architecture + "::communicationSubsystem::xBandTransmitter", value: 300, gap: -100, explanation: "300 < 400" },
        { id: "748d20ec", reqId: "REQ-PRP-001", name: "nonToxicPropellant", text: "The propulsion shall not use hydrazine (REACH regulation).", status: "NotSatisfied", constraint: "subj.propellant != \"hydrazine\"", satisfiedBy: architecture + "::propulsionSubsystem::propellantTank", explanation: "\"hydrazine\" == \"hydrazine\"" },
        {
            id: "bdb93bec", reqId: "REQ-SYS-001", name: "massBudget", text: "The spacecraft dry mass, computed as the total mass of all units plus a 20% system margin, shall not exceed 230 kg.", status: "NotSatisfied",
            constraint: "subj.mass * 1.2 <= 230", assumption: "subj.maxBetaAngle <= 40 [deg]", satisfiedBy: architecture, value: 191.9, valueWithoutMargins: 180.5, unit: "kg", gap: -0.28, explanation: "230.28 > 230",
            warnings: ["'solarArray' is next to 'kestrel1' in the same package, not inside it, and carries mass (4 [kg]), which this check does not count."]
        },
        { id: "d935fcad", reqId: "REQ-SYS-004", name: "designLife", text: "The spacecraft shall be designed for a lifetime in orbit of at least 5 years.", status: "Satisfied", constraint: "subj.designLife >= 5 [yr]", satisfiedBy: architecture, value: 7, unit: "yr", gap: 2, explanation: "7 [yr] >= 5 [yr]" },
        { id: "5e0c7d1a", name: "harnessMass", status: "NotEvaluated", constraint: "subj.harness.mass <= 12", satisfiedBy: architecture, explanation: "No part under 'Kestrel::Architecture::kestrelMission::kestrel1' has a numeric value for 'mass'." },
        { id: "a71c02f3", name: "equipmentCount", text: "At most one equipment of the payload shall weigh more than 10 kg.", status: "NotSatisfied", constraint: "(subj.camera, subj.radio)->select {in p; p.mass > 10}->size() <= 1", satisfiedBy: architecture + "::payloadSubsystem", value: 2, gap: -1, explanation: "2 > 1" },
        { id: "c43e9b10", reqId: "REQ-SYS-009", name: "busVoltage", status: "NotSatisfied", constraint: "subj.voltage == 28", satisfiedBy: architecture + "::powerSubsystem", value: 27.5, unit: "V", gap: -0.5, explanation: "27.5 [V] != 28" }
    ];

    const issues = [
        issue("broken-reference", "Error", "0b1e7f22", "SatisfyRequirementUsage in Kestrel::Requirements::SystemRequirements", "The SatisfyRequirementUsage has no satisfiedRequirement, which the metamodel requires."),
        issue("requirement-without-constraint", "Warning", "f5f195ac", "Kestrel::Requirements::SubsystemRequirements::wheelRedundancy", "The requirement has no required constraint, for example subj.mass <= 150 [kg], so it cannot be verified."),
        ...Array.from({ length: 25 }, (_, index) => issue("unused-definition", "Information", "u" + index, "Kestrel::Catalog::Power::PCDU_" + (100 + index),
            "No feature is typed by the PartDefinition and nothing specializes it: use it, or remove it if it is left over."))
    ];

    return {
        requirements,
        issues,
        rules: rules(issues),
        elementCount: 4532,
        details: {
            bdb93bec: { id: "bdb93bec", name: "massBudget", type: "RequirementUsage", qualifiedName: "Kestrel::Requirements::SystemRequirements::massBudget" }
        }
    };
}

/**
 * Builds a model whose requirements are all met and in which no rule finds an issue.
 * @param {number} count The number of requirements.
 * @returns {object} The model.
 */
function healthy(count) {
    const requirements = Array.from({ length: count }, (_, index) => ({
        id: "r" + index, reqId: "REQ-" + String(index + 1).padStart(3, "0"), name: "requirement" + index, status: "Satisfied", constraint: "subj.mass <= 10", satisfiedBy: "Model::system", value: 5, gap: 5, explanation: "5 <= 10"
    }));
    return { requirements, issues: [], rules: rules([]), elementCount: 42, details: {} };
}

/**
 * Gives one page of the checks of a model, as check_requirements does: nextOffset is left out on the last page.
 * @param {object} model The model.
 * @param {{offset: number, limit: number}} args The arguments of the call.
 * @returns {object} The result of check_requirements.
 */
function checkPage(model, { offset = 0, limit = 20 } = {}) {
    const count = status => model.requirements.filter(check => check.status === status).length;
    const page = model.requirements.slice(offset, offset + limit);
    const end = offset + page.length;
    return {
        satisfiedCount: count("Satisfied"), notSatisfiedCount: count("NotSatisfied"), notVerifiableCount: count("NotVerifiable"), notEvaluatedCount: count("NotEvaluated"),
        totalFound: model.requirements.length, requirements: page, ...(end < model.requirements.length ? { nextOffset: end } : {})
    };
}

/**
 * Gives the report of validate_model on a model, with the chosen rules only, and one page of their issues.
 * @param {object} model The model.
 * @param {{rules: string[], offset: number, limit: number}} args The arguments of the call.
 * @returns {object} The result of validate_model.
 */
function validationPage(model, { rules: names, offset = 0, limit = 20 }) {
    const checked = names ? model.rules.filter(rule => names.includes(rule.name)) : model.rules;
    const issues = model.issues.filter(item => checked.some(rule => rule.name === item.rule));
    const count = severity => checked.filter(rule => rule.severity === severity).reduce((sum, rule) => sum + rule.issueCount, 0);
    const page = issues.slice(offset, offset + limit);
    const end = offset + page.length;
    return {
        elementCount: model.elementCount, errorCount: count("Error"), warningCount: count("Warning"), informationCount: count("Information"), rules: checked,
        totalFound: issues.length, issues: page, ...(end < issues.length ? { nextOffset: end } : {})
    };
}

/**
 * Wraps the content of a tool in its result, as the C# SDK sends it: the JSON in a text block.
 * @param {object} content The content.
 * @returns {object} The result of the tool.
 */
function textResult(content) {
    return { content: [{ type: "text", text: JSON.stringify(content) }] };
}

/**
 * Builds the tools of Fabric on a model.
 * @param {object} model The model.
 * @returns {function(string, object): object} The tools: from the name and the arguments of a call, its result.
 */
function fabric(model) {
    return (name, args) => {
        if (name === "check_requirements") return textResult(checkPage(model, args));
        if (name === "validate_model") return textResult(validationPage(model, args));
        if (name === "get_element_details") return textResult(model.details[args.elementId]);
        throw new Error("The view called " + name);
    };
}

/**
 * Lets the pending promises of the view go on.
 * @returns {Promise<void>} Resolved once they have run.
 */
function flush() {
    return new Promise(resolve => setImmediate(resolve));
}

/**
 * Gets an element of the page.
 * @param {object} page The page.
 * @param {string} id The id of the element.
 * @returns {object} The element.
 */
function byId(page, id) {
    return page.document.getElementById(id);
}

/**
 * Gets the messages that the view has sent to the host.
 * @param {object} page The page.
 * @returns {object[]} The messages.
 */
function messages(page) {
    return page.sent.map(entry => entry.message);
}

/**
 * Gets the requests that the view has sent to the host.
 * @param {object} page The page.
 * @param {string} method The method of the requests.
 * @returns {object[]} The requests.
 */
function requests(page, method) {
    return messages(page).filter(message => message.method === method && message.id !== undefined);
}

/**
 * Gets the calls of tools that the view has made: the name and the arguments of each.
 * @param {object} page The page.
 * @returns {object[]} The calls.
 */
function toolCalls(page) {
    return requests(page, "tools/call").map(message => message.params);
}

/**
 * Answers a request of the view, then lets the view go on.
 * @param {object} page The page.
 * @param {object} message The request.
 * @param {object} result The result, or nothing when the host answers with an error.
 * @param {object} [error] The JSON-RPC error.
 */
async function answer(page, message, result, error) {
    page.receive(error ? { jsonrpc: "2.0", id: message.id, error } : { jsonrpc: "2.0", id: message.id, result });
    await flush();
}

/**
 * Answers the calls of tools that wait for an answer, and the calls they lead to, as the host does with the server; returns
 * once the view has nothing more to ask.
 * @param {object} page The page.
 * @param {function(string, object): object} server The tools of the server.
 * @param {function(object): boolean} [chosen] Which calls to answer, all of them when not given.
 */
async function serve(page, server, chosen = () => true) {
    page.answered ??= new Set();
    for (;;) {
        await flush();
        const call = requests(page, "tools/call").find(message => !page.answered.has(message.id) && chosen(message));
        if (!call) return;
        page.answered.add(call.id);
        await answer(page, call, server(call.params.name, call.params.arguments));
    }
}

/**
 * Reads the problems of the view, by section.
 * @param {object} page The page.
 * @returns {string[]} The title of each section, then each of its items: kind, identifier and title, lines and buttons.
 */
function problems(page) {
    return byId(page, "items").querySelectorAll(".sec, .it").map(element => {
        if (element.classList.contains("sec")) return "## " + element.textContent;
        const head = element.querySelector(".id").textContent + " " + element.querySelector(".t").textContent;
        const lines = element.querySelectorAll(".det").map(line => line.textContent).join(" / ");
        const buttons = element.querySelector(".acts").querySelectorAll("button").map(button => button.textContent).join(", ");
        return [element.className, head, lines, buttons].join(" | ");
    });
}

/**
 * Gets the item of a requirement or of a rule.
 * @param {object} page The page.
 * @param {string} key The key of the item, for example rule:unused-definition.
 * @returns {object} The item, or undefined.
 */
function item(page, key) {
    return byId(page, "items").querySelectorAll(".it").find(element => element.dataset.key === key);
}

/**
 * Gets a button of an element from its label.
 * @param {object} element The element.
 * @param {string} label The label of the button.
 * @returns {object} The button, or undefined.
 */
function buttonOf(element, label) {
    return element.querySelectorAll("button").find(button => button.textContent === label);
}

/**
 * Reads the details of an item, shown by Reveal.
 * @param {object} element The item.
 * @returns {string[]} Each label with its value, or each issue with its message.
 */
function details(element) {
    const panel = element.querySelector(".mh-detail");
    const labels = panel.querySelectorAll("dt").map(term => term.textContent);
    const values = panel.querySelectorAll("dd").map(value => value.textContent);
    return labels.length > 0 ? labels.map((label, index) => label + ": " + values[index]) : panel.querySelectorAll(".mh-issue").map(row => row.textContent);
}

/**
 * Reads the counters.
 * @param {object} page The page.
 * @returns {object} The values, the texts with their classes, and the bars.
 */
function counters(page) {
    return {
        met: byId(page, "met").textContent,
        metDetail: byId(page, "met_detail").textContent + " (" + byId(page, "met_detail").className + ")",
        metBar: byId(page, "met_track").className + " " + byId(page, "met_bar").style.width,
        errors: byId(page, "errors").textContent,
        validationDetail: byId(page, "validation_detail").textContent + " (" + byId(page, "validation_detail").className + ")",
        validationBar: byId(page, "validation_bar").className + " " + byId(page, "validation_bar").style.width
    };
}

describe("model health view", () => {
    let page;
    const realNow = Date.now;

    /**
     * Initializes the view, gives it the result of check_requirements called by the assistant, then answers its calls.
     * @param {object} model The model.
     */
    async function open(model) {
        await answer(page, messages(page)[0], { hostContext: { theme: "light" } });
        page.receive({ jsonrpc: "2.0", method: "ui/notifications/tool-result", params: textResult(checkPage(model)) });
        await serve(page, fabric(model));
    }

    beforeEach(() => {
        page = loadPage(path.join(APP_FOLDER, "model-health.html"), path.join(APP_FOLDER, "model-health.js"));
    });

    afterEach(() => {
        Date.now = realNow;
        assert.deepEqual(toolCalls(page).filter(call => !READ_TOOLS.has(call.name)), []);
    });

    describe("talking with the host", () => {
        it("initializes with the parent frame, whatever its origin", () => {
            assert.deepEqual(page.sent, [{
                message: {
                    jsonrpc: "2.0",
                    id: 1,
                    method: "ui/initialize",
                    params: { protocolVersion: "2026-01-26", appInfo: { name: "model-health", version: "0.1.0" }, appCapabilities: {} }
                },
                targetOrigin: "*"
            }]);
        });

        it("applies the theme of the host, then sends the next messages to its origin only", async () => {
            await answer(page, messages(page)[0], { hostContext: { theme: "dark", styles: { variables: { "--color-background-primary": "#262624", "--font-sans": "" } } } });

            const style = page.document.documentElement.style;
            assert.equal(style["--color-background-primary"], "#262624");
            assert.equal(style["--font-sans"], undefined);
            assert.equal(style.colorScheme, "dark");
            assert.deepEqual(page.sent.at(-1), { message: { jsonrpc: "2.0", method: "ui/notifications/initialized", params: {} }, targetOrigin: HOST_ORIGIN });
        });

        it("follows the changes of theme, and answers the requests of the host", async () => {
            await answer(page, messages(page)[0], {});
            page.receive({ jsonrpc: "2.0", method: "ui/notifications/host-context-changed", params: { theme: "light", styles: { variables: { "--color-text-primary": "#1f2937" } } } });
            page.receive({ jsonrpc: "2.0", id: 9, method: "ping" });
            page.receive({ jsonrpc: "2.0", method: "ui/notifications/host-context-changed" });

            const style = page.document.documentElement.style;
            assert.equal(style.colorScheme, "light");
            assert.equal(style["--color-text-primary"], "#1f2937");
            assert.deepEqual(page.sent.at(-1), { message: { jsonrpc: "2.0", id: 9, result: {} }, targetOrigin: HOST_ORIGIN });
        });

        it("ignores the messages of another frame, of another origin, that are not JSON-RPC, or that answer nothing", async () => {
            await answer(page, messages(page)[0], {});
            const count = page.sent.length;

            page.receive({ jsonrpc: "2.0", id: 7, method: "ping" }, HOST_ORIGIN, {});
            page.receive({ jsonrpc: "2.0", id: 7, method: "ping" }, "https://other.example");
            page.receive({ id: 7, method: "ping" });
            page.receive(null);
            page.receive({ jsonrpc: "2.0", id: 42, result: {} });

            assert.equal(page.sent.length, count);
            assert.equal(byId(page, "subtitle").textContent, "Waiting for the checks…");
        });

        it("keeps sending to any origin when the frame of the host has none", () => {
            page.receive({ jsonrpc: "2.0", id: 9, method: "ping" }, "null");

            assert.equal(page.sent.at(-1).targetOrigin, "*");
        });

        it("tells the host the size of the view", () => {
            page.document.documentElement.style.height = "auto";

            page.resize();

            assert.deepEqual(page.sent.at(-1).message, { jsonrpc: "2.0", method: "ui/notifications/size-changed", params: { width: 600, height: 413 } });
            assert.equal(page.document.documentElement.style.height, "auto");
        });

        it("tells that the host did not start the view", async () => {
            await answer(page, messages(page)[0], undefined, { code: -32603, message: "Unknown method" });

            assert.equal(byId(page, "error").hidden, false);
            assert.equal(byId(page, "error").textContent, "The host did not start the view: Unknown method");
        });
    });

    describe("showing the health of the model", () => {
        it("runs the checks and the validation of the whole model itself once the assistant has checked the requirements", async () => {
            await answer(page, messages(page)[0], {});
            assert.equal(byId(page, "kpis").hidden, true);

            page.receive({ jsonrpc: "2.0", method: "ui/notifications/tool-result", params: textResult(checkPage(eosat(), { limit: 2 })) });

            assert.equal(byId(page, "subtitle").textContent, "Checking the requirements and the rules…");
            assert.equal(byId(page, "check_again").disabled, true);
            assert.equal(byId(page, "view").getAttribute("aria-busy"), "true");
            assert.deepEqual(toolCalls(page), [
                { name: "check_requirements", arguments: { offset: 0, limit: 20 } },
                { name: "validate_model", arguments: { limit: 1 } }
            ]);
        });

        it("shows the numbers of check_requirements and validate_model on EOSat-1", async () => {
            await open(eosat());

            assert.equal(byId(page, "title").textContent, "Model health · EOSat1");
            assert.equal(byId(page, "subtitle").textContent, "548 elements · checked just now");
            assert.equal(byId(page, "kpis").hidden, false);
            assert.deepEqual(counters(page), {
                met: "0 / 6",
                metDetail: "6 not checked (s warn)",
                metBar: "bar warn 0%",
                errors: "0 errors",
                validationDetail: "12 warnings · 9 rules (s warn)",
                validationBar: "warn 100%"
            });
            assert.equal(byId(page, "check_again").disabled, false);
            assert.equal(byId(page, "view").getAttribute("aria-busy"), "false");
            assert.equal(byId(page, "met_list").hidden, true);
            assert.equal(byId(page, "show_all").hidden, false);
            assert.equal(byId(page, "error").hidden, true);
        });

        it("lists the requirements not checked with their reason, then each rule with issues and the names of its elements", async () => {
            await open(eosat());

            const reason = NO_CONSTRAINT + " " + NO_PART;
            const names = "attitudeKnowledge, downlinkRate, eclipseEnergy, massBudget, payloadPower, wheelRedundancy";
            assert.deepEqual(problems(page), [
                "## Not checked",
                `it na | REQ-SYS-001 The satellite launch mass, computed as the total dry mass of all units plus a 20% system margin, shall not exceed 150 kg. | ${reason} | Reveal, Fix with Hypha`,
                `it na | REQ-SYS-002 The payload (optical camera and mass memory) shall consume less than 70 W in imaging mode. | ${reason} | Reveal, Fix with Hypha`,
                `it na | REQ-SYS-003 The AOCS shall provide an attitude knowledge accuracy better than 10 arcsec, using at least two star trackers for redundancy. | ${reason} | Reveal, Fix with Hypha`,
                `it na | REQ-SYS-004 The AOCS shall include at least four reaction wheels so that the mission survives the failure of one wheel. | ${reason} | Reveal, Fix with Hypha`,
                `it na | REQ-SYS-005 The X-band downlink shall provide a data rate of at least 150 Mbps. | ${reason} | Reveal, Fix with Hypha`,
                `it na | REQ-SYS-006 The battery shall provide at least 300 Wh to power the satellite during the longest eclipse. | ${reason} | Reveal, Fix with Hypha`,
                "## Warnings",
                `it warn | requirement-without-constraint × 6 | ${names} | Reveal, Fix with Hypha, Ignore rule here`,
                `it warn | requirement-without-satisfy × 6 | ${names} | Reveal, Fix with Hypha, Ignore rule here`
            ]);
            assert.equal(item(page, "rule:requirement-without-satisfy").querySelector(".h").title,
                "No satisfy link refers to a requirement, so no part is known to satisfy it and it cannot be checked on the design.");
            assert.equal(item(page, "requirement:6b93c533-0395-7e18-1bf6-4475deb47ba5:").querySelector(".h").title,
                "The satellite launch mass, computed as the total dry mass of all units plus a 20% system margin, shall not exceed 150 kg. (not verifiable)");
        });

        it("reads the issues of each rule that finds some, and nothing more", async () => {
            await open(eosat());

            assert.deepEqual(toolCalls(page).slice(2), [
                { name: "validate_model", arguments: { rules: ["requirement-without-constraint"], offset: 0, limit: 20 } },
                { name: "validate_model", arguments: { rules: ["requirement-without-satisfy"], offset: 0, limit: 20 } }
            ]);
        });

        it("shows the requirements not met with their part, explanation, gap and warnings, then those not checked, the errors and the information", async () => {
            await open(kestrel());

            assert.deepEqual(problems(page), [
                "## Not met",
                "it bad | REQ-COM-001 The X-band downlink shall provide a data rate of at least 400 Mbps. | xBandTransmitter · 300 < 400 · short by 100 | Reveal, Run trade-off, Fix with Hypha",
                "it bad | REQ-PRP-001 The propulsion shall not use hydrazine (REACH regulation). | propellantTank · \"hydrazine\" == \"hydrazine\" | Reveal, Fix with Hypha",
                "it bad | REQ-SYS-001 The spacecraft dry mass, computed as the total mass of all units plus a 20% system margin, shall not exceed 230 kg. | kestrel1 · 230.28 > 230 · over by 0.28 kg"
                + " / 'solarArray' is next to 'kestrel1' in the same package, not inside it, and carries mass (4 [kg]), which this check does not count. | Reveal, Run trade-off, Fix with Hypha",
                "it bad | equipmentCount At most one equipment of the payload shall weigh more than 10 kg. | payloadSubsystem · 2 > 1 · over by 1 | Reveal, Run trade-off, Fix with Hypha",
                "it bad | REQ-SYS-009 busVoltage | powerSubsystem · 27.5 [V] != 28 · off by 0.5 V | Reveal, Run trade-off, Fix with Hypha",
                "## Not checked",
                "it na | REQ-AOCS-002 The AOCS shall include four reaction wheels so that the mission survives the failure of one wheel. | aocsSubsystem · " + NO_CONSTRAINT + " | Reveal, Fix with Hypha",
                "it na | harnessMass  | kestrel1 · No part under 'Kestrel::Architecture::kestrelMission::kestrel1' has a numeric value for 'mass'. | Reveal, Fix with Hypha",
                "## Errors",
                "it bad | broken-reference × 1 | SatisfyRequirementUsage in SystemRequirements | Reveal, Fix with Hypha, Ignore rule here",
                "## Warnings",
                "it warn | requirement-without-constraint × 1 | wheelRedundancy | Reveal, Fix with Hypha, Ignore rule here",
                "## Information",
                "it info | unused-definition × 25 | PCDU_100, PCDU_101, PCDU_102, PCDU_103, PCDU_104 and 20 more | Reveal, Fix with Hypha, Ignore rule here"
            ]);
            assert.equal(item(page, "requirement:bdb93bec:Kestrel::Architecture::kestrelMission::kestrel1").querySelector(".det.mh-warn").textContent,
                "'solarArray' is next to 'kestrel1' in the same package, not inside it, and carries mass (4 [kg]), which this check does not count.");
        });

        it("folds the requirements met, naming the part of a requirement checked on several parts", async () => {
            await open(kestrel());

            assert.equal(byId(page, "met_list").hidden, false);
            assert.equal(byId(page, "met_summary").textContent, "3 requirements met");
            const rows = byId(page, "met_rows").children;
            assert.deepEqual(rows.map(row => row.textContent), [
                "REQ-AOCS-001Each star tracker shall provide an attitude knowledge accuracy better than 5 arcsec. · starTracker1",
                "REQ-AOCS-001Each star tracker shall provide an attitude knowledge accuracy better than 5 arcsec. · starTracker2",
                "REQ-SYS-004The spacecraft shall be designed for a lifetime in orbit of at least 5 years."
            ]);
            assert.equal(rows[2].title, "subj.designLife >= 5 [yr] · 7 [yr] >= 5 [yr]");
        });

        it("counts the requirements not met and the errors in red", async () => {
            await open(kestrel());

            assert.equal(byId(page, "title").textContent, "Model health · Kestrel");
            assert.deepEqual(counters(page), {
                met: "3 / 10",
                metDetail: "5 not met · 2 not checked (s bad)",
                metBar: "bar 30%",
                errors: "1 error",
                validationDetail: "1 warning · 9 rules (s warn)",
                validationBar: "bad 100%"
            });
        });

        it("tells that there is nothing to fix when every requirement is met and no rule finds an issue", async () => {
            await open(healthy(3));

            assert.deepEqual(counters(page), {
                met: "3 / 3",
                metDetail: "All met (s ok)",
                metBar: "bar 100%",
                errors: "0 errors",
                validationDetail: "0 warnings · 9 rules (s)",
                validationBar: "ok 100%"
            });
            assert.equal(byId(page, "items").textContent, "Nothing to fix: no requirement is left unmet or unchecked, and no rule finds an issue.");
            assert.equal(byId(page, "show_all").hidden, true);
            assert.equal(byId(page, "title").textContent, "Model health · Model");
        });

        it("tells when the model has no requirement, and names no model without qualified name", async () => {
            await open(healthy(0));

            assert.equal(byId(page, "met").textContent, "0 / 0");
            assert.equal(byId(page, "met_detail").textContent, "No requirement in the model");
            assert.equal(byId(page, "met_detail").className, "s");
            assert.equal(byId(page, "met_bar").style.width, "0%");
            assert.equal(byId(page, "title").textContent, "Model health");
        });

        it("reads every page of the checks", async () => {
            await open(healthy(25));

            assert.deepEqual(toolCalls(page).filter(call => call.name === "check_requirements").map(call => call.arguments), [{ offset: 0, limit: 20 }, { offset: 20, limit: 20 }]);
            assert.equal(byId(page, "met").textContent, "25 / 25");
            assert.equal(byId(page, "met_rows").childElementCount, 25);
        });

        it("tells how long ago the checks ran", async () => {
            page.tick();
            assert.equal(byId(page, "subtitle").textContent, "Waiting for the checks…");

            const start = realNow();
            Date.now = () => start;
            await open(eosat());

            Date.now = () => start + 5 * 60000;
            page.tick();
            assert.equal(byId(page, "subtitle").textContent, "548 elements · checked 5 min ago");

            Date.now = () => start + 2 * 3600000;
            page.tick();
            assert.match(byId(page, "subtitle").textContent, /^548 elements · checked at \d{2}[:.]\d{2}/);
        });
    });

    describe("revealing an element", () => {
        it("shows the details of a requirement, read with get_element_details once, and hides them again", async () => {
            await open(kestrel());
            const massBudget = item(page, "requirement:bdb93bec:Kestrel::Architecture::kestrelMission::kestrel1");
            const reveal = buttonOf(massBudget, "Reveal");

            reveal.dispatch("click");
            assert.equal(massBudget.querySelector(".mh-detail").hidden, false);
            assert.equal(reveal.getAttribute("aria-expanded"), "true");
            assert.deepEqual(details(massBudget)[0], "Requirement: Reading…");
            await serve(page, fabric(kestrel()));

            assert.deepEqual(details(massBudget), [
                "Requirement: Kestrel::Requirements::SystemRequirements::massBudget",
                "Type: RequirementUsage",
                "Constraint: subj.mass * 1.2 <= 230",
                "Assumption: subj.maxBetaAngle <= 40 [deg]",
                "Satisfied by: Kestrel::Architecture::kestrelMission::kestrel1",
                "Value: 191.9 kg (180.5 kg without margins)"
            ]);
            assert.deepEqual(toolCalls(page).at(-1), { name: "get_element_details", arguments: { elementId: "bdb93bec" } });

            reveal.dispatch("click");
            assert.equal(massBudget.querySelector(".mh-detail").hidden, true);
            assert.equal(reveal.getAttribute("aria-expanded"), "false");

            const count = toolCalls(page).length;
            reveal.dispatch("click");
            assert.equal(toolCalls(page).length, count);
            assert.equal(details(massBudget)[0], "Requirement: Kestrel::Requirements::SystemRequirements::massBudget");

            const downlink = item(page, "requirement:f587d208:Kestrel::Architecture::kestrelMission::kestrel1::communicationSubsystem::xBandTransmitter");
            buttonOf(downlink, "Reveal").dispatch("click");
            await answer(page, requests(page, "tools/call").at(-1), textResult({ id: "f587d208", type: "RequirementUsage", qualifiedName: "Kestrel::Requirements::downlinkRate" }));
            assert.equal(details(downlink).at(-1), "Value: 300");
        });

        it("shows a requirement without constraint nor part, and tells when its details cannot be read", async () => {
            await open(eosat());
            const downlink = item(page, "requirement:1e9fa5b1-208e-12b8-440b-e106061d3eb3:");

            buttonOf(downlink, "Reveal").dispatch("click");
            await answer(page, requests(page, "tools/call").at(-1), { isError: true, content: [{ type: "text", text: "No element has the identifier 1e9fa5b1." }] });

            assert.deepEqual(details(downlink), ["Requirement: The details could not be read: No element has the identifier 1e9fa5b1.", "Constraint: none", "Satisfied by: no part"]);
        });

        it("lists the issues of a rule page by page", async () => {
            await open(kestrel());
            const unused = item(page, "rule:unused-definition");

            buttonOf(unused, "Reveal").dispatch("click");
            const rows = details(unused);
            assert.equal(rows.length, 20);
            assert.equal(rows[0], "Kestrel::Catalog::Power::PCDU_100No feature is typed by the PartDefinition and nothing specializes it: use it, or remove it if it is left over.");

            const more = buttonOf(unused, "Show 5 more");
            more.dispatch("click");
            assert.equal(more.disabled, true);
            await serve(page, fabric(kestrel()));

            assert.deepEqual(toolCalls(page).at(-1), { name: "validate_model", arguments: { rules: ["unused-definition"], offset: 20, limit: 20 } });
            assert.equal(details(unused).length, 25);
            assert.equal(details(unused)[24].startsWith("Kestrel::Catalog::Power::PCDU_124"), true);
            assert.equal(buttonOf(unused, "Show 5 more"), undefined);
        });

        it("tells when the next issues cannot be read, and lets the user try again", async () => {
            await open(kestrel());
            const unused = item(page, "rule:unused-definition");
            buttonOf(unused, "Reveal").dispatch("click");
            const more = buttonOf(unused, "Show 5 more");

            more.dispatch("click");
            await answer(page, requests(page, "tools/call").at(-1), undefined, { code: -32603, message: "The host refused the call." });

            assert.equal(more.disabled, false);
            assert.equal(byId(page, "error").textContent, "The issues could not be read: The host refused the call.");
            assert.equal(details(unused).length, 20);
        });

        it("shows the issues of every rule, and keeps the details shown when the checks run again", async () => {
            await open(kestrel());

            byId(page, "show_all").dispatch("click");
            const shown = () => byId(page, "items").querySelectorAll(".it").filter(element => !element.querySelector(".mh-detail").hidden).map(element => element.dataset.key);
            assert.deepEqual(shown(), ["rule:broken-reference", "rule:requirement-without-constraint", "rule:unused-definition"]);
            assert.deepEqual(details(item(page, "rule:broken-reference")), [
                "SatisfyRequirementUsage in Kestrel::Requirements::SystemRequirementsThe SatisfyRequirementUsage has no satisfiedRequirement, which the metamodel requires."
            ]);

            buttonOf(item(page, "requirement:bdb93bec:Kestrel::Architecture::kestrelMission::kestrel1"), "Reveal").dispatch("click");
            await serve(page, fabric(kestrel()));
            byId(page, "check_again").dispatch("click");
            await serve(page, fabric(kestrel()));

            assert.deepEqual(shown(), ["requirement:bdb93bec:Kestrel::Architecture::kestrelMission::kestrel1", "rule:broken-reference", "rule:requirement-without-constraint", "rule:unused-definition"]);
            assert.equal(toolCalls(page).filter(call => call.name === "get_element_details").length, 2);
        });
    });

    describe("asking the assistant", () => {
        /**
         * Clicks a button of an item, then answers the request of the view.
         * @param {string} key The key of the item.
         * @param {string} label The label of the button.
         * @param {object} [result] The result of ui/message, empty when not given.
         * @returns {Promise<object>} The button.
         */
        async function click(key, label, result = {}) {
            const button = buttonOf(item(page, key), label);
            button.dispatch("click");
            await answer(page, messages(page).at(-1), result);
            return button;
        }

        /**
         * Gets the text of the last message for the assistant.
         * @returns {string} The text.
         */
        function lastMessage() {
            return messages(page).filter(message => message.method === "ui/message").at(-1).params.content[0].text;
        }

        it("puts the fix of a requirement not met in the chat box, with what Fabric found", async () => {
            await open(kestrel());

            const button = await click("requirement:bdb93bec:Kestrel::Architecture::kestrelMission::kestrel1", "Fix with Hypha");

            assert.equal(requests(page, "ui/message").at(-1).params.role, "user");
            assert.equal(lastMessage(), [
                "Fix this requirement, which is not met:",
                "Requirement REQ-SYS-001 (massBudget, id bdb93bec): \"The spacecraft dry mass, computed as the total mass of all units plus a 20% system margin, shall not exceed 230 kg.\"",
                "Check of Fabric: not met for Kestrel::Architecture::kestrelMission::kestrel1. 230.28 > 230",
                "Constraint: subj.mass * 1.2 <= 230; assumption: subj.maxBetaAngle <= 40 [deg]",
                "Gap: over by 0.28 kg.",
                "Warning: 'solarArray' is next to 'kestrel1' in the same package, not inside it, and carries mass (4 [kg]), which this check does not count.",
                "Prepare the batch with apply_changes."
            ].join("\n"));
            assert.equal(button.disabled, true);
            assert.equal(button.textContent, "Sent");
            const note = item(page, "requirement:bdb93bec:Kestrel::Architecture::kestrelMission::kestrel1").querySelector(".mh-note");
            assert.equal(note.hidden, false);
            assert.equal(note.textContent, "The message is in the chat box: check it and press Enter.");
        });

        it("asks to make a requirement checkable, with or without identifier", async () => {
            await open(kestrel());

            await click("requirement:f5f195ac:Kestrel::Architecture::kestrelMission::kestrel1::aocsSubsystem", "Fix with Hypha");
            assert.equal(lastMessage(), [
                "Make this requirement checkable:",
                "Requirement REQ-AOCS-002 (wheelRedundancy, id f5f195ac): \"The AOCS shall include four reaction wheels so that the mission survives the failure of one wheel.\"",
                "Check of Fabric: not verifiable for Kestrel::Architecture::kestrelMission::kestrel1::aocsSubsystem. " + NO_CONSTRAINT,
                "Prepare the batch with apply_changes."
            ].join("\n"));

            await click("requirement:5e0c7d1a:Kestrel::Architecture::kestrelMission::kestrel1", "Fix with Hypha");
            assert.equal(lastMessage(), [
                "Make this requirement checkable:",
                "Requirement harnessMass (id 5e0c7d1a)",
                "Check of Fabric: not evaluated for Kestrel::Architecture::kestrelMission::kestrel1. No part under 'Kestrel::Architecture::kestrelMission::kestrel1' has a numeric value for 'mass'.",
                "Constraint: subj.harness.mass <= 12",
                "Prepare the batch with apply_changes."
            ].join("\n"));
        });

        it("names a requirement that has neither identifier nor name, and tells when no part satisfies it", async () => {
            const model = healthy(0);
            model.requirements = [{ id: "9a", text: "The harness shall be shielded.", status: "NotVerifiable", explanation: NO_CONSTRAINT + " " + NO_PART }];
            await open(model);

            assert.deepEqual(problems(page), ["## Not checked", `it na | Requirement The harness shall be shielded. | ${NO_CONSTRAINT} ${NO_PART} | Reveal, Fix with Hypha`]);
            await click("requirement:9a:", "Fix with Hypha");
            assert.equal(lastMessage(), [
                "Make this requirement checkable:",
                "Requirement id 9a: \"The harness shall be shielded.\"",
                `Check of Fabric: not verifiable. ${NO_CONSTRAINT} ${NO_PART}`,
                "Prepare the batch with apply_changes."
            ].join("\n"));
        });

        it("asks for a trade-off that meets a requirement", async () => {
            await open(kestrel());

            await click("requirement:f587d208:Kestrel::Architecture::kestrelMission::kestrel1::communicationSubsystem::xBandTransmitter", "Run trade-off");

            assert.equal(lastMessage(), [
                "Run a trade-off to meet this requirement:",
                "Requirement REQ-COM-001 (downlinkRate, id f587d208): \"The X-band downlink shall provide a data rate of at least 400 Mbps.\"",
                "Check of Fabric: not met for Kestrel::Architecture::kestrelMission::kestrel1::communicationSubsystem::xBandTransmitter. 300 < 400",
                "Constraint: subj.dataRate >= 400",
                "Gap: short by 100.",
                "Use solve_trade_off with this requirement as a constraint."
            ].join("\n"));
        });

        it("asks to fix the issues of a rule, with its elements and how to get the others", async () => {
            await open(kestrel());

            await click("rule:requirement-without-constraint", "Fix with Hypha");
            assert.equal(lastMessage(), [
                "Fix the issues of the validation rule requirement-without-constraint (Warning, 1 element): A requirement has no required constraint, so it is only text and cannot be verified.",
                "- Kestrel::Requirements::SubsystemRequirements::wheelRedundancy (id f5f195ac): The requirement has no required constraint, for example subj.mass <= 150 [kg], so it cannot be verified.",
                "Prepare the batch with apply_changes."
            ].join("\n"));

            await click("rule:unused-definition", "Fix with Hypha");
            const lines = lastMessage().split("\n");
            assert.equal(lines.length, 23);
            assert.equal(lines[0], "Fix the issues of the validation rule unused-definition (Information, 25 elements): No feature is typed by a definition and nothing specializes it, so it takes no part in the model.");
            assert.equal(lines[21], "- and 5 more: call validate_model with rules [\"unused-definition\"] to list them.");
        });

        it("gives the message to the assistant when the host does not put it in the chat box", async () => {
            await open(kestrel());
            const key = "rule:broken-reference";

            const button = buttonOf(item(page, key), "Fix with Hypha");
            button.dispatch("click");
            await answer(page, requests(page, "ui/message").at(-1), { isError: true });
            await answer(page, requests(page, "ui/update-model-context").at(-1), {});

            assert.equal(requests(page, "ui/update-model-context").at(-1).params.content[0].text, lastMessage());
            assert.equal(item(page, key).querySelector(".mh-note").textContent, "Sent to the assistant: type \"go\" in the chat.");
            assert.equal(button.textContent, "Sent");
        });

        it("tells that the host refused the message, and lets the user try again", async () => {
            await open(kestrel());
            const key = "rule:broken-reference";

            const button = buttonOf(item(page, key), "Fix with Hypha");
            button.dispatch("click");
            await answer(page, requests(page, "ui/message").at(-1), undefined, { code: -32601, message: "Method not found" });
            await answer(page, requests(page, "ui/update-model-context").at(-1), undefined, { code: -32601, message: "Method not found" });

            assert.equal(item(page, key).querySelector(".mh-note").textContent, "The host refused the message.");
            assert.equal(button.disabled, false);
            assert.equal(button.textContent, "Fix with Hypha");
        });
    });

    describe("checking again", () => {
        it("shows the new state of the model after a batch, without the assistant", async () => {
            await open(eosat());
            const count = messages(page).length;

            byId(page, "check_again").dispatch("click");
            assert.equal(byId(page, "check_again").disabled, true);
            assert.equal(byId(page, "subtitle").textContent, "548 elements · checked just now");
            await serve(page, fabric(eosatAfterBatch()));

            assert.deepEqual(messages(page).slice(count).map(message => message.method).filter(method => method !== "tools/call"), []);
            assert.equal(byId(page, "subtitle").textContent, "605 elements · checked just now");
            assert.deepEqual(counters(page), {
                met: "0 / 6",
                metDetail: "1 not met · 5 not checked (s bad)",
                metBar: "bar 0%",
                errors: "0 errors",
                validationDetail: "10 warnings · 9 rules (s warn)",
                validationBar: "warn 100%"
            });
            assert.equal(problems(page)[1],
                "it bad | REQ-SYS-001 The satellite launch mass, computed as the total dry mass of all units plus a 20% system margin, shall not exceed 150 kg. | eosat1 · 150.96 [kg] > 150 [kg] · over by 0.96 | Reveal, Run trade-off, Fix with Hypha");
        });

        it("no longer runs a rule ignored here, until the user runs it again", async () => {
            await open(eosat());
            const calls = toolCalls(page).length;

            buttonOf(item(page, "rule:requirement-without-satisfy"), "Ignore rule here").dispatch("click");

            assert.equal(item(page, "rule:requirement-without-satisfy"), undefined);
            assert.equal(byId(page, "ignored").hidden, false);
            assert.equal(byId(page, "ignored").textContent, "Not run here: requirement-without-satisfy. Run them again");
            await serve(page, fabric(eosat()));

            const allRules = eosat().rules.map(rule => rule.name);
            const otherRules = allRules.filter(name => name !== "requirement-without-satisfy");
            assert.deepEqual(toolCalls(page).slice(calls), [
                { name: "validate_model", arguments: { rules: otherRules, limit: 1 } },
                { name: "validate_model", arguments: { rules: ["requirement-without-constraint"], offset: 0, limit: 20 } }
            ]);
            assert.equal(byId(page, "validation_detail").textContent, "6 warnings · 8 rules");

            byId(page, "check_again").dispatch("click");
            await serve(page, fabric(eosat()));
            assert.deepEqual(toolCalls(page).filter(call => call.name === "validate_model" && call.arguments.limit === 1).at(-1).arguments.rules, otherRules);

            buttonOf(byId(page, "ignored"), "Run them again").dispatch("click");
            assert.equal(byId(page, "ignored").hidden, true);
            await serve(page, fabric(eosat()));

            assert.deepEqual(toolCalls(page).filter(call => call.name === "validate_model" && call.arguments.limit === 1).at(-1).arguments, { limit: 1 });
            assert.equal(byId(page, "validation_detail").textContent, "12 warnings · 9 rules");
            assert.notEqual(item(page, "rule:requirement-without-satisfy"), undefined);
        });

        it("runs no rule when every rule is ignored", async () => {
            const model = healthy(1);
            model.issues = model.rules.map((rule, index) => issue(rule.name, rule.severity, "e" + index, "Model::element" + index, "Problem " + index));
            model.rules = rules(model.issues);
            await open(model);

            for (const rule of model.rules) {
                buttonOf(item(page, "rule:" + rule.name), "Ignore rule here").dispatch("click");
                await serve(page, fabric(model));
            }

            assert.deepEqual(toolCalls(page).filter(call => call.name === "validate_model" && call.arguments.limit === 1).at(-1).arguments.rules, ["unused-definition"]);
            assert.equal(byId(page, "items").querySelectorAll(".it").length, 0);
            assert.equal(byId(page, "errors").textContent, "–");
            assert.equal(byId(page, "validation_detail").textContent, "No rule checked");
            assert.equal(byId(page, "validation_bar").style.width, "0%");
            assert.equal(byId(page, "subtitle").textContent, "checked just now");
        });

        it("only shows the last run", async () => {
            await open(eosat());

            byId(page, "check_again").dispatch("click");
            const older = new Set(requests(page, "tools/call").map(message => message.id));
            byId(page, "check_again").dispatch("click");

            await serve(page, fabric(eosatAfterBatch()), message => !older.has(message.id));
            await serve(page, fabric(eosat()));

            assert.equal(byId(page, "met_detail").textContent, "1 not met · 5 not checked");
            assert.equal(byId(page, "check_again").disabled, false);
        });

        it("ignores the failure of a run that a later run has replaced", async () => {
            await open(eosat());

            byId(page, "check_again").dispatch("click");
            const older = new Set(requests(page, "tools/call").map(message => message.id));
            byId(page, "check_again").dispatch("click");

            await serve(page, fabric(eosatAfterBatch()), message => !older.has(message.id));
            await serve(page, () => ({ isError: true, content: [{ type: "text", text: "Too late." }] }));

            assert.equal(byId(page, "error").hidden, true);
            assert.equal(byId(page, "met_detail").textContent, "1 not met · 5 not checked");
        });
    });

    describe("when the checks cannot run", () => {
        it("shows the result of the assistant's call and the error of the tool", async () => {
            await answer(page, messages(page)[0], {});
            page.receive({ jsonrpc: "2.0", method: "ui/notifications/tool-result", params: textResult(checkPage(eosat())) });

            await answer(page, requests(page, "tools/call")[0], { isError: true, content: [{ type: "text", text: "The model is being loaded." }] });
            await answer(page, requests(page, "tools/call")[1], textResult(validationPage(eosat(), {})));

            assert.equal(byId(page, "error").hidden, false);
            assert.equal(byId(page, "error").textContent, "The view could not run the checks: The model is being loaded.");
            assert.equal(byId(page, "subtitle").textContent, "As checked for the assistant");
            assert.equal(byId(page, "met").textContent, "0 / 6");
            assert.equal(byId(page, "errors").textContent, "–");
            assert.equal(byId(page, "check_again").disabled, false);
        });

        it("tells the error of the host, without anything to show when the assistant's call failed too", async () => {
            await answer(page, messages(page)[0], {});
            page.receive({ jsonrpc: "2.0", method: "ui/notifications/tool-result", params: { isError: true, content: [{ type: "text", text: "An error occurred." }] } });

            await answer(page, requests(page, "tools/call")[0], undefined, { code: -32603, message: "Tool calls are not allowed." });

            assert.equal(byId(page, "error").textContent, "The view could not run the checks: Tool calls are not allowed.");
            assert.equal(byId(page, "kpis").hidden, true);
        });

        it("runs the checks itself when the result of the assistant's call has no text", async () => {
            await answer(page, messages(page)[0], {});
            page.receive({ jsonrpc: "2.0", method: "ui/notifications/tool-result", params: { content: [] } });

            await answer(page, requests(page, "tools/call")[0], { isError: true, content: [{ type: "text", text: "Busy." }] });

            assert.equal(byId(page, "error").textContent, "The view could not run the checks: Busy.");
            assert.equal(byId(page, "kpis").hidden, true);
        });

        it("names a tool that fails without text, and reads a result that is not JSON as nothing", async () => {
            await answer(page, messages(page)[0], {});
            page.receive({ jsonrpc: "2.0", method: "ui/notifications/tool-result", params: { content: [{ type: "text", text: "not JSON" }] } });

            await answer(page, requests(page, "tools/call")[0], { isError: true, content: [] });

            assert.equal(byId(page, "error").textContent, "The view could not run the checks: check_requirements has failed.");
            assert.equal(byId(page, "kpis").hidden, true);
        });

        it("reads the structured content of a result when the host gives it, and hides the error of the last run", async () => {
            await answer(page, messages(page)[0], {});
            page.receive({ jsonrpc: "2.0", method: "ui/notifications/tool-result", params: { structuredContent: checkPage(eosat()) } });
            await answer(page, requests(page, "tools/call")[0], { isError: true, content: [{ type: "text", text: "Busy." }] });
            assert.equal(byId(page, "met").textContent, "0 / 6");

            byId(page, "check_again").dispatch("click");
            await serve(page, (name, args) => ({ structuredContent: name === "check_requirements" ? checkPage(kestrel(), args) : validationPage(kestrel(), args) }), message => message.id > 3);

            assert.equal(byId(page, "error").hidden, true);
            assert.equal(byId(page, "met").textContent, "3 / 10");
        });
    });
});
