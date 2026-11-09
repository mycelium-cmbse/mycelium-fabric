// ------------------------------------------------------------------------------------------------
//  <copyright file="requirement-extraction.test.js" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

// Tests of the view "Extract requirements" (McpApp/requirement-extraction/requirement-extraction.js), run by Node.js:
// node --test Mycelium.Fabric.Mcp.Tests/McpApp/requirement-extraction/requirement-extraction.test.js

const { describe, it, beforeEach, afterEach } = require("node:test");
const assert = require("node:assert/strict");
const path = require("node:path");
const { loadPage } = require("../fake-page.js");

const APP_FOLDER = path.join(__dirname, "..", "..", "..", "Mycelium.Fabric.Mcp", "McpApp");

// The tools that the view calls: it imports and saves, and never calls another tool.
const IMPORT_TOOL = "import_requirements";
const SAVE_TOOL = "save_requirement_list";

const PROPOSAL_ID = "f40d2207-3be4-439a-92d4-1b7525698544";
const TOKEN = "8ca2fe4f418c1fff2fd4bb7ef33db6d3";

// The titles of the choices of a requirement that has a rewrite.
const REWRITE = "Rewrite with a threshold";
const GOAL = "Keep as a design goal";
const MERGE = "Merge into";
const LEAVE_OUT = "Leave out";

/**
 * Builds a row of a proposal, as propose_requirements sends it.
 * @param {object} requirement The requirement given by the assistant.
 * @param {string} sourceTitle The title of the section where its statement is written, or null.
 * @param {string} excerpt The passage of the document, or null.
 * @param {object[]} findings The findings of the server.
 * @returns {object} The row.
 */
function row(requirement, sourceTitle, excerpt, findings = []) {
    const blocked = findings.some(finding => finding.kind === "AlreadyModelled" || finding.kind === "Repeated");
    return { requirement, sourceTitle, excerpt, findings, isReady: findings.length === 0, isBlocked: blocked };
}

/**
 * Builds the proposal of section 3 of EOSat1-requirements.md (real result of propose_requirements on EOSat-1), with a rewrite
 * for REQ-PAY-004 and a unit refused for REQ-PAY-003.
 * @returns {object} The proposal, kept by the server with its identifier and token.
 */
function payload() {
    return {
        fileName: "EOSat1-requirements.md",
        sectionNumber: 5,
        sectionTitle: "3. Payload requirements",
        ownerId: "896440c0-d061-7856-fb8f-fe278e26de93",
        ownerName: "EOSat1::Requirements",
        newPackage: "PayloadRequirements",
        requirements: [
            row({
                reqId: "REQ-PAY-001", name: "groundSampling", kind: "Performance",
                text: "The camera shall have a ground sampling distance of at most 5 m from an altitude of 600 km.",
                constraint: { attribute: "groundSamplingDistance", operator: "<=", limit: 5, unit: "m" }
            }, "3.1 Camera", "| REQ-PAY-001 | The camera shall have a ground sampling distance of at most 5 m from an altitude of 600 km. | H |"),
            row({
                reqId: "REQ-PAY-002", name: "cameraMass", kind: "Performance",
                text: "The camera mass shall not exceed 40 kg, as allocated by REQ-SYS-001.",
                constraint: { attribute: "mass", operator: "<=", limit: 40, unit: "kg", margin: 20 }
            }, "3.1 Camera", "| REQ-PAY-002 | The camera mass shall not exceed 40 kg, as allocated by REQ-SYS-001. | M |"),
            row({
                reqId: "REQ-PAY-003", name: "imageStorage", kind: "Performance",
                text: "The mass memory shall store at least 512 Gbit of images.",
                constraint: { attribute: "storageCapacity", operator: ">=", limit: 512, unit: "Gb" }
            }, "3.2 Data handling", "REQ-PAY-003: The mass memory shall store at least 512 Gbit of images.", [{
                kind: "InvalidChange",
                message: "The unit 'Gb' is not known."
            }]),
            row({
                reqId: "REQ-PAY-004", name: "downlinkDelay", kind: "Operational",
                text: "The images shall be downlinked within a responsive time (target TBD).",
                clarification: "TBD target and vague term 'responsive'.",
                rewrittenText: "The images shall be downlinked within 2 h of their acquisition.",
                rewrittenConstraint: { attribute: "downlinkDelay", operator: "<=", limit: 2, unit: "h" },
                mergeInto: "REQ-PAY-002"
            }, "3.2 Data handling", "REQ-PAY-004: The images shall be downlinked within a responsive time (target TBD).", [{
                kind: "ToClarify", message: "TBD target and vague term 'responsive'."
            }])
        ],
        missedIds: [],
        id: PROPOSAL_ID,
        token: TOKEN
    };
}

/**
 * Builds the proposal of section 2 of EOSat1-requirements.md (real result of propose_requirements on EOSat-1): every
 * requirement is already modelled, and one is given twice; the list misses REQ-SYS-004.
 * @returns {object} The proposal.
 */
function system() {
    const modelled = reqId => ({ kind: "AlreadyModelled", message: `The model already has a requirement ${reqId}: it is not imported again.` });
    return {
        ...payload(),
        sectionNumber: 4,
        sectionTitle: "2. System requirements",
        newPackage: null,
        requirements: [
            row({ reqId: "REQ-SYS-001", name: "launchMass", text: "The launch mass shall not exceed 150 kg." }, null, null, [
                { kind: "NotInSection", message: "The statement is not written as such in section 4." },
                modelled("REQ-SYS-001")
            ]),
            row({ reqId: "REQ-SYS-008", name: "eclipse", text: "The battery shall last the eclipse." }, "2. System requirements", "The battery shall last the eclipse."),
            row({ reqId: "REQ-SYS-008", name: "eclipseAgain", text: "The battery shall last the eclipse." }, "2. System requirements", "The battery shall last the eclipse.",
                [{ kind: "Repeated", message: "REQ-SYS-008 is given twice: only its first row can be imported." }])
        ],
        missedIds: ["REQ-SYS-004"]
    };
}

/**
 * Builds the result of import_requirements once the batch is applied (real result on EOSat-1).
 * @returns {object} The result of the tool, its import as structured content.
 */
function imported() {
    const message = "The user has imported requirements of section 5 '3. Payload requirements' of 'EOSat1-requirements.md' in one batch.";
    return { content: [{ type: "text", text: message }], structuredContent: { message, result: { createdElements: [], problems: [], applied: true }, imported: true } };
}

/**
 * Lets the pending promises of the view go on.
 * @returns {Promise<void>} Resolved on the next turn of the event loop.
 */
function flush() {
    return new Promise(resolve => setImmediate(resolve));
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
 * Gets the requests of a method that the view has sent to the host.
 * @param {object} page The page.
 * @param {string} method The method of the requests.
 * @returns {object[]} The requests.
 */
function requests(page, method) {
    return messages(page).filter(message => message.method === method && message.id !== undefined);
}

/**
 * Answers a request of the view, then lets the view go on.
 * @param {object} page The page.
 * @param {object} message The request.
 * @param {object} result The result, or nothing when the host answers with an error.
 * @param {object} [error] The error of the host.
 * @returns {Promise<void>} Resolved once the view has handled the answer.
 */
async function answer(page, message, result, error) {
    page.receive(error ? { jsonrpc: "2.0", id: message.id, error } : { jsonrpc: "2.0", id: message.id, result });
    await flush();
}

/**
 * Reads the rows of the table: the identifier, whether it is ticked or disabled, the constraint and the quality of each.
 * @param {object} page The page.
 * @returns {object[]} The rows.
 */
function rows(page) {
    return page.document.getElementById("rows").querySelectorAll("tr.rx-row").map(tr => {
        const tick = tr.querySelector(".cb");
        return {
            reqId: tr.querySelector(".rx-id").textContent,
            ticked: tick.getAttribute("aria-checked") === "true",
            disabled: tick.disabled,
            constraint: tr.querySelector(".cst").textContent,
            quality: tr.querySelector(".qtag").textContent
        };
    });
}

/**
 * Gets the row of a requirement in the table.
 * @param {object} page The page.
 * @param {string} reqId The identifier of the requirement.
 * @returns {object} The row.
 */
function rowOf(page, reqId) {
    return page.document.getElementById("rows").querySelectorAll("tr.rx-row").find(tr => tr.querySelector(".rx-id").textContent === reqId);
}

/**
 * Gets a choice of the selected requirement.
 * @param {object} page The page.
 * @param {string} title The title of the choice.
 * @returns {object} The choice.
 */
function choice(page, title) {
    return page.document.getElementById("detail").querySelectorAll(".oc").find(item => item.querySelector("b").textContent === title);
}

/**
 * Reads the choices of the selected requirement.
 * @param {object} page The page.
 * @returns {string[]} The titles of the choices.
 */
function choices(page) {
    return page.document.getElementById("detail").querySelectorAll(".oc b").map(item => item.textContent);
}

/**
 * Reads the choice picked in the card.
 * @param {object} page The page.
 * @returns {string} The title of the choice, or null when none is picked.
 */
function picked(page) {
    return page.document.getElementById("detail").querySelector(".oc.on")?.querySelector("b").textContent ?? null;
}

/**
 * Gets the identifier of the requirement shown in the card.
 * @param {object} page The page.
 * @returns {string} The identifier.
 */
function selected(page) {
    return page.document.getElementById("detail").querySelector(".card-h").textContent.split("REQ-")[1].slice(0, 7).replace(/^/, "REQ-");
}

/**
 * Picks a choice of the selected requirement, then applies it with the button of the card.
 * @param {object} page The page.
 * @param {string} title The title of the choice.
 */
function applyChoice(page, title) {
    choice(page, title).dispatch("click");
    page.document.getElementById("apply").dispatch("click");
}

/**
 * Sets the target of the merge in the card.
 * @param {object} page The page.
 * @param {string} reqId The identifier of the target.
 */
function setTarget(page, reqId) {
    const list = page.document.getElementById("detail").querySelector("select");
    list.value = reqId;
    list.dispatch("change");
}

/**
 * Reads the text of the footer.
 * @param {object} page The page.
 * @returns {string} The text.
 */
function summary(page) {
    return page.document.getElementById("summary").textContent;
}

/**
 * Selects a requirement.
 * @param {object} page The page.
 * @param {string} reqId The identifier of the requirement.
 */
function selectRow(page, reqId) {
    rowOf(page, reqId).dispatch("click");
}

/**
 * Ticks or unticks a requirement.
 * @param {object} page The page.
 * @param {string} reqId The identifier of the requirement.
 */
function tick(page, reqId) {
    rowOf(page, reqId).querySelector(".cb").dispatch("click");
}

describe("requirement extraction view", () => {
    let page;

    /**
     * Initializes the view, then gives it the result of propose_requirements.
     * @param {object} proposal The proposal kept by the server.
     * @param {object} [context] The context of the host.
     */
    async function open(proposal, context = { theme: "light" }) {
        await answer(page, messages(page)[0], { hostContext: context });
        page.receive({ jsonrpc: "2.0", method: "ui/notifications/tool-result", params: { content: [], structuredContent: proposal } });
        await flush();
    }

    /**
     * Gives the view the room of the mock-up.
     */
    function widen() {
        globalThis.window.innerWidth = 1440;
        page.resize();
    }

    /**
     * Clicks a button of the footer, then answers the call of the tool.
     * @param {string} id The id of the button.
     * @param {object} result The result of the tool.
     * @param {object} [error] The error of the host, instead of a result.
     * @returns {Promise<object>} The arguments of the call.
     */
    async function callWith(id, result, error) {
        page.document.getElementById(id).dispatch("click");
        const call = requests(page, "tools/call").at(-1);
        await answer(page, call, result, error);
        return call.params.arguments;
    }

    /**
     * Clicks the import button, then answers the call of the tool.
     * @param {object} result The result of import_requirements.
     * @param {object} [error] The error of the host, instead of a result.
     * @returns {Promise<object>} The arguments of the call.
     */
    function importWith(result, error) {
        return callWith("import", result, error);
    }

    beforeEach(() => {
        page = loadPage(path.join(APP_FOLDER, "requirement-extraction", "requirement-extraction.html"), path.join(APP_FOLDER, "host.js"), path.join(APP_FOLDER, "requirement-extraction", "requirement-extraction.js"));
    });

    afterEach(() => {
        assert.deepEqual(requests(page, "tools/call").filter(call => call.params.name !== IMPORT_TOOL && call.params.name !== SAVE_TOOL), []);
    });

    describe("display", () => {
        it("asks for the full screen when the host offers it, and offers the way back", async () => {
            await open(payload(), { theme: "light", displayMode: "inline", availableDisplayModes: ["inline", "fullscreen"] });

            const [ask] = requests(page, "ui/request-display-mode");
            assert.equal(ask.params.mode, "fullscreen");
            assert.equal(page.document.getElementById("back").hidden, true);
            await answer(page, ask, { mode: "fullscreen" });
            assert.equal(page.document.getElementById("view").classList.contains("full"), true);
            assert.equal(page.document.getElementById("back").hidden, false);

            page.document.getElementById("back").dispatch("click");
            const back = requests(page, "ui/request-display-mode").at(-1);
            assert.equal(back.params.mode, "inline");
            await answer(page, back, {});
            assert.equal(page.document.getElementById("view").classList.contains("full"), true);

            page.receive({ jsonrpc: "2.0", method: "ui/notifications/host-context-changed", params: { displayMode: "inline" } });
            assert.equal(page.document.getElementById("view").classList.contains("full"), false);
            assert.equal(page.document.getElementById("back").hidden, true);
        });

        it("stays in the conversation when the host offers no full screen, or refuses it, or is already in it", async () => {
            await open(payload(), { availableDisplayModes: ["inline"], displayMode: "inline" });
            assert.deepEqual(requests(page, "ui/request-display-mode"), []);
            assert.equal(page.document.getElementById("back").hidden, true);

            page = loadPage(path.join(APP_FOLDER, "requirement-extraction", "requirement-extraction.html"), path.join(APP_FOLDER, "host.js"), path.join(APP_FOLDER, "requirement-extraction", "requirement-extraction.js"));
            await open(payload(), { availableDisplayModes: ["fullscreen"], displayMode: "fullscreen" });
            assert.deepEqual(requests(page, "ui/request-display-mode"), []);
            assert.equal(page.document.getElementById("back").hidden, false);

            page = loadPage(path.join(APP_FOLDER, "requirement-extraction", "requirement-extraction.html"), path.join(APP_FOLDER, "host.js"), path.join(APP_FOLDER, "requirement-extraction", "requirement-extraction.js"));
            await open(payload(), { availableDisplayModes: ["inline", "fullscreen"], displayMode: "inline" });
            await answer(page, requests(page, "ui/request-display-mode")[0], undefined, { code: -32000, message: "No." });
            assert.equal(page.document.getElementById("error").textContent, "The host did not change the display: No.");
        });

        it("tells when the host does not start the view", async () => {
            await answer(page, messages(page)[0], undefined, { code: -32000, message: "No view." });
            assert.equal(page.document.getElementById("error").textContent, "The host did not start the view: No view.");
        });

        it("keeps the card next to the table with room, and unfolds it under its row without", async () => {
            await open(payload());

            const body = page.document.getElementById("body");
            const detail = page.document.getElementById("detail");
            const fold = () => page.document.getElementById("rows").querySelector("tr.rx-fold");
            assert.equal(detail.parentElement.parentElement.classList.contains("rx-fold"), true);
            assert.equal(page.document.getElementById("rows").children.indexOf(fold()), 3);

            selectRow(page, "REQ-PAY-003");
            assert.equal(fold(), null);
            assert.equal(detail.hidden, true);

            selectRow(page, "REQ-PAY-004");
            widen();
            assert.equal(fold(), null);
            assert.equal(detail.parentElement, body);
            assert.equal(detail.hidden, false);
            assert.equal(selected(page), "REQ-PAY-004");

            for (const [width, columns] of [[1000, "6"], [600, "4"], [400, "3"]]) {
                globalThis.window.innerWidth = width;
                page.resize();
                assert.equal(fold().querySelector("td").getAttribute("colspan"), columns);
            }

            const sent = page.sent.length;
            page.resize();
            assert.equal(page.sent.length, sent + 1);
            assert.equal(messages(page).at(-1).method, "ui/notifications/size-changed");
        });

        it("shows the card only on a requirement with a problem", async () => {
            await open(payload());
            widen();
            const detail = page.document.getElementById("detail");
            assert.equal(detail.hidden, false);

            selectRow(page, "REQ-PAY-001");
            assert.equal(detail.hidden, true);
            assert.equal(rowOf(page, "REQ-PAY-001").classList.contains("sel"), false);

            selectRow(page, "REQ-PAY-004");
            assert.equal(detail.hidden, false);
            selectRow(page, "REQ-PAY-004");
            assert.equal(detail.hidden, true);

            await open({ ...payload(), requirements: payload().requirements.slice(0, 2) });
            assert.equal(detail.hidden, true);
        });
    });

    describe("showing the proposal", () => {
        it("starts the talk with the host, then waits for the requirements", () => {
            const [initialize] = messages(page);
            assert.equal(initialize.method, "ui/initialize");
            assert.equal(initialize.params.appInfo.name, "requirement-extraction");
            assert.equal(page.document.getElementById("subtitle").textContent, "Waiting for the requirements…");
            assert.notEqual(page.document.getElementById("import").getAttribute("disabled"), null);
        });

        it("lists the requirements with their constraint and quality, the ready ones ticked, the first one to review selected", async () => {
            await open(payload());

            assert.equal(page.document.getElementById("title").textContent, "Extract requirements · EOSat1-requirements.md");
            assert.equal(page.document.getElementById("subtitle").textContent,
                "§ 3. Payload requirements · 4 requirements found · 2 need your attention · target package: EOSat1::Requirements::PayloadRequirements (new)");
            assert.deepEqual(rows(page), [
                { reqId: "REQ-PAY-001", ticked: true, disabled: false, constraint: "groundSamplingDistance <= 5 [m]", quality: "Ready" },
                { reqId: "REQ-PAY-002", ticked: true, disabled: false, constraint: "mass * 1.2 <= 40 [kg]", quality: "Ready" },
                { reqId: "REQ-PAY-003", ticked: false, disabled: false, constraint: "storageCapacity >= 512 [Gb]", quality: "Refused" },
                { reqId: "REQ-PAY-004", ticked: false, disabled: false, constraint: "no threshold", quality: "To clarify" }
            ]);
            assert.equal(selected(page), "REQ-PAY-003");
            assert.equal(summary(page), "2 selected · creates 2 requirements and 2 constraints");
            assert.equal(page.document.getElementById("import").disabled, false);
            assert.equal(page.document.getElementById("missed").hidden, true);
        });

        it("blocks the requirements already modelled or given twice, and names the identifiers that the list misses", async () => {
            await open(system());

            assert.deepEqual(rows(page).map(item => [item.reqId, item.ticked, item.disabled, item.quality]), [
                ["REQ-SYS-001", false, true, "Already modelled"],
                ["REQ-SYS-008", true, false, "Ready"],
                ["REQ-SYS-008", false, true, "Duplicate"]
            ]);
            assert.match(page.document.getElementById("subtitle").textContent, /target package: EOSat1::Requirements$/);
            assert.equal(page.document.getElementById("missed").hidden, false);
            assert.match(page.document.getElementById("missed").textContent, /REQ-SYS-004, which the list misses: ask the assistant to propose it\./);

            tick(page, "REQ-SYS-001");
            assert.equal(rows(page)[0].ticked, false);
        });

        it("filters the ready requirements and those that need attention, and keeps a selected row among those shown", async () => {
            await open(payload());
            widen();
            const filter = name => page.document.getElementById("filters").querySelectorAll("button").find(chip => chip.dataset.filter === name);

            assert.equal(page.document.getElementById("count_ready").textContent, "2");
            assert.equal(page.document.getElementById("count_attention").textContent, "2");
            filter("attention").dispatch("click");
            assert.deepEqual(rows(page).map(item => item.reqId), ["REQ-PAY-003", "REQ-PAY-004"]);
            assert.equal(filter("attention").getAttribute("aria-pressed"), "true");
            filter("ready").dispatch("click");
            assert.deepEqual(rows(page).map(item => item.reqId), ["REQ-PAY-001", "REQ-PAY-002"]);
            assert.equal(page.document.getElementById("detail").hidden, true);
            filter("all").dispatch("click");
            assert.equal(rows(page).length, 4);

            await open({ ...payload(), requirements: payload().requirements.slice(2) });
            filter("ready").dispatch("click");
            assert.equal(rows(page).length, 0);
            assert.equal(page.document.getElementById("rows").textContent, "No requirement here.");
            assert.equal(page.document.getElementById("detail").hidden, true);
        });

        it("sorts on a column, ascending, then descending, then back to the order of the document", async () => {
            await open(payload());
            widen();
            const sort = column => page.document.getElementById("head").querySelector(`button[data-sort="${column}"]`);
            const order = () => rows(page).map(item => item.reqId.slice(-1));

            sort("id").dispatch("click");
            assert.deepEqual(order(), ["1", "2", "3", "4"]);
            assert.equal(sort("id").parentElement.getAttribute("aria-sort"), "ascending");
            sort("id").dispatch("click");
            assert.deepEqual(order(), ["4", "3", "2", "1"]);
            assert.equal(sort("id").parentElement.getAttribute("aria-sort"), "descending");
            sort("id").dispatch("click");
            assert.deepEqual(order(), ["1", "2", "3", "4"]);
            assert.equal(sort("id").parentElement.getAttribute("aria-sort"), "none");

            sort("quality").dispatch("click");
            assert.deepEqual(rows(page).map(item => item.quality), ["Ready", "Ready", "Refused", "To clarify"]);
            sort("kind").dispatch("click");
            assert.deepEqual(order(), ["4", "1", "2", "3"]);
            sort("constraint").dispatch("click");
            assert.deepEqual(order(), ["1", "2", "4", "3"]);
            sort("requirement").dispatch("click");
            assert.deepEqual(order(), ["2", "1", "4", "3"]);
        });

        it("tells when the host sends no proposal, or when the tool has failed", async () => {
            await answer(page, messages(page)[0], {});
            page.receive({ jsonrpc: "2.0", method: "ui/notifications/tool-result", params: { content: [{ type: "text", text: "{}" }] } });
            assert.match(page.document.getElementById("error").textContent, /sent no proposal/);

            page.receive({ jsonrpc: "2.0", method: "ui/notifications/tool-result", params: { isError: true, content: [{ type: "text", text: "The list of requirements is empty." }] } });
            assert.equal(page.document.getElementById("error").textContent, "The list of requirements is empty.");
            assert.equal(page.document.getElementById("body").hidden, true);
        });
    });

    describe("choosing", () => {
        it("unticks and ticks a ready requirement", async () => {
            await open(payload());

            tick(page, "REQ-PAY-001");
            assert.equal(rows(page)[0].ticked, false);
            assert.equal(summary(page), "1 selected · creates 1 requirement and 1 constraint");
            tick(page, "REQ-PAY-001");
            tick(page, "REQ-PAY-002");
            assert.deepEqual(rows(page).map(item => item.ticked), [true, false, false, false]);
        });

        it("shows the passage of the document with the statement marked, and why it needs attention", async () => {
            await open(payload());
            widen();

            selectRow(page, "REQ-PAY-004");
            const detail = page.document.getElementById("detail");
            assert.equal(detail.querySelector(".excerpt mark").textContent, "The images shall be downlinked within a responsive time (target TBD).");
            assert.equal(detail.querySelector(".excerpt").textContent, "REQ-PAY-004: The images shall be downlinked within a responsive time (target TBD).");
            assert.equal(detail.querySelector(".why").textContent, "Why: TBD target and vague term 'responsive'.");
            assert.deepEqual(choices(page), [REWRITE, GOAL, MERGE, LEAVE_OUT]);
            assert.equal(rowOf(page, "REQ-PAY-004").classList.contains("sel"), true);
        });

        it("picks the rewrite when the server accepts it, and nothing otherwise", async () => {
            await open(payload());
            widen();

            assert.equal(picked(page), null);
            assert.deepEqual(choices(page), [GOAL, MERGE, LEAVE_OUT]);
            assert.equal(page.document.getElementById("apply").disabled, true);

            selectRow(page, "REQ-PAY-004");
            assert.equal(picked(page), REWRITE);
            assert.equal(page.document.getElementById("apply").disabled, false);

            const refused = payload();
            refused.requirements[3].findings.push({ kind: "InvalidRewrite", message: "Rewrite: refused." });
            await open(refused);
            selectRow(page, "REQ-PAY-004");
            assert.equal(picked(page), null);
            assert.deepEqual(choices(page), [GOAL, MERGE, LEAVE_OUT]);
        });

        it("counts a choice only once it is applied, then goes to the next requirement to review", async () => {
            await open(payload());
            widen();

            choice(page, GOAL).dispatch("click");
            assert.equal(choice(page, GOAL).getAttribute("aria-checked"), "true");
            assert.equal(page.document.getElementById("apply").disabled, false);
            assert.equal(rows(page)[2].ticked, false);
            assert.equal(summary(page), "2 selected · creates 2 requirements and 2 constraints");

            page.document.getElementById("apply").dispatch("click");
            assert.deepEqual(rows(page)[2], { reqId: "REQ-PAY-003", ticked: true, disabled: false, constraint: "no threshold", quality: "Refused" });
            assert.equal(summary(page), "3 selected · creates 3 requirements and 2 constraints");
            assert.equal(selected(page), "REQ-PAY-004");
            assert.equal(rowOf(page, "REQ-PAY-004").scrolledIntoView, true);
            assert.equal(page.document.getElementById("apply").textContent, "Apply");

            page.document.getElementById("apply").dispatch("click");
            assert.deepEqual(rows(page)[3], { reqId: "REQ-PAY-004", ticked: true, disabled: false, constraint: "downlinkDelay <= 2 [h]", quality: "To clarify" });
            assert.equal(selected(page), "REQ-PAY-004");

            choice(page, LEAVE_OUT).dispatch("keydown", { key: "Tab" });
            assert.equal(picked(page), REWRITE);
            const leave = choice(page, LEAVE_OUT).dispatch("keydown", { key: "Enter" });
            assert.equal(leave.defaultPrevented, true);
            page.document.getElementById("apply").dispatch("click");
            assert.equal(rows(page)[3].ticked, false);
            assert.equal(picked(page), LEAVE_OUT);
        });

        it("goes back with Previous, and ignores the lines that have nothing to review", async () => {
            await open(payload());
            widen();

            const buttons = () => page.document.getElementById("detail").querySelectorAll(".foot-bar button");
            assert.equal(buttons()[0].disabled, true);
            selectRow(page, "REQ-PAY-004");
            buttons()[0].dispatch("click");
            assert.equal(selected(page), "REQ-PAY-003");

            await open(system());
            assert.equal(selected(page), "REQ-SYS-001");
            assert.deepEqual(choices(page), []);
            assert.equal(page.document.getElementById("detail").querySelector(".excerpt").textContent, "The launch mass shall not exceed 150 kg.");
            assert.equal(page.document.getElementById("detail").querySelector(".why").textContent.includes("not imported again"), true);
        });

        it("selects a requirement that needs a choice instead of ticking it, then ticks and unticks it once applied", async () => {
            await open(payload());
            widen();

            tick(page, "REQ-PAY-004");
            assert.equal(rows(page)[3].ticked, false);
            assert.equal(selected(page), "REQ-PAY-004");
            page.document.getElementById("apply").dispatch("click");
            assert.equal(rows(page)[3].ticked, true);
            tick(page, "REQ-PAY-004");
            assert.equal(rows(page)[3].ticked, false);
            assert.equal(rows(page)[3].constraint, "downlinkDelay <= 2 [h]");
            tick(page, "REQ-PAY-004");
            assert.equal(rows(page)[3].ticked, true);
        });

        it("ticks and unticks all the rows that have something applied with the box of the footer", async () => {
            await open(payload());
            widen();
            const all = page.document.getElementById("all");
            selectRow(page, "REQ-PAY-004");
            page.document.getElementById("apply").dispatch("click");
            assert.equal(all.getAttribute("aria-checked"), "true");

            all.dispatch("click");
            assert.deepEqual(rows(page).map(item => item.ticked), [false, false, false, false]);
            assert.equal(all.getAttribute("aria-checked"), "false");
            assert.equal(summary(page), "Nothing selected");

            tick(page, "REQ-PAY-001");
            assert.equal(all.getAttribute("aria-checked"), "mixed");
            all.dispatch("click");
            assert.deepEqual(rows(page).map(item => item.ticked), [true, true, false, true]);

            await open(system());
            tick(page, "REQ-SYS-008");
            await open({ ...system(), requirements: [system().requirements[0]] });
            assert.equal(all.disabled, true);
        });

        it("cannot import while nothing is chosen", async () => {
            await open(payload());

            tick(page, "REQ-PAY-001");
            tick(page, "REQ-PAY-002");

            assert.equal(summary(page), "Nothing selected");
            assert.equal(page.document.getElementById("import").disabled, true);
        });
    });

    describe("merging", () => {
        it("offers the other rows that can be imported, with the row proposed by the assistant first picked", async () => {
            await open(payload());
            widen();
            selectRow(page, "REQ-PAY-004");

            const list = page.document.getElementById("detail").querySelector("select");
            assert.deepEqual(list.querySelectorAll("option").map(item => item.value), ["REQ-PAY-001", "REQ-PAY-002", "REQ-PAY-003"]);
            assert.equal(list.value, "REQ-PAY-002");

            selectRow(page, "REQ-PAY-003");
            assert.equal(page.document.getElementById("detail").querySelector("select").value, "REQ-PAY-001");

            await open({ ...system(), requirements: [payload().requirements[3]] });
            assert.deepEqual(choices(page), [REWRITE, GOAL, LEAVE_OUT]);
        });

        it("does not create a merged requirement, and imports the others", async () => {
            await open(payload());
            widen();
            selectRow(page, "REQ-PAY-004");
            setTarget(page, "REQ-PAY-001");
            assert.equal(picked(page), MERGE);
            page.document.getElementById("apply").dispatch("click");

            assert.deepEqual(rows(page)[3], { reqId: "REQ-PAY-004", ticked: true, disabled: false, constraint: "merged into REQ-PAY-001", quality: "To clarify" });
            assert.equal(summary(page), "3 selected · creates 2 requirements and 2 constraints · 1 merged");

            const args = await importWith(imported());
            assert.deepEqual(args.choices, [
                { reqId: "REQ-PAY-001", option: "AsWritten" },
                { reqId: "REQ-PAY-002", option: "AsWritten" },
                { reqId: "REQ-PAY-004", option: "MergeInto", mergeInto: "REQ-PAY-001" }
            ]);
            assert.equal(summary(page), "Imported in one commit: REQ-PAY-001, REQ-PAY-002.");
        });

        it("blocks the import while the target of a merge is not imported, and says why", async () => {
            await open(payload());
            widen();
            selectRow(page, "REQ-PAY-004");
            choice(page, MERGE).dispatch("click");
            page.document.getElementById("apply").dispatch("click");

            const importButton = page.document.getElementById("import");
            const reason = page.document.getElementById("reason");
            assert.equal(reason.hidden, true);

            tick(page, "REQ-PAY-002");
            assert.equal(reason.textContent, "REQ-PAY-004 is merged into REQ-PAY-002, which is not imported.");
            assert.equal(reason.hidden, false);
            assert.equal(importButton.disabled, true);

            tick(page, "REQ-PAY-002");
            assert.equal(importButton.disabled, false);
        });

        it("blocks the import when the target is merged too, or when every row is merged", async () => {
            await open(payload());
            widen();
            setTarget(page, "REQ-PAY-004");
            page.document.getElementById("apply").dispatch("click");
            setTarget(page, "REQ-PAY-003");
            page.document.getElementById("apply").dispatch("click");

            assert.match(page.document.getElementById("reason").textContent, /is merged into REQ-PAY-00[34], which is merged too\.$/);
            assert.equal(page.document.getElementById("import").disabled, true);
        });
    });

    describe("saving the list", () => {
        it("sends the rows and the choices, imports nothing, and says so", async () => {
            await open(payload());
            widen();
            tick(page, "REQ-PAY-001");
            selectRow(page, "REQ-PAY-004");
            page.document.getElementById("apply").dispatch("click");

            const args = await callWith("export", { content: [{ type: "text", text: "Saved." }] });

            assert.deepEqual(JSON.parse(JSON.stringify(args)), {
                proposalId: PROPOSAL_ID,
                token: TOKEN,
                rows: [
                    { reqId: "REQ-PAY-001", option: "AsWritten", ticked: false },
                    { reqId: "REQ-PAY-002", option: "AsWritten", ticked: true },
                    { reqId: "REQ-PAY-003", ticked: false },
                    { reqId: "REQ-PAY-004", option: "WithRewrite", ticked: true }
                ]
            });
            assert.equal(requests(page, "tools/call").at(-1).params.name, SAVE_TOOL);
            assert.match(page.document.getElementById("note").textContent, /^List saved, nothing imported/);
        });

        it("shows why the server refuses to save", async () => {
            await open(payload());

            await callWith("export", { isError: true, content: [{ type: "text", text: "This list can no longer be saved." }] });
            assert.equal(page.document.getElementById("error").textContent, "This list can no longer be saved.");
            assert.equal(page.document.getElementById("note").textContent, "");
            assert.equal(page.document.getElementById("export").disabled, false);
        });

        it("restores the saved choices that still apply, on the findings computed again", async () => {
            await open({
                ...payload(),
                savedRows: [
                    { reqId: "REQ-PAY-001", option: "AsWritten", ticked: false },
                    { reqId: "REQ-PAY-002", option: "AsGoal", ticked: true },
                    { reqId: "REQ-PAY-003", option: "AsGoal", ticked: true },
                    { reqId: "REQ-PAY-004", option: "MergeInto", mergeInto: "REQ-PAY-001", ticked: true },
                    { reqId: "REQ-PAY-099", option: "AsGoal", ticked: true },
                    { reqId: "REQ-PAY-005", ticked: false }
                ]
            });

            assert.deepEqual(rows(page).map(item => [item.ticked, item.constraint]), [
                [false, "groundSamplingDistance <= 5 [m]"],
                [true, "mass * 1.2 <= 40 [kg]"],
                [true, "no threshold"],
                [true, "merged into REQ-PAY-001"]
            ]);
            assert.equal(page.document.getElementById("note").textContent, "Saved list restored: 3 choices kept, 2 no longer valid.");
        });

        it("drops the saved choices that no longer apply", async () => {
            const rewrite = { reqId: "REQ-PAY-004", option: "WithRewrite", ticked: true };
            await open({ ...payload(), savedRows: [rewrite, { reqId: "REQ-PAY-003", option: "WithRewrite", ticked: true }, { reqId: "REQ-PAY-001", option: "MergeInto", mergeInto: "REQ-PAY-002", ticked: true }, { reqId: "REQ-PAY-002", option: "Unknown", ticked: true }] });
            assert.deepEqual(rows(page).map(item => item.ticked), [true, true, false, true]);
            assert.equal(page.document.getElementById("note").textContent, "Saved list restored: 1 choice kept, 3 no longer valid.");

            await open({ ...payload(), savedRows: [rewrite] });
            assert.equal(page.document.getElementById("note").textContent, "Saved list restored: 1 choice kept.");

            await open({ ...payload(), savedRows: [] });
            assert.equal(page.document.getElementById("note").textContent, "");
        });
    });

    describe("importing", () => {
        it("imports the chosen requirements with their option, then puts the message of the server in the chat box", async () => {
            await open(payload());
            widen();
            applyChoice(page, GOAL);

            const args = await importWith(imported());

            assert.deepEqual(args, {
                proposalId: PROPOSAL_ID,
                token: TOKEN,
                choices: [
                    { reqId: "REQ-PAY-001", option: "AsWritten" },
                    { reqId: "REQ-PAY-002", option: "AsWritten" },
                    { reqId: "REQ-PAY-003", option: "AsGoal" }
                ]
            });
            const [message] = requests(page, "ui/message");
            assert.equal(message.params.role, "user");
            assert.match(message.params.content[0].text, /^The user has imported requirements of section 5/);
            await answer(page, message, {});

            assert.equal(summary(page), "Imported in one commit: REQ-PAY-001, REQ-PAY-002, REQ-PAY-003.");
            assert.equal(page.document.getElementById("note").textContent, "The message is in the chat box: check it and press Enter.");
            assert.equal(page.document.getElementById("import").hidden, true);
            assert.equal(page.document.getElementById("export").hidden, true);
            assert.equal(page.document.getElementById("all").hidden, true);
            assert.deepEqual(rows(page).map(item => item.disabled), [true, true, true, true]);
            assert.deepEqual(choices(page), []);

            tick(page, "REQ-PAY-001");
            assert.equal(rows(page)[0].ticked, true);
        });

        it("gives the message to the assistant when the host does not put it in the chat box, and tells when it refuses both", async () => {
            await open(payload());
            await importWith(imported());
            await answer(page, requests(page, "ui/message")[0], { isError: true });
            const [context] = requests(page, "ui/update-model-context");
            assert.match(context.params.content[0].text, /^The user has imported/);
            await answer(page, context, {});
            assert.equal(page.document.getElementById("note").textContent, "Sent to the assistant: type \"go\" in the chat.");

            await open(payload());
            await importWith(imported());
            await answer(page, requests(page, "ui/message").at(-1), undefined, { code: -32601, message: "Method not found" });
            await answer(page, requests(page, "ui/update-model-context").at(-1), undefined, { code: -32601, message: "Method not found" });
            assert.equal(page.document.getElementById("note").textContent, "The host refused the message: tell the assistant what you have imported.");
        });

        it("shows why the server refuses the import, and lets the user try again", async () => {
            await open(payload());

            await importWith({ isError: true, content: [{ type: "text", text: "A batch of changes waits for your decision in the conversation: apply or reject it first, then import." }] });
            assert.match(page.document.getElementById("error").textContent, /^A batch of changes waits/);
            assert.equal(page.document.getElementById("import").disabled, false);
            assert.equal(page.document.getElementById("note").textContent, "");

            await importWith({ structuredContent: { message: "refused", imported: false, result: { createdElements: [], problems: ["Change 2 (create RequirementUsage): refused."], applied: false } } });
            assert.equal(page.document.getElementById("error").textContent, "Nothing has been imported: Change 2 (create RequirementUsage): refused.");
            assert.equal(page.document.getElementById("import").disabled, false);

            await importWith(undefined, { code: -32000, message: "The host refused the call." });
            assert.equal(page.document.getElementById("error").textContent, "The host refused the call.");

            await importWith(imported());
            assert.equal(page.document.getElementById("error").hidden, true);
            assert.equal(requests(page, "tools/call").length, 4);
            assert.equal(requests(page, "ui/message").length, 1);
        });
    });
});
